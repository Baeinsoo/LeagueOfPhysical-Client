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

## 3.5 접속 핸드셰이크에도 "주장한 이름"이 남아 있다

인게임 메시지의 신원 필드는 1c에서 물리 삭제했다. 그러나 접속 순간에는 아직 클라가 이름을 신고한다.

```csharp
new CustomProperties { userId = ..., accessToken = ..., characterId = 0 }
```

사칭은 불가능하다 — 서버가 `sub == 주장한 userId`를 대조해 다르면 거부한다(1c, 라이브 검증 완료).
그러나 **토큰 안에 이미 `sub`로 들어 있는 값을 한 번 더 받는 중복**이고, "클라가 자기 신원을 신고하는
구조"가 마지막으로 남아 있는 자리다.

### Mirror 기본 인증기는 자격증명만 보낸다

저장소에 들어 있는 Mirror 인증기 두 개를 확인했다.

```csharp
// Authenticators/BasicAuthenticator.cs
public struct AuthRequestMessage : NetworkMessage
{
    // use whatever credentials make sense for your game
    // for example, you might want to pass the accessToken if using oauth
    public string authUsername;
    public string authPassword;
}

// Authenticators/DeviceAuthenticator.cs
public struct AuthRequestMessage : NetworkMessage
{
    public string clientDeviceID;
}
```

**둘 다 자격증명만 싣고, 별도의 "나는 누구다"를 보내지 않는다.** 주석은 OAuth를 쓸 경우 이 자리에
accessToken을 넘기라고 명시한다 — 우리가 하려는 바로 그 모양이다.

같은 파일에서 Mirror가 중복 처리를 `HashSet<NetworkConnectionToClient>`(연결 **객체**)로 관리하는 것도
확인했다. 1c에서 `connectionId`가 kcp2k에서 재사용되는 문제로 연결 객체 키로 바꾼 것이 원래 표준이었다.

### 결정 — 주장한 userId를 제거한다

`CustomProperties`에서 `userId`를 뺀다. 신원은 오직 토큰의 `sub`에서 나온다.

```
전:  중복가드 → 명단 대조 → introspect → sub == 주장한 userId
후:  중복가드 → introspect → sub가 명단에 있나
```

세 단계가 두 단계로 줄고, **"주장한 이름"이라는 개념이 사라진다.**

**받아들이는 비용(정직하게 기록)**: 명단 선검사가 사라지므로 소켓 하나가 로비 호출 하나를 유발한다.
중복 가드가 연결당 1회로 묶지만, 소켓을 여는 것은 싸다. 그리고 이 호출은 introspect의 레이트리밋에
걸리지 않는다 — 가짜 토큰도 응답이 200(`active:false`)이고 리미터는 실패(4xx)만 세기 때문이다.
방은 참가자 몇 명짜리 단명 대상이고 주소는 매칭이 참가자에게만 알려주므로 지금은 수용한다.
실환경에서 문제가 되면 게임서버 측 호출 빈도 제한을 얹는다.

### 미인증 연결이 남는 문제 — Mirror가 이미 도구를 준다

접속만 하고 인증 요청을 보내지 않는 연결은 영원히 남는다(1c 최종 리뷰의 잔여 항목).
Mirror에 `TimeoutAuthenticator`가 기본 제공되며(기본 60초), 제한 시간 안에 인증하지 않은 연결을 끊는다.
**같은 동작을 이번에 넣는다** — 위에서 늘어난 미인증 연결의 체류 시간을 묶는 짝이기도 하다.

**단, 그 컴포넌트를 끼우지 않고 우리 인증기 안에 구현한다.** `TimeoutAuthenticator`는 다른 인증기를
감싸는 데코레이터 MonoBehaviour라, 쓰려면 NetworkManager의 `authenticator`를 그것으로 바꾸고 내부
인증기를 인스펙터로 연결해야 한다 — 씬 편집이 따라오고, 클라에는 `networkManager.authenticator`를
`LOPNetworkAuthenticator`로 캐스트하는 자리가 있어(`LOPRoom.cs:107`) 잘못 끼우면 그 캐스트가 깨진다.
동작 자체는 "제한 시간 뒤 `conn.isAuthenticated`가 false면 끊는다"는 코루틴 10줄이므로,
**Mirror 구현을 참조 삼아 서버 인증기의 `OnServerAuthenticate`에 직접 넣는다.** 씬 변경이 없고
클라 경로도 건드리지 않는다.

### 에디터 경로 — `sub`를 어디서 얻나

에디터의 게임서버는 introspect를 건너뛴다(조회 키를 커밋하지 않기 위해, 1c 결정). 주장한 userId까지
없애면 **에디터에서는 신원을 알 방법이 사라진다.**

**결정**: 에디터 경로에 한해 토큰 페이로드에서 `sub`만 읽는다. **서명을 검증하지 않는다** — 검증은
introspect의 몫이고 에디터는 그것을 건너뛰기로 이미 결정했다. base64url 디코드 후 `sub` 한 필드만
꺼내는 짧은 코드이며 `#if UNITY_EDITOR`로 묶어 플레이어 빌드에는 들어가지 않는다.

> 1c에서 지운 `GameFramework.Auth.Jwt`는 **HMAC 서명 검증기**였고 파드에 서명키를 두지 않기로 해서
> 사용처가 사라진 것이다. 여기서 추가하는 것은 검증기가 아니라 *에디터 전용 클레임 리더*로, 목적과
> 신뢰 수준이 다르다. 이름과 주석으로 그 차이를 분명히 한다.

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
연결 해제   : conn → 세션 → 그 세션이 이 연결의 것일 때만 정리  ← §2의 ③을 막는 자리
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

바뀌는 것은 해제 처리다. 세션이 지금 들고 있는 연결이 **이 연결일 때만** 정리한다.

> **인과를 정확히**: 조회 키를 계정에서 세션으로 바꾼 것만으로는 §2의 ③이 닫히지 않는다. 세션은 한 번
> 만들어지면 지워지지 않아(`RemoveSession` 호출부 0곳) 한 계정의 **모든 연결이 같은 세션 id를 들고**,
> 늦게 온 옛 해제도 같은 세션을 찾아낸다. 실제로 막는 것은 아래 참조 동일성 검사다. 조회 키 변경의
> 몫은 따로다 — 수신 경로가 계정을 경유하지 않게 되고, 잘못된 캐스트가 사라진다.
> **같은 이유로 수신 경로에도 같은 검사가 필요하다**(최종 리뷰 지적): 안 하면 좀비 연결의 요청이 산
> 연결로 응답을 보내게 만든다.

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
| LeagueOfPhysical-Server | `ConnectionIdentity` 신설, 인증기(판정 순서·에디터 클레임 리더)·`LOPRoom`·디스패처·핸들러 3종, `CustomProperties.userId` 제거, `TimeoutAuthenticator` 배선 |
| LeagueOfPhysical-Client | `CustomProperties.userId` 제거(전송 중단), 인증기에서 대입 제거 |

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
| 훼손된 토큰으로 접속 | 거부(1c 검증과 동일. 단 이제 명단 선검사 없이 introspect가 먼저 돈다) |
| 명단 밖 계정의 정상 토큰 | 거부 — `sub`가 참가자 명단에 없음 |
| 접속만 하고 인증 요청 안 보냄 | `TimeoutAuthenticator`가 제한 시간 뒤 끊음 |

재접속 재현 방법: 클라를 두 번 연속 실행하거나, 방 접속 후 프로세스를 강제 종료하고 즉시 다시 접속한다(해제 감지 타임아웃 안에 재접속해야 ③ 순서가 성립).

## 7. 산업 표준 매핑

| 우리 것 | 대응 |
|---|---|
| `conn.authenticationData`에 서버 확정 신원 | Mirror의 per-connection auth data 관용 |
| 접속 메시지에 자격증명(토큰)만 싣는다 | Mirror `BasicAuthenticator`(username/password), `DeviceAuthenticator`(deviceID) — 둘 다 주장한 신원을 따로 보내지 않는다. 주석이 OAuth면 accessToken을 넘기라고 명시 |
| 연결 **객체**로 중복/상태 관리 | Mirror `BasicAuthenticator`의 `HashSet<NetworkConnectionToClient>` |
| 미인증 연결 제한 시간 | Mirror `TimeoutAuthenticator` |
| 연결 → 세션 → (속성으로서의) userId | Unreal `UNetConnection`→`PlayerController`→`PlayerState.UniqueNetId` |
| 세션 id ≠ 계정 id | Photon `ActorNr` ≠ `UserId`, 웹 세션 id ≠ 유저 id |
| 늦은 해제 이벤트에 대한 연결 동일성 확인 | 일반적인 stale-handle 방어 |

## 8. 결정 요약

1. **접속 메시지에서 주장한 `userId`를 제거한다** — 자격증명(토큰)만 보낸다. Mirror 기본 인증기가
   그 모양이고, 신원은 `sub` 하나에서만 나온다. 판정은 `중복가드 → introspect → sub가 명단에 있나`.
2. **연결이 자기 세션 id를 들고 있다** — `ConnectionIdentity`를 `conn.authenticationData`에 싣는다.
3. **`ISessionManager`는 그대로 둔다** — 전송 계층 개념을 앱 비종속 계층에 넣지 않는다.
4. **`ClientMessage<T>`가 세션을 나른다** — 핸들러의 계정 기준 조회를 없앤다.
5. **해제는 연결 동일성을 확인한 뒤에만 정리한다** — 늦게 온 옛 해제가 산 세션을 끄지 못한다.
6. **`TimeoutAuthenticator`를 붙인다** — 인증하지 않는 연결이 무한정 남지 않게.
7. **에디터는 토큰 페이로드에서 `sub`만 읽는다**(서명 검증 없음, 에디터 전용) — introspect를 건너뛰는
   경계 안에서 신원을 얻기 위해서다.
8. **재접속 정책·동시 접속 정책은 건드리지 않는다** — 별도 결정 사항.
