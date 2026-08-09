# 매치 시작 틱 정렬 구현 계획

> **에이전트용:** 필수 하위 스킬 — superpowers:subagent-driven-development 또는
> superpowers:executing-plans로 태스크 단위 실행. 각 단계는 체크박스(`- [ ]`)로 추적.

**Goal:** 매치 시작 직후 클라 입력이 전량 폐기되는 문제를 없앤다 — 틱을 남이 준 옛날 값이 아니라
자기 시계에서 유도하도록 양쪽을 고친다.

**Architecture:** 서버는 `Run(0,…,0)` 대신 자기 시계(`ServerNow`)에서 시작 틱을 계산해 `tick`과
`elapsedTime`의 자기모순을 없앤다. 클라는 `gameInfo`의 과거 스냅샷 대신 자기 목표 시각
(`PredictedTime + AheadMargin`)에서 출발한다. 목표 시각 계산식은 `LOPTickUpdater` 한 곳에 둔다.

**Tech Stack:** Unity 6, C#, Mirror(NetworkTime), VContainer.

설계: `docs/superpowers/specs/2026-08-09-tick-origin-alignment-design.md`

## Global Constraints

- **틱 간격은 상수로 박지 않는다** — 런타임 값(`gameInfo.Interval` / `TICK_INTERVAL`)에서 환산.
- **동작을 바꾸는 변경은 이 두 곳뿐이다.** `ClockDilator`·`LeadController`·catch-up 상한은 손대지 않는다.
- **`LeadController` 경계값을 틱 배수로 바꾸는 정리는 이 계획 밖이다**(spec §10).
- 클·서 양쪽 Unity 콘솔 컴파일 에러 0.
- **플레이 검증 전 반드시 플레이를 정지하고 리컴파일한다** — 플레이 중 리컴파일은 도메인 리로드로
  런타임 DI 참조를 날려 `LOPEntityView`가 매 프레임 NRE를 뱉는다(코드 결함 아님).

---

## File Structure

| 파일 | 책임 |
|---|---|
| `LeagueOfPhysical-Server/Assets/Scripts/Room/LOPRoom.cs` | 서버 시작 틱을 자기 시계에서 유도 |
| `LeagueOfPhysical-Client/Assets/Scripts/Netcode/LOPTickUpdater.cs` | 목표 시각(`TargetTime`)을 한 곳에서 계산·노출 |
| `LeagueOfPhysical-Client/Assets/Scripts/Room/LOPRoom.cs` | 클라 시작 틱을 그 목표 시각에서 유도 |
| `LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs` | 더 이상 읽히지 않는 필드에 주석 |

**유닛 테스트가 없는 이유**: 네 파일 모두 클·서 `Assembly-CSharp`라 EditMode 테스트가 불가능하다
(알려진 제약). GameFramework·LOP-Shared는 건드리지 않는다. 검증은 **컴파일 + 플레이 측정**이다.

---

### Task 1: 서버 시작 틱을 자기 시계에서 유도

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Room/LOPRoom.cs:134`

**Interfaces:**
- Consumes: `runner.networkTime.ServerNow` — `InitializeAsync`에서 설정되며 `StartGameAsync`보다 먼저 실행된다(확인함)
- Produces: 서버 `tick`이 첫 프레임부터 `elapsedTime / TICK_INTERVAL`과 일치

- [ ] **Step 1: 시드를 바꾼다**

`runner.Run(0, TICK_INTERVAL, 0);` 를 아래로 교체:

```csharp
            // 틱을 자기 시계에서 유도한다. 0을 넣으면 다음 프레임에 elapsedTime이 ServerNow(프로세스
            // 가동 시간)로 덮이면서 tick만 뒤처지고, 프레임당 8틱 상한 탓에 몇 초를 8배속으로 질주한다.
            // 그동안 tick과 elapsedTime이 서로 안 맞아 gameInfo가 자기모순인 값을 클라에 보낸다.
            double now = runner.networkTime.ServerNow;
            runner.Run((long)(now / TICK_INTERVAL), TICK_INTERVAL, now);
```

- [ ] **Step 2: 컴파일 확인**

서버 Unity에서 refresh 후 콘솔 에러 0. (플레이 중이면 먼저 정지)

- [ ] **Step 3: 커밋**

```bash
git add Assets/Scripts/Room/LOPRoom.cs
git commit -m "fix(netcode): 서버 시작 틱을 자기 시계에서 유도한다"
```

---

### Task 2: 클라 목표 시각을 한 곳에 두고, 거기서 시드

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Netcode/LOPTickUpdater.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Room/LOPRoom.cs:132-137`

**Interfaces:**
- Produces: `LOPTickUpdater.TargetTime` (double) — 이 시계가 수렴해 갈 목표 시각
- Consumes: `runner.tickUpdater`(= `ITickUpdater`)를 `LOPTickUpdater`로 캐스팅. 기존 `LOPRunner`도
  `((LOPTickUpdater)tickUpdater)` 형태를 쓰고 있어 같은 관용을 따른다

- [ ] **Step 1: `TargetTime`을 노출하고 기존 계산을 그리로 모은다**

`LOPTickUpdater.cs` 전체를 아래로 교체:

```csharp
using GameFramework;
using GameFramework.Runner;
using UnityEngine;
using VContainer;
using GameFramework.Netcode;

namespace LOP
{
    public class LOPTickUpdater : TickUpdaterBase
    {
        [Inject]
        private LeadState leadState;

        public GameFramework.Netcode.INetworkTime networkTime;

        private readonly ClockDilator clockDilator = new ClockDilator();

        /// <summary>
        /// 이 시계가 수렴해 갈 목표 시각 — 서버 추정 시각에 앞서갈 여유를 더한 값.
        /// 매치 시작 시드도 여기서 가져간다(같은 식을 두 곳에 두면 어긋난다).
        /// </summary>
        public double TargetTime
        {
            get
            {
                // 동적 lead(LeadState)는 입력 타이밍 피드백으로 갱신됨. 주입 전(초기 프레임)엔 기본값.
                double aheadMargin = leadState != null ? leadState.AheadMargin : LeadState.DefaultMargin;
                return networkTime.PredictedTime + aheadMargin;
            }
        }

        protected override void OnElapsedTimeUpdate()
        {
            elapsedTime = clockDilator.Advance(elapsedTime, TargetTime, Time.deltaTime);
        }
    }
}
```

- [ ] **Step 2: 클라 시드를 목표 시각에서 잡는다**

`LOPRoom.StartGameAsync`를 아래로 교체:

```csharp
        public async Task StartGameAsync()
        {
            var gameInfo = gameDataStore.gameInfo;

            // 출발선을 제 위치(서버보다 앞)에 놓는다. gameInfo.Tick/ElapsedTime은 보낸 순간의 값이라
            // 받았을 땐 이미 과거다. 속도 보정(ClockDilator)은 달리는 중 드리프트를 잡는 장치이지
            // 잘못된 출발점을 메우는 장치가 아니다 — 0.5초 미만 오차는 5%씩만 좁혀 수 초가 걸린다.
            double target = ((LOPTickUpdater)runner.tickUpdater).TargetTime;
            runner.Run((long)(target / gameInfo.Interval), gameInfo.Interval, target);
        }
```

- [ ] **Step 3: 컴파일 확인**

클라 Unity refresh 후 콘솔 에러 0.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Netcode/LOPTickUpdater.cs Assets/Scripts/Room/LOPRoom.cs
git commit -m "fix(netcode): 클라 시작 틱을 자기 목표 시각에서 유도한다"
```

---

### Task 3: 더 이상 읽히지 않는 `gameInfo` 필드에 주석

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs:78-80`

- [ ] **Step 1: 주석을 단다**

```csharp
                    GameInfo = new GameInfo
                    {
                        // Tick·ElapsedTime은 클라가 더 이상 시드로 쓰지 않는다(자기 시계에서 유도).
                        // 진단용으로만 남긴다 — 이 값으로 시작 시점을 정하면 보낸 순간의 과거 값이라 어긋난다.
                        Tick = runner.tickUpdater.tick,
                        Interval = runner.tickUpdater.interval,
                        ElapsedTime = runner.tickUpdater.elapsedTime,
                        MatchSeed = matchSeed.Value,
                    },
```

> `Interval`·`MatchSeed`는 계속 필요하다(spec §7) — 지우지 말 것.

- [ ] **Step 2: 커밋**

```bash
git add Assets/Scripts/Game/MessageHandler/GameInfoMessageHandler.cs
git commit -m "docs(netcode): gameInfo.Tick/ElapsedTime은 시드로 쓰지 않는다고 명시"
```

---

### Task 4: 플레이 측정 (사람이 수행)

**측정 절차가 기존과 다르다** — warm-up 없이 **매치 시작 순간부터** 잰다.

- [ ] **Step 1: 준비**

양쪽 에디터 **플레이 정지 → 리컴파일 → 콘솔 Clear**. 씬 시뮬 설정은 로컬 픽스처 그대로
(`latency 150 / jitter 0.02 / unreliableLoss 2 / unreliableScramble 2`).

- [ ] **Step 2: 입장 직후 바로 걷기**

`Reset stats`를 **누르지 않는다**(시작 구간이 측정 대상이다). 입장하자마자 걷기 시작.

- [ ] **Step 3: 10초 뒤 `Dump`**

- [ ] **Step 4: 판정**

| | 지금 (실측) | 기대 |
|---|---|---|
| `pruneTot` | **384** (시작 폭주 포함) | **한 자릿수** |
| `worstD` | **+296** | **음수** |
| 서버 콘솔 `[TickUpdater] catch-up capped` | 시작 시 발생 | **없음** |
| 시작 직후 서버 캐릭터 | 원점 정지 | 클라와 함께 움직임 |

**단일 판정 기준: 시작 직후 `prune`이 세 자릿수에서 0 근처로 떨어지는가.**

`stalls`가 0이 아니면 그 측정은 버리고 다시 한다.

- [ ] **Step 5: 되돌리기 기준**

판정이 실패하거나 새 증상(시작 시 캐릭터 순간이동, 스냅 보정 폭주 등)이 보이면 **Task 1·2를 되돌린다**
(`git revert`). 두 커밋이 독립적이라 서버만 / 클라만 되돌려 어느 쪽이 원인인지 가릴 수 있다.

---

---

## 실행 중 드러난 것 — Task 5·6 추가

Task 1~4를 돌린 결과, **계획이 예상한 원인 중 절반만 맞았다.**

| | 결과 |
|---|---|
| Task 1 (서버 시드) | ✅ **검증** — `catch-up capped` 1680 → **12** |
| Task 2 (클라 시드) | ⚪ **중립** — 284 ↔ 288, 변화 없음 |

클라 질주의 진짜 원인은 시드 출처가 아니라 **시계가 맞기 전에 시뮬을 시작하는 것**이었다
(spec §8.1). 계획이 위험 1로 적어두고 "수렴 후일 것"이라 넘겨짚은 바로 그것.

### Task 5: 시계 수렴 대기 (A) — ✅ 완료

- `LOPRoom`에 `WaitForClockSyncAsync` — 보정값이 3연속 10ms 미만이면 수렴으로 보고 출발, 5초 타임아웃
- 접속 직후부터 굴려 join 왕복과 **겹친다**(순서대로 두면 로딩에 그대로 더해진다)
- **결과: 클라 `catch-up capped` 0건, `dAvg` +142 → −4, 드르륵 소멸**
- ⚠️ **대가: 실측 대기 4300ms.** Mirror EWMA(20샘플×100ms → 95% 수렴 ≈3.1초)라
  임계값을 풀어도 3초 아래로는 못 내려간다 — 구조적 비용이다

### Task 6: snap-forward (B) — 구현 완료, 측정 대기

밀린 틱을 계산하지 않고 번호만 옮긴다. A와 달리 **중간 멈춤에도 듣고 대기 비용이 없다.**

- GameFramework `MaxCatchUpTicks` (기본 무한 = 건너뛰지 않음), 클라만 32틱으로 override
- **서버는 켜지 않는다** — 건너뛰기는 "누군가 상태를 다시 세워 준다"는 전제가 필요한데
  서버에는 그런 존재가 없다(자기가 권위)
- 계측: `snapForwardCount` → 덤프 `snaps=`

- [ ] **측정**: 입장 후 1~2분 이동 → `Dump`. `stalls=0`인데 `snaps`가 오르면 의도대로
- [ ] **B 검증되면 A(Task 5) 제거 검토** — 4.3초 로딩을 되돌린다. 대신 시작 시 `snaps` 1회 +
      위치 교정 1회가 보일 것이다. 둘 중 뭐가 나은지는 재보고 정한다

---

## 완료 후

- [ ] ROADMAP에 결과 기록(수치 포함)
- [ ] 변경된 3레포(GameFramework·Client·Server) 머지 + 푸시
- [ ] 임시 계측 정리: `[Room] 시계 수렴 대기 Nms` 로그
- [x] spec §8 위험 표에 실제로 일어난 것 추가 (§8.1·§8.2)
