# 세션 신원을 연결 기준으로 — 구현 계획 (슬라이스 2a)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 접속 메시지에서 클라가 주장하는 userId를 없애고, 서버가 신원과 세션을 **연결에서** 도출하게 한다.

**Architecture:** 신원은 토큰의 `sub` 하나에서만 나온다. 인증이 끝나면 `conn.authenticationData`에 서버 확정 신원(`ConnectionIdentity`)을 싣고, 세션이 만들어질 때 그 안에 `SessionId`를 채워 **연결이 자기 세션을 가리키게** 한다. 이후 수신·해제는 계정 id를 경유하지 않는다.

**Tech Stack:** Unity 2022 + Mirror(kcp2k) + VContainer + MessagePipe + UniTask

**Spec:** `LeagueOfPhysical-Client/docs/superpowers/specs/2026-08-09-session-identity-by-connection-design.md`

## Global Constraints

- **판정 순서는 `중복가드 → introspect → sub가 명단에 있나`.** 명단 대조가 introspect **뒤로** 간다 — 주장한 userId가 없어져 미리 걸러낼 값이 없기 때문이다.
- **신원은 `sub`에서만 나온다.** 클라가 보낸 값을 신원으로 쓰는 코드가 남아 있으면 이 슬라이스는 실패다.
- 실패는 전부 거부(fail closed). introspect 예외·타임아웃(3초)·`null`·`active:false`·명단 밖 전부.
- `conn.authenticationData`에는 **`ConnectionIdentity`만** 넣는다. 클라 와이어 타입(`CustomProperties`)을 얹지 않는다 — 접근 토큰이 연결 수명 내내 남는 문제도 이걸로 사라진다.
- **`ISessionManager`/`SessionManager`(GameFramework)는 건드리지 않는다.** Mirror 개념을 앱 비종속 계층에 넣지 않는다.
- 해제는 **`ReferenceEquals(session.networkConnection, conn)`이 참일 때만** 정리한다.
- 에디터 클레임 리더는 **서명을 검증하지 않으며** 파일 전체를 `#if UNITY_EDITOR`로 감싼다 — 플레이어 빌드에 컴파일되지 않게.
- 재접속 정책(같은 계정이면 세션 재사용)과 동시 접속 정책은 **바꾸지 않는다**.
- 주석: 한국어, **왜**만. 코드로 자명한 것은 쓰지 않는다.
- 새 `.cs`에는 Unity가 만든 `.meta`를 함께 커밋한다. 손으로 만들지 않는다.
- UnityMCP는 **호출마다 `unity_instance` 명시.** 서버 = `LeagueOfPhysical-Server@37450a0ab4f67bdd`, 클라 = `LeagueOfPhysical-Client@dc70ef3a594a3fe0`.
- 커밋은 `git add <명시 경로>`만. 두 Unity 레포 모두 무관한 변경/미추적 파일이 있다.

---

## File Structure

**LeagueOfPhysical-Server**
- 신규 `Assets/Scripts/Room/ConnectionIdentity.cs` — 인증된 연결의 서버 측 신원
- 신규 `Assets/Scripts/Room/EditorAccessTokenClaims.cs` — 에디터 전용 `sub` 리더(검증 없음)
- 수정 `Assets/Scripts/Room/CustomProperties.cs` — `userId` 제거
- 수정 `Assets/Scripts/Room/LOPNetworkAuthenticator.cs` — 판정 순서, 에디터 경로, 인증 타임아웃, `ConnectionIdentity` 저장
- 수정 `Assets/Scripts/Network/ClientMessage.cs` — `UserId` → `ISession Session`
- 수정 `Assets/Scripts/Network/NetworkMessageDispatcher.cs` — 세션을 나른다
- 수정 `Assets/Scripts/Room/LOPRoom.cs` — 수신 경계·접속·해제
- 수정 `Assets/Scripts/Game/MessageHandler/{GameInfo,GameInput,GameEntity}MessageHandler.cs`

**LeagueOfPhysical-Client**
- 수정 `Assets/Scripts/Room/CustomProperties.cs` — `userId` 제거
- 수정 `Assets/Scripts/Room/LOPNetworkAuthenticator.cs` — `userId` 대입 제거 + 죽은 주입 제거

> **와이어 호환**: `CustomProperties`는 `AuthRequestMessage` 안에 실려 Mirror Weaver가 직렬화한다. **필드 구성이 양쪽에서 같아야 한다** — 클·서를 함께 배포해야 하고, 한쪽만 나가면 인증이 깨진다.

---

## Task 1: 서버 — 신원을 `sub`에서만 얻는다

**Repo:** `LeagueOfPhysical-Server`

**Files:**
- Create: `Assets/Scripts/Room/ConnectionIdentity.cs` (+ `.meta`)
- Create: `Assets/Scripts/Room/EditorAccessTokenClaims.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Room/CustomProperties.cs`
- Modify: `Assets/Scripts/Room/LOPNetworkAuthenticator.cs`

**Interfaces:**
- Produces: `ConnectionIdentity { string UserId { get; }  string SessionId { get; set; } }`, ctor `ConnectionIdentity(string userId)`
- Produces: `conn.authenticationData`는 이제 **`ConnectionIdentity`** 다(이전엔 `CustomProperties`). Task 2가 이걸 읽는다.
- Produces: `CustomProperties { string accessToken; int characterId; }`

- [ ] **Step 1: `ConnectionIdentity`를 만든다**

`Assets/Scripts/Room/ConnectionIdentity.cs`:

```csharp
namespace LOP
{
    /// <summary>인증이 끝난 연결의 서버 측 신원. 클라가 보낸 값이 아니라 서버가 확정한 것만 담는다.</summary>
    public class ConnectionIdentity
    {
        public string UserId { get; }

        //  세션이 만들어지는 시점(LOPRoom.OnPlayerConnect)에 채워진다. Mirror가 인증 완료와 접속
        //  콜백을 따로 부르기 때문에 한 번에 다 채울 수 없다.
        public string SessionId { get; set; }

        public ConnectionIdentity(string userId)
        {
            UserId = userId;
        }
    }
}
```

- [ ] **Step 2: 에디터 전용 클레임 리더를 만든다**

`Assets/Scripts/Room/EditorAccessTokenClaims.cs`:

```csharp
#if UNITY_EDITOR
using System;
using System.Text;

namespace LOP
{
    /// <summary>에디터 전용. 액세스 토큰 페이로드에서 <c>sub</c>만 꺼낸다.
    /// <para><b>서명을 검증하지 않는다.</b> 검증은 로비 introspect의 몫이고, 에디터는 조회 키를 git에
    /// 커밋하지 않으려고 introspect를 건너뛴다. 그 경계 안에서 "누가 접속했나"만 알기 위한 것이며,
    /// 파일 전체가 <c>UNITY_EDITOR</c>로 묶여 플레이어 빌드에는 들어가지 않는다.</para></summary>
    public static class EditorAccessTokenClaims
    {
        public static bool TryReadSubject(string accessToken, out string subject)
        {
            subject = null;

            if (string.IsNullOrEmpty(accessToken))
            {
                return false;
            }

            string[] parts = accessToken.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            string payload = DecodeBase64Url(parts[1]);
            if (payload == null)
            {
                return false;
            }

            const string key = "\"sub\":\"";
            int start = payload.IndexOf(key, StringComparison.Ordinal);
            if (start < 0)
            {
                return false;
            }

            start += key.Length;
            int end = payload.IndexOf('"', start);
            if (end < 0)
            {
                return false;
            }

            subject = payload.Substring(start, end - start);
            return string.IsNullOrEmpty(subject) == false;
        }

        private static string DecodeBase64Url(string value)
        {
            try
            {
                string padded = value.Replace('-', '+').Replace('_', '/');
                padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
                return Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            }
            catch
            {
                return null;
            }
        }
    }
}
#endif
```

- [ ] **Step 3: `CustomProperties`에서 `userId`를 뺀다**

`Assets/Scripts/Room/CustomProperties.cs`의 필드를 이렇게 남긴다:

```csharp
        public string accessToken;
        public int characterId;
```

- [ ] **Step 4: 판정 로직을 바꾼다**

`Assets/Scripts/Room/LOPNetworkAuthenticator.cs`에서 `DecideAsync`와 `Accept`를 아래로 교체하고, `AcceptIfOnRoster`를 새로 넣는다. `OnAuthRequestMessage`/`AuthenticateAsync`/`Reject`와 `handledConnections` 필드는 **그대로 둔다**.

```csharp
        private async UniTask DecideAsync(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            string accessToken = msg.customProperties?.accessToken;

#if UNITY_EDITOR
            //  에디터의 게임서버는 가짜 방·가짜 명단으로 돈다(ConfigureRoomComponent). 조회 키를 git에
            //  커밋하지 않으려고 introspect도 같은 경계 안에 둔다. 실환경에서는 아래 #else를 반드시 탄다.
            Debug.LogWarning("[Auth] 에디터라 introspect를 건너뜁니다. 서명을 검증하지 않고 sub만 읽습니다.");

            if (EditorAccessTokenClaims.TryReadSubject(accessToken, out string editorUserId) == false)
            {
                Reject(conn, "토큰에서 sub를 읽지 못했습니다");
                return;
            }

            AcceptIfOnRoster(conn, editorUserId);
#else
            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("INTERNAL_API_KEY")))
            {
                Debug.LogError("[Auth] INTERNAL_API_KEY가 없습니다. 접속을 허용할 수 없습니다.");
                Reject(conn, "server misconfigured");
                return;
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(IntrospectTimeoutSeconds));
            IntrospectResponse introspect = await WebAPI.Introspect(accessToken, timeout.Token);

            if (introspect == null || introspect.active == false)
            {
                //  응답 본문이 비어 있으면(게이트웨이 이상 등) 역직렬화 결과가 null이다 — 토큰이
                //  유효하지 않은 경우와 동일하게 거부한다(fail closed).
                Reject(conn, "토큰이 유효하지 않음");
                return;
            }

            AcceptIfOnRoster(conn, introspect.sub);
#endif
        }

        //  명단 대조가 토큰 확인 *뒤에* 온다. 클라가 이름을 주장하지 않으므로 확인 전에는 대조할
        //  값 자체가 없다 — 신원은 토큰에서만 나온다.
        private void AcceptIfOnRoster(NetworkConnectionToClient conn, string userId)
        {
            if (roomDataStore.match.playerList.Contains(userId) == false)
            {
                Reject(conn, $"명단에 없는 참가자: {userId}");
                return;
            }

            Accept(conn, userId);
        }

        private void Accept(NetworkConnectionToClient conn, string authenticatedUserId)
        {
            //  connectionId만 보고 "살아있다"고 판단하면 안 된다 — kcp2k는 connectionId를 클라 주소로
            //  만들어 재접속 시 재사용한다. 로비에 물어보는 ≤3초 사이 같은 클라가 끊겼다 재접속하면,
            //  같은 connectionId를 가진 *다른* 연결 객체가 딕셔너리에 들어와 있을 수 있다. 그러면 이
            //  presence-only 검사는 통과하지만 우리가 들고 있는 conn은 이미 죽은 객체라, 그 연결을
            //  accept해도 아무것도 받지 못하는 좀비 세션이 된다. 그래서 존재 여부가 아니라 "지금
            //  등록된 연결이 바로 이 conn 객체인가"(참조 동일성)까지 확인한다.
            if (NetworkServer.connections.TryGetValue(conn.connectionId, out var current) == false || ReferenceEquals(current, conn) == false)
            {
                return;
            }

            //  클라가 보낸 객체를 그대로 얹지 않는다 — 서버가 확정한 신원만 담은 타입을 새로 만든다.
            //  덤으로 접근 토큰이 연결 수명 내내 남는 문제도 사라진다(이 객체엔 토큰이 없다).
            conn.authenticationData = new ConnectionIdentity(authenticatedUserId);

            conn.Send(new AuthResponseMessage { code = 200, message = "success" });
            ServerAccept(conn);
        }
```

- [ ] **Step 5: 인증 타임아웃을 넣는다**

같은 파일의 빈 `OnServerAuthenticate`를 아래로 바꾸고, 코루틴과 상수를 더한다.

```csharp
        //  접속만 하고 인증 요청을 보내지 않는 연결은 그대로 두면 영원히 남아 maxConnections를 갉아먹는다.
        //  Mirror의 TimeoutAuthenticator와 같은 동작이되, 그 컴포넌트는 다른 인증기를 감싸는 데코레이터라
        //  씬에서 NetworkManager의 authenticator를 갈아끼워야 한다 — 여기서 직접 처리한다.
        private const float AuthenticationTimeoutSeconds = 60f;

        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
            StartCoroutine(DisconnectIfNotAuthenticated(conn));
        }

        private IEnumerator DisconnectIfNotAuthenticated(NetworkConnectionToClient conn)
        {
            yield return new WaitForSecondsRealtime(AuthenticationTimeoutSeconds);

            if (conn.isAuthenticated)
            {
                yield break;
            }

            //  Disconnect()는 connectionId로 끊는데 kcp2k는 그 id를 클라 주소로 만들어 재사용한다.
            //  60초 사이에 이 연결이 사라지고 같은 주소의 *다른* 연결이 그 자리를 차지했다면, 그냥
            //  끊어버리는 순간 애먼 연결이 죽는다. 지금 등록된 것이 바로 이 객체인지 확인한다.
            //  (Mirror의 TimeoutAuthenticator에는 이 확인이 없다 — kcp2k의 id 재사용과 겹치면
            //  같은 사고가 난다.)
            if (NetworkServer.connections.TryGetValue(conn.connectionId, out var current) == false || ReferenceEquals(current, conn) == false)
            {
                yield break;
            }

            Debug.LogWarning($"[Auth] 제한 시간 안에 인증하지 않아 연결을 끊습니다. connectionId: {conn.connectionId}");
            conn.Disconnect();
        }
```

`OnStopServer`에 `StopAllCoroutines();`를 더한다 — 방이 닫힌 뒤에도 남아 있던 코루틴이 깨어나
끊기를 시도하지 않게, 그리고 죽은 연결 객체를 최대 60초간 붙들고 있지 않게.

파일 상단 `using`에 `System.Collections`가 있는지 확인하고 없으면 추가한다.

- [ ] **Step 6: 컴파일을 확인한다**

UnityMCP로 서버 인스턴스(`unity_instance="LeagueOfPhysical-Server@37450a0ab4f67bdd"`)에 `refresh_unity`(compile: request) → `isCompiling`이 내려갈 때까지 대기 → `read_console`(errors).

**이 시점에는 에러가 남아 있는 게 정상이다** — `LOPRoom`이 아직 `conn.authenticationData`를 `CustomProperties`로 캐스트하고 `customProperties.userId`를 읽는다. Task 2에서 고친다. **`LOPRoom.cs` 관련 에러만 남았는지** 확인하고, 그 외 파일의 에러가 있으면 이 태스크에서 고친다.

- [ ] **Step 7: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Room/ConnectionIdentity.cs Assets/Scripts/Room/ConnectionIdentity.cs.meta \
        Assets/Scripts/Room/EditorAccessTokenClaims.cs Assets/Scripts/Room/EditorAccessTokenClaims.cs.meta \
        Assets/Scripts/Room/CustomProperties.cs \
        Assets/Scripts/Room/LOPNetworkAuthenticator.cs
git commit -m "feat(auth): 신원을 토큰의 sub에서만 얻는다 + 인증 타임아웃"
```

---

## Task 2: 서버 — 세션을 연결에서 도출한다

**Repo:** `LeagueOfPhysical-Server`

**Files:**
- Modify: `Assets/Scripts/Network/ClientMessage.cs`
- Modify: `Assets/Scripts/Network/NetworkMessageDispatcher.cs`
- Modify: `Assets/Scripts/Room/LOPRoom.cs`
- Modify: `Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs`
- Modify: `Assets/Scripts/Game/MessageHandler/GameInputMessageHandler.cs`
- Modify: `Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs`

**Interfaces:**
- Consumes: `ConnectionIdentity`(Task 1) — `conn.authenticationData`에 들어 있다.
- Produces: `ClientMessage<T> { ISession Session; T Message; }`, `NetworkMessageDispatcher.Dispatch(ISession session, IMessage message)`

- [ ] **Step 1: 봉투가 세션을 나르게 한다**

`Assets/Scripts/Network/ClientMessage.cs`:

```csharp
using GameFramework;

namespace LOP
{
    /// <summary>클라가 보낸 메시지와, 그 연결에서 서버가 찾아낸 세션을 함께 나른다.
    /// 메시지 안에는 신원이 없다 — 신원은 연결에서만 나온다.</summary>
    public readonly struct ClientMessage<T> where T : IMessage
    {
        public ISession Session { get; }
        public T Message { get; }

        public ClientMessage(ISession session, T message)
        {
            Session = session;
            Message = message;
        }
    }
}
```

- [ ] **Step 2: 디스패처를 맞춘다**

`Assets/Scripts/Network/NetworkMessageDispatcher.cs`에서 세 곳을 바꾼다(`using GameFramework;`는 이미 있다).

```csharp
        private readonly Dictionary<Type, Action<ISession, IMessage>> routes = new();
```

```csharp
        private void Register<T>(IPublisher<ClientMessage<T>> publisher) where T : IMessage
        {
            routes[typeof(T)] = (session, message) => publisher.Publish(new ClientMessage<T>(session, (T)message));
        }

        public void Dispatch(ISession session, IMessage message)
        {
            if (routes.TryGetValue(message.GetType(), out var route))
            {
                route(session, message);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[NetworkMessageDispatcher] 미등록 메시지 타입: {message.GetType()}");
            }
        }
```

- [ ] **Step 3: 수신 경계가 연결에서 세션을 찾게 한다**

`Assets/Scripts/Room/LOPRoom.cs`의 `StartRoomServerAsync` 안 핸들러 등록을 바꾸고, 같은 클래스에 헬퍼를 더한다.

```csharp
            NetworkServer.RegisterHandler<CustomMirrorMessage>((conn, message) =>
            {
                if (TryGetSession(conn, out ISession session) == false)
                {
                    return;
                }

                dispatcher.Dispatch(session, message.payload);
            });
```

```csharp
        //  연결 → 세션. 계정 id를 거치지 않는다 — 같은 계정이 여러 연결을 가질 수 있어서,
        //  계정으로 찾으면 "어느 연결이 보냈나"에 답하지 못한다.
        private bool TryGetSession(NetworkConnectionToClient conn, out ISession session)
        {
            session = null;

            if (conn.authenticationData is not ConnectionIdentity identity || string.IsNullOrEmpty(identity.SessionId))
            {
                return false;
            }

            return sessionManager.TryGetSessionById(identity.SessionId, out session);
        }
```

- [ ] **Step 4: 접속 시 연결과 세션을 묶는다**

`OnPlayerConnect`의 본문(`conn` 획득 이후)을 아래로 바꾼다.

```csharp
            if (conn.authenticationData is not ConnectionIdentity identity)
            {
                //  Mirror는 소켓이 붙는 즉시(인증 완료 전에도) 이 콜백을 부를 수 있다. 아직 신원이
                //  확인 안 된 연결이라 할 일이 없다 — 인증이 끝나면 그때 세션이 만들어진다.
                return;
            }

            Debug.Log($"[OnPlayerEnter] userId: {identity.UserId}, identity: {conn.identity}");

            if (sessionManager.TryGetSessionByUserId<LOPSession>(identity.UserId, out LOPSession session) == false)
            {
                session = new LOPSession(identity.UserId, conn);
                sessionManager.AddSession(session);
            }
            else
            {
                session.networkConnection = conn;
            }

            //  연결이 자기 세션을 가리키게 한다. 이후 수신·해제는 이 값으로 세션을 찾는다.
            identity.SessionId = session.sessionId;
```

- [ ] **Step 5: 해제가 자기 연결만 정리하게 한다**

`OnPlayerDisconnect`의 본문(`conn` 획득 이후)을 아래로 바꾼다.

```csharp
            if (conn.authenticationData is not ConnectionIdentity identity || string.IsNullOrEmpty(identity.SessionId))
            {
                //  인증되지 못한 채 끊긴 연결이다(로비 장애·타임아웃·만료 토큰·틀린 키 등 — 더 이상
                //  드문 예외가 아니라 흔히 오는 경로다). 세션을 만든 적이 없으니 더 할 일이 없다.
                return;
            }

            Debug.Log($"[OnPlayerLeave] userId: {identity.UserId}, identity: {conn.identity}");

            if (sessionManager.TryGetSessionById(identity.SessionId, out ISession found) == false || found is not LOPSession session)
            {
                return;
            }

            //  이미 새 연결로 갈아탄 세션이면 건드리지 않는다. Mirror의 해제 감지는 타임아웃이라
            //  옛 연결의 해제가 재접속보다 늦게 도착할 수 있는데, 그때 세션을 끄면 방금 다시 들어온
            //  플레이어가 아무 조작도 못 하게 된다.
            if (ReferenceEquals(session.networkConnection, conn) == false)
            {
                return;
            }

            session.networkConnection = null;
```

- [ ] **Step 6: 핸들러 세 곳에서 조회를 없앤다**

`GameInputMessageHandler.cs`의 `OnInputCommandToS` 첫 줄을 바꾼다:

```csharp
            //  세션은 연결에서 이미 찾아 왔다 — 여기서 계정으로 다시 조회하지 않는다.
            ISession session = received.Session;
```

`GameEntityMessageHandler.cs`의 `OnStatAllocationToS` 첫 줄도 같게 바꾼다:

```csharp
            ISession session = received.Session;
```

`GameInfoMessageHandler.cs`의 `Tick` 루프 안 두 줄을 바꾼다:

```csharp
                var session = received.Session;
                string entityId = entitySpawner.GetEntityIdByUserId(session.userId);
```

세 파일 모두 `sessionManager` 필드가 더 이상 쓰이지 않으면 **생성자 파라미터와 필드를 함께 제거한다**. 다른 곳에서 아직 쓰고 있으면 남긴다 — 지우기 전에 각 파일에서 `sessionManager`를 검색해 확인할 것.

- [ ] **Step 7: 컴파일과 잔여 참조를 확인한다**

UnityMCP로 서버 인스턴스(`unity_instance="LeagueOfPhysical-Server@37450a0ab4f67bdd"`) `refresh_unity` → 대기 → `read_console`. **에러 0이어야 한다.**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
grep -rn "GetSessionByUserId\|customProperties.userId\|received.UserId" Assets/Scripts
```
기대: `LOPRoom.cs`의 `TryGetSessionByUserId`(재접속 시 세션 재사용) 한 곳만 남는다. `received.UserId`와 `customProperties.userId`는 0건.

- [ ] **Step 8: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Network/ClientMessage.cs \
        Assets/Scripts/Network/NetworkMessageDispatcher.cs \
        Assets/Scripts/Room/LOPRoom.cs \
        Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs \
        Assets/Scripts/Game/MessageHandler/GameInputMessageHandler.cs \
        Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs
git commit -m "feat(session): 세션을 계정이 아니라 연결에서 찾는다"
```

---

## Task 3: 클라 — 이름을 신고하지 않는다

**Repo:** `LeagueOfPhysical-Client`

**Files:**
- Modify: `Assets/Scripts/Room/CustomProperties.cs`
- Modify: `Assets/Scripts/Room/LOPNetworkAuthenticator.cs`

**Interfaces:**
- Consumes: 서버의 `CustomProperties`(Task 1)와 **필드 구성이 정확히 같아야 한다** — Mirror Weaver가 이 타입을 직렬화하므로 어긋나면 인증이 깨진다.

- [ ] **Step 1: `CustomProperties`에서 `userId`를 뺀다**

`Assets/Scripts/Room/CustomProperties.cs`의 필드를 이렇게 남긴다(서버와 동일):

```csharp
        public string accessToken;
        public int characterId;
```

- [ ] **Step 2: 대입을 지운다**

`Assets/Scripts/Room/LOPNetworkAuthenticator.cs`의 `OnClientAuthenticate`를 아래로 바꾼다.

```csharp
        public override void OnClientAuthenticate()
        {
            //  이름을 신고하지 않는다 — 서버가 토큰의 sub로 신원을 확정한다.
            var customProperties = new CustomProperties
            {
                accessToken = preparedAccessToken,
                characterId = 0,
            };

            NetworkClient.Send(new AuthRequestMessage { customProperties = customProperties });
        }
```

- [ ] **Step 3: 죽은 주입을 제거한다**

`userDataStore`가 이 파일에서 더 이상 쓰이지 않으면 필드를 지운다:

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
grep -n "userDataStore" Assets/Scripts/Room/LOPNetworkAuthenticator.cs
```
남은 사용처가 없으면 아래 두 줄을 삭제한다. 다른 곳에서 쓰고 있으면 남긴다.

```csharp
        [Inject]
        private IUserDataStore userDataStore;
```

`using`이 그 때문에만 있었다면 함께 정리한다.

- [ ] **Step 4: 컴파일과 테스트를 확인한다**

UnityMCP로 클라 인스턴스(`unity_instance="LeagueOfPhysical-Client@dc70ef3a594a3fe0"`) `refresh_unity` → 대기 → `read_console`(에러 0) → `run_tests`(EditMode).

기대: **433 passed / 0 failed**(현재 기준선). 이 태스크는 패키지 코드를 건드리지 않으므로 수치가 변하면 안 된다.

- [ ] **Step 5: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Room/CustomProperties.cs Assets/Scripts/Room/LOPNetworkAuthenticator.cs
git commit -m "feat(auth): 접속 시 userId를 보내지 않는다"
```

---

## 배포·검증

**클·서를 함께 내보내야 한다.** `CustomProperties`의 필드 구성이 바뀌므로 한쪽만 배포하면 인증이 깨진다.

1. `LeagueOfPhysical-Server`, `LeagueOfPhysical-Client` 머지 → push
2. `gh workflow run gameserver-deploy.yml --ref main` (서버 레포)
3. ConfigMap의 `GAME_SERVER_IMAGE`가 새 sha로 바뀐 뒤 **`kubectl rollout restart deployment/room-server`** — 이걸 해야 새 방이 새 이미지로 뜬다
4. 클라는 유니티 에디터에서 실행

**수동 검증**

| 케이스 | 방법 | 기대 |
|---|---|---|
| 정상 입장 | 매칭 → 방 입장 | 입장 성공. 로비 로그에 `POST /auth/introspect 200`(77B), 게임서버에 `[OnPlayerEnter]` |
| 훼손된 토큰 | 클라 `PrepareCredentialAsync`에서 토큰 마지막 글자를 바꾸는 임시 코드 | 입장 거부. 로비 `200`(16B), 게임서버 `[Auth] 접속 거부: 토큰이 유효하지 않음` |
| 명단 밖 계정 | 로비에서 새 계정으로 로그인한 클라로 남의 방에 접속 | 거부 — `명단에 없는 참가자` |
| **재접속(핵심)** | 방 입장 후 클라를 강제 종료하고 **곧바로** 다시 접속(서버가 해제를 감지하기 전) | 새 연결이 정상 동작. 게임서버 로그에 `[OnPlayerLeave]`가 뒤늦게 찍혀도 조작이 계속 먹혀야 한다 |
| 인증 타임아웃 | (선택) 임시로 `AuthenticationTimeoutSeconds`를 5로 낮추고 인증 메시지를 안 보내게 해 확인 | 제한 시간 뒤 끊김 로그 |

임시 검증 코드는 **확인 후 반드시 되돌린다**.
