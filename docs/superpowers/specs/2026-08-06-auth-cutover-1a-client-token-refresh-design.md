# 인증 cutover 1a — 클라 토큰 갱신

> 슬라이스 1(인증 cutover)의 첫 조각. 결정 기록: `2026-08-06-auth-cutover-decisions.md`
> 선행: `2026-08-06-http-client-layer-standardization-design.md`(슬라이스 0), `2026-08-04-anonymous-auth-session-design.md`

## 0. 왜 이걸 먼저 하나

토큰 검사를 켜는 것(1b)과 갱신을 붙이는 것(1a) 중 **갱신이 먼저**다.

액세스 토큰 수명은 1시간인데 **지금 갱신을 부르는 코드가 한 곳도 없다.** `AuthenticationService.RefreshIfNeededAsync()`
는 구현돼 있지만 호출자가 0곳이다. 이 상태로 1b(서버가 토큰을 검사)를 켜면 **1시간 넘는 세션이 전부 깨진다.**

1a는 **서버를 한 줄도 안 건드린다.** 지금 배포해도 아무것도 깨지지 않고, 1b를 켤 때 이미 갱신이 돌고 있게 만든다.

## 1. 범위

**한다**

- `IAccessTokenProvider` 포트 신설 (GameFramework.Http)
- `BearerTokenHandler`에 미리 갱신 + 401 재시도 1회 + 재시도 가드
- `SingleFlight` 프리미티브 신설 (GameFramework.Threading)
- `AuthenticationService`가 `IAccessTokenProvider`를 구현하고 자기 갱신을 single-flight로 감싼다
- 배선 교체 (`WebAPI.SetAccessTokenProvider`, `RootLifetimeScope`)

**안 한다** — 다른 조각의 몫이다

| 항목 | 어디로 |
|---|---|
| 백엔드 토큰 강제, 레이트리밋, `trust proxy`, k8s Secret | 1b |
| 방 접속 인증(introspection), `CustomProperties.token` 리네임 | 1c |
| `GameFramework.Auth.Jwt` 삭제, `PUT /lobby/leave/:id` 삭제 | 1b/1c (해당 코드를 손대는 조각에서) |

건드리는 저장소는 **GameFramework와 클라 둘뿐**이다.

### 파일

| 저장소 | 파일 | 작업 |
|---|---|---|
| GameFramework | `Runtime/Scripts/Http/IAccessTokenProvider.cs` | 신규 |
| GameFramework | `Runtime/Scripts/Http/BearerTokenHandler.cs` | 수정 |
| GameFramework | `Runtime/Scripts/Threading/SingleFlight.cs` | 신규 (폴더째 신규) |
| GameFramework | `Tests/Runtime/Http/BearerTokenHandlerTests.cs` | 수정 |
| GameFramework | `Tests/Runtime/Http/FakeAccessTokenProvider.cs` | 신규 |
| GameFramework | `Tests/Runtime/Threading/SingleFlightTests.cs` | 신규 |
| 클라 | `Assets/Scripts/Auth/AuthenticationService.cs` | 수정 |
| 클라 | `Assets/Scripts/Auth/DeferredAccessTokenProvider.cs` | 신규 |
| 클라 | `Assets/Scripts/WebAPI/WebAPI.cs` | 수정 |
| 클라 | `Assets/Scripts/RootLifetimeScope.cs` | 수정 |

새 폴더·파일의 `.meta`는 Unity가 만든 것을 함께 커밋한다.

## 2. 지금 이음새

```csharp
// RootLifetimeScope.cs
WebAPI.SetAccessTokenProvider(() => authenticationService.AccessToken);
```

`Func<string>`이라 **기다릴 수 없다.** 갱신은 네트워크 왕복이므로 이 자리가 `await` 가능해져야 한다.

## 3. 포트 — `IAccessTokenProvider`

```csharp
namespace GameFramework.Http
{
    /// <summary>요청에 실을 토큰을 준다. 필요하면 갱신까지 하고 준다 — 부르는 쪽은 갱신을 모른다.</summary>
    public interface IAccessTokenProvider
    {
        /// <param name="forceRefresh">true면 만료가 남았어도 무조건 새로 받아온다(401을 맞은 뒤).</param>
        UniTask<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken);
    }
}
```

**계약**

- 로그인 상태가 아니면 `null`. 예외를 던지지 않는다.
- 갱신에 실패해도 **예외를 던지지 않고 지금 가진 토큰을 그대로 돌려준다.** 갱신 실패는 "이 토큰이 죽었다"는
  뜻이 아니다(오프라인일 수도 있다). 호출자는 그 토큰으로 시도해 보고, 진짜 거부당하면 그때 401을 받는다.
- 이 계약 때문에 **"갱신했는데 토큰이 그대로"** 가 관찰 가능해진다 — §4의 재시도 가드가 그걸 신호로 쓴다.

### 왜 `GameFramework.Http`에 두나 (Auth가 아니라)

**쓰는 쪽이 필요한 모양을 정의한다.** `BearerTokenHandler`가 소비자이고, 포트를 Http에 두면 Http가 Auth를
전혀 몰라도 된다(의존 방향이 한쪽으로 정리된다). 구현은 인증 쪽이 가져간다.

업계도 그렇다 — OkHttp의 `Authenticator`는 auth 패키지가 아니라 okhttp3에 있고, Azure의 `TokenCredential`도
파이프라인 옆에 산다.

### 명명 근거

| 이름 | 출처 |
|---|---|
| `IAccessTokenProvider` | Kiota(`Microsoft.Kiota.Abstractions.Authentication`), ASP.NET Core WASM 양쪽에 실재 |
| `forceRefresh` | MSAL `AcquireTokenSilent(forceRefresh:)` |

메서드 시그니처는 생태계마다 다르다(Kiota는 `GetAuthorizationTokenAsync(Uri, 컨텍스트, ct)`, WASM은
`RequestAccessToken()`). **표준 시그니처는 없으므로** 이름만 표준에서 빌리고 인자는 우리가 실제로 필요한
최소로 둔다.

## 4. `BearerTokenHandler`

```csharp
public override async UniTask<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
{
    string accessToken = await accessTokenProvider.GetAccessTokenAsync(false, cancellationToken);
    SetAuthorization(request, accessToken);

    HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

    if (response.StatusCode != HttpStatusUnauthorized)
    {
        return response;
    }

    string refreshed = await accessTokenProvider.GetAccessTokenAsync(true, cancellationToken);

    //  토큰이 안 바뀌었다는 건 갱신이 실패했다는 뜻이다. 방금 거부당한 토큰을 그대로 다시 보내봐야
    //  결과가 같으므로, 헛수고 대신 원래 401을 돌려준다.
    if (string.IsNullOrEmpty(refreshed) || refreshed == accessToken)
    {
        return response;
    }

    SetAuthorization(request, refreshed);
    return await base.SendAsync(request, cancellationToken);
}
```

**흐름 세 갈래**

```
성공        요청 → 200                                     → 갱신 안 부름
갱신 성공   요청 → 401 → 강제 갱신(새 토큰) → 재전송 → 200
갱신 실패   요청 → 401 → 강제 갱신(그대로)  → 재전송 안 함 → 401 그대로
```

**무한 루프가 구조적으로 불가능한 이유**: 재전송은 루프가 아니라 **직선 코드 한 번**이다. 재전송이 401이어도
그 응답이 그대로 반환된다. OkHttp가 무한 루프 이슈(#960, #3984) 끝에 정착한 처방("이미 Authorization이 붙어
있던 요청이면 포기")과 같은 취지다.

**재귀도 불가능하다**: 갱신은 `WebAPI.Login`을 부르는데 그건 `anonymous` 클라이언트라 이 핸들러를 안 지난다.

## 5. `SingleFlight`

```csharp
namespace GameFramework.Threading
{
    /// <summary>같은 작업이 이미 돌고 있으면 새로 시작하지 않고 그 결과를 함께 기다린다.
    /// 동시에 들어온 갱신 요청 N개를 실제 실행 1번으로 접는다.</summary>
    public class SingleFlight<T>
    {
        public UniTask<T> RunAsync(Func<UniTask<T>> operation);
    }
}
```

**계약**

- 진행 중이면 그 작업의 결과를 함께 기다린다. 실제 실행은 1번.
- 성공이든 실패든 **끝나면 자리를 비운다.** 다음 호출은 새로 실행한다 — 결과를 캐시하지 않는다.
- 실패는 모든 대기자에게 같은 예외로 전달된다.

Go의 `singleflight`가 그대로 이 계약이다(CDN 쪽 용어로는 request coalescing).

> **구현 함정 — `UniTask`는 두 번 await할 수 없다.** 진행 중인 작업을 여러 대기자에게 나눠주려면 반드시
> `.Preserve()`로 감싼 것을 보관해야 한다. 안 하면 두 번째 대기자가 터진다.

> **취소는 공유 작업에 전달하지 않는다.** 한 요청이 취소됐다고 다른 대기자들의 갱신까지 죽이면 안 된다.
> 갱신은 로그인 호출 하나라 `HttpClient` 타임아웃(30초)으로 이미 상한이 있다.

### 왜 핸들러 안이 아니라 여기인가

Azure.Core는 이 합치기를 `BearerTokenAuthenticationPolicy` **안**에 둔다(중첩 `AccessTokenCache`가
`TaskCompletionSource` + 락으로 처리). 소비자가 HTTP 파이프라인 하나뿐이라 그게 맞다.

우리는 **1c에서 소비자가 둘이 된다** — HTTP 요청과 Mirror 소켓 접속 전 미리 갱신. 핸들러 안에 두면 소켓
쪽은 그 줄에 서지 않아 재로그인이 2번 나간다. 소비자가 둘 이상일 때의 업계 모양은 SignalR의
`AccessTokenProvider`다 — HTTP negotiate와 WebSocket 연결이 **같은 공급자 하나**를 부르고, 문서가 "갱신은
이 함수 안에서 하라"고 명시한다.

즉 **"핸들러 안이 틀렸다"가 아니라 "소비자가 하나면 핸들러 안, 우리는 곧 둘"** 이다.

### 합치기가 없으면 무슨 일이 나나 (이게 이 조각의 진짜 이유)

로비 진입 시 요청 여러 개가 동시에 나간다. 만료가 임박한 순간이면 셋 다 각자 재로그인한다. 낭비가 문제가
아니라 — **그중 하나가 401을 맞으면 `AuthenticationService`가 자격증명을 지우고 새 계정을 만든다.** 우리가
이미 겪은 부류의 사고다(더블클릭 계정 2개 생성).

## 6. LOP 측 — `AuthenticationService`

```csharp
public class AuthenticationService : IAccessTokenProvider
{
    private readonly SingleFlight<string> refreshFlight = new SingleFlight<string>();

    public async UniTask<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (IsSignedIn == false)
        {
            return null;
        }

        if (forceRefresh == false && Current.Token.NeedsRefresh(DateTimeOffset.UtcNow, AccessTokenInfo.DefaultRefreshMargin) == false)
        {
            return AccessToken;
        }

        return await refreshFlight.RunAsync(RefreshAsync);
    }
}
```

`RefreshAsync`는 기존 `RefreshIfNeededAsync`의 본문에서 **NeedsRefresh 판단만 걷어낸 것**이다(판단은 위로
올라갔다). 실패 시 조용히 넘어가고 **현재 토큰을 반환**하는 동작은 그대로 유지한다 — §3의 계약이자 §4
재시도 가드의 전제다.

`RefreshIfNeededAsync`는 **삭제한다.** 호출자가 0곳이고, 역할이 `GetAccessTokenAsync(false, ct)`로 흡수된다.

## 7. 배선

```csharp
// WebAPI.cs
private static IAccessTokenProvider accessTokenProvider;
public static void SetAccessTokenProvider(IAccessTokenProvider provider) => accessTokenProvider = provider;

private static readonly HttpClient authorized =
    new HttpClient(new BearerTokenHandler(new UnityWebRequestHandler(), new DeferredAccessTokenProvider(() => accessTokenProvider)));
```

`authorized`는 정적 필드라 **`SetAccessTokenProvider`보다 먼저 만들어질 수 있다.** 지금은 `Func<string>`
람다가 그 시차를 흡수하고 있다(호출 시점에 정적 필드를 읽는다). 포트로 바꾸면서도 같은 성질이 필요하므로,
**부를 때마다 현재 공급자를 찾아 넘기는** 얇은 어댑터를 클라 측에 둔다.

`Deferred`인 이유는 `Lazy<T>`(한 번 계산하고 캐시)와 다르기 때문이다 — 이건 매번 다시 읽는다. 공급자가
아직 없으면 `null`을 반환한다(= 헤더를 안 붙인다). 로그인 전 요청이 그렇게 동작해 왔고, 그 동작을 바꾸지
않는다.

```csharp
// RootLifetimeScope.cs
WebAPI.SetAccessTokenProvider(container.Resolve<AuthenticationService>());
```

`anonymous` 클라이언트는 **손대지 않는다.** 로그인·가입에 Bearer가 붙으면 안 되는 이유는 슬라이스 0에
주석으로 박혀 있다(만료 임박 토큰이 얹혀 나가 401 → 계정 유실 오판).

## 8. 테스트

GameFramework EditMode에 **10건 추가**. 기준선: GameFramework 어셈블리 146건 / 전체 EditMode 412건
(슬라이스 0 종료 시점).

**`BearerTokenHandlerTests`** (기존 파일 확장)

| 테스트 | 확인하는 것 |
|---|---|
| 200이면 강제 갱신을 부르지 않는다 | 정상 경로에 갱신이 끼어들지 않음 |
| 401이면 강제 갱신 후 1회 재전송한다 | 재전송에 **새** 토큰이 실림, 안쪽 핸들러 호출 2회 |
| 갱신해도 토큰이 그대로면 재전송하지 않는다 | 안쪽 호출 1회, 원래 401 반환 |
| 갱신이 null을 주면 재전송하지 않는다 | 위와 같은 가드의 다른 입구 |
| 재전송도 401이면 그대로 반환한다 | 안쪽 호출이 정확히 2회 (3회 아님) |
| 보내기 전 공급자를 forceRefresh=false로 부른다 | 미리 갱신 경로 |

기존 테스트 2건(헤더 부착/미부착)은 공급자 타입 변경에 맞춰 갱신한다.

`FakeHttpMessageHandler`는 **손대지 않는다** — 이미 있는 `Requests` 리스트로 호출 횟수를 센다. 다만
재전송 검증은 그것만으로 안 된다: 재전송은 **같은 `HttpRequestMessage` 인스턴스**를 다시 보내므로
`Requests[0]`과 `Requests[1]`이 같은 객체이고 헤더도 최종값 하나만 보인다. 따라서 **보낼 때의 헤더 값을
그 자리에서 기록**하는 방식으로 검증한다(테스트의 `onSend` 람다 안에서).

**`SingleFlightTests`** (신규)

| 테스트 | 확인하는 것 |
|---|---|
| 동시 호출 3개가 실제 실행 1번 | 실행 카운터 == 1, 셋 다 같은 값 |
| 끝난 뒤 다시 부르면 새로 실행한다 | 카운터 == 2 (결과를 캐시하지 않음) |
| 실패가 모든 대기자에게 전달된다 | 셋 다 같은 예외 |
| 실패 후 다음 호출은 새로 시도한다 | 실패를 캐시하지 않음 |

두 번째·네 번째가 핵심이다 — 캐시해 버리면 "한 번 실패한 뒤 영영 갱신 안 됨"이 된다.

## 9. 수동 검증

1b가 아직 없어서 **서버가 401을 주지 않는다.** 따라서 401 재시도 경로는 이 조각에서 끝까지 확인할 수
없다 — 단위 테스트로 덮고, 실제 401 경로는 **1b 배포 시 확인**한다(그때의 필수 확인 항목).

지금 확인 가능한 것:

1. **회귀 없음** — 로그인 → 로비 진입 → 매칭 요청/취소가 이전과 동일. ✅ (2026-08-06 확인)
2. **갱신이 실제 서버 상대로 끝까지 도는가** — `AccessTokenInfo.DefaultRefreshMargin`을 일시적으로
   **61분**으로 바꾸면 모든 인증 요청이 갱신을 유발한다. 이 상태로 평소처럼 플레이해 백엔드 로그에서
   `POST /auth/login` **뒤에 오는 요청이 200으로 처리되는지**, userId가 끝까지 같은지(계정 유실 없음),
   Unity 콘솔에 `[Auth]` 경고가 없는지 확인한다. 확인 후 값을 5분으로 복구.
   ✅ (2026-08-06 확인 — 갱신 13회 전부 200, 뒤따르는 요청 전부 200, userId 동일, 콘솔 경고 0)

   > **61분인 이유 — 59분은 안 된다.** 마진 59분은 "발급 후 1분이 지나야 갱신"을 뜻한다. 짧은 플레이가
   > 그 1분 안에 끝나면 갱신이 **한 번도 안 돈다**(실제로 첫 시도가 그렇게 헛돌았다). 마진이 토큰 수명
   > (60분)보다 커야 발급 직후부터 조건이 성립한다.

   > **동시 갱신 합치기는 이 방법으로 검증할 수 없다.** 로비 진입 요청은 `LoadUserComponent`에서
   > `await`로 **순차** 실행되므로 동시성이 생기지 않는다(초안은 "한꺼번에 나간다"고 잘못 전제했다).
   > 합치기는 `SingleFlight` 단위 테스트가 검증한다 — 동시 호출 3개→실행 1회, 동기 구간 재진입,
   > 실패 전파, 실패 후 재시도.

## 10. 산업 표준 매핑

| 우리 | 대응 |
|---|---|
| `IAccessTokenProvider` | Kiota / ASP.NET Core WASM의 동명 인터페이스, MSAL `AcquireTokenSilent`의 `forceRefresh` |
| `BearerTokenHandler`의 미리 갱신 | Azure.Core `BearerTokenAuthenticationPolicy`의 `TokenNeedsBackgroundRefresh` |
| 401 → 갱신 → 1회 재전송 | OkHttp `Authenticator`(반응형 인증), Azure.Core `AuthorizeRequestOnChallengeAsync` |
| 재시도 가드 | OkHttp "이미 Authorization이 붙어 있으면 포기" |
| `SingleFlight` | Go `singleflight`, Azure.Core `AccessTokenCache`의 gating |
| 공급자를 HTTP·소켓이 공유 | SignalR `HttpConnectionOptions.AccessTokenProvider` |

**검토했다가 안 쓴 것 — 타이머 주기 갱신.** Unity Gaming Services가 이 방식이다(백그라운드로 주기 갱신,
실패 시 `Expired` 이벤트). 우리는 안 쓴다 — 요청할 때 확인하는 방식은 **놀고 있을 때 아무 일도 안 한다.**
로비에서 오래 가만히 있다가 뭔가 누르면 그 순간 갱신되고 나간다. 타이머는 그동안 계속 재로그인을 때리고,
모바일 절전·복귀 처리가 따라붙는다. (참고로 UGS도 콘솔 3종에서는 자동 갱신이 안 돌아 수동 처리를 요구한다.)

## 11. 위험

| 위험 | 대응 |
|---|---|
| `UniTask`를 두 번 await해서 터짐 | `SingleFlight`가 `.Preserve()`로 보관. 동시 호출 테스트가 이걸 잡는다 |
| 실패를 캐시해 영영 갱신 안 됨 | "실패 후 다음 호출은 새로 시도" 테스트로 고정 |
| 정적 `authorized`가 공급자보다 먼저 생성됨 | `DeferredAccessTokenProvider` 어댑터 (§7). 현재 람다가 하던 역할과 동일 |
| 401 경로를 실제로 못 밟음 | 단위 테스트로 덮고 1b 배포 시 확인 (§9) |
