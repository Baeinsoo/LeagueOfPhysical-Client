# HTTP 클라이언트 계층 표준화 설계

> **슬라이스 0.** 인증 cutover(슬라이스 1)가 이 계층 위에 서기 때문에 먼저 한다.
> cutover에서 이미 확정한 결정들은 `2026-08-06-auth-cutover-decisions.md`에 따로 기록해 두었다.

## 1. 왜 지금인가

인증 cutover를 설계하다가 막혔다. 토큰 갱신·401 재시도를 넣을 자리가 **구조적으로 없다.**

`WebRequest<T>`가 **생성자 안에서 요청을 전송한다.**

```csharp
this.webRequestParam.webRequestInterceptor?.OnBeforeRequest(this.unityWebRequest);
this.asyncOperation = this.unityWebRequest.SendWebRequest();   // 생성 즉시 발사
```

그래서 `IWebRequestInterceptor.OnBeforeRequest`에서는 두 가지가 다 불가능하다.

1. **시그니처가 동기**(`void`)라 안에서 `await` 할 수 없다. 억지로 블로킹하면 `UnityWebRequest`가
   메인 스레드에서 완료되는 구조라 데드락이다.
2. 설령 async로 바꿔도 **생성자가 await 할 수 없으므로** 훅이 끝나기 전에 전송이 나간다.

즉 인터셉터 시그니처만 고쳐서 될 일이 아니라 **"만들기 = 보내기"를 떼어내야** 한다. 그게 구조
변경이고, 구조 변경을 할 거라면 같은 자리의 다른 결함들도 같이 정리하는 편이 낫다.

## 2. 현재 계층과 표준의 격차

`GameFramework/Runtime/Scripts/WebRequest/` — 11파일 419줄.

| # | 지금 | 표준 (.NET `HttpClient`/`DelegatingHandler`, UniTask 관행) | 결과 |
|---|---|---|---|
| 1 | 만들기 = 보내기 (생성자 전송) | `SendAsync()` 로 분리 | 갱신·재시도 불가 |
| 2 | **실패가 한 덩어리** — 연결 실패도 401도 `isSuccess == false` | 전송 실패는 예외, HTTP 상태는 응답 속성 | 계정 유실 버그의 뿌리 |
| 3 | **awaiter 3종 경쟁** | 계약 하나 | 조용한 동작 뒤집힘 |
| 4 | 인터셉터가 동기 + 체인 아님 | 핸들러 체인 | 이번 작업이 막힌 지점 |
| 5 | 취소 없음 | `CancellationToken` 관통 | 씬 나가도 요청이 살아 있음 |
| 6 | 타임아웃 미설정 | 클라이언트가 보유 | 응답 없으면 무한 대기 |
| 7 | 테스트 0건 | 전송 seam에 가짜를 꽂아 테스트 | 검증이 전부 수동 |

### 2번 — 실패 종류가 뭉개진 것이 실제 사고를 냈다

`isSuccess => unityWebRequest.result == UnityWebRequest.Result.Success` 이므로 **연결 실패
(`ConnectionError`)와 HTTP 오류(`ProtocolError`, 4xx/5xx)가 구분되지 않는다.** 인증 슬라이스에서
`AuthenticationService`가 `isSuccess == false`를 "서버가 자격증명을 거부함"으로 읽어, **오프라인으로
게임을 켠 플레이어의 계정을 지우고 새 계정을 만들었다.** 현재는 `responseCode == 401`을 별도로
확인해 증상만 막아둔 상태다. 표준 구조에서는 애초에 섞이지 않는다.

### 3번 — awaiter 3종의 정체

`WebRequestAwaiter<T>`(실패 시 예외), `UnityWebRequestAwaiter`, 그리고 `WebRequest<T>`가
`CustomYieldInstruction`(= `IEnumerator`)을 상속하는 탓에 잡히는 **UniTask의 `IEnumerator`용
awaiter**(예외 없음). 어느 것이 바인딩되는지가 **그 파일에 `using GameFramework;`가 있는지**로
갈린다 — `HttpExtensions.GetAwaiter<T>(this WebRequest<T>)` 확장 메서드 때문이다.

실측: WebAPI를 부르는 12개 파일 중 **11개가 예외 방식, `AuthenticationService.cs` 1개만 다르다.**
하필 그 파일이 401을 보고 자격증명을 지울지 판단하는 곳이다. **`using GameFramework;` 한 줄이
추가되면 동작이 조용히 뒤집힌다.** IDE 자동 import 한 번이면 충분하다.

## 3. 목표 구조 — .NET `HttpClient`를 그대로 옮긴다

새로 발명하지 않는다. 전송만 `UnityWebRequest`로 바뀐다.

```
HttpClient                요청 진입점. 타임아웃 보유
  └ DelegatingHandler…    체인. 각자 앞뒤로 await 가능, 필요하면 재전송
      └ UnityWebRequestHandler   체인의 끝 — 실제 전송
```

### 산업 표준 매핑

| .NET | 우리 | 역할 |
|---|---|---|
| `HttpMessageHandler` | `HttpMessageHandler` | `SendAsync(request, ct)` 추상 |
| `DelegatingHandler` | `DelegatingHandler` | 다음 핸들러를 들고 감싸는 베이스 |
| `SocketsHttpHandler` | **`UnityWebRequestHandler`** | 체인의 끝. **가짜로 갈아끼우면 테스트 가능** |
| `HttpRequestMessage` | `HttpRequestMessage` | 메서드·URI·헤더·본문 |
| `HttpResponseMessage` | `HttpResponseMessage` | 상태코드·헤더·본문(문자열) |
| `HttpRequestException` | `HttpRequestException` | `StatusCode`(nullable) 보유 |
| `IHttpClientFactory` named client | 두 개의 `HttpClient` 인스턴스 | 인증 붙임/안 붙임 |
| `EnsureSuccessStatusCode()` | `HttpResponseMessage.EnsureSuccessStatusCode()` | 2xx 아니면 예외 |

### 역직렬화를 체인 밖에 둔다

.NET이 그렇듯 **핸들러 체인은 제네릭이 아니다.** `HttpResponseMessage`는 본문을 문자열로만 들고, `T`로
바꾸는 일은 그 위 계층이 한다.

```
체인 안 :  HttpRequestMessage → HttpResponseMessage         (상태코드 + 문자열 본문)
체인 위 :  HttpResponseMessage → T                    (역직렬화 + MessagePipe 발행)
```

핸들러가 제네릭 지옥을 피하고 체인 조립이 단순해진다. MessagePipe 발행도 자연스럽게 `T`를 아는
위쪽으로 올라간다.

## 4. 실패 모델 — 이번 정리의 핵심 소득

```csharp
public class HttpRequestException : Exception
{
    public long? StatusCode { get; }   // null = 연결 실패 (서버가 답을 안 함)
                                       // 401  = 서버가 거부함
    public string ResponseBody { get; }
}
```

`StatusCode`가 nullable이고 null이 곧 "전송 자체가 실패"라는 것은 **.NET 5부터
`HttpRequestException.StatusCode`가 정확히 이 모양**이다. 우리가 만든 규칙이 아니다.

### 던지는 위치 — 체인 위, 체인 안 아님

**중요.** 상태코드 기반 예외는 **체인이 아니라 그 위 타입드 계층에서** 던진다.

| 계층 | 동작 |
|---|---|
| `UnityWebRequestHandler` | 전송 실패 시 `HttpRequestException(StatusCode: null)` 을 던진다 |
| `HttpClient.SendAsync` / 핸들러 체인 | 4xx·5xx여도 **던지지 않고 `HttpResponseMessage`를 그대로 반환** |
| 타입드 `SendAsync<T>` | `EnsureSuccessStatusCode()` → 2xx 아니면 `HttpRequestException(StatusCode: 401 등)` |

체인 안에서 던지면 **슬라이스 1의 401 재시도 핸들러가 401을 볼 수 없다.** 핸들러가 응답을 보고
"이건 재시도 대상"이라고 판단할 수 있어야 하므로 이 순서는 협상 대상이 아니다. .NET의 분업
(`HttpClient.SendAsync`는 상태로 안 던지고, `EnsureSuccessStatusCode()`가 던짐)과 동일하다.

### 던지는 시점은 현행 관례 유지

타입드 계층은 **2xx가 아니면 던진다.** 호출부 11개 파일이 이미 이 전제로 쓰였고(`try/catch` +
본문 `code` 확인), Retrofit·OkHttp도 같은 기본값이다. .NET만 기본이 "안 던짐"인데 거기 맞추면
호출부 22곳의 판단 로직을 전부 고쳐야 해서 이득 없는 churn이다.

앱 레벨 결과(`ResponseBase.code`)는 지금처럼 본문에서 확인한다. 두 층의 역할 분담은 그대로다.

- **HTTP/전송 실패** → 예외
- **앱 레벨 결과** → 응답 본문의 `code`

## 5. 타입 인벤토리

### 새로 만드는 것 — `GameFramework.Http`

| 타입 | 내용 |
|---|---|
| `HttpRequestMessage` | `Method`, `Uri`, `Headers`, `Body`(object → JSON). 정적 팩토리 `Get/Post/Put/Delete` |
| `HttpResponseMessage` | `StatusCode`(long), `Body`(string), `Headers`, `IsSuccessStatusCode`, `EnsureSuccessStatusCode()` |
| `HttpMessageHandler` | 추상. `UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage, CancellationToken)` |
| `DelegatingHandler` | `HttpMessageHandler` 상속 + `InnerHandler` 보유 |
| `UnityWebRequestHandler` | 체인의 끝. `UnityWebRequest` 전송. 전송 실패 시 `StatusCode: null` 예외 |
| `HttpClient` | 체인 진입점 + `Timeout`(linked CTS로 적용) |
| `HttpRequestException` | `long? StatusCode`, `string ResponseBody` |
| `HttpClientJsonExtensions` | `SendAsync<T>(this HttpClient, HttpRequestMessage, ct)` — 전송 → `EnsureSuccessStatusCode()` → 역직렬화 |
| `HttpJson` | Newtonsoft 래퍼 (`WebRequestJson` 이름만 이동) |
| `BearerTokenHandler` | `DelegatingHandler`. 공급자가 준 토큰이 있으면 `Authorization: Bearer` 부착 |

> **왜 `HttpRequest`/`HttpResponse`가 아니라 `...Message`인가.** `LOP.HttpResponse`가 이미 있다 —
> 클·서 양쪽에서 **모든 API 응답 DTO의 베이스 클래스**(`public class HttpResponse { public int code; }`)
> 다. 짧은 이름을 쓰면 WebAPI 파일마다 충돌한다. .NET의 정식 이름이 애초에
> `HttpRequestMessage`/`HttpResponseMessage`이므로 그쪽이 표준에도 더 가깝다.
> (`LOP.HttpResponse`는 앱 레벨 응답 베이스인데 전송 계층 같은 이름을 쓰고 있다 — 백엔드의
> `ResponseBase`에 맞춰 리네임하는 것이 옳지만 이번 범위 밖. §12 후속 과제.)

> **`BearerTokenHandler`를 GameFramework에 두는 이유 — 개념이 먼저다.**
> `lop-repo-topology.md`의 코드 분배 결정 트리 첫 질문은 "앱 비종속 인프라인가(다른 게임에도 그대로
> 쓸 만한가)"이고, 이 핸들러는 거기 깨끗하게 걸린다. 하는 일이 **`Authorization: Bearer <토큰>`
> 헤더를 붙인다**가 전부이며 이는 RFC 6750이 정한 표준 동작이지 LOP가 만든 규칙이 아니다.
> **LOP 타입을 하나도 모른다** — 생성자가 받는 것은 `Func<string>` 하나다. 모든 HTTP 클라이언트
> 라이브러리가 이걸 기본 제공한다(.NET `AuthenticationHeaderValue`, Refit
> `AuthenticatedHttpClientHandler`, OkHttp `Authenticator`). 선례도 있다 — `GameFramework.Auth`에
> `Jwt`·`AccessTokenInfo`·`IAuthCredentialStore`가 이미 있다.
> "지금은 클라만 쓴다"는 반대 근거가 아니다. 기준은 소비자 수가 아니라 앱 종속성이다.
>
> 부수 효과로 EditMode 테스트가 가능해진다("토큰 있으면 붙임 / 없으면 안 붙임"). 이는 배치가
> 옳다는 **뒷받침이지 이유가 아니다** — 클라 본체에는 테스트 어셈블리가 없다.
>
> **슬라이스 1까지 봐도 같은 경계다.** "401이면 갱신 후 1회 재시도"와 single-flight는 앱 비종속
> 정책(OkHttp `Authenticator`가 정확히 그것)이라 GameFramework, "갱신 = 저장된 익명 secret으로
> `/auth/login`을 친다"는 LOP 도메인이라 델리게이트로 주입된다. **정책은 프레임워크, 내용물은 앱.**

### 삭제하는 것

`WebRequest<T>`, `IWebRequest<T>`, `WebRequestBuilder<T>`, `WebRequestParam<T>`,
`IWebRequestParam<T>`, `IWebRequestInterceptor`, `WebRequestAwaiter<T>`, `UnityWebRequestAwaiter`,
`WebRequestException`, 그리고 `HttpExtensions`의 `GetAwaiter` 확장 2개.

`WebRequestJson`은 삭제가 아니라 **`HttpJson`으로 이름만 바뀐다**(내용 동일).

`HttpExtensions`의 `SetRequestHeader`/`ToQueryString`/`AppendQueryString`은 유지한다.
`HttpMethod` enum도 유지한다.

**`SetForm` / `IMultipartFormSection` 지원은 없앤다** — 클라·서버 통틀어 사용처가 0이다.

### 클라·서버 각자

| 리포 | 타입 |
|---|---|
| 클라 `LOP` | `WebAPI`가 보유하는 `HttpClient` 2개(`BearerTokenHandler` 있음/없음) + 사설 전송 헬퍼(역직렬화 후 MessagePipe 발행) |
| 서버 `LOP` | `WebAPI`가 보유하는 `HttpClient` 1개 (서버는 토큰을 안 붙인다) |
| 양쪽 | 기존 `LOPWebRequestInterceptor.cs` 삭제 |

## 6. 호출부 마이그레이션

### WebAPI가 DTO를 직접 돌려준다

```csharp
// 지금
public static WebRequest<JoinLobbyResponse> JoinLobby(string userId)
    => new WebRequestBuilder<JoinLobbyResponse>()
        .SetUri(...).SetMethod(HttpMethod.PUT)
        .SetWebRequestInterceptor(LOPWebRequestInterceptor.Default).Build();

// 바뀐 후
public static UniTask<JoinLobbyResponse> JoinLobby(string userId, CancellationToken ct = default)
    => authorized.SendAsync<JoinLobbyResponse>(HttpRequestMessage.Put($"{...}/lobby/join/{userId}"), ct);
```

호출부는 한 겹이 벗겨진다.

```csharp
if (joinLobby.response.code != ResponseCode.SUCCESS)   // 지금
if (joinLobby.code != ResponseCode.SUCCESS)            // 바뀐 후
```

요청 객체를 들고 다닐 이유가 없어졌으므로(상태코드는 예외가 들고 온다) 결과를 바로 준다.
Retrofit·Refit이 `suspend fun getUser(): User`로 DTO를 바로 주는 것과 같은 모양이다.

**호출 지점 수: 클라 14, 서버 8 — 총 22곳.** 대부분 위 정도의 기계적 수정이다.

### 실질적으로 좋아지는 한 곳 — `AuthenticationService`

```csharp
catch (HttpRequestException e) when (e.StatusCode == 401)   // 서버가 거부 → 자격증명 폐기
catch (HttpRequestException e)                              // 그 외(오프라인 포함) → 보존
```

지금은 `isSuccess`가 둘을 뭉개서 `responseCode`를 따로 뒤져야 했다. 이제 타입이 구분을 강제한다.

### 인증 붙이기 / 안 붙이기 — 클라이언트 두 개

```csharp
authorized = new HttpClient(new BearerTokenHandler(new UnityWebRequestHandler(), () => accessToken));
anonymous  = new HttpClient(new UnityWebRequestHandler());
```

`WebAPI.SignInAnonymous`/`Login`만 `anonymous`를 쓴다. .NET의 named client 관행 그대로이며,
지난 슬라이스에서 확정한 **"URL 문자열이 아니라 호출부가 스스로 선언한다"** 는 성질이 유지된다.
(그때 URL 접두사 매칭이 `/lobby/auth/...` 때문에 아무 데서도 매치되지 않는 죽은 검사가 됐던 이력이
있다 — 그 방식으로 돌아가지 않는다.)

### MessagePipe 발행 — 위치만 옮기고 동작은 동일

**중요:** 이 발행은 장식이 아니라 앱의 핵심 배선이다. `UserDataStore`가 `GetUserResponse`·
`GetUserStatsResponse`·`GetUserLocationResponse`를, `RoomDataStore`가 `GetMatchResponse`·
`RoomJoinableResponse`를 구독해 자기 상태를 채운다. **끊기면 유저 데이터가 아예 안 들어온다.**

```
UnityWebRequestHandler → HttpResponseMessage(문자열)
  → 역직렬화 T
    → GlobalMessagePipe.GetPublisher<T>().Publish(dto)   ← 여기 (타입드 계층)
      → UserDataStore / RoomDataStore 구독 (기존 그대로)
```

### `WebAPI`는 static으로 둔다

DI 인스턴스로 바꾸지 않는다. **개념적으로는 DI가 더 맞다** — 타입드 API 클라이언트를 주입되는
서비스로 두는 것이 표준이다(Refit, .NET typed `HttpClient`). 하지만 **이번 슬라이스의 목적은 전송
계층 재정리**이고, 22곳 호출부의 생성자·주입을 고치는 것은 그 목적과 무관한 churn이다. 한 슬라이스에
두 종류의 변경을 섞으면 회귀가 났을 때 원인을 가릴 수 없다. **범위 때문에 미루는 것이지 옳지 않아서가
아니다** — §12 후속 과제로 기록.

(부수적으로, 지금 바꿔도 클라 본체엔 테스트 어셈블리가 없어 DI의 주 이득인 테스트 가능성이 나오지
않는다. 테스트가 필요한 로직은 GameFramework 쪽 핸들러 체인에 있고 거기서 실제로 테스트된다.)

### 서버의 fire-and-forget 2곳

`LOPRoom.Heartbeat`, `LOPRoom.UpdateRoomStatus`는 `await` 없이 호출한다. 지금은 생성자가 전송하니
동작하지만 **`SendAsync`로 바뀌면 호출만 하고 안 기다리면 아무 일도 일어나지 않는다.**
UniTask의 `.Forget()`을 명시적으로 붙여 "의도적으로 안 기다림"을 코드에 남긴다.

### 취소·타임아웃

`CancellationToken`을 `SendAsync`까지 관통시키고, `WebAPI` 메서드는 **선택 인자(기본 `default`)**
로 받는다. 22곳을 당장 안 고쳐도 컴파일되고, 취소가 필요한 곳부터 점진 적용한다.
타임아웃은 `HttpClient`가 보유한다(.NET과 동일).

## 7. 테스트 전략

체인의 끝(`UnityWebRequestHandler`)을 가짜로 갈아끼우면 나머지 전부가 **GameFramework EditMode에서
Unity 없이** 돌아간다. .NET에서 `HttpMessageHandler`를 목으로 만드는 것과 같은 방식이다.
**이 계층 최초의 자동 검증이다.**

| # | 케이스 | 고정하려는 것 |
|---|---|---|
| 1 | 2xx + JSON | DTO로 역직렬화되어 돌아온다 |
| 2 | 401 응답 | `HttpRequestException.StatusCode == 401` |
| 3 | **연결 실패** | `StatusCode == null` — 계정 유실 버그의 회귀 방지 |
| 4 | 5xx | 2번과 같은 모양 |
| 5 | 핸들러 체인 순서 | 바깥이 먼저 보고 나중에 받는다 |
| 6 | **핸들러가 재전송할 수 있다** | 슬라이스 1의 401 재시도가 성립하는지 미리 고정 |
| 7 | 취소 토큰 | 취소하면 즉시 끊긴다 |
| 8 | 타임아웃 | 응답 없으면 예외 |
| 9 | 헤더·본문 | 붙인 헤더가 실제로 실린다 |

**3번과 6번이 이 슬라이스의 존재 이유다.** 3번은 실제로 당한 사고를 타입 수준에서 봉인하고,
6번은 슬라이스 1이 딛고 설 바닥을 미리 검증한다.

TDD로 간다 — 테스트를 먼저 쓰고, **일부러 되돌려 실제로 실패하는지 확인한다.**

`UnityWebRequestHandler` 자체(진짜 전송)는 단위 테스트 대상이 아니다. 얇은 어댑터라 수동 검증으로
덮는다 — .NET에서도 실제 소켓 핸들러는 그렇게 다룬다.

### 수동 검증

| 대상 | 시나리오 |
|---|---|
| 클라 | 진입 → 로그인 → 유저 로드 → 로비 입장 → 매칭 → 방 입장 |
| 클라 | **서버를 끈 채로 재기동** → 계정이 보존되는가 (3번의 실전 확인) |
| 서버 | 방 생성 → heartbeat 유지 → 상태 갱신 → 매치 조회 |

### 컴파일 관문 (필수)

GameFramework EditMode 테스트는 **별도 어셈블리라 `Assembly-CSharp`이 깨져도 통과한다.** 지난
슬라이스에서 이걸 몰라 "테스트 통과"가 클라 컴파일을 보증하지 못했다. 각 태스크마다 배치모드
컴파일 + `error CS` 개수 0을 **별도 관문으로** 확인한다.

## 8. 작업 순서 — 항상 컴파일되는 상태로

`manifest.json`이 GameFramework를 `file:` 로컬 경로로 참조하므로 변경이 양쪽에 즉시 보인다.
따라서 **새 계층을 옛 계층 옆에 먼저 세우고, 다 옮긴 뒤 옛 것을 지운다**(strangler).

| 단계 | 리포 | 내용 | 상태 |
|---|---|---|---|
| 1 | GameFramework | 새 계층 추가 + 테스트 9건 | 옛 계층 그대로 → 아무것도 안 깨짐 |
| 2 | 클라 | `WebAPI` + 호출부 14곳 이전, `LOPWebRequestInterceptor` 삭제 | 수동 검증 |
| 3 | 서버 | `WebAPI` + 호출부 8곳 이전(`.Forget()` 포함), 인터셉터 삭제 | 수동 검증 |
| 4 | GameFramework | 옛 계층 삭제 | 양쪽 재컴파일 확인 |

4번을 마지막에 두는 것이 핵심이다. 2·3번 중간에 어느 쪽이 깨져도 되돌릴 곳이 남아 있다.

## 9. 저장소·브랜치 제약

3개 리포에 걸친다: `GameFramework`, `LeagueOfPhysical-Client`, `LeagueOfPhysical-Server`.

**클라·서버는 워크트리를 쓸 수 없다.** `manifest.json`이 GameFramework를 상대경로
(`file:../../GameFramework`)로 참조해 워크트리 위치에서는 해석되지 않는다. 본체 체크아웃에서
브랜치를 전환해 작업한다. (인증 슬라이스에서 확인된 제약.)

병합 순서는 의존 방향대로 **GameFramework → 클라 → 서버**.

다른 머신에서 병행 작업이 있으므로 시작 전 3개 리포를 다시 당긴다.

## 10. 이번에 하지 않는 것

- **인증 갱신·재시도** — 슬라이스 1. 이 슬라이스의 `BearerTokenHandler`는 **현행 동작 그대로**
  (토큰이 있으면 붙임)만 한다. 구조 정리에 기능 변경을 섞지 않는다.
- **`WebAPI`를 DI 인스턴스로** — §12 후속 과제
- **재시도 정책**(지수 백오프·서킷브레이커) — 쓸 데가 없다
- **직렬화 교체** — Newtonsoft 유지
- **백엔드 변경** — 0

## 11. 성공 기준

- GameFramework EditMode 테스트 9건 통과(기존 125건 유지), 각 테스트가 되돌리면 실패함을 확인
- 클라·서버 컴파일 에러 0 (배치모드로 직접 확인)
- 수동 검증 시나리오 전부 통과 — 특히 **서버 꺼진 채 재기동 시 계정 보존**
- `WebRequest<T>` 및 awaiter 3종이 코드베이스에서 사라짐 — `grep`으로 잔여 참조 0 확인
- 슬라이스 1이 `BearerTokenHandler` 안에서 갱신·재시도를 구현할 수 있는 상태

## 12. 후속 과제

- **`WebAPI`를 DI 인스턴스로** — 클라 본체에 테스트 어셈블리가 생기면 값을 한다. 지금은 아니다.
- **`LOP.HttpResponse` 리네임** — 앱 레벨 응답 베이스인데 전송 계층 같은 이름이다. 백엔드가 쓰는
  `ResponseBase`에 맞추는 것이 옳다. 클·서 DTO 다수가 상속하므로 별도 건.
- **재시도 정책** — 5xx·일시 장애에 지수 백오프. 실제로 겪은 뒤에 넣는다.
- **`ResponseBase.code` 규약** — `/auth/*`가 이 규약을 안 따른다(HTTP 상태로만 판단). 규약을
  통일할지, 예외를 명문화할지는 별도 건.
