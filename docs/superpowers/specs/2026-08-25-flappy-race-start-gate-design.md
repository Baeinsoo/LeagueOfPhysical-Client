# Flappy Race — 레이스 시작 게이트 설계

> 참가자가 다 모일 때까지 새를 출발선에 세워 두고, 카운트다운 뒤에 함께 출발시킨다.

**작성일**: 2026-08-25
**브랜치**: `feature/flappy-race-start-gate` — **`feature/flappy-ghost-extrapolation` 위에서 분기했다**
(그 슬라이스가 아직 main에 안 들어갔고, 이 게이트가 바로 그 슬라이스의 눈 검증을 여는 도구라
둘이 같이 올라가야 한다. `FlappyWorld.Mutation`을 양쪽이 건드리는 것도 이유다.)

---

## 1. 배경 — 아무도 안 기다린다

방 서버가 부팅되는 순간 레이스가 시작된다.

```
방 서버 Awake
  → 맵 로드
  → FlappyRaceRuleSystem.Initialize()   ← 새를 여기서 다 세운다
  → runner.Run(...)                      ← 이 줄부터 틱이 돈다 = 새가 떨어지기 시작
  ─────────────────────────────────────
  (클라는 아직 접속조차 안 했다)
```

실서비스에선 매치메이킹이 붙여 주니 1~2초 차이지만, **손으로 에디터를 켜는 로컬에선 그 간격이
몇 분**이다. 2026-08-24 검증 시도에서 두 새가 y ≈ −28,000에 있었던 것이 이것이다.

덤으로 이미 있던 버그가 하나 드러난다 — `LOPRunner.LateUpdate`의 5분 매치 타이머도 **방 부팅부터**
돈다. 준비하는 동안 판이 끝난다.

### 이것이 막고 있는 것

이 게이트는 그 자체로 게임에 필요한 기능이지만, **지금 착수하는 직접적 이유는 검증 리그**다.
`feature/flappy-ghost-extrapolation`(유령정지 + 원격 외삽)이 코드 완료 상태로 머지를 못 하고 있고,
남은 유일한 조건이 "두 클라를 붙여서 눈으로 확인"이다. 붙을 창이 없어서 그걸 못 한다.

---

## 2. 목표와 비목표

**목표**

- 참가자가 다 준비될 때까지(또는 상한까지) 새를 출발선에 정지시킨다.
- 전원 준비 또는 상한 만료 시 3초 카운트다운 후 **모두 같은 틱에** 출발한다.
- 클라 화면에 대기 인원과 카운트다운을 보여 준다.
- 매치 시계를 방 부팅이 아니라 출발 시점부터 센다.
- 로컬 2인 검증 리그가 성립한다.

**비목표 (이번에 하지 않는다)**

- 결승선·순위·경기 종료 판정 — 별도 슬라이스
- 미접속자 오토파일럿/고스트 — 입력이 없으니 그냥 떨어진다. 나중 고려
- 출발 직전 탭이 부스터가 되는 스타트 대시 — 게임 디자인 확장
- 로딩 게이지·참가자 목록 등 본격 카운트다운 연출 — 가운데 라벨 하나로 시작
- 명단(roster) 자동화 — §9 참조. 지금 아픈 곳이 아니다

---

## 3. 핵심 결정

### 3.1 틱 루프는 멈추지 않는다. 새만 얼린다

시계 동기·스냅샷·접속 핸드셰이크가 전부 틱 위에 얹혀 있다. 특히 서버의 입장 응답
(`GameInfoMessageHandler`)이 **틱 시스템**이라, 러너를 안 돌리면 클라가 입장조차 못 한다.

또 `runner.Run(0, ...)` 형태로 시작했다가 틱과 `elapsedTime`이 어긋나 몇 초를 8배속으로 질주한
사고가 있었다(`LOPRoom.StartGameAsync` 주석).

**업계도 같다** — 언리얼 `WaitingToStart`는 *"액터는 틱을 돌지만 플레이어는 아직 스폰되지 않은"*
상태이고, Photon Quantum은 시뮬을 계속 돌리면서 **이동 시스템만 `StartDisabled`로 꺼 둔다.**
CS의 `mp_freezetime`도 서버는 그대로 돌고 플레이어만 얼린다.

### 3.2 틱 카운터는 서버 수명에 묶는다 (0부터 다시 세지 않는다)

"실제 출발할 때 0틱으로 시작하면 안 되나"를 검토했고, **하지 않는다.**

- **표준이 아니다.** Unity Netcode for Entities의 `ServerTick`은 *서버 기동 시 1부터 시작해 끊김 없이
  증가*한다(0은 무효값).
- **우리 코드에서 특히 비싸다.** 클·서 모두 `틱 = 절대시각 ÷ 간격`으로 유도한다
  (`runner.Run((long)(now / TICK_INTERVAL), ...)`). 이 *공유된 식* 덕분에 클라가 서버로부터 틱 시드를
  받지 않아도 되고(코드 주석이 명시), 그것이 Phase 2 clock sync의 토대다. 0부터 세려면 매치 기준시각
  T0을 도입해 `(시각 − T0) ÷ 간격`으로 바꿔야 하는데, 네 단계에 걸쳐 안정시킨 시스템을 수술하는
  일이고 T0이 어긋나면 나오는 증상이 방금 없앤 러버밴딩이다.
- **불변식도 아니다.** 지각 입장자는 어차피 현재 틱에서 시작한다.

**대신 매치 시계만 출발 기준으로 센다** — 언리얼이 `GetTimeSeconds()`와 `MatchState` 진입 시각을
따로 두는 것과 같다.

### 3.3 "지금 무슨 상태" 대신 "몇 틱에 출발"을 보낸다

두 값이 답하는 질문이 다르다.

| | 답하는 질문 | 성격 |
|---|---|---|
| 페이즈 | **지금** 무슨 상태냐 | 현재값. 메시지 도착 시 갱신 |
| 출발틱 | **몇 번 틱부터** 게임플레이냐 | 예정. 카운트다운 진입 시 확정, 이후 불변 |

**출발틱을 페이즈로 대체할 수 없는 이유** — 클라는 틱을 되감아 다시 굴린다(`Reconciler`). 되감아
100틱을 재시뮬할 때 물어야 하는 건 *"100틱일 때 상태가 뭐였나"* 이고, 현재값 필드는 이 질문에
답할 수 없다. 출발틱은 `100 >= 출발틱`으로 즉답한다.

**반대로 페이즈는 출발틱으로 대체된다:**

```
출발틱 == -1        → 대기 중
tick < 출발틱       → 카운트다운  (남은 초 = (출발틱 − tick) × 간격)
tick >= 출발틱      → 진행 중
```

`Finished`는 이 메시지에 없어도 된다 — `MatchEndedToC`(id 2)가 이미 그 일을 한다.

**그리고 페이즈를 보내면 오히려 틀린다.** 클라 시계는 서버보다 앞서 있다(Phase 2). 페이즈 필드를
믿으면 클라는 *자기 새가 이미 움직여야 할 틱을 지나서* "이제 진행 중"을 알게 되어, 화면의 "GO!"와
새가 실제로 출발하는 순간이 어긋난다. 출발틱은 각 클라가 **자기 시계로 그 틱에 도달할 때** 출발하므로
이 문제가 없다.

> 언리얼은 `AGameState::MatchState`를 그대로 복제한다 — 페이즈를 보내는 쪽이다. 다만 언리얼 클라는
> 앞서 달리지도, 되감지도 않는다. **앞서 달리고 되감는 클라에서는 "지금 상태"가 아니라 "예정 시각"을
> 보내야 한다** — 롤백 넷코드(GGPO·Quantum)가 사실에 프레임 번호를 박는 것과 같은 이유다.

### 3.4 출발틱은 `WorldBase`에 둔다 (게임별 훅 없이)

"게임플레이가 몇 틱에 시작하나"는 **게임 종류와 무관한 숫자**다. 서버는 `MatchStartSystem`이,
클라는 메시지 핸들러가 각각 같은 한 줄로 대입한다 — 양쪽 다 게임을 모른다.

게임별 해석(무엇을 안 굴릴 것인가)은 각 월드의 `Mutation` 안에만 있다. B2-c에서 "넷코드가 게임을
알아버린" 문제를 고친 방향과 같다.

*검토했다가 뺀 것*: `IGameRuleSystem.OnMatchPhaseChanged` 훅(언리얼 `HandleMatchHasStarted` 대응).
클라에는 `IGameRuleSystem`이 없어 대칭 배관을 하나 더 만들어야 하는데, 양쪽 배관이 하는 일이
**숫자 하나 대입**이었다. 게임별 시작 처리(랩 타임 기록 등)가 실제로 생길 때 그때 넣는다.

### 3.5 입력은 막지 않는다

카운트다운 중 탭하면 **양쪽 다 똑같이 무시**한다 — `FlappyMoveSystem`이 아예 안 돌기 때문이다.
어긋날 여지가 없어 게이트를 따로 두지 않는다. 입력 스트림 자체는 계속 흘러야 한다
(Phase 3c의 연속 command-frame — 끊으면 유실 복구가 깨진다).

빠뜨린 게 아니라 **안 하기로 한 것**이다.

---

## 4. 상태 모델

```
                    MatchReadyToS (클라 → 서버)
                              │
                              ▼
  ┌───────────────────────────────────────────────┐
  │ MatchStartSystem (서버, 틱 시스템)              │
  │   · 준비 메시지를 게이트에 넣는다                │
  │   · 매 틱 gate.Tick(현재틱)                     │
  │   · 바뀌면 → 전원에게 MatchStartToC             │
  │            → world.GameplayStartTick 대입       │
  └───────────────────────────────────────────────┘
```

```
대기 ──[전원 준비 OR tick ≥ 무장틱+상한]──> 카운트다운 ──[tick ≥ 출발틱]──> 진행
        출발틱 = tick + 카운트다운틱 확정
```

**카운트다운에 들어간 뒤엔 아무것도 되돌리지 않는다.** 늦게 붙은 사람이 준비를 보내도 출발틱은
이미 확정이다 — 카운트다운 중에 출발선이 밀리면 이미 3-2-1을 보고 있던 사람이 배신당한다.

### 값

| 무엇 | 값 (50Hz 기준) | 근거 |
|---|---|---|
| 카운트다운 | **3초** = 150틱 | 카트라이더·로켓리그 관례 |
| 대기 상한 (실서비스) | **30초** = 1,500틱 | 모바일 콜드 로딩 + 맵 로드를 덮는 흔한 값 |
| 대기 상한 (`Standalone`) | **600초** = 30,000틱 | 손으로 에디터를 켜는 시간 |
| 매치 길이 | 5분 (기존값 유지) | 기준점만 출발틱으로 바뀐다 |

상한은 `EnvironmentSettings.active.Standalone` 한 줄로 갈린다. 서버 코드 곳곳이 이미 이 분기를 쓴다.

---

## 5. GameFramework

### `MatchPhase`

```csharp
namespace GameFramework
{
    /// <summary>매치 진행 단계. 언리얼 AGameMode::MatchState에 대응한다.</summary>
    public enum MatchPhase
    {
        WaitingForPlayers,
        Countdown,
        InProgress,
        Finished,
    }
}
```

**서버 안에서만 산다.** 와이어를 타지 않는다(§3.3).

### `MatchStartGate`

순수 C#. Unity를 참조하지 않아 EditMode에서 전 경우를 돌린다. 이번 작업에서 **틀릴 수 있는 것의
대부분이 여기 있다.**

```csharp
namespace GameFramework
{
    public sealed class MatchStartGate
    {
        public MatchStartGate(int expectedPlayers, long waitCapTicks, long countdownTicks);

        public MatchPhase Phase { get; }
        public int ReadyCount { get; }
        public int ExpectedPlayers { get; }

        /// <summary>게임플레이가 시작될 틱. 아직 확정 전이면 long.MaxValue.</summary>
        public long StartTick { get; }

        /// <summary>같은 사람이 여러 번 보내도 한 번으로 센다.</summary>
        public void MarkReady(string userId);

        /// <summary>페이즈 전이는 여기서만 일어난다.</summary>
        public void Tick(long tick);
    }
}
```

**전이를 `Tick`에서만 하는 이유**: 메시지 도착 순간에 바꾸면 틱 중간에 페이즈가 갈려, 같은 틱을
보는 시스템들이 서로 다른 답을 본다.

**대기 상한을 언제부터 재나 (무장틱)**: `Tick`이 처음 불린 틱을 기준으로 잡는다. 생성자에 틱을
넘기지 않는 이유는, 게이트가 만들어지는 시점(DI 조립)과 러너가 돌기 시작하는 시점이 다르기
때문이다 — 조립 시각부터 재면 맵 로딩 시간이 상한을 갉아먹는다.

### `WorldBase.GameplayStartTick`

```csharp
/// <summary>이 틱 전에는 게임플레이가 시작되지 않았다. 확정 전엔 long.MaxValue.</summary>
public long GameplayStartTick { get; set; } = long.MaxValue;

protected bool HasStarted(long tick) => tick >= GameplayStartTick;
```

`IWorld`에도 노출한다 — 서버·클라 양쪽의 호출부가 구체 월드 타입을 몰라도 대입할 수 있어야 한다.

---

## 6. LOP-Shared

### `FlappyWorld.Mutation` 앞 가드

```csharp
protected override void Mutation(long tick, float deltaTime)
{
    if (HasStarted(tick) == false)
    {
        FreezeAll();   // 속도 0. 중력도 전진도 없다.
        return;
    }
    ...기존 5페이즈...
}
```

속도를 명시적으로 0으로 **두는** 이유는 스냅샷과 물리 팔로워가 그 값을 읽기 때문이다. 스폰 직후엔
어차피 0이라 결과는 같지만, 명시하는 쪽이 안전하다.

**롤백 안전**: `tick >= GameplayStartTick`은 숫자 비교라 몇 번을 되감아 다시 굴려도 같은 답이다.
저장할 상태가 아니므로 `SaveGameState`/`LoadGameState`는 손대지 않는다.

### 와이어

```proto
// MatchReadyToS.proto — 필드가 없다. 누가 보냈는지는 세션이 안다.
message MatchReadyToS {}
```

```proto
// MatchStartToC.proto — MatchEndedToC(id 2)와 짝이 된다.
message MatchStartToC {
  int64 start_tick  = 1;   // 아직 안 정해졌으면 -1
  int32 ready_count = 2;
  int32 total_count = 3;
}
```

`MessageIds`에 15(`MatchStartToC`), 16(`MatchReadyToS`)으로 이어 붙인다.

**`phase`를 proto enum으로 두지 않는 이유**는 §3.3. 열거형을 두 벌 만들면 어긋나도 컴파일러가
아무 말을 안 한다. 코드베이스 선례: `StatModifier.StatType`이 `int`이고 `EntityStatType`으로 캐스트한다.

---

## 7. 서버

### `MatchStartSystem` (신규, 틱 시스템)

```csharp
protected override void UpdateRunner()
{
    RunPhase<Begin>(...);
    matchStartSystem.Tick(...);      // ← 새 줄. 맨 앞이다.
    serverInputSystem.Tick(...);
    world.Tick(...);
    ...
}
```

**맨 앞인 이유**: 이번 틱이 출발틱인지가 먼저 정해져야 월드가 그걸 보고 굴린다.

하는 일:

1. `MatchReadyToS` 수신분을 `gate.MarkReady(session.userId)`로 넣는다
2. `gate.Tick(tickUpdater.tick)`
3. `StartTick` 또는 `ReadyCount`가 바뀌었으면
   - `world.GameplayStartTick = gate.StartTick`
   - 전원에게 `MatchStartToC` (**reliable** — 놓치면 출발을 모른다)

예상 인원은 `roomDataStore.match.playerList.Length`에서 온다.

### 새 세션에게 즉시 알리기

`GameInfoMessageHandler`가 `GameInfoToC`를 보낸 **바로 뒤에** 그 세션에게만 현재
`MatchStartToC`를 보낸다.

이것이 **지각 입장자를 공짜로 처리한다.** 이미 달리는 판에 붙으면 출발틱을 즉시 받아 자기 월드에
꽂고 바로 참여한다 — 별도 경로가 없다.

### 매치 시계 기준 교정

```csharp
// 지금 — 방이 부팅된 때부터 잰다
if (initialized && tickUpdater.elapsedTime > 60 * 5) EndMatch();

// 바꾼 뒤 — 출발틱부터 센다
if (matchStartSystem.Phase == MatchPhase.InProgress
    && tickUpdater.tick - matchStartSystem.StartTick > MatchDurationTicks) EndMatch();
```

`Standalone` 상한이 600초이므로 이 교정이 없으면 **준비하는 동안 판이 끝난다.** 이 슬라이스가
만드는 버그가 아니라 이미 있던 것이 드러나는 것이다.

---

## 8. 클라

### 준비 신호

클라 입장 순서의 **끝에 한 줄** 붙인다.

```
클라 LOPRoom.Awake
  ① InitializeAsync      맵 로드 + 러너 생성
  ② ConnectRoomServer    Mirror 접속
  ③ ┌ WaitForClockSettle  시계 안정 대기 (최대 7초, 병렬)
    └ JoinRoomServer      GameInfoToS → GameInfoToC 수신 → 엔티티 스폰
  ④ StartGameAsync       클라 러너 시작
  ⑤ ★ MatchReadyToS 송신  ← 여기
```

⑤ 시점에는 맵이 떠 있고, 내 새가 있고, 시계가 안정됐고, 러너가 돌고 있다. **"나 이제 진짜 플레이
가능"이 정직하게 성립하는 유일한 지점**이다. 더 일찍 보내면 시계가 어긋난 채 출발선을 긋게 되고,
그게 `WaitForClockSettleAsync` 주석이 길게 경고하는 입장 직후 러버밴딩이다.

### 수신

```csharp
// 메시지 핸들러 — 이게 전부다
world.GameplayStartTick = msg.StartTick < 0 ? long.MaxValue : msg.StartTick;
raceStartState.Update(msg.StartTick, msg.ReadyCount, msg.TotalCount);   // 화면용
```

### 화면

`RaceStartView` + `RaceStartViewModel`, 밴드는 **`UILayer.Window`**. 기존 `DebugHudView` 패턴
그대로 — uxml 하나에 가운데 라벨 하나.

> **정정 (2026-08-25 라이브)**: 처음엔 `UILayer.Notification`으로 적었다. 이유가 "위에 뜨되 아래
> 입력을 막지 않음"이었는데, **입력을 안 막는 것은 밴드가 아니라 `picking-mode="Ignore"`가 하는
> 일**이다. 밴드는 z-순서만 정한다. `Notification`(3)은 `Loading`(2)보다 위라 대기 문구가
> **로딩 화면을 뚫고 나왔다.** 카운트다운은 토스트가 아니라 게임 화면이므로 전체화면 오버레이에는
> 가려져야 한다 — 같은 인게임 UI인 `FlapPadView`·`DebugHudView`와 같은 `Window` 밴드가 맞다.
> 밴드 안에서는 연 순서대로 쌓이므로(코디네이터가 마지막에 연다) 게임 UI 위에는 그대로 뜬다.

여는 것은 이미 있는 `FlappyHudCoordinator`가 맡는다. 내 새가 생기면 `FlapPadView`·`DebugHudView`를
여는 그 자리에 한 줄 더한다.

표시 규칙:

| 상태 | 문구 |
|---|---|
| 출발틱 미정 | `"2 / 4 대기 중"` |
| 남은 1.8초 | `"2"` — 올림이라야 3·2·1이 각각 1초씩 보인다 |
| 남은 1틱 | `"1"` |
| 출발틱 도달 | `"GO!"` (1초 뒤 닫힘) |

이 계산은 **ViewModel 안에 그대로 둔다.** 테스트하려고 다른 어셈블리로 옮기지 않는다 — 이유는 §9.

페이즈·준비 인원은 R3(이벤트로 오는 값), **카운트다운 숫자는 매 프레임 틱에서 유도**한다.
후자를 R3로 두지 않는 것은 `DebugHudView`가 폴링을 쓰는 것과 같은 이유 — 변경 이벤트가 없는
샘플링 값이라서다.

---

## 9. 테스트

**각 테스트는 일부러 깨서 빨강을 확인한 뒤 커밋한다.** 직전 슬라이스에서 "초록인데 아무것도
안 지키는 테스트"가 여섯 개 나왔다.

### `MatchStartGateTests` (GameFramework, EditMode)

| 경우 | 기대 |
|---|---|
| 전원 준비 | 즉시 카운트다운, `출발틱 = 현재틱 + 150` |
| 일부만 준비 + 상한 만료 | 카운트다운 (안 온 사람 안 기다림) |
| 일부만 준비 + 상한 전 | 대기 유지 |
| 같은 사람이 두 번 준비 | `ReadyCount`는 1 |
| 카운트다운 중 나머지가 준비 | **출발틱 안 밀림** |
| `tick == 출발틱 − 1` | 아직 카운트다운 |
| `tick == 출발틱` | 진행 |
| 진행 후 준비 도착 | 무시, 아무것도 안 바뀜 |
| 예상 인원 0 | 즉시 카운트다운 (빈 방에서 멈추지 않는다) |

경계값 두 줄이 중요하다 — 카운트다운을 "남은 틱을 깎는 카운터"로 구현하면 발표한 출발틱과 실제
전이 시점이 어긋날 수 있고, 클라는 발표받은 숫자로 출발하므로 그대로 갈린다. 직전 슬라이스에서
`<= 0f` 하나로 한 틱이 밀렸던 것과 같은 부류다.

> *검토했다가 뺀 케이스*: "같은 틱으로 `Tick`을 두 번 불러도 전이 한 번만". `TickUpdaterBase`가
> `tick++`으로 매 틱 정확히 한 번씩만 내보내고, 밀려도 건너뛰지 않고 catch-up한다(snap-forward는
> 2026-08-09에 해보고 접었다). **일어날 수 없는 상황이라 어떻게 구현하든 영원히 초록**이다.

### `FlappyWorldStartGateTests` (LOP-Shared, EditMode)

기존 `FlappyWorldFixture`를 쓴다.

- `GameplayStartTick` 미설정(기본 `long.MaxValue`) → 200틱을 굴려도 위치·속도 불변
- `tick < 출발틱` → 불변
- `tick == 출발틱` → 그 틱부터 움직임
- 출발 경계를 가로지르는 구간을 두 번 굴려 같은 결과 (기존 `FlappyWorldDeterminismTests` 패턴)

### 자동 검증이 안 되는 것 — 정직한 공백

`RaceStartView`·`RaceStartViewModel`(클라 `Assembly-CSharp`)과 `MatchStartSystem`의 배선은 EditMode로
못 잡는다. 테스트 asmdef가 `Assembly-CSharp`를 참조할 수 없기 때문이다. **직전 슬라이스에서 574개
테스트를 전부 통과하고도 유령 연출이 런타임에 통째로 무효였던 그 공백이다.**

**그렇다고 테스트를 위해 코드를 다른 어셈블리로 옮기지 않는다.** 대신 무엇이 어디 있는지를 가른다:

| 무엇 | 틀리면 | 어디서 지키나 |
|---|---|---|
| `tick >= GameplayStartTick` | 클·서가 다른 틱에 출발 = **시뮬이 갈린다** | 월드 안. `FlappyWorldStartGateTests` |
| 준비 집계·상한·출발틱 결정 | 출발이 안 하거나 엉뚱한 틱에 한다 | `MatchStartGate`. `MatchStartGateTests` |
| 카운트다운 숫자(올림) | 화면에 "2, 1, 0"이 뜬다 | §10의 눈 검증 |
| 라벨 이름 오타 | 아무것도 안 보인다 | §10의 눈 검증 |

**위험한 두 개는 순수 C#에 있고 이미 테스트된다.** 아래 두 개는 연출이고, 화면을 켜는 순간
드러난다 — 그 정도 위험에 어셈블리 경계를 건널 이유가 없다.

---

## 10. 로컬 2인 검증 리그

이 슬라이스의 실질적 목적.

```
서버 에디터 재생
   └ [MatchStart] 대기 중 0/2 (상한 600초)      ← standalone 분기
클라 A 재생 (몇 초 뒤)
   └ [MatchStart] 대기 중 1/2
클라 B 재생 (또 몇 분 뒤여도 됨)
   └ [MatchStart] 2/2 → 카운트다운 → 3 → 2 → 1 → GO!
```

**사전조건** — 클라·서버 둘 다 env `local`.

- 클라 `local`: 로비·매치메이킹은 진짜 k8s 백엔드(`localhost/lobby`), 방 접속만
  `useLocalRoomInstance: 1`로 에디터 서버(`localhost:7777`)로 우회한다.
- 서버 `local`: `standalone: 1`. `ROOM_ID`가 없으므로 `ConfigureRoomComponent`가
  `#if UNITY_EDITOR`로 방·매치를 지어낸다.
- 그 픽스처의 `playerList`에 **두 계정 uuid가 다 있어야 한다.** MPPM 클론은 `-name` 인자로
  `AuthProfile`이 갈려 자기만의 익명 계정을 쓰고, 명단에 없으면 `LOPNetworkAuthenticator`가
  접속을 거부한다. 계정이 초기화되면 거부 로그(`명단에 없는 참가자: <uuid>`)의 값을 붙여넣는다.
  (2026-08-25 현재 두 개가 채워져 있다 — 메인 에디터 게스트 + MPPM 가상 플레이어.)

명단을 자동화하는 안(에디터에서 명단 대조 대신 정원 검사 + 접속 시 스폰, 언리얼 `PostLogin` 표준)도
검토했으나 **하지 않는다** — 지금 아프지 않은 곳을 고치면서 인증 경로와 두 게임의 스폰 시점을
건드리는 일이다. 계정 초기화가 잦아지면 그때 별도 슬라이스로 한다.

### 이 리그가 서면 바로 이어서 할 일

`feature/flappy-ghost-extrapolation`의 눈 검증 — 유령 반투명(G2), 원격 새의 매끄러움(G3), 낑겼을 때
카메라. 그것이 그 슬라이스 머지의 유일한 남은 조건이다.

---

## 11. 잘못될 수 있는 것

| 상황 | 어떻게 되나 |
|---|---|
| 클라가 준비를 영영 안 보냄 | 상한이 덮는다. 그 사람 새는 입력 없이 떨어진다 |
| `MatchStartToC` 유실 | reliable 채널이라 유실되지 않는다 |
| 이미 지난 출발틱을 받음 (지각 입장) | `tick >= 출발틱`이 바로 참 → 즉시 참여. 되감기 없음 |
| 클라 시계가 어긋남 | 자기 틱 기준으로 출발하므로 남보다 조금 이르거나 늦다. clock sync 정확도 축이고 `ClockSettle`이 이미 다룬다 |
| 카운트다운 중 접속 종료 | 아무것도 되돌리지 않는다. 그 새는 그냥 안 움직인다 |
| 예상 인원 0 (빈 명단) | 즉시 카운트다운 — 서버가 대기에서 영원히 멈추지 않는다 |

### 11.1 Panchigi가 이 슬라이스의 영향을 받는다 (범위 밖, 무해)

`MatchStartSystem`은 서버 공용 `GameplayInstaller`에 등록돼 있고, `LOPRunner.LateUpdate`가 5분 매치
타이머를 `Phase == InProgress`에 걸었다. 그래서 이 슬라이스 대상이 아닌 Panchigi(스텁 모드)도 매치
타이머 시작이 부팅 시점에서 `waitCap + 3초`만큼 늦춰졌다. 지금은 무해하다 — `PanchigiWorld.Mutation`이
비어 있어 얼릴 시뮬 자체가 없고, 게임플레이 결과가 달라지지 않는다. Panchigi가 실제 시뮬레이션(새
이동·판정 등)을 갖추는 순간부터는 이 지연이 진짜 게임플레이 영향을 만들 수 있으므로, 그때는 Panchigi를
게이트 대상에서 뺄지 자체 게이트를 둘지 결정해야 한다.

---

## 12. 산업 표준 매핑

임의 명명을 피하기 위해 결정 시점에 확인한 대응 관계.

| 우리 것 | 대응 | 비고 |
|---|---|---|
| `MatchPhase` | 언리얼 `AGameMode::MatchState` (`WaitingToStart`/`InProgress`/`WaitingPostMatch`) | 이름·단계 구성 모두 그쪽을 따랐다 |
| `MatchStartGate` | 언리얼 `ReadyToStartMatch()` + `bDelayedStart` | "전원 준비됐나"를 묻고 `StartMatch()`로 전이 |
| 대기 중 새를 얼림 | Quantum `StartDisabled` 시스템 / CS `mp_freezetime` | 시뮬은 돌고 이동만 멈춘다 |
| 틱은 서버 수명 기준 | Unity Netcode for Entities `ServerTick` (기동 시 1부터 연속) | 매치 기준으로 0부터 세지 않는다 |
| 상태 대신 출발틱 송신 | GGPO/Quantum이 사실에 프레임 번호를 박는 방식 | 앞서 달리고 되감는 클라에 필요 |
| `MatchStartToC` | 기존 `MatchEndedToC`와 짝 | 코드베이스 내 일관 |

---

## 13. 참고

- [Unreal — Game Mode and Game State](https://dev.epicgames.com/documentation/en-us/unreal-engine/game-mode-and-game-state-in-unreal-engine)
- [Unreal — AGameMode::StartMatch](https://docs.unrealengine.com/5.2/en-US/API/Runtime/Engine/GameFramework/AGameMode/StartMatch/)
- [Unreal — bDelayedStart](https://docs.unrealengine.com/5.0/en-US/API/Runtime/Engine/GameFramework/AGameMode/bDelayedStart/)
- [Photon Quantum — Systems (StartDisabled, 상태머신 예시)](https://doc.photonengine.com/quantum/current/manual/quantum-ecs/systems)
- [Unity Netcode for Entities — NetworkTime.ServerTick](https://docs.unity3d.com/Packages/com.unity.netcode@1.1/api/Unity.NetCode.NetworkTime.ServerTick.html)
- [CS — mp_freezetime](https://totalcsgo.com/commands/mpfreezetime)
- 내부: `docs/netcode-redesign.md` §4~5 (clock sync, 입력 버퍼), `docs/world-core-connection-architecture.md`
  (Engine↔Simulation 책임 분리), `docs/superpowers/specs/2026-08-24-flappy-ghost-and-remote-extrapolation-design.md`
