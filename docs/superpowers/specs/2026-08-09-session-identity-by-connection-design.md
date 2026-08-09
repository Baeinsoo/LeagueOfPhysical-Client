# 슬라이스 2a — 세션 신원을 연결 기준으로

**날짜**: 2026-08-09
**선행**: 인증 cutover 1c(방 접속 인증 — 신원을 클라 주장값이 아니라 연결에서 도출)
**후속**: 슬라이스 2b(내부 전용 변경 라우트 차단)

---

## 1. 배경 — 1c가 절반만 옮겼다

1c는 **신원의 출처**를 옮겼다: 메시지에 적힌 값 대신 `conn.authenticationData`(introspect가 확인한 `sub`)를 쓴다. 사칭은 이걸로 막혔다.

그런데 **그 값으로 무엇을 하는가**는 그대로다. 서버는 확인된 userId를 들고 다시 계정 단위로 세션을 찾는다.

```csharp
ISession session = sessionManager.GetSessionByUserId(received.UserId);   // 세 핸들러 모두
```

`SessionManager`의 인덱스는 `sessionId`와 `userId` 둘뿐이고 — **연결로 세션을 찾는 경로는 존재하지 않는다.**

```csharp
private readonly Dictionary<string, ISession> sessionsById;
private readonly Dictionary<string, ISession> sessionsByUserId;
```

즉 연결이 손에 있는데도 접속·수신·해제가 전부 계정 id를 한 번 거쳐 간다.

## 2. 이 구조가 만드는 실제 버그

`LOPRoom`의 접속/해제는 둘 다 userId로 세션을 찾는다. 그래서 이런 순서가 성립한다.

```
① connA(유저 X) 접속       → 세션 S 생성, S.networkConnection = connA
② 서버가 connA의 끊김을 감지하기 전에 connB(같은 X)로 재접속
                           → userId로 S를 찾아 S.networkConnection = connB      (의도대로)
③ 뒤늦게 connA의 해제 콜백 도착
                           → userId로 또 S를 찾아 S.networkConnection = null    ← 버그
```

**끊어진 옛 연결이 살아 있는 새 연결의 세션을 꺼버린다.** Mirror의 연결 해제는 타임아웃으로 감지되므로 ③이 ②보다 늦게 오는 것은 예외가 아니라 정상이다. 유저에겐 "다시 들어갔는데 아무 조작도 안 먹힘"으로 보인다.

원인은 ③이 **"어느 연결이 끊겼나"를 계정으로 물었기** 때문이다. 연결로 물었다면 connA에 묶인 세션은 이미 없으므로 아무 일도 일어나지 않는다.

> **보안 구멍은 아니다.** 다른 계정을 사칭하려면 그 계정의 토큰이 필요하고, 그 길은 1c가 닫았다. 이것은 정확성·견고성 문제다.

## 3. 산업 표준 — 연결이 1차 키, 계정 id는 그 위의 속성

조사한 모든 스택이 같은 모양이다. **"지금 말하고 있는 상대"는 전송 계층의 연결 핸들로 식별하고, 계정 id는 거기 붙는 값이다.**

| 스택 | 연결(세션) 키 | 계정 id |
|---|---|---|
| **Mirror**(우리 것) | `NetworkConnectionToClient` — `NetworkServer.connections`가 이걸로 색인, per-connection 상태는 `conn.authenticationData`에 둔다 | `authenticationData`에 담는 값 |
| Photon | `ActorNr` / `PlayerRef` (입장마다 부여) | `UserId` — 별개 개념으로 명시 분리 |
| Unreal | `UNetConnection` → `APlayerController` | `APlayerState.UniqueNetId` |
| Unity NGO | `ulong ClientId` (연결마다) | 앱 레벨 메타데이터 |
| 웹 세션 | 세션 id(쿠키) | 세션 *안에 든* 유저 id |

이유도 공통이다:

1. **한 계정이 동시에 여러 연결을 가질 수 있다** — 재접속 경합, 멀티 클라이언트(이 프로젝트는 MPPM을 쓴다)
2. **연결은 소켓과 함께 죽지만 계정 id는 살아남는다** — 계정 id로는 "죽은 쪽"을 가릴 수 없다
3. **해제 이벤트는 늦게 온다** — §2의 ③이 정확히 이 경우다

계정 id는 *누가* 에는 답하지만 *어느 연결이* 에는 답하지 못한다.

## 4. 결정 — 연결이 자기 세션을 들고 있게 한다

`ISessionManager`에 연결 기준 조회를 추가하지 **않는다.** GameFramework는 Mirror를 모르고(`ISession`에 연결 필드가 없는 것도 그래서다), 거기에 전송 계층 개념을 넣으면 앱 비종속 계약이 깨진다.

대신 **연결이 자기 세션 id를 들고 있게** 한다. 이것이 Mirror의 관용(`authenticationData` = per-connection 상태)과 정확히 맞고, 기존 `sessionsById` 인덱스를 그대로 쓰므로 새 자료구조가 없다.

### 4.1 연결에 싣는 것 — `ConnectionIdentity`

지금은 `conn.authenticationData`에 **클라가 보낸 와이어 타입 `CustomProperties`** 를 그대로 얹고, 서버가 그 안의 `userId`를 덮어써서 쓴다. 서버 측 신원을 클라 입력 타입에 얹는 것은 두 가지가 섞인 상태다 — 분리한다.

```csharp
/// <summary>인증이 끝난 연결의 서버 측 신원. 클라가 보낸 값이 아니라 서버가 확정한 것만 담는다.</summary>
public class ConnectionIdentity
{
    public string UserId { get; }              // introspect가 돌려준 sub
    public string SessionId { get; set; }      // 세션 생성 시점에 채워진다
}
```

`SessionId`가 `set`인 이유: Mirror는 `ServerAccept` → `OnServerConnect` 순서로 부르므로 신원 확정(인증기)과 세션 생성(`LOPRoom`)이 두 시점으로 나뉜다. 한 시점에 다 채울 수 없다.

### 4.2 흐름

```
인증 성공   : conn.authenticationData = new ConnectionIdentity(sub)
세션 생성   : identity.SessionId = session.sessionId          ← 연결과 세션이 여기서 묶인다
메시지 수신 : conn → identity.SessionId → GetSessionById      ← userId를 경유하지 않는다
연결 해제   : conn → 그 연결의 세션만 정리                     ← §2의 ③이 구조적으로 불가능
```

### 4.3 봉투가 나르는 것 — userId → 세션

1c가 만든 `ClientMessage<T>`가 확인된 userId를 날랐다. 이제 **세션을 싣는다.**

```csharp
public readonly struct ClientMessage<T> where T : IMessage
{
    public ISession Session { get; }
    public T Message { get; }
}
```

핸들러는 조회를 하지 않는다. 계정 id가 필요하면 `received.Session.userId`로 읽는다 — 연결에서 도출된 세션의 속성이므로 여전히 위조 불가다.

부수 효과로 리뷰가 지적한 위험이 사라진다: `GetSessionByUserId`는 **딕셔너리 인덱서라 없으면 `KeyNotFoundException`을 던진다.** 매 입력 패킷마다 그 조회를 하던 것이 없어진다.

### 4.4 재접속은 세션을 재사용한다 (현행 유지)

같은 userId가 다시 붙으면 기존 세션의 연결을 새 연결로 갈아끼운다. 엔티티가 `GetEntityIdByUserId`로 계정에 묶여 있어, 세션을 새로 만들면 그 매핑을 다시 이어야 한다. **이 슬라이스는 재접속 정책을 바꾸지 않는다.**

바뀌는 것은 해제 처리다. 세션이 지금 들고 있는 연결이 **이 연결일 때만** 정리한다:

```csharp
if (ReferenceEquals(session.networkConnection, conn) == false)
{
    //  이미 새 연결로 갈아탄 세션이다. 늦게 도착한 옛 연결의 해제가 산 세션을 끄면 안 된다.
    return;
}
```

## 5. 범위

**포함**

| 저장소 | 변경 |
|---|---|
| LeagueOfPhysical-Server | `ConnectionIdentity` 신설, 인증기·`LOPRoom`·디스패처·핸들러 3종 |
| LeagueOfPhysical-Client | `CustomProperties`에서 서버 전용 필드 정리 여부 확인(와이어 타입은 유지) |

**제외**

- `ISessionManager`/`SessionManager` 변경 없음 — 연결 개념을 GameFramework에 넣지 않는다.
- 재접속 정책 변경 없음(§4.4).
- 같은 계정의 **동시** 두 연결을 금지하지 않는다. 지금은 뒤엣놈이 세션의 연결을 가져간다. 막으려면 "먼저 붙은 쪽을 끊는다 / 나중 것을 거부한다"는 게임 디자인 결정이 필요하므로 별도 항목으로 남긴다.
- `GameInfoToC.SessionId`(서버→클라)는 유지. 클라가 자기 세션을 아는 것은 정상이다.

## 6. 테스트

Unity 앱 프로젝트에는 asmdef가 없어 유닛 테스트를 붙일 수 없다(앱 코드가 `Assembly-CSharp`에 있고 테스트 어셈블리가 이를 참조할 수 없다). 따라서 검증은 **컴파일 클린 + 수동**이다.

| 케이스 | 기대 |
|---|---|
| 정상 접속·플레이 | 이전과 동일하게 동작 |
| 재접속(§2의 순서 재현) | 새 연결이 살아남는다. 옛 연결의 해제가 세션을 끄지 않는다 |
| 미인증 연결이 끊김 | 아무 일도 없음(예외 없음) |
| 입력·스탯 배분 | 세션 경유로 정상 처리 |

재접속 재현 방법: 클라를 두 번 연속 실행하거나, 방 접속 후 프로세스를 강제 종료하고 즉시 다시 접속한다(해제 감지 타임아웃 안에 재접속해야 ③ 순서가 성립).

## 7. 산업 표준 매핑

| 우리 것 | 대응 |
|---|---|
| `conn.authenticationData`에 서버 확정 신원 | Mirror의 per-connection auth data 관용 |
| 연결 → 세션 → (속성으로서의) userId | Unreal `UNetConnection`→`PlayerController`→`PlayerState.UniqueNetId` |
| 세션 id ≠ 계정 id | Photon `ActorNr` ≠ `UserId`, 웹 세션 id ≠ 유저 id |
| 늦은 해제 이벤트에 대한 연결 동일성 확인 | 일반적인 stale-handle 방어 |

## 8. 결정 요약

1. **연결이 자기 세션 id를 들고 있다** — `ConnectionIdentity`를 `conn.authenticationData`에 싣는다.
2. **`ISessionManager`는 그대로 둔다** — 전송 계층 개념을 앱 비종속 계층에 넣지 않는다.
3. **`ClientMessage<T>`가 세션을 나른다** — 핸들러의 계정 기준 조회를 없앤다.
4. **해제는 연결 동일성을 확인한 뒤에만 정리한다** — 늦게 온 옛 해제가 산 세션을 끄지 못한다.
5. **재접속 정책·동시 접속 정책은 건드리지 않는다** — 별도 결정 사항.
