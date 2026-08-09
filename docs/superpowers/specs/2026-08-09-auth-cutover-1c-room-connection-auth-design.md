# 인증 cutover 1c — 방 접속 인증 설계

**날짜**: 2026-08-09
**선행**: 1a(클라 토큰 갱신, 완료) · 1b(로비/매칭 라우트 강제, 완료·배포·검증)
**관련**: `2026-08-06-auth-cutover-decisions.md` §8(1c 설계 제약)

---

## 1. 배경 — 지금 무엇이 열려 있나

방 접속은 사실상 무인증이다. 클라가 보내는 값이 이렇다 (`LOPNetworkAuthenticator.cs:59`):

```csharp
var customProperties = new CustomProperties
{
    userId = userDataStore.user.id,
    token = "token",        // ← 토큰 자리에 "token"이라는 문자열
    characterId = 0,
};
```

게임서버는 토큰을 **보지 않고**, `userId`가 매치 참가자 명단에 있는지만 확인한다
(`LOPNetworkAuthenticator.cs:94`).

```csharp
if (roomDataStore.match.playerList.Contains(msg.customProperties.userId) == false)
```

**결과**: 참가자의 userId와 방의 `ip:port`만 알면 그 사람 자리로 접속할 수 있다.
1a/1b로 HTTP는 닫혔지만 소켓은 그대로다.

### 접속만 막아서는 반쪽인 이유

접속 이후 클라→서버 메시지 세 종이 모두 **자기 신원을 스스로 적어 보내고, 서버가 그대로 믿는다.**

```protobuf
message GameInfoToS       { string user_id = 1; }
message InputCommandToS   { string session_id = 1; ... }
message StatAllocationToS { string session_id = 1; ... }
```

이게 사슬로 이어진다:

1. `GameInfoToS`에 남의 userId를 적어 보낸다
2. 서버가 그 사람의 `session_id`를 응답으로 돌려준다 (`GameInfoMessageHandler.cs:74`)
3. 그 `session_id`로 `InputCommandToS`를 보낸다
4. 남의 캐릭터를 조종한다

따라서 1c는 **접속 인증**과 **메시지 신원 귀속**을 함께 닫는다.

---

## 2. 결정 — introspect + 조회 전용 키

### 2.1 원래 잠가둔 방향과 달라진 점

로드맵/decisions에 잠가둔 방향은 "게임서버가 로비에 물어본다(RFC 7662 introspection) —
방마다 뜨고 지는 파드에 **서명키를 뿌리지 않는다**"였다. 그 방향은 유지한다.

**달라진 전제 하나**: RFC 7662 §2.1은 introspection 엔드포인트가 **호출자를 인증할 것**을 요구한다.
그리고 이 클러스터의 인그레스는 `/lobby(/|$)(.*)` 와일드카드로 로비를 통째로 외부에 노출한다
(`infrastructure/k8s/platform/ingress/ingress.yaml`). 즉 그냥 만들면
`/lobby/auth/introspect`가 인터넷에서 호출 가능해져, **훔친 토큰의 유효성을 무료로 확인해주는
오라클**이 된다.

→ **게임서버 파드에도 자격증명은 필요하다.** "파드에 아무것도 두지 않는다"는 introspection으로도
달성되지 않는다. 대신 두는 것을 **위조 불가능한 종류**로 만든다.

| 열쇠 | 보유처 | 할 수 있는 일 | 유출 시 |
|---|---|---|---|
| **서명키** `AUTH_JWT_SECRET` | 로비 · 매칭 | 토큰 발급 + 검증 | **모든 계정 사칭** |
| **조회 키** `INTERNAL_API_KEY` | 로비 · 게임서버 파드 | introspect 호출 | 토큰 유효성 조회만 (발급 불가) |

### 2.2 대안 비교

| 안 | 파드에 두는 것 | 접속 시 네트워크 호출 | 채택 여부 |
|---|---|---|---|
| **A. introspect + 조회 키** | 조회 전용 키 | 있음(로비 1회) | **채택** |
| B. 비대칭 서명(RS256) + 공개키 | 공개키(유출 무해) | 없음 | 미채택 |
| C. 매치 전용 1회용 입장 티켓 | 없음 | 없음 | 미채택 |

**A 채택 근거**

- 1a/1b에서 막 안정화한 **토큰 발급 경로를 건드리지 않는다**. B는 서명 알고리즘 교체라 이미 발급된
  토큰·로그인 전체에 영향이 간다.
- 나중에 **즉시 무효화(강제 로그아웃)** 가 필요해지면 introspection에 그대로 얹힌다. B에는 없다.
- C는 발급·보관·클라 전달 배관을 새로 만들어야 하고, 이미 있는 토큰을 놀린다.
- **산업 선례가 정확히 일치**: Steam은 게임서버가 *퍼블리셔 Web API 키*로
  `ISteamUserAuth/AuthenticateUserTicket`에 "이 티켓 진짜냐"를 질의한다. 우리 구조와 1:1이다.

**A의 비용(수용)**: 접속마다 로비 호출 1회 + 로비 가용성에 접속이 묶인다. 방당 접속은 소수(파티 규모)라
호출량은 무시할 수준이고, 로비가 죽으면 애초에 매칭이 성립하지 않으므로 새 결합이 아니다.

---

## 3. 아키텍처 — 접속 인증 흐름

```
클라                        게임 서버                     로비
 │                             │                          │
 │ ① StartClient 직전 토큰 갱신 │                          │
 │───────────────────────────────────────────────────────▶│
 │                             │                          │
 │ ② AuthRequestMessage        │                          │
 │   { userId, accessToken }   │                          │
 │────────────────────────────▶│                          │
 │                             │ ③ 명단 대조 (실패 시 즉시 거부)
 │                             │                          │
 │                             │ ④ POST /auth/introspect  │
 │                             │   X-Internal-Api-Key     │
 │                             │─────────────────────────▶│
 │                             │◀─────────────────────────│
 │                             │   { active, sub, exp }   │
 │                             │                          │
 │                             │ ⑤ active && sub == userId│
 │◀────────────────────────────│                          │
 │   AuthResponseMessage       │                          │
```

**③이 ④보다 먼저인 이유(순서 고정)**: 소켓 연결 한 번(싸다)이 HTTP 호출 한 번(비싸다)으로 증폭되는
것을 막는다. 명단 대조는 메모리 조회라 공짜이고, 명단 밖 userId는 로비를 부르지 않고 떨어진다.

**⑤가 필요한 이유**: ④는 "이 토큰의 주인은 B"까지만 알려준다. ③을 A의 이름으로 통과했을 수 있으므로
**토큰 주인과 주장한 이름이 같은지**를 확인해야 사슬이 닫힌다.

**저장은 `sub` 기준**: `conn.authenticationData`에 넣는 `CustomProperties.userId`는 클라가 보낸 값이
아니라 **introspect가 돌려준 `sub`** 로 덮어쓴다. 이후 모든 서버 로직(§7)이 이 값을 신원으로 쓴다.

---

## 4. 조회 키 — 배치와 전달

### 4.1 Secret 분리 (필수)

**서명키와 같은 Secret에 넣지 않는다.** 1b에서 만든 `auth-secret`은 `AUTH_JWT_SECRET`을 담고
로비/매칭이 `envFrom: secretRef`로 통째로 주입한다. 게임서버 파드가 같은 Secret을 참조하면
서명키까지 딸려 들어가 이번 작업의 목적이 무너진다.

```
auth-secret            : AUTH_JWT_SECRET      → 로비, 매칭
internal-api-secret    : INTERNAL_API_KEY     → 로비, 게임서버 파드
```

- **로비**: `envFrom: - secretRef: { name: internal-api-secret }` (해당 Secret에 키가 하나뿐이라 안전)
- **게임서버 파드**: `secretKeyRef`로 **그 키 하나만** 주입 — `envFrom` 금지

### 4.2 게임서버 파드로 가는 경로

게임서버 파드는 정적 매니페스트가 아니라 룸 서버가 런타임에 만든다
(`lop-backend/apps/room-server/src/services/room.service.ts:143`). 파드 스펙의 `env` 배열에 항목을 추가한다:

```ts
env: [
    { name: 'ROOM_ID', value: room.id },
    { name: 'PORT', value: String(port) },
    //  값이 아니라 참조를 적는다 — 룸 서버 프로세스는 키 값을 만지지 않는다.
    { name: 'INTERNAL_API_KEY', valueFrom: { secretKeyRef: { name: 'internal-api-secret', key: 'INTERNAL_API_KEY' } } },
],
```

**룸 서버 자신은 키를 알 필요가 없다** — 쿠버네티스가 파드 기동 시 직접 꽂는다.

### 4.3 부트스트랩

`infrastructure/README.md`의 시크릿 생성 절에 한 줄 추가한다(1b의 `auth-secret`과 같은 형식):

```bash
kubectl create secret generic internal-api-secret --from-literal=INTERNAL_API_KEY='<값>'
```

---

## 5. 엔드포인트 계약 — `POST /auth/introspect`

RFC 7662(OAuth 2.0 Token Introspection)를 따른다.

### 5.1 요청

```
POST /auth/introspect
Content-Type: application/json
X-Internal-Api-Key: <조회 키>

{ "token": "eyJhbGciOi..." }
```

### 5.2 응답

| 상황 | 상태 | 본문 |
|---|---|---|
| 토큰 유효 | 200 | `{ "active": true, "sub": "<userId>", "exp": 1754... }` |
| 토큰 위조/만료/형식오류 | **200** | `{ "active": false }` |
| 조회 키 없음/틀림 | 401 | `{ "message": "..." }` |
| 본문 형식 오류 | 400 | (기존 `validationMiddleware`) |

**가짜 토큰에 401이 아니라 200을 주는 것이 표준이며, 이 설계에서 반드시 지켜야 한다.**
401은 "부른 쪽의 자격이 없다"는 뜻으로 예약된다. 둘을 섞으면 게임서버가
"내 조회 키가 잘못됐다"와 "플레이어 토큰이 가짜다"를 구분할 수 없고, 운영 중 조회 키 오배포를
"플레이어 잘못"으로 오진하게 된다.

**`active: false`에 이유를 붙이지 않는다** — 만료인지 위조인지 알려주면 밖에서 토큰 상태를 떠볼 수 있다
(1b의 `login` 401 처리와 같은 원칙).

### 5.3 서버 측 구현

- 미들웨어: `internalApiKeyMiddleware`(신규, `@lop/server-core`) → `validationMiddleware(IntrospectRequestDto, 'body')`
- 키 비교는 **`crypto.timingSafeEqual`** — 일반 `===`는 앞 글자부터 순차 비교라 응답 시간으로 키를
  한 글자씩 알아낼 수 있다(1a의 `Jwt.ConstantTimeEquals`와 같은 이유). 길이가 다르면 비교 전에 거부한다.
- `INTERNAL_API_KEY` 미설정은 **서버 설정 오류(500)** 로 다루고 요청은 통과시키지 않는다.
  기동 시 `validateEnv({ AUTH_JWT_SECRET: str(), INTERNAL_API_KEY: str() })`로 먼저 막는다.
- 검증은 기존 `verifyAccessToken`을 재사용한다. `exp`는 `jwt.decode`가 아니라 **검증된 payload**에서 읽는다.
- **레이트리밋은 걸지 않는다** — 호출자가 키로 이미 제한돼 있고, 방 하나가 접속 폭주 시
  자기 자신의 입장을 막게 된다. (키 유출 시의 남용은 키 회전으로 대응한다.)

---

## 6. 게임서버 인증기

### 6.1 비동기 수락

`OnAuthRequestMessage`는 동기 콜백이지만, Mirror는 **`ServerAccept` 호출을 늦춰도 된다**
(`NetworkAuthenticator`에 자체 타임아웃이 없다). 그래서 introspect를 기다렸다가 수락한다.

```csharp
public void OnAuthRequestMessage(NetworkConnectionToClient conn, AuthRequestMessage msg)
{
    AuthenticateAsync(conn, msg).Forget();
}
```

### 6.2 판정 순서와 실패 처리

| 단계 | 실패 시 |
|---|---|
| 이 연결이 이미 인증 요청을 보냈나 | **무시**(응답도 안 보냄) — 호출 증폭 차단 |
| 명단(`match.playerList`)에 없나 | 거부. **로비를 부르지 않는다** |
| (에디터면 여기서 수락 — §6.4) | — |
| `INTERNAL_API_KEY`가 없나 | 거부 + `Debug.LogError` (설정 오류) |
| introspect 실패/타임아웃(3초) | **거부** — 확인 못 하면 막는 쪽 |
| `active == false` | 거부 |
| `sub != 주장한 userId` | 거부 |
| 수락 직전 연결이 끊겼나 | 아무것도 하지 않음 |

- 타임아웃은 `CancellationTokenSource(TimeSpan.FromSeconds(3))`로 준다. 공유 `HttpClient.Timeout`은
  30초라 그대로 두면 죽은 로비를 30초간 기다린다.
- 중복 요청 판별은 `HashSet<int>`(connectionId). 방 하나의 수명은 짧고 연결 수는 참가자 수로
  묶이므로 별도 정리 없이 `OnStopServer`에서 통째로 비운다.
- 거부 시에도 클라에는 기존과 같은 `AuthResponseMessage { code = 401 }`을 보낸다 — 사유를 나누지 않는다.

### 6.3 introspect 호출

`WebAPI`에 추가한다. **`SendAsync<T>`(전역 발행 버전)를 쓰지 않는다** — 구독자가 없는데
`GlobalMessagePipe.GetPublisher<T>()`를 도는 것은 IL2CPP에서 open generic 미지원으로 터질 수 있고
(decisions §8의 그 함정), 브로커를 등록할 이유도 없다. `httpClient.SendAsync<T>`를 직접 쓴다.

```csharp
public static UniTask<IntrospectResponse> Introspect(string accessToken, CancellationToken cancellationToken = default)
```

- URL: `{EnvironmentSettings.active.lobbyBaseURL}/auth/introspect`
- 헤더: `X-Internal-Api-Key: {Environment.GetEnvironmentVariable("INTERNAL_API_KEY")}`
- 응답 DTO `IntrospectResponse { bool active; string sub; long exp; }` — `HttpResponse`를 상속하지 않는다
  (이 엔드포인트는 `code` 봉투를 쓰지 않는다).

### 6.4 에디터 정책

에디터의 게임서버는 이미 **가짜 방·가짜 매치 명단**으로 돈다
(`ConfigureRoomComponent.cs`의 `#if UNITY_EDITOR` 블록). introspect도 같은 경계에 둔다:

- `#if UNITY_EDITOR`이면 명단 대조까지만 하고 introspect를 **건너뛴다**.
- 건너뛸 때 `Debug.LogWarning`으로 한 번 알린다 — 조용히 인증이 꺼져 있는 상태를 만들지 않는다.
- 이때 `authenticationData.userId`는 `sub`이 없으므로 **클라가 주장한 값**을 그대로 쓴다. 에디터
  전용 경로이며, 실환경에서는 §3대로 `sub`이 들어간다.

**근거**: 조회 키를 git에 커밋하지 않기 위해서다. 에디터에 키를 넣으려면 (a) 에셋/`.env`에 커밋하거나
(b) 개발자가 매번 환경변수를 세팅해야 하는데, (a)는 1b 후속(커밋된 `.env`에서 키 제거)을 역행하고
(b)는 두 에디터를 동시에 띄우는 현 개발 흐름에 마찰이 크다.

**이 예외가 썩지 않도록**: 엔드포인트는 백엔드 통합 테스트가 덮고, 실제 경로는 배포 환경에서
수동 검증한다(§10). 클라가 진짜 토큰을 싣는 경로는 에디터에서도 그대로 돈다.

---

## 7. 클라이언트 — 접속 전 갱신

### 7.1 제약

`OnClientAuthenticate()`는 **동기 Mirror 콜백이라 `await`할 수 없다.** 그 안에서 갱신을 시작하면
갱신이 끝나기 전에 함수가 반환된다. 따라서 **`StartClient()` 이전에** 갱신을 끝내야 한다
(decisions §8).

### 7.2 갱신 책임을 인증기가 갖는다

"다른 파일에서 미리 갱신해 두기"는 그 관계가 인증기 쪽에서 보이지 않아 조용히 깨진다.
인증기에 비동기 준비 메서드를 두고, 호출자가 그것을 기다린다.

```csharp
// LOPNetworkAuthenticator (클라)
public async UniTask PrepareCredentialAsync(CancellationToken cancellationToken)
{
    preparedAccessToken = await accessTokenProvider.GetAccessTokenAsync(false, cancellationToken);
}

public override void OnClientAuthenticate()
{
    NetworkClient.Send(new AuthRequestMessage
    {
        customProperties = new CustomProperties
        {
            userId = userDataStore.user.id,
            accessToken = preparedAccessToken,
            characterId = 0,
        },
    });
}
```

```csharp
// LOPRoom.ConnectRoomServerAsync
await ((LOPNetworkAuthenticator)networkManager.authenticator).PrepareCredentialAsync(destroyCancellationToken);
networkManager.StartClient();
```

인증기는 `IAccessTokenProvider`(1a에서 도입)를 주입받는다. 구체 타입(`AuthenticationService`)이 아니라
인터페이스로 받는다 — 1a가 그 목적으로 만든 포트다.

### 7.3 토큰 신선도는 기본 마진으로 충분

`GetAccessTokenAsync(forceRefresh: false, ...)`는 만료 5분 전부터만 갱신하므로, 55분 된 토큰이
그대로 나갈 수 있다(decisions §8이 판단하라고 남긴 항목).

**추가 신선도 요구를 두지 않는다.** 게임서버는 **접속 시점에 한 번만** 검사하고, 접속 후에는 토큰을
다시 보지 않는다. 매치 길이(현재 5분)보다 남은 수명이 짧아도 이미 수락된 연결은 영향받지 않는다.
`forceRefresh: true`를 쓰면 1a의 30초 스로틀과 얽혀 접속 지연만 생긴다.

---

## 8. 인게임 메시지 신원 귀속

### 8.1 지금 — 받아놓고 버린다

```csharp
// LOPRoom.cs:99 (서버)
NetworkServer.RegisterHandler<CustomMirrorMessage>((conn, message) =>
{
    dispatcher.Dispatch(message.payload);   // conn을 버린다
});
```

### 8.2 바뀐 뒤 — 연결에서 확인된 신원을 함께 넘긴다

```csharp
NetworkServer.RegisterHandler<CustomMirrorMessage>((conn, message) =>
{
    var customProperties = conn.authenticationData as CustomProperties;
    dispatcher.Dispatch(customProperties.userId, message.payload);
});
```

`conn.authenticationData`는 §3에서 서버가 introspect 결과(`sub`)로 직접 채운 값이라 클라가 건드릴 수 없다.
`CustomMirrorMessage` 핸들러는 Mirror 기본값대로 인증된 연결만 받으므로(`RegisterHandler`의
`requireAuthentication` 기본 `true`) 여기서 `authenticationData`가 비어 있을 수는 없다.

MessagePipe는 페이로드를 하나만 실어 나르므로 봉투 타입을 둔다:

```csharp
/// <summary>클라가 보낸 메시지와, 그 연결에서 서버가 확인한 신원을 함께 나른다.</summary>
public readonly struct ClientMessage<T> where T : IMessage
{
    public string UserId { get; }
    public T Message { get; }
}
```

핸들러 세 곳이 이렇게 바뀐다:

```csharp
// 전
ISession session = sessionManager.GetSessionById(inputCommandToS.SessionId);
// 후
ISession session = sessionManager.GetSessionByUserId(received.UserId);
```

**연결 객체(`NetworkConnectionToClient`)를 통째로 넘기지 않는 이유**: 현재 핸들러들은 Mirror 타입을
전혀 모른다. 연결을 넘기면 Mirror 의존이 게임 로직까지 번진다. 확인된 `userId` 하나만 넘기면 그
경계가 유지된다. (Mirror 자신은 `(conn, msg)` 두 인자로 넘기고, 메시징 프레임워크의 통용 모양도
"메시지 + 발신자 메타"의 짝이다 — MassTransit `ConsumeContext<T>`의 `.Message` + 발신 정보.)

### 8.3 필드 자체를 제거한다

가장 확실한 방어는 **보내지 않는 것**이다. 안 보내면 위조할 대상이 없다.

| proto | 변경 |
|---|---|
| `GameInfoToS` | `user_id` 삭제 → 빈 메시지("준비됐다" 신호) |
| `InputCommandToS` | `session_id` 삭제 |
| `StatAllocationToS` | `session_id` 삭제 |

지운 자리에는 **`reserved 1;`** 을 남긴다 — 나중에 다른 필드가 같은 번호를 재사용하면, 배포 시점이
어긋난 구버전이 옛 값을 새 필드로 읽는다.

클라 송신부 3곳만 손대면 된다: `LOPRoom.cs:115`, `PlayerInputManager.cs:117`, `StatsViewModel.cs:72`.

**재생성 도구**: 이 저장소는 `Tools/Protobuf/protoc-28.2-win64`만 갖고 있어 macOS에서 돌지 않는다.
같은 버전(28.2)의 macOS universal 바이너리를 `Tools/Protobuf/protoc-28.2-osx-universal`로 함께
넣고 `compile_protos.sh`가 플랫폼을 분기하게 한다. **동일 출력은 실측으로 확인했다** — `.proto`를
고치지 않은 채 28개를 전부 재생성해 커밋본과 비교한 결과 내용이 다른 파일 0개(바이트 동일).

**`generate_protos.sh`가 아니라 `compile_protos.sh`만 돌린다.** 전자는 `Runtime.Generated/Scripts/Protobuf`를
`rm -rf`로 지우고 시작해 `.meta` 파일이 전부 새 GUID로 재생성되며, 3개 필드 변경에 수십 개 파일이
흔들린다. 후자는 기존 폴더에 덮어쓰므로 실제로 바뀐 `.cs` 3개만 diff에 남는다.

**서버→클라 방향의 `GameInfoToC.SessionId`는 유지한다** — 클라가 자기 세션을 아는 것은 정상이며
위조 문제와 무관하다. (클라가 로컬에서 쓰는 `playerContext.session`도 그대로 둔다.)

**서버→클라 방향의 `GameInfoToC.SessionId`는 유지한다** — 클라가 자기 세션을 아는 것은 정상이며
위조 문제와 무관하다. (클라가 로컬에서 쓰는 `playerContext.session`도 그대로 둔다.)

---

## 9. 삭제 · 리네임

| 대상 | 처리 | 근거 |
|---|---|---|
| `CustomProperties.token` (클·서) | → `accessToken` | 실제로 담기는 것이 액세스 토큰임을 이름이 말하게 |
| `GameFramework.Auth.Jwt` + `JwtTests` | **삭제** | 사용처가 없다. 이 설계는 파드에서 로컬 서명 검증을 하지 않으므로 앞으로도 없다 |

`Jwt` 삭제는 `.meta` 파일까지 함께 커밋한다.

---

## 10. 테스트

### 10.1 백엔드 통합 테스트 (`apps/lobby-server/test/integration/`)

기존 하네스(testcontainers postgres/redis)를 그대로 쓴다.

| 케이스 | 기대 |
|---|---|
| 조회 키 헤더 없음 | 401 |
| 조회 키 틀림 | 401 |
| 조회 키 정상 + 진짜 토큰 | 200, `active: true`, `sub == userId` |
| 조회 키 정상 + 위조 토큰 | 200, `active: false` |
| 조회 키 정상 + 만료 토큰 | 200, `active: false` |
| 조회 키 정상 + `token` 필드 누락 | 400 |
| `active: false` 응답에 `sub`/`exp` 없음 | 정보 누출 없음 확인 |

### 10.2 게임서버 — 자동 테스트를 붙일 수 없다

**Unity 앱 프로젝트(클라·서버) 어느 쪽에도 asmdef가 없다.** 모든 앱 코드가 `Assembly-CSharp`에 있고,
테스트 어셈블리(asmdef)는 `Assembly-CSharp`을 참조할 수 없다(Unity의 미리 정의된 어셈블리는 참조
방향이 반대다). 현재 클라에서 도는 434건은 전부 *패키지*(GameFramework / LOP-Shared /
MasterData-Client)의 테스트가 `testables`로 실행되는 것이다.

따라서 게임서버 판정 로직에 유닛 테스트를 붙이려면 서버 프로젝트에 asmdef 구조를 도입해야 하고,
그것은 이 슬라이스의 범위를 넘는 별도 작업이다. **1c에서 게임서버 측 검증은 컴파일 클린 + §10.3
수동 검증이다.** 아래 판정 케이스는 그 수동 검증에서 확인할 목록으로 남긴다.

| 케이스 | 기대 |
|---|---|
| 명단 밖 userId | 거부 |
| 위조/만료 토큰(`active: false`) | 거부 |
| `sub != 주장한 userId` | 거부 |
| 로비 무응답 | 거부 |
| 정상 | 수락 |

**Unity 앱 프로젝트의 테스트 가능성 확보(asmdef 도입)는 §13 후속으로 올린다.**

### 10.3 수동 검증 (배포 환경)

1. 정상 매칭 → 방 입장 성공, 로비 로그에 `POST /auth/introspect 200`
2. 클라에서 토큰을 일부러 훼손 → 방 입장 거부(1b의 401 검증과 같은 임시 코드 방식, 검증 후 되돌림)
3. 게임서버 파드 env에 `INTERNAL_API_KEY`가 실제로 꽂혔는지 확인
4. 밖에서 `POST /lobby/auth/introspect`를 키 없이 호출 → 401

### 10.4 회귀

- 클라 EditMode 전체 (현재 434건) green
- 클·서 컴파일 클린 (UnityMCP, `unity_instance` 명시)

---

## 11. 배포 순서

1b에서 확인된 제약(`backend-deploy`의 `bump-tags`가 infrastructure `main`을 체크아웃한다)이 그대로 적용된다.

```
① infrastructure   — internal-api-secret 참조 추가 + README
② lop-backend      — introspect 엔드포인트 + 룸 서버 파드 스펙
③ 게임서버 이미지    — 검사 활성화
④ 클라이언트        — 진짜 토큰 송신
```

- **Secret은 배포 전에 클러스터에 먼저 만들어 둔다.** 없으면 새 파드가 크래시 루프에 빠진다.
- ②까지는 기존 클라·게임서버가 그대로 동작한다(엔드포인트가 늘 뿐).
- **③과 ④는 붙여서 내보낸다.** ③만 나가면 아직 `"token"`을 보내는 구 클라가 전부 입장 거부된다.
  (proto 필드 제거도 ③④ 동시 배포를 전제한다.)

---

## 12. 산업 표준 매핑

| 우리 것 | 대응 |
|---|---|
| `POST /auth/introspect`, `{ active, sub, exp }` | RFC 7662 OAuth 2.0 Token Introspection |
| 가짜 토큰 → 200 `active:false`, 호출자 실패 → 401 | RFC 7662 §2.2 / §2.3 |
| 게임서버가 전용 키로 플랫폼에 티켓 진위 질의 | Steam `ISteamUserAuth/AuthenticateUserTicket` + 퍼블리셔 Web API 키 |
| 서명키는 발급자에만, 검증자는 조회만 | 최소 권한(least privilege) — 키 등급 분리 |
| `ClientMessage<T>`(메시지 + 확인된 발신자) | Mirror `(NetworkConnectionToClient, T)` / MassTransit `ConsumeContext<T>` |
| 지연 `ServerAccept` | Mirror `NetworkAuthenticator`의 비동기 인증 관용 |
| 확인 실패 시 거부(fail closed) | 인증 일반 원칙 |

---

## 13. 범위 밖 — 후속

- **`characterId` 검증**: 지금 하드코딩 `0`이며 인증과 무관하다. 캐릭터 선택이 생길 때 "이 유저가 이
  캐릭터를 소유하는가"를 확인해야 한다.
- **토큰 즉시 무효화(강제 로그아웃)**: introspection이 그 자리를 열어두지만 지금은 필요가 없다.
  필요해지면 `active` 판정에 폐기 목록을 더한다.
- **내부 전용 라우트 전반 보호**: 1b 후속으로 남아 있는 `PUT /room/heartbeat/:id`, `PUT /room/status`,
  `PUT /user/location`, `POST /room`, `DELETE /room/:id`. 이번에 만드는 `internalApiKeyMiddleware`를
  그대로 붙이면 된다. **1c에서는 introspect에만 적용한다**(범위 유지).
- **게임서버 HTTP 전반에 조회 키 부착**: 위 항목이 진행되면 매 호출에 헤더를 붙이는 `DelegatingHandler`
  (1a의 `BearerTokenHandler`와 같은 모양)로 승격한다. 지금은 introspect 한 곳이라 호출부에서 직접 붙인다.
- **에디터 introspect 경로 실행**: 로컬 시크릿 관리 방식(SealedSecrets/SOPS 등)을 도입하면 에디터
  예외를 없앨 수 있다. 그 트랙과 함께 재검토한다.
- **Unity 앱 프로젝트 테스트 가능성(asmdef 도입)**: 클라·서버 모두 앱 코드가 `Assembly-CSharp`에 있어
  유닛 테스트를 붙일 수 없다(§10.2). 피처 단위 asmdef 분리는 `architecture-guidelines.md`가 이미
  목표 구조로 정의해 두었으므로, 그 작업의 일부로 다룬다.
- **인증 대기 연결의 상한**: 현재 미인증 연결은 introspect 3초 안에 정리되지만, 연결 자체의 개수 제한은
  Mirror 기본값에 맡긴다. 방 하나가 소수 인원이라 지금은 문제가 아니다.

---

## 14. 결정 요약

1. **게임서버는 로비에 물어본다** — 서명키는 파드에 두지 않는다.
2. **조회 전용 키를 별도 Secret으로** 파드에 준다 — introspection 오라클을 막기 위해 필요하고,
   유출돼도 토큰 위조는 불가능하다.
3. **판정 순서 = 명단 → introspect → `sub` 일치**. 신원은 `sub`를 진실로 저장한다.
4. **확인 실패는 전부 거부**(fail closed). 로비 무응답 포함, 타임아웃 3초.
5. **클라는 `StartClient()` 전에 갱신**하고, 그 책임은 인증기가 갖는다.
6. **인게임 메시지의 신원 필드를 proto에서 제거**하고, 서버가 연결에서 확인한 신원을 쓴다.
7. **에디터는 introspect를 건너뛴다**(키를 커밋하지 않기 위해). 경고 로그를 남긴다.
