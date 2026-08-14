# 유저 위치 조회를 서비스 하나로 (클라)

매치메이킹 FSM과 UI가 **각자** 유저 위치를 조회·해석하던 것을, **`UserLocationService` 하나가 조회하고
나머지는 구독**하는 형태로 정리한다. **위치 컨셉·FSM 구조·상태 이름은 그대로 두고, 배선만 바꾼다.**

- 범위: **클라 단독**. 백엔드·서버 변경 없음. 와이어 계약 무변경.
- 목표: **값 동치** — ④를 뺀 나머지는 겉보기 동작이 같아야 한다.

---

## 1. 왜 (문제 4가지 — 전부 코드로 확인)

| # | 문제 | 실측 |
|---|---|---|
| ① | **스토어가 값 변화를 알리지 않는다** | `UserDataStore.userLocation`은 평범한 프로퍼티. 구독 수단 없음 |
| ② | **UI가 전송 객체(DTO)를 직접 구독한다** | `MatchLoadingViewModel(ISubscriber<GetUserLocationResponse>)` — 도메인 `UserLocation`이 아니라 HTTP 응답 |
| ③ | **폴링 주인이 없다** | `CheckMatch`(3회 재시도) · `InMatchmaking`(1초 루프 + 5연속 실패 포기)이 **각자** `WebAPI.GetUserLocation` 호출 + 각자 정책 |
| ④ | **티켓 id를 받아놓고 버린다** | `MatchmakingResponse.ticketId`를 안 읽음. 취소 시 마지막 폴링 결과(`userLocation.locationDetail`)에서 되찾음 |

**②는 ①의 결과다** — 스토어가 벙어리라 소비자가 스토어를 못 쓰고 DTO로 우회했다.

**④의 실제 증상:** 매칭 요청 직후 취소를 누르면 폴링이 아직 한 바퀴 안 돌아 `locationDetail`이
`MatchmakingLocationDetail`이 아니다 → `"User is not in matchmaking."` 로그 후 `RecheckRequested`로
위치 재확인 한 바퀴를 돈다. 티켓 id를 들고 있으면 이 의존이 사라진다.

---

## 2. 하지 않는 것 (검토 후 명시적 제외)

| 안 함 | 이유 |
|---|---|
| **FSM 제거·상태 재구성** | FSM은 표준이다. Unity 공식 샘플 Boss Room의 `ConnectionManager`가 같은 구조(상태 6개). 우리도 6개. 과설계 아님 |
| **티켓 축으로 재설계** | 우리 위치 축은 표준의 *복구 경로*(PlayFab `ListMatchmakingTicketsForPlayer`)를 상시 경로로 쓰는 변형이고, 유저당 티켓 1개 불변식(DB 기본키, 08-04) 덕에 성립한다. 파티·초대가 없고 백엔드 진행 단계가 3개뿐이라 축을 바꿔도 표현할 게 늘지 않는다 |
| **`locationDetail` 타입 강화** | 백엔드 계약(JSON) — 다른 기계 몫 |
| **서버 push / presence TTL / 매치 종료 시 위치 정리** | 백엔드 몫. 이 슬라이스는 그것들과 독립이고, push가 생겨도 **서비스 내부만** 바뀐다 |

### 산업 표준 매핑

| 우리 것 | 대응 |
|---|---|
| 유저 위치를 서버가 권위로 보관 | Xbox **MPSD**(Multiplayer Session Directory), EOS **Presence Interface** |
| `GetUserLocation`으로 내 티켓 id를 되찾음 | PlayFab **`ListMatchmakingTicketsForPlayer`**(클라 재시작 시 티켓 복구용) |
| 흐름을 FSM으로 | Unity **Boss Room `ConnectionManager`** — *"a simple state machine that owns the connection flow"* |
| 상태는 입력을 **받는다** | 같은 문서: *"receives inputs … and handles the inputs according to its current state"* ← ③이 고치는 지점 |

> **근거 강도:** 위 4행은 공식 문서로 확인. "FSM이 I/O를 소유하면 안 된다"는 일반론은 신뢰할 만한
> 출처를 못 찾았으므로 근거로 쓰지 않는다 — ③의 판단은 Boss Room 대조와 우리 코드 실측에만 기댄다.

---

## 3. 선 긋기 — 상태가 무엇까지 해도 되나

| 상태가 하는 일 | 판정 |
|---|---|
| 행동을 **한 번** 시작하고 결과로 전이 (요청·취소·입장) | ✅ 그대로 둔다. Boss Room `ClientConnecting`과 같은 모양 |
| **감시 루프를 소유** (폴링·재시도·백오프) | ⚠️ 서비스로 옮긴다 |

그래서 **감시 루프가 빠지는 상태는 2개**: `CheckMatch`, `InMatchmaking`.
`RequestMatchmaking`·`CancelMatchmaking`·`InGameRoom`은 **HTTP를 한 번 쏘는 모양 그대로** 두되,
`RequestMatchmaking`·`CancelMatchmaking`은 ④(티켓 id 배선) 때문에 **한 줄씩** 바뀐다.
`Idle`은 무변경.

---

## 4. 목표 구조

```
        ┌─────────────────────────────────────────┐
        │  UserLocationService  (Root Singleton)  │
        │   · 폴링 루프 소유 (시작/중단 자기 판단) │──HTTP──→ 서버
        │   · 재시도·백오프 정책 소유              │
        │   · 티켓 id 보관                         │
        │   · Location 을 R3로 노출                │
        └───────────────┬─────────────────────────┘
                        │ 구독
        ┌───────────────┼──────────────────┐
   CheckMatch      InMatchmaking     MatchLoadingViewModel
   (Refresh 1회)   (변화 대기)        (구독)
```

### `UserLocationService` (신규, `Assets/Scripts/Matchmaking/`)

```csharp
public interface IUserLocationService
{
    ReadOnlyReactiveProperty<UserLocation> UserLocation { get; }  // 현재 위치 + 변화 알림
    string TicketId { get; }                                      // 매칭 중이면 티켓 id (없으면 null)
    Observable<Unit> Faulted { get; }                             // 조회를 연속 실패해 폴링을 포기했다

    UniTask<bool> RefreshAsync(CancellationToken ct);             // 1회 조회 + 재시도. 성공 여부
    void OnMatchmakingRequested(string ticketId);                 // 요청 응답의 티켓 id 보관 + 폴링 시작
}
```

> **이름:** 프로퍼티를 `Location`으로 두면 클래스 안에서 `Location.Matchmaking`이 enum이 아니라 이
> 프로퍼티로 해석돼 컴파일이 깨진다. 그래서 `UserLocation`이다.
>
> **`Faulted`가 필요한 이유:** 지금 `InMatchmaking`은 조회가 5번 내리 실패하면 `LocationIsNone`으로
> 빠진다. 폴링을 서비스로 옮기면 그 판단도 서비스로 가는데, **서비스는 FSM 이벤트를 모른다.**
> 그렇다고 위치를 억지로 `None`으로 밀면 "모른다"를 "없다"로 거짓 보고하는 것이다. 그래서 *"조회를
> 포기했다"* 는 사실만 신호로 내보내고, 전이 결정은 `InMatchmaking`이 한다.

**폴링 시작·중단은 서비스가 스스로 판단한다** — `Location.Matchmaking`이면 1초 간격으로 돌고, 그
밖이면 멈춘다. 호출자가 켜고 끄면 정책이 다시 상태로 새어나간다.

**값의 집은 `UserDataStore`에 그대로 둔다.** 지금처럼 `GetUserLocationResponse`를 MessagePipe로 받아
스토어가 갱신되고(다른 스토어들과 같은 패턴), 서비스는 그 값을 R3로 재노출한다. **소비자는 서비스만
본다** — 나중에 push로 바뀌어도 소비자 코드가 그대로다.

### `UserDataStore` 변경 (①)

`userLocation`을 `ReactiveProperty<UserLocation>`로 바꾸고 읽기 전용으로 노출한다. 기존 직접 읽기
(`userDataStore.userLocation.locationDetail` 2곳)는 `.CurrentValue`로 대응한다.

> `IDataStore.Clear()`가 `new UserLocation()`으로 리셋하는 동작은 유지(값 대입 → 알림 발생).

---

## 5. 호출부별 변경

| 파일 | 지금 | 바꾼 뒤 |
|---|---|---|
| `CheckMatch` | `WebAPI.GetUserLocation` + 3회 재시도 루프 | `await service.RefreshAsync(ct)` → `service.Location.CurrentValue.location`으로 전이 결정. 재시도는 서비스 안 |
| `InMatchmaking` | 1초 폴링 루프 + 실패 카운트 | `service.Location`을 구독해 `GameRoom`/`None`이 되면 전이. **자체 HTTP·타이머 없음** |
| `RequestMatchmaking` | 응답의 `ticketId` 버림 | `service.OnMatchmakingRequested(response.ticketId)` 호출 후 전이 (④) |
| `CancelMatchmaking` | `userLocation.locationDetail`에서 티켓 id 꺼냄 | `service.TicketId` 사용. null이면 기존처럼 `RecheckRequested` (④) |
| `MatchLoadingViewModel` | `ISubscriber<GetUserLocationResponse>` | `IUserLocationService.Location` 구독 (②) |
| `LoadUserComponent` | `await WebAPI.GetUserLocation(userId)` | `await service.RefreshAsync(ct)` |
| `InGameRoom` | `userLocation.locationDetail is GameRoomLocationDetail` | 그대로 (`.CurrentValue` 경유) |

### DI 등록

`RootLifetimeScope`에 `UserLocationService`를 **Singleton**으로 등록한다(`As<IUserLocationService>()`).
Root인 이유: `MatchLoadingViewModel`이 Root Singleton이고 **씬 경계를 넘어** 살아야 하기 때문
(로비 → 룸). FSM 상태들은 Lobby 스코프에서 이 Root 인스턴스를 주입받는다.

기존 `AuthenticationService`(Root Singleton)와 같은 자리·같은 명명이다.

---

## 6. 위험과 대응

| 위험 | 대응 |
|---|---|
| **폴링이 안 멈춰 게임 중에도 돈다** | 서비스가 `Location != Matchmaking`이면 멈춘다. 로비 씬을 벗어나도 Root라 살아 있으므로, **게임 진입 후 폴링 0회**를 로그로 실측 확인한다 |
| **`InMatchmaking`이 구독 전에 값이 바뀌어 놓친다** | 구독 시 **현재 값부터 흘려보낸다**(R3 `ReactiveProperty`의 기본 동작). 진입 시점에 이미 `GameRoom`이면 즉시 전이 |
| **실패 정책이 두 곳(상태·서비스)에 남는다** | `CheckMatch`·`InMatchmaking`의 재시도/실패 카운트 상수를 **삭제**하고 서비스로 옮긴다. 남으면 리뷰에서 잡는다 |
| **`Clear()` 시 알림이 소비자를 깨운다** | 로그아웃·룸 teardown에서 `Location`이 빈 값으로 알림. 소비자 3곳이 그 값에 어떻게 반응하는지 확인 |

---

## 7. 검증

### 유닛 테스트 — 이번 슬라이스는 없다 (결정)

클라에는 `Assets/Tests`도 asmdef도 **없어서** EditMode 테스트를 붙일 수 없다(ROADMAP 후속
"Unity 앱 asmdef 도입"이 이 한계 자체 — 인증 2a·2b도 같은 이유로 리뷰+라이브 검증만으로 갔다).

**테스트를 위해 코드를 GameFramework로 쪼개지 않는다.** 배치는 성격으로 정한다(사용자 결정):

- `UserLocationService`는 **클라 전용 정책**이다 — 서버는 유저 위치를 폴링하지 않는다. 토폴로지
  결정 트리의 "한쪽만 쓰는 I/O·정책 → 각자".
- 재시도/폴링 정책만 떼어 GF로 올리는 것도 **지금은 근거가 약하다.** 클라의 재시도 루프는 현재
  3곳(`CheckMatch`·`InMatchmaking`·`RoomConnector`)인데 앞의 둘이 이 작업으로 **한 벌로 합쳐져
  2곳이 된다.** 남는 하나로 범용 조각을 뽑는 건 "테스트를 위해 쪼개는 것"과 구분되지 않는다.
- **후속 조건:** 재시도 루프가 **세 번째로 다시 생기면** 그때 `GameFramework/Threading/`
  (`SingleFlight`·`Throttle` 옆)으로 뽑고 EditMode 테스트를 붙인다.

### 실제 검증 수단

- **컴파일**: 클라 UnityMCP (`unity_instance` = 클라 인스턴스).
- **최종 whole-branch 리뷰**(opus) — 테스트가 없으므로 이게 주 안전망이다.
- **인게임(수동)** — 회귀 없음이 곧 성공:
  1. 로그인 → 로비 진입 (위치 조회 1회)
  2. 매칭 요청 → 대기 → 매치 성사 → 게임 진입 → 로딩 화면이 뜨고 게임 시작 시 닫힘
  3. **매칭 요청 직후 즉시 취소** — ④가 고쳐졌으면 `"User is not in matchmaking."` 로그 **없이** 한 번에 취소된다 (**유일하게 겉보기 동작이 바뀌는 지점**)
  4. 대기 중 취소 (기존 경로)
  5. **게임 진입 후 폴링 0회** 로그 확인
  6. 매치 종료 → 로비 복귀

---

## 8. 구현하며 드러난 것 (2026-08-14, 최종 whole-branch 리뷰)

### 겉보기 동작 변화는 **둘**이다 (설계 때는 하나로 봤다)

1. **의도한 것** — 매칭 요청 직후 취소가 한 번에 된다(§1 ④).
2. **뒤늦게 드러난 것 — 부팅 실패 모드가 바뀐다.** 이전엔 부팅 중 위치 조회가 네트워크 예외로 실패하면
   그 예외가 `EntranceScene.ExecuteEntranceComponents`까지 올라가 **부팅이 중단**됐다(`BootCompleted`
   미발화 → 로비 입장·마스터데이터 로드 미실행). 이제 서비스가 삼키고 **부팅이 그대로 진행**된다.
   결과는 개선이다 — 로비의 `CheckMatch`가 어차피 다시 조회하므로, 위치 조회 한 번 실패로 앱이 아예
   못 뜨는 편이 더 나쁘다. **그대로 둔다.**

> 이 변화는 서비스가 예외를 삼키는 것(코어 신설)과 호출부를 바꾸는 것(소비자 전환)이 **다른 슬라이스에
> 흩어져 있어** 각 단계만 보면 드러나지 않는다. 쪼갠 작업에서는 마지막에 전체를 한 번 봐야 잡힌다.

### 알아 둘 실패 모드 — stale `Matchmaking`의 종착지

서버가 `SUCCESS`를 주면서 `userLocation`을 안 실어 보내면(계약 위반) 스토어가 그 응답을 버린다.
그러면 폴링 시작도 안 걸리는데 `CheckMatch`는 **이전 값**인 `Matchmaking`을 읽고 대기 화면으로 들어간다
→ **폴러도 없고 포기 신호도 없어 취소 말고는 빠져나갈 길이 없다.**

방어 코드는 넣지 않았다(계약 위반이 전제이고 오늘 도달 불가). **매칭 대기 화면이 멈춰 있다는 제보가
오면 여기부터 의심할 것.**

### 폴링 재개가 참조 동등성에 기대고 있다

`UserLocation`은 `Equals`를 오버라이드하지 않은 class고 스토어가 매번 새 인스턴스를 넣는다. 그래서 R3
프로퍼티가 **같은 값에도 항상 발행**하고, 조회가 성공할 때마다 폴링이 재보장된다.
**이 타입에 값 동등성을 주면** 중복 발행이 스킵되어 "위치는 매칭 중인데 폴러는 죽은" 조합이 생기고,
위와 같은 영구 대기가 된다. 그 리팩터를 할 때 `UserLocationService`를 함께 볼 것.

### 티켓 폴백은 백엔드 타이밍에 걸려 있다 (미검증)

§1 ④의 수정은 요청 응답의 티켓 id를 들고 있다가 쓰는데, 그 값은 **첫 폴링 응답 하나로 지워진다**.
즉 "요청 성공 = 서버 위치가 즉시 `Matchmaking`"이 아니면 무력화된다. **구 코드도 똑같이 깨졌으므로
회귀는 아니다.** 판정 증거는 요청 성공 직후 첫 위치 응답의 `location` 값 1건이다.

## 9. 후속 (이 슬라이스 밖)

- 백엔드: `locationDetail` 타입 강화 / 티켓 진행 세분(timeout·failed) / push / presence TTL /
  **매치 종료 시 위치 정리**(재접속 루프 — ROADMAP 파킹 항목)
- push가 생기면 `UserLocationService` 내부만 폴링 → 구독으로 바뀐다. 소비자 무변경.
- `UserDataStore.Clear()`는 현재 **호출부가 0곳**이다. 로그아웃을 배선할 때, 그것이 위치 알림을
  발행해 매칭 FSM·로딩 화면을 깨운다는 점을 함께 볼 것.

---

## 참고

- [Unity — Boss Room architecture](https://docs.unity3d.com/Packages/com.unity.netcode.gameobjects@2.6/manual/samples/bossroom/architecture.html)
- [PlayFab — Matchmaking quickstart](https://learn.microsoft.com/en-us/gaming/playfab/multiplayer/matchmaking/quickstart) / [ListMatchmakingTicketsForPlayer](https://learn.microsoft.com/en-us/rest/api/playfab/multiplayer/matchmaking/list-matchmaking-tickets-for-player)
- [Xbox — MPSD overview](https://learn.microsoft.com/en-us/gaming/gdk/docs/services/multiplayer/mpsd/live-mpsd-overview) · [EOS — Presence Interface](https://dev.epicgames.com/docs/epic-online-services/accounts-and-social/eos-presence-interface)
