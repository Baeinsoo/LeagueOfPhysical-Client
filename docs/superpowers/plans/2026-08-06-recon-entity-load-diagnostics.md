# Recon 엔티티-로드 진단 계측 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 엔티티가 많을 때 생기는 recon 러버밴딩의 원인을 A(서버 틱 밀림) / B(클라 프레임 저하) / 제3 / 재현 안 됨 중 하나로 확정할 계측기를 깔고, 실험을 수행해 판정한다.

**Architecture:** 클라 HUD가 주 계기판이다 — FPS·엔티티 수·스냅 도착 간격·서버틱 지연을 기존 Recon/Lead 값 옆에 붙인다. 서버는 확증용 로그 한 줄만 남긴다. 부하는 서버 인스펙터의 ContextMenu로 즉시 만들고 되돌린다. 순수 계산(도착 간격 통계)은 GameFramework에 두어 EditMode로 테스트한다.

**Tech Stack:** Unity 6 · C# · VContainer · UI Toolkit · NUnit(EditMode) · Mirror

## Global Constraints

- **spec:** `docs/superpowers/specs/2026-08-06-recon-entity-load-diagnostics-design.md`. 이 계획은 그 spec을 구현한다.
- **3레포에 걸친다.** 각 레포의 작업 위치가 다르다:
  - **GameFramework** — `C:\Users\re5na\workspace\LOP\GameFramework`, **main 체크아웃**에서 브랜치 `feature/snapshot-arrival-stats`. ⚠️ 워크트리를 쓰지 말 것 — 클·서 Unity가 `file:` 경로로 main 체크아웃을 보므로 워크트리 코드는 에디터에 보이지 않고 EditMode 테스트도 못 돈다.
  - **클라이언트** — 현재 워크트리 `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client\.claude\worktrees\netcode-recon-diagnostics`, 브랜치 `worktree-netcode-recon-diagnostics`.
  - **서버** — `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`, **main 체크아웃**에서 브랜치 `feature/recon-load-diagnostics`. (서버 에디터도 자기 main 체크아웃을 본다.)
- **main 브랜치에 직접 커밋 금지.** 세 레포 모두 피처 브랜치에서 작업한다.
- **⚠️ 클라 `Assets/` 코드는 머지 전 컴파일 검증이 불가능하다.** 연결된 Unity 에디터가 워크트리가 아니라 클라 main 체크아웃을 보기 때문이다. Task 2·3은 grep과 코드 리뷰가 유일한 안전망이고, 진짜 관문은 Task 6이다. **GameFramework와 서버는 main 체크아웃에서 작업하므로 즉시 컴파일·테스트된다.**
- **UnityMCP 호출에는 항상 `unity_instance`를 명시한다.** 서버·클라 에디터가 동시에 붙어 있어 생략하면 엉뚱한 인스턴스로 간다. id 확인: 리소스 `mcpforunity://instances`를 읽어 `name`이 `LeagueOfPhysical-Client` / `LeagueOfPhysical-Server`인 항목의 전체 `id`(`Name@hash`)를 쓴다. 해시는 바뀔 수 있다.
- **주석 컨벤션:** 코드로 자명한 것에는 주석을 달지 않는다. 비자명한 *의도(왜)* 만 일상어로 짧게. 전문용어를 설명 없이 던지지 않는다.
- **`.meta` 파일은 반드시 커밋한다.** 새 `.cs`를 만든 뒤 Unity가 생성한 `.meta`를 함께 커밋한다. 직접 만들지 않는다 — Unity가 스캔할 때까지 기다린다.
- **명명은 기존 짝에 맞춘다:** 새 통계 홀더는 `ReconciliationStats`·`InputTimingStats`·`InterpolationDelayEstimator`와 같은 어휘·같은 모양을 따른다.

---

## 파일 구조

| 레포 | 파일 | 책임 |
|---|---|---|
| GF | **생성** `Runtime/Scripts/Netcode/SnapshotArrivalStats.cs` | 스냅 배치 도착의 최신 tick·간격 통계 (순수 계산) |
| GF | **생성** `Tests/Runtime/Netcode/SnapshotArrivalStatsTests.cs` | 위의 EditMode 테스트 |
| 클라 | **수정** `Assets/Scripts/Netcode/RemoteInterpolationClock.cs` | 적응형 쿠션 읽기 노출 |
| 클라 | **수정** `Assets/Scripts/Netcode/ReconciliationStats.cs` | `Reset()` 추가 |
| 클라 | **수정** `Assets/Scripts/Game/GameLifetimeScope.cs` | `SnapshotArrivalStats` DI 등록 |
| 클라 | **수정** `Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs` | 스냅 도착 기록 |
| 클라 | **수정** `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs` | 새 지표 getter + 리셋 커맨드 |
| 클라 | **수정** `Assets/UI/DebugHud/DebugHud.uxml` | 라벨 5개 + 리셋 버튼 |
| 클라 | **수정** `Assets/Scripts/UI/DebugHud/DebugHudView.cs` | 라벨 바인딩 + 버튼 배선 |
| 서버 | **수정** `Assets/Scripts/Game/GameRuleSystem.cs` | 스폰 공개 + 적 전체 디스폰 |
| 서버 | **생성** `Assets/Scripts/Diagnostics/DebugEnemySpawner.cs` | 인스펙터에서 부하 조절 |
| 서버 | **생성** `Assets/Scripts/Diagnostics/TickHealthLogger.cs` | 틱 밀림·프레임 주기 로그 |
| 클라 | **수정** `docs/ROADMAP.md` | 실험 결과 기록 (Task 7) |

---

### Task 1: `SnapshotArrivalStats` (GameFramework)

스냅 배치가 얼마나 고르게 도착하는지, 그리고 가장 최근에 받은 서버 tick이 몇인지를 재는 순수 계산 홀더. 클라가 "서버가 밀리고 있나"를 판정하는 재료다.

**Files:**
- Create: `C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\Netcode\SnapshotArrivalStats.cs`
- Test: `C:\Users\re5na\workspace\LOP\GameFramework\Tests\Runtime\Netcode\SnapshotArrivalStatsTests.cs`

**Interfaces:**
- Consumes: 없음 (독립)
- Produces: `GameFramework.Netcode.SnapshotArrivalStats` —
  `void Record(long serverTick, double arrivalTime)` ·
  `void Reset()` ·
  `long LatestTick { get; }` (도착 없음 = `-1`) ·
  `double AverageInterval { get; }` (초) ·
  `double MaxInterval { get; }` (초) ·
  `int SampleCount { get; }`

- [ ] **Step 1: 브랜치 생성**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git checkout main
git checkout -b feature/snapshot-arrival-stats
```

- [ ] **Step 2: 실패하는 테스트 작성**

`C:\Users\re5na\workspace\LOP\GameFramework\Tests\Runtime\Netcode\SnapshotArrivalStatsTests.cs`:

```csharp
using GameFramework.Netcode;
using NUnit.Framework;

namespace GameFramework.Tests.Netcode
{
    public class SnapshotArrivalStatsTests
    {
        [Test]
        public void NoArrivals_LatestTickIsMinusOne()
        {
            var s = new SnapshotArrivalStats();
            Assert.AreEqual(-1, s.LatestTick);
            Assert.AreEqual(0, s.SampleCount);
            Assert.AreEqual(0.0, s.AverageInterval, 1e-9);
            Assert.AreEqual(0.0, s.MaxInterval, 1e-9);
        }

        [Test]
        public void FirstArrival_SetsTick_ButHasNoIntervalYet()
        {
            var s = new SnapshotArrivalStats();
            s.Record(10, 1.0);
            Assert.AreEqual(10, s.LatestTick);
            Assert.AreEqual(0, s.SampleCount);
        }

        [Test]
        public void SecondArrival_RecordsInterval()
        {
            var s = new SnapshotArrivalStats();
            s.Record(10, 1.0);
            s.Record(11, 1.05);
            Assert.AreEqual(11, s.LatestTick);
            Assert.AreEqual(1, s.SampleCount);
            Assert.AreEqual(0.05, s.AverageInterval, 1e-9);
            Assert.AreEqual(0.05, s.MaxInterval, 1e-9);
        }

        [Test]
        public void StaleOrDuplicateTick_IsIgnored()
        {
            var s = new SnapshotArrivalStats();
            s.Record(10, 1.0);
            s.Record(11, 1.05);
            s.Record(11, 1.06);   // 같은 틱이 청킹돼 또 온 경우
            s.Record(9, 1.07);    // 순서가 뒤집혀 온 경우
            Assert.AreEqual(11, s.LatestTick);
            Assert.AreEqual(1, s.SampleCount);
            Assert.AreEqual(0.05, s.MaxInterval, 1e-9);
        }

        [Test]
        public void MaxInterval_KeepsLargest()
        {
            var s = new SnapshotArrivalStats();
            s.Record(1, 0.0);
            s.Record(2, 0.02);
            s.Record(3, 0.20);
            s.Record(4, 0.22);
            Assert.AreEqual(0.18, s.MaxInterval, 1e-9);
        }

        [Test]
        public void Average_IsMeanOfIntervals()
        {
            var s = new SnapshotArrivalStats();
            s.Record(1, 0.0);
            s.Record(2, 0.10);
            s.Record(3, 0.30);
            Assert.AreEqual(3, s.LatestTick);
            Assert.AreEqual(2, s.SampleCount);
            Assert.AreEqual(0.15, s.AverageInterval, 1e-9);
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var s = new SnapshotArrivalStats();
            s.Record(1, 0.0);
            s.Record(2, 0.30);
            s.Reset();
            Assert.AreEqual(-1, s.LatestTick);
            Assert.AreEqual(0, s.SampleCount);
            Assert.AreEqual(0.0, s.AverageInterval, 1e-9);
            Assert.AreEqual(0.0, s.MaxInterval, 1e-9);
        }

        [Test]
        public void AfterReset_NextArrivalStartsFresh()
        {
            var s = new SnapshotArrivalStats();
            s.Record(1, 0.0);
            s.Record(2, 0.30);
            s.Reset();
            s.Record(3, 5.0);   // 리셋 직후 첫 도착 — 리셋 전 시각과의 간격을 만들면 안 된다
            Assert.AreEqual(0, s.SampleCount);
            Assert.AreEqual(0.0, s.MaxInterval, 1e-9);
        }
    }
}
```

- [ ] **Step 3: 테스트가 실패하는지 확인**

Unity 클라 에디터에서 EditMode 테스트를 돌린다(GameFramework는 `testables`에 등록돼 있다).

```
mcp__UnityMCP__run_tests(mode="EditMode", filter="SnapshotArrivalStatsTests",
                         unity_instance="LeagueOfPhysical-Client@<hash>")
```

Expected: **컴파일 실패** — `SnapshotArrivalStats` 타입이 없다.

- [ ] **Step 4: 최소 구현 작성**

`C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\Netcode\SnapshotArrivalStats.cs`:

```csharp
using System.Collections.Generic;

namespace GameFramework.Netcode
{
    /// <summary>
    /// 스냅 배치가 얼마나 고르게 도착하는지 재는 통계. 순수 — EditMode 테스트.
    /// 서버가 자기 틱을 못 지키면 도착 간격이 흔들리고, 최신 tick이 벽시계 기준 추정보다 뒤처진다.
    /// 그 두 가지를 클라에서 보기 위한 재료다.
    /// </summary>
    public class SnapshotArrivalStats
    {
        // 평균 창(샘플 수). 50Hz 기준 약 1.2초 — 조건이 바뀌면 그만큼 안에 값이 따라온다.
        private const int WindowSize = 60;

        private readonly Queue<double> window = new Queue<double>(WindowSize);
        private double sum;
        private double lastArrival;
        private bool hasLast;

        /// <summary>가장 최근에 받은 서버 tick. 아직 하나도 못 받았으면 -1.</summary>
        public long LatestTick { get; private set; } = -1;

        public double AverageInterval { get; private set; }

        public double MaxInterval { get; private set; }

        public int SampleCount => window.Count;

        /// <summary>
        /// 호출자는 틱당 한 번만 부른다. 같은 틱이 여러 메시지로 쪼개져 와도 첫 것만 — 간격이
        /// 0에 가깝게 찍혀 통계가 망가지는 걸 막는다. 순서가 뒤집혀 온 오래된 틱도 무시한다.
        /// </summary>
        public void Record(long serverTick, double arrivalTime)
        {
            if (serverTick <= LatestTick)
            {
                return;
            }
            LatestTick = serverTick;

            if (hasLast)
            {
                double interval = arrivalTime - lastArrival;
                if (interval > MaxInterval)
                {
                    MaxInterval = interval;
                }
                window.Enqueue(interval);
                sum += interval;
                if (window.Count > WindowSize)
                {
                    sum -= window.Dequeue();
                }
                AverageInterval = sum / window.Count;
            }
            lastArrival = arrivalTime;
            hasLast = true;
        }

        /// <summary>실험 조건을 바꿀 때 부른다. 이전 조건의 최대값이 다음 조건에 섞이지 않게.</summary>
        public void Reset()
        {
            window.Clear();
            sum = 0;
            lastArrival = 0;
            hasLast = false;
            LatestTick = -1;
            AverageInterval = 0;
            MaxInterval = 0;
        }
    }
}
```

- [ ] **Step 5: 테스트 통과 확인**

```
mcp__UnityMCP__run_tests(mode="EditMode", filter="SnapshotArrivalStatsTests",
                         unity_instance="LeagueOfPhysical-Client@<hash>")
```

Expected: **8 passed, 0 failed.**

- [ ] **Step 6: GameFramework 전체 EditMode 회귀 확인**

```
mcp__UnityMCP__run_tests(mode="EditMode", filter="GameFramework",
                         unity_instance="LeagueOfPhysical-Client@<hash>")
```

Expected: 기존 테스트가 전부 통과(추가된 8개만큼 총계가 늘어남). 실패가 하나라도 있으면 멈추고 원인을 찾는다.

- [ ] **Step 7: 커밋**

`.meta` 파일 2개(새 `.cs` 각각)가 Unity에 의해 생성됐는지 먼저 확인한다. 없으면 에디터가 스캔할 때까지 기다린다 — 직접 만들지 않는다.

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git status --short
git add Runtime/Scripts/Netcode/SnapshotArrivalStats.cs Runtime/Scripts/Netcode/SnapshotArrivalStats.cs.meta \
        Tests/Runtime/Netcode/SnapshotArrivalStatsTests.cs Tests/Runtime/Netcode/SnapshotArrivalStatsTests.cs.meta
git commit -m "feat(netcode): 스냅 도착 간격·최신 tick 통계 추가

서버가 틱을 못 지키면 도착 간격이 흔들리고 최신 tick이 벽시계 기준 추정보다
뒤처진다. 클라가 그걸 보고 원인을 가르기 위한 재료."
```

---

### Task 2: 클라 계측 배선 + HUD 표시

새 지표 다섯 개를 HUD에 띄운다. 표시는 읽기 전용이고 리셋 버튼은 다음 태스크다.

**Files:**
- Modify: `Assets/Scripts/Netcode/RemoteInterpolationClock.cs`
- Modify: `Assets/Scripts/Game/GameLifetimeScope.cs:100-111`
- Modify: `Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs`
- Modify: `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs`
- Modify: `Assets/UI/DebugHud/DebugHud.uxml`
- Modify: `Assets/Scripts/UI/DebugHud/DebugHudView.cs`

**Interfaces:**
- Consumes: Task 1의 `GameFramework.Netcode.SnapshotArrivalStats`
- Produces: `DebugHudViewModel`의 새 getter —
  `float Fps` · `float FrameMs` · `int EntityCount` · `double CushionMs` ·
  `long ServerTickLag` · `double SnapIntervalAvgMs` · `double SnapIntervalMaxMs`

> ⚠️ 이 태스크의 코드는 **머지 전까지 컴파일되지 않는다**(Global Constraints 참고). 각 스텝 뒤에 grep으로 배선을 눈으로 확인하고 넘어간다.

- [ ] **Step 1: 보간 쿠션 노출**

`Assets/Scripts/Netcode/RemoteInterpolationClock.cs`의 `HasSnapshot` 프로퍼티 바로 아래에 추가:

```csharp
        public bool HasSnapshot => hasSnapshot;

        /// <summary>지금 쓰고 있는 보간 쿠션(초). 스냅 도착이 들쭉날쭉할수록 커진다 — 진단 표시용.</summary>
        public double Cushion => initialized ? estimator.Cushion : 0;
```

- [ ] **Step 2: DI 등록**

`Assets/Scripts/Game/GameLifetimeScope.cs`에서 `builder.Register<InputTimingStats>(Lifetime.Singleton);` 바로 아래에 추가:

```csharp
            builder.Register<GameFramework.Netcode.SnapshotArrivalStats>(Lifetime.Singleton);
```

- [ ] **Step 3: 스냅 도착 기록**

`Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs`:

(a) 필드 추가 — `private readonly RemoteInterpolationClock remoteInterpolationClock;` 아래:

```csharp
        private readonly GameFramework.Netcode.SnapshotArrivalStats snapshotArrivalStats;
```

(b) 생성자 파라미터 추가 — `RemoteInterpolationClock remoteInterpolationClock,` 아래:

```csharp
            GameFramework.Netcode.SnapshotArrivalStats snapshotArrivalStats,
```

(c) 생성자 본문에 대입 — `this.remoteInterpolationClock = remoteInterpolationClock;` 아래:

```csharp
            this.snapshotArrivalStats = snapshotArrivalStats;
```

(d) `OnEntitySnapsToC`의 **기존 틱당-1회 dedupe 블록 안**에 기록 추가:

```csharp
            if (entitySnapsToC.Tick > lastRecordedArrivalTick)
            {
                remoteInterpolationClock.RecordArrival(entitySnapsToC.Tick, UnityEngine.Time.timeAsDouble);
                snapshotArrivalStats.Record(entitySnapsToC.Tick, UnityEngine.Time.timeAsDouble);
                lastRecordedArrivalTick = entitySnapsToC.Tick;
            }
```

> 왜 여기인가: 이 메시지는 **내 캐릭터를 포함한 전 엔티티**를 담아 오므로 적이 0마리여도 매 틱 도착한다. 그리고 이 `if`가 이미 청킹된 중복을 걸러 주고 있어, 통계가 필요로 하는 "틱당 1회" 규약이 그대로 성립한다.

- [ ] **Step 4: ViewModel에 지표 추가**

`Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs`:

(a) 최상단 using에 추가:

```csharp
using UnityEngine;
```

(b) 필드·생성자에 세 의존을 추가한다. 기존 생성자 전체를 아래로 교체:

```csharp
        private readonly IRunner runner;
        private readonly ReconciliationStats reconciliationStats;
        private readonly InputTimingStats inputTimingStats;
        private readonly GameFramework.Netcode.SnapshotHistory snapshotHistory;
        private readonly GameFramework.Netcode.SnapshotArrivalStats snapshotArrivalStats;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly RemoteInterpolationClock remoteInterpolationClock;

        public DebugHudViewModel(
            IRunner runner,
            ReconciliationStats reconciliationStats,
            InputTimingStats inputTimingStats,
            GameFramework.Netcode.SnapshotHistory snapshotHistory,
            GameFramework.Netcode.SnapshotArrivalStats snapshotArrivalStats,
            GameFramework.World.EntityRegistry entityRegistry,
            RemoteInterpolationClock remoteInterpolationClock)
        {
            this.runner = runner;
            this.reconciliationStats = reconciliationStats;
            this.inputTimingStats = inputTimingStats;
            this.snapshotHistory = snapshotHistory;
            this.snapshotArrivalStats = snapshotArrivalStats;
            this.entityRegistry = entityRegistry;
            this.remoteInterpolationClock = remoteInterpolationClock;
        }
```

(c) 클래스 끝(`SnapshotLatestTick` 아래)에 getter 추가:

```csharp
        // Time.smoothDeltaTime = Unity가 평활한 프레임 간격. 한 프레임 튄 값에 숫자가 요동치지 않는다.
        public float Fps => Time.smoothDeltaTime > 0f ? 1f / Time.smoothDeltaTime : 0f;

        public float FrameMs => Time.smoothDeltaTime * 1000f;

        public int EntityCount => entityRegistry.Count;

        public double CushionMs => remoteInterpolationClock.Cushion * 1000;

        // 벽시계로 추정한 서버 tick − 실제로 받은 최신 스냅의 tick. 절대값엔 편도지연이 상수로
        // 깔려 있으니 보는 건 "자라는가"다. 자라면 서버가 자기 틱을 못 따라가고 있다는 뜻.
        // 스냅을 아직 못 받았으면(LatestTick=-1, 리셋 직후 포함) 0으로. 안 그러면 거대한 수가 잠깐 뜬다.
        public long ServerTickLag => snapshotArrivalStats.LatestTick < 0
            ? 0
            : ServerTickEstimate - snapshotArrivalStats.LatestTick;

        public double SnapIntervalAvgMs => snapshotArrivalStats.AverageInterval * 1000;

        public double SnapIntervalMaxMs => snapshotArrivalStats.MaxInterval * 1000;
```

- [ ] **Step 5: UXML에 라벨 추가**

`Assets/UI/DebugHud/DebugHud.uxml`의 `snapshot-tick-text` 라벨 **아래**, `</ui:VisualElement>` 앞에 추가:

```xml
            <ui:Label name="fps-text" class="debug-text" text="FPS: 0 (0.0 ms)" />
            <ui:Label name="entity-count-text" class="debug-text" text="Entities: 0" />
            <ui:Label name="snap-lag-text" class="debug-text" text="Snap lag: 0 tick" />
            <ui:Label name="snap-interval-text" class="debug-text" text="Snap gap: 0.0 / 0.0 ms" />
            <ui:Label name="cushion-text" class="debug-text" text="Cushion: 0.0 ms" />
```

- [ ] **Step 6: View에 바인딩 추가**

`Assets/Scripts/UI/DebugHud/DebugHudView.cs`:

(a) 필드 추가 — `private Label _snapshotTickText;` 아래:

```csharp
        private Label _fpsText;
        private Label _entityCountText;
        private Label _snapLagText;
        private Label _snapIntervalText;
        private Label _cushionText;
```

(b) `OnOpen`의 `_snapshotTickText = Root.Q<Label>("snapshot-tick-text");` 아래:

```csharp
            _fpsText = Root.Q<Label>("fps-text");
            _entityCountText = Root.Q<Label>("entity-count-text");
            _snapLagText = Root.Q<Label>("snap-lag-text");
            _snapIntervalText = Root.Q<Label>("snap-interval-text");
            _cushionText = Root.Q<Label>("cushion-text");
```

(c) `Refresh`의 마지막 줄 아래:

```csharp
            _fpsText.text = $"FPS: {_viewModel.Fps:F0} ({_viewModel.FrameMs:F1} ms)";
            _entityCountText.text = $"Entities: {_viewModel.EntityCount}";
            _snapLagText.text = $"Snap lag: {_viewModel.ServerTickLag} tick";
            _snapIntervalText.text = $"Snap gap: {_viewModel.SnapIntervalAvgMs:F1} / {_viewModel.SnapIntervalMaxMs:F1} ms";
            _cushionText.text = $"Cushion: {_viewModel.CushionMs:F1} ms";
```

- [ ] **Step 7: 배선을 눈으로 확인**

컴파일러가 없으므로 grep으로 대조한다. 아래 셋이 **모두** 나와야 한다.

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/netcode-recon-diagnostics"
grep -rn "SnapshotArrivalStats" Assets/Scripts/ | sort
grep -n "fps-text\|entity-count-text\|snap-lag-text\|snap-interval-text\|cushion-text" Assets/UI/DebugHud/DebugHud.uxml Assets/Scripts/UI/DebugHud/DebugHudView.cs
grep -n "Cushion" Assets/Scripts/Netcode/RemoteInterpolationClock.cs
```

Expected:
- `SnapshotArrivalStats` — GameLifetimeScope 1곳 · GameEntityMessageHandler 3곳(필드·파라미터·대입) + 기록 1곳 · DebugHudViewModel 3곳 이상
- UXML의 라벨 이름 5개가 View의 `Q<Label>` 5개와 **철자까지 일치**
- `RemoteInterpolationClock.Cushion` 존재

- [ ] **Step 8: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/netcode-recon-diagnostics"
git add Assets/Scripts/Netcode/RemoteInterpolationClock.cs Assets/Scripts/Game/GameLifetimeScope.cs \
        Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs \
        Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs Assets/Scripts/UI/DebugHud/DebugHudView.cs \
        Assets/UI/DebugHud/DebugHud.uxml
git commit -m "feat(debug-hud): FPS·엔티티 수·스냅 도착·서버틱 지연 표시

원인 A(서버 틱 밀림)와 B(클라 프레임 저하)를 가르는 직접 지표. 도착 기록은
기존 틱당 1회 dedupe 블록 안에 넣어 청킹된 중복을 그대로 걸러 쓴다."
```

---

### Task 3: HUD 리셋 버튼

실험 조건을 바꿀 때 누적 통계를 0으로 되돌린다. 이게 없으면 부하 조건의 `Recon max`가 기준선 조건의 잔재일 수 있어 비교 자체가 성립하지 않는다.

**Files:**
- Modify: `Assets/Scripts/Netcode/ReconciliationStats.cs`
- Modify: `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs`
- Modify: `Assets/UI/DebugHud/DebugHud.uxml`
- Modify: `Assets/Scripts/UI/DebugHud/DebugHudView.cs`

**Interfaces:**
- Consumes: Task 2의 `DebugHudViewModel`(생성자에 `snapshotArrivalStats` 보유)
- Produces: `ReconciliationStats.Reset()` · `DebugHudViewModel.ResetStats()`

- [ ] **Step 1: `ReconciliationStats.Reset()` 추가**

`Assets/Scripts/Netcode/ReconciliationStats.cs`의 `Record` 메서드 아래에 추가:

```csharp
        /// <summary>실험 조건을 바꿀 때 부른다. Max는 누적이라 리셋하지 않으면 이전 조건 값이 남는다.</summary>
        public void Reset()
        {
            _window.Clear();
            _sum = 0;
            Last = 0;
            Max = 0;
            Average = 0;
        }
```

- [ ] **Step 2: ViewModel에 리셋 커맨드 추가**

`Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs` 클래스 끝에 추가:

```csharp
        public void ResetStats()
        {
            reconciliationStats.Reset();
            snapshotArrivalStats.Reset();
        }
```

- [ ] **Step 3: UXML에 버튼 추가**

`Assets/UI/DebugHud/DebugHud.uxml`의 `cushion-text` 라벨 아래에 추가:

```xml
            <ui:Button name="reset-button" class="debug-text" text="Reset stats" />
```

> 패널 루트가 `picking-mode="Ignore"`지만 그건 그 요소 자신만 클릭을 안 받는다는 뜻이고, 자식 버튼은 정상적으로 눌린다.

- [ ] **Step 4: View에 버튼 배선**

`Assets/Scripts/UI/DebugHud/DebugHudView.cs`:

(a) 필드 추가 — `_cushionText` 아래:

```csharp
        private Button _resetButton;
```

(b) `OnOpen`의 `_cushionText = Root.Q<Label>("cushion-text");` 아래:

```csharp
            _resetButton = Root.Q<Button>("reset-button");
            _resetButton.clicked += _viewModel.ResetStats;
```

(c) `Dispose`를 아래로 교체(구독 해제):

```csharp
        public override void Dispose()
        {
            if (_resetButton != null)
            {
                _resetButton.clicked -= _viewModel.ResetStats;
            }
            _tick?.Pause();
            base.Dispose();
        }
```

- [ ] **Step 5: 배선 확인**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/netcode-recon-diagnostics"
grep -rn "ResetStats\|reset-button" Assets/
grep -n "public void Reset" Assets/Scripts/Netcode/ReconciliationStats.cs
```

Expected: `reset-button`이 UXML과 View에 각 1회, `ResetStats`가 VM 정의 1 + View 구독/해제 2, `ReconciliationStats.Reset` 존재.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/Netcode/ReconciliationStats.cs Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs \
        Assets/Scripts/UI/DebugHud/DebugHudView.cs Assets/UI/DebugHud/DebugHud.uxml
git commit -m "feat(debug-hud): 통계 리셋 버튼

Recon max는 누적이라 리셋 없이는 조건 A의 최대값이 조건 B에 그대로 섞인다.
조건을 바꿔 가며 비교하는 실험에서 이건 비교를 무의미하게 만든다."
```

---

### Task 4: 서버 부하 조절 (`DebugEnemySpawner`)

지금은 적이 **10초마다 자동으로 늘기만 하고**(커밋된 코드는 10마리씩, 상한 100마리. 로컬 픽스처가 그걸 1마리로 낮춰 놓은 상태다) **줄일 방법이 없다.** 같은 세션 안에서 조건을 바꿀 수 있어야 원인을 엔티티에 귀속시킬 수 있다.

**Files:**
- Modify: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server\Assets\Scripts\Game\GameRuleSystem.cs:162`
- Create: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server\Assets\Scripts\Diagnostics\DebugEnemySpawner.cs`
- Modify: 서버 씬 `Assets/Scenes/LOPGame.unity` (DebugHud GameObject에 컴포넌트 추가)

**Interfaces:**
- Consumes: 없음 (서버 독립)
- Produces: `GameRuleSystem.SpawnEnemies(int count)` (private → public) · `GameRuleSystem.DespawnAllEnemies()`

- [ ] **Step 1: 브랜치 생성**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git checkout main
git checkout -b feature/recon-load-diagnostics
```

- [ ] **Step 2: `GameRuleSystem`에 공개 API 추가**

`Assets/Scripts/Game/GameRuleSystem.cs`의 `#region Spawn` 안에서 `SpawnEnemies` 접근자를 바꾸고, 그 아래에 디스폰을 추가한다:

```csharp
        // 진단(부하 실험)에서도 부른다 — 자동 스폰의 100마리 상한은 OnTick에만 있어 여기엔 안 걸린다.
        public void SpawnEnemies(int count)
        {
            for (int i = 0; i < count; i++)
            {
                SpawnEnemy(GetRandomSpawnPosition());
            }
        }

        /// <summary>진단용: 플레이어가 아닌 캐릭터를 전부 디스폰 큐에 넣는다.</summary>
        public void DespawnAllEnemies()
        {
            // Despawn은 큐에 넣기만 하고 실제 제거·클라 통보는 틱 끝의 FlushDespawns가 한다 →
            // 여기서 registry를 순회하며 불러도 순회 중 컬렉션이 바뀌지 않는다.
            foreach (var entity in entityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                if (entity.Has<GameFramework.World.Ownership>())
                {
                    continue;   // Ownership이 있으면 플레이어다
                }
                entitySpawner.Despawn(entity.Id);
            }
        }
```

- [ ] **Step 3: 디버그 컴포넌트 생성**

`C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server\Assets\Scripts\Diagnostics\DebugEnemySpawner.cs`:

```csharp
using GameFramework;   // SceneInjectMonoBehaviour
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 부하 실험용 적 스폰 조절. 인스펙터 우클릭 메뉴로 부르므로 Game 뷰에 포커스가 없어도 된다
    /// (에디터 두 개를 띄워 놓고 실험하면 포커스가 한쪽에만 있다).
    /// </summary>
    [SceneInjectMonoBehaviour]
    public class DebugEnemySpawner : MonoBehaviour
    {
        [SerializeField] private int spawnCount = 50;

        [Inject] private GameRuleSystem gameRuleSystem;

        [ContextMenu("Spawn Enemies")]
        private void SpawnEnemies()
        {
            gameRuleSystem.SpawnEnemies(spawnCount);
        }

        [ContextMenu("Despawn All Enemies")]
        private void DespawnAllEnemies()
        {
            gameRuleSystem.DespawnAllEnemies();
        }
    }
}
```

- [ ] **Step 4: 컴파일 확인**

```
mcp__UnityMCP__refresh_unity(unity_instance="LeagueOfPhysical-Server@<hash>")
mcp__UnityMCP__read_console(types=["error"], unity_instance="LeagueOfPhysical-Server@<hash>")
```

Expected: 에러 0. `SceneInjectMonoBehaviour`가 없다는 에러가 나면 `DebugHudHost.cs`가 쓰는 실제 속성명·네임스페이스를 확인해 맞춘다.

- [ ] **Step 5: 씬에 컴포넌트 붙이기**

서버 `Assets/Scenes/LOPGame.unity`에서 **DebugHud GameObject**(`DebugHudHost`가 붙어 있는 것)를 찾아 `DebugEnemySpawner`를 추가한다. 이 GameObject를 쓰는 이유는 게임 스코프 DI가 이미 여기까지 닿는 것이 확인됐기 때문이다.

```
mcp__UnityMCP__manage_scene(action="load", name="LOPGame", unity_instance="LeagueOfPhysical-Server@<hash>")
mcp__UnityMCP__find_gameobjects(query="DebugHud", unity_instance="LeagueOfPhysical-Server@<hash>")
mcp__UnityMCP__manage_components(action="add", target="<DebugHud GameObject>", component="DebugEnemySpawner",
                                 unity_instance="LeagueOfPhysical-Server@<hash>")
mcp__UnityMCP__manage_scene(action="save", unity_instance="LeagueOfPhysical-Server@<hash>")
```

- [ ] **Step 6: 실제로 동작하는지 확인**

서버·클라를 띄워 게임에 들어간 뒤, 서버 인스펙터에서 `DebugEnemySpawner` 우클릭 → **Spawn Enemies**.

Expected:
- 클라 화면에 적이 50마리 나타난다
- 클라 HUD의 `Entities`가 50 이상 늘어난다 (Task 2가 머지되기 전이면 서버 콘솔의 스폰 로그로 갈음)

이어서 **Despawn All Enemies**.

Expected: 적이 전부 사라지고, **내 캐릭터는 남는다**(Ownership 가드가 동작). 남아 있던 아이템도 그대로다(Character 가드).

- [ ] **Step 7: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git status --short
git add Assets/Scripts/Game/GameRuleSystem.cs Assets/Scripts/Diagnostics/ Assets/Scenes/LOPGame.unity
git commit -m "feat(diagnostics): 부하 실험용 적 스폰·디스폰

같은 세션 안에서 엔티티 수만 바꿔 비교할 수 있어야 원인을 엔티티에 귀속시킬
수 있다. 지금은 10초에 1마리씩만 늘고 줄일 방법이 없어 실험이 불가능했다."
```

> `Assets/Scripts/Diagnostics/` 폴더와 새 `.cs`의 `.meta`가 함께 잡혔는지 `git status`로 확인한다.
>
> ⚠️ **커밋 안 할 것:** 서버 로컬 픽스처(`GameRuleSystem`의 스폰 마릿수를 실험하며 손댄 값,
> `ConfigureRoomComponent` 에디터 부팅 설정)와 Unity 자동 노이즈(`PackageManagerSettings` 등)는
> 이 커밋에 섞지 않는다. `git status --short`로 하나씩 확인하고 필요한 것만 `add` 한다.

---

### Task 5: 서버 틱 건강 로그 (`TickHealthLogger`)

원인 A를 **확증**하는 한 줄. 클라의 서버틱 지연은 간접 증거(밀린 결과)이고, 이건 밀리는 현장을 직접 본다.

**Files:**
- Create: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server\Assets\Scripts\Diagnostics\TickHealthLogger.cs`
- Modify: 서버 씬 `Assets/Scenes/LOPGame.unity`

**Interfaces:**
- Consumes: `GameFramework.Runner.IRunner`(`tickUpdater`·`gameState`) · `GameFramework.World.EntityRegistry`
- Produces: 없음 (로그만)

> **핵심:** 밀림은 새로 계산할 필요가 없다. `ITickUpdater.processibleTick`(벽시계 `elapsedTime`을 `interval`로 나눈 **기대 틱**)과 `tick`(실제 진행한 틱)의 차가 곧 밀림이다. 서버의 `elapsedTime`은 `networkTime.ServerNow`라 부하와 무관하게 흐르고, `tick`은 프레임당 8틱 상한(`MaxTicksPerFrame`)에 걸리면 뒤처진다.

- [ ] **Step 1: 컴포넌트 생성**

`C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server\Assets\Scripts\Diagnostics\TickHealthLogger.cs`:

```csharp
using GameFramework;          // SceneInjectMonoBehaviour
using GameFramework.Runner;   // IRunner, RunnerState
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 서버가 자기 틱 속도를 지키고 있는지 주기적으로 남긴다. 기본 꺼짐 — 진단할 때만 켠다.
    /// 밀림 = 기대 틱(벽시계 기준) − 실제 틱. 이 값이 0에서 안 떨어지면 서버는 건강하다.
    /// </summary>
    [SceneInjectMonoBehaviour]
    public class TickHealthLogger : MonoBehaviour
    {
        [SerializeField] private bool logEnabled;
        [SerializeField] private float logIntervalSeconds = 2f;

        [Inject] private IRunner runner;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        private float nextLogTime;
        private float maxFrameMs;

        private void Update()
        {
            if (logEnabled == false || runner.tickUpdater == null || runner.gameState < RunnerState.Playing)
            {
                return;
            }

            // 프레임 최대치를 창 안에서 모은다 — 평균은 한 번씩 크게 튀는 프레임을 가려 버린다.
            float frameMs = Time.unscaledDeltaTime * 1000f;
            if (frameMs > maxFrameMs)
            {
                maxFrameMs = frameMs;
            }

            if (Time.unscaledTime < nextLogTime)
            {
                return;
            }
            nextLogTime = Time.unscaledTime + logIntervalSeconds;

            var tickUpdater = runner.tickUpdater;
            long expected = tickUpdater.processibleTick;
            Debug.Log($"[TickHealth] tick={tickUpdater.tick} expected={expected} lag={expected - tickUpdater.tick}" +
                      $" frameMaxMs={maxFrameMs:F1} budgetMs={tickUpdater.interval * 1000:F1}" +
                      $" entities={entityRegistry.Count}");
            maxFrameMs = 0f;
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

```
mcp__UnityMCP__refresh_unity(unity_instance="LeagueOfPhysical-Server@<hash>")
mcp__UnityMCP__read_console(types=["error"], unity_instance="LeagueOfPhysical-Server@<hash>")
```

Expected: 에러 0.

- [ ] **Step 3: 씬에 붙이고 켜기**

Task 4와 **같은 DebugHud GameObject**에 추가하고, 인스펙터에서 `Log Enabled`를 체크한다.

```
mcp__UnityMCP__manage_components(action="add", target="<DebugHud GameObject>", component="TickHealthLogger",
                                 unity_instance="LeagueOfPhysical-Server@<hash>")
mcp__UnityMCP__manage_scene(action="save", unity_instance="LeagueOfPhysical-Server@<hash>")
```

> 씬에 저장되는 기본값은 **꺼짐**으로 둔다. 실험할 때만 인스펙터에서 켠다 — 평소 서버 로그를 오염시키지 않기 위해서다. Step 3에서 켠 것은 아래 Step 4 확인용이고, 커밋 전에 다시 끄고 씬을 저장한다.

- [ ] **Step 4: 로그가 나오는지 확인**

게임에 들어가 서버 콘솔을 읽는다.

```
mcp__UnityMCP__read_console(types=["log"], filter="TickHealth", unity_instance="LeagueOfPhysical-Server@<hash>")
```

Expected: 2초마다 한 줄. 건강한 유휴 상태의 기준선은 **`lag=-1`이 평평하게 유지**되는 것이다.

> **`-1`이 정상인 이유(실측 + 소스 확인):** `tick`은 "다음에 처리할 틱"을 가리키고 `processibleTick`은
> "지금 처리 가능한 틱"이라 한 칸 차이가 구조적으로 남는다. 게다가 `Update()`는 코루틴이 `elapsedTime`을
> 갱신하기 *전에* 돌아 직전 스냅샷을 읽는다. 그래서 완전히 건강해도 `-1`이 나온다.
>
> **`frameMaxMs`가 `budgetMs`를 넘는 것은 그 자체로 이상이 아니다.** 캐치업이 프레임당 8틱까지
> 허용하므로 여유가 `8 × interval`(≈160ms)이다. 에디터 플레이 모드는 렌더링 때문에 유휴에도
> 예산을 넘는다(실측 37~40ms vs 예산 20ms). **볼 것은 `lag`이 `-1`에서 양수 쪽으로 자라는가**다.

- [ ] **Step 5: 끄고 커밋**

인스펙터에서 `Log Enabled` 체크 해제 → 씬 저장 → 커밋.

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git status --short
git add Assets/Scripts/Diagnostics/ Assets/Scenes/LOPGame.unity
git commit -m "feat(diagnostics): 서버 틱 밀림·프레임 주기 로그 (기본 꺼짐)

밀림은 이미 계산돼 있다 — processibleTick(벽시계 기준 기대 틱) − tick.
클라의 서버틱 지연이 간접 증거라면 이건 밀리는 현장을 직접 본다."
```

---

### Task 6: 통합 — 머지 + 컴파일 검증

클라 코드는 여기서 처음으로 컴파일된다. **이 태스크가 진짜 관문이다.**

**Files:** 없음 (머지·검증만)

**Interfaces:**
- Consumes: Task 1~5 전부
- Produces: 세 레포 `main`에 반영된 계측기

- [ ] **Step 1: GameFramework 머지**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git checkout main
git merge --no-ff feature/snapshot-arrival-stats -m "Merge feature/snapshot-arrival-stats: 스냅 도착 통계"
git log --oneline -1
```

- [ ] **Step 2: 서버 머지**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git checkout main
git merge --no-ff feature/recon-load-diagnostics -m "Merge feature/recon-load-diagnostics: 부하 실험 계측"
git log --oneline -1
```

- [ ] **Step 3: 클라 머지**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git checkout main
git merge --no-ff worktree-netcode-recon-diagnostics -m "Merge netcode-recon-diagnostics: recon 진단 계측"
git log --oneline -1
```

- [ ] **Step 4: 클라 컴파일 검증**

```
mcp__UnityMCP__refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>", force=true)
mcp__UnityMCP__read_console(types=["error"], unity_instance="LeagueOfPhysical-Client@<hash>")
```

Expected: **에러 0.** 여기서 나오는 흔한 실패와 대처:
- `DebugHudViewModel` 생성자 인자 불일치 → VContainer가 등록 안 된 타입을 못 찾는 경우다. `GameLifetimeScope`에 `SnapshotArrivalStats` 등록이 들어갔는지 확인
- `Root.Q<Button>("reset-button")`이 null → UXML 이름 철자 확인
- `Time`이 모호하다 → `DebugHudViewModel`의 `using UnityEngine;` 확인

- [ ] **Step 5: 서버 컴파일 재검증**

```
mcp__UnityMCP__refresh_unity(unity_instance="LeagueOfPhysical-Server@<hash>", force=true)
mcp__UnityMCP__read_console(types=["error"], unity_instance="LeagueOfPhysical-Server@<hash>")
```

Expected: 에러 0.

- [ ] **Step 6: GameFramework EditMode 전체 확인**

```
mcp__UnityMCP__run_tests(mode="EditMode", unity_instance="LeagueOfPhysical-Client@<hash>")
```

Expected: 전부 green.

- [ ] **Step 7: HUD가 실제로 값을 보여주는지 확인**

서버·클라를 띄워 게임에 들어간다. HUD에서 확인:

| 항목 | 유휴 상태에서 기대값 |
|---|---|
| `FPS` | 에디터 기준 상식적인 값(수십~수백) |
| `Entities` | 실제 스폰된 수와 일치 |
| `Snap lag` | 작은 상수 근처에서 안정 (편도지연이 상수로 깔림) |
| `Snap gap` | avg가 서버 송신 간격 근처, max가 그보다 조금 큼 |
| `Cushion` | 송신 간격의 2배 근처 |

`Reset stats`를 눌러 `Recon max`와 `Snap gap` max가 0으로 떨어지는지 확인한다. **안 떨어지면 실험을 진행하지 말 것** — 리셋이 실험의 전제다.

---

### Task 7: 실험 수행 + 판정 + 기록

**Files:**
- Modify: `docs/ROADMAP.md` (클라 레포)

**Interfaces:**
- Consumes: Task 6까지 전부
- Produces: 원인 판정 + 로드맵 기록

- [ ] **Step 1: 기준선 측정**

서버 인스펙터에서 `TickHealthLogger.Log Enabled` 체크. 게임 진입 후 **주변에 아무것도 없는 빈 곳**으로 이동해 자리를 기억해 둔다(이후 조건에서 같은 자리를 쓴다).

`Reset stats` → 제자리 점프 ×10 → 아래를 기록:

| 기록 항목 | 어디서 |
|---|---|
| FPS · Entities · Snap lag · Snap gap avg/max · Cushion · **Recon max**(+avg) · Prune · SeqGap | 클라 HUD 스크린샷 |
| tick · expected · lag · frameMaxMs · budgetMs · entities | 서버 `[TickHealth]` **연속 5줄 이상**(≈10초) |
| `[TickUpdater] catch-up capped` 경고 유무 | 서버 콘솔 |

> **스크린샷 타이밍이 중요하다** — `Recon avg`는 1.2초 이동평균이라 점프가 끝나고 한참 뒤에 찍으면
> 0으로 수렴한다. **점프 도중이나 마지막 착지 직후 1초 안에** 찍을 것. `Recon max`는 리셋 이후
> 누적이라 타이밍에 덜 민감하지만, 두 값을 같이 남기려면 이 타이밍이 필요하다.
>
> **서버 로그는 한 줄이 아니라 여러 줄로 읽는다** — `lag`은 프레임 위상에 따라 `-1`/`0`을 오가고
> `frameMaxMs`도 창마다 흔들린다. 한 줄만 옮기면 그 위상을 뽑는 셈이다. **`lag`의 최대값과 추세**로 볼 것.

> ⏱ **5분 제한을 의식할 것.** 서버가 경과 5분에 매치를 자동 종료한다. 3조건(기준선 + 부하 30초 안정
> + 되돌리기 30초 안정)이 3~5분 걸리므로, HUD의 `Elapsed`를 보며 진행하고 여유가 없으면 방을 새로
> 만들고 시작한다. **3단계(되돌리기)가 잘리면 실험 자체가 무효**다 — 귀속 판정이 거기 걸려 있다.

> ⚠️ **디스폰은 반드시 클라가 접속한 상태에서만 누른다.** 접속 세션이 0개일 때 디스폰하면 기존 버그로
> 서버 틱 코루틴이 통째로 죽는다(아래 "알려진 버그"). 실험을 끝낼 때는 디스폰을 먼저 하고 클라를 내린다.

- [ ] **Step 2: 부하 측정**

서버 인스펙터 `DebugEnemySpawner` 우클릭 → **Spawn Enemies**(50). 30초 안정 → `Reset stats` → **같은 자리에서 같은 점프 ×10** → 같은 항목 기록.

> 50마리로 어느 지표도 움직이지 않으면 `Spawn Count`를 100·200으로 올려 반복한다. 신호가 안 나오는 것과 부하가 부족한 것은 다르다.

- [ ] **Step 3: 되돌리기 측정**

**Despawn All Enemies** → 30초 안정 → `Reset stats` → 같은 점프 ×10 → 기록.

- [ ] **Step 4 (선택): 지연 주입 반복**

부하 조건에서 Mirror `LatencySimulation`으로 왕복 150ms를 주고 1~3단계를 반복한다.
갭이 증폭되는지 본다.

> **선택인 이유:** 로컬 2-에디터는 RTT가 와이어 지연이 아니라 프레임·스로틀 지연이라 값 자체가
> 현실적이지 않다. 주 실험의 변수는 **엔티티 수 하나**로 유지하고, 이건 보조 관찰로만 쓴다.
> 1~3단계에서 판정이 이미 났다면 건너뛴다.

- [ ] **Step 5: 판정**

세 조건의 값을 표로 정리하고 아래 판정표에 대입한다.

**결론 열은 `Recon max`(리셋 이후 누적)로 읽는다. `avg`는 보조다.**

> **⚠️ 왜 avg로 판정하면 안 되나 (최종 리뷰가 잡음).** `Recon avg`는 **최근 60샘플 ≈ 1.2초 이동평균**이다
> (`Reconciler`가 클라 틱당 1회 기록, 50Hz). 점프 10회는 15~25초가 걸리므로 **스크린샷 시점에 창에
> 남는 건 마지막 1.2초 — 대개 착지 후 정지 구간**이고, 세 조건 전부 `avg ≈ 0`이 나올 수 있다.
> 그러면 표가 "재현 안 됨" 행으로 떨어져 **실제로 러버밴딩이 있어도 파킹 항목을 닫아 버린다.**
> `Recon max`는 리셋 이후 누적이라 점프 구간을 붙잡는다.

| FPS | Snap lag / Snap gap · Cushion | **Recon max** | 판정 |
|---|---|---|---|
| 급락 | 정상 | 증가 | **B — 클라 프레임 저하** |
| 정상 | 증가·요동 | 증가 | **A — 서버 틱 밀림.** 확증 = 서버 콘솔의 **`[TickUpdater] catch-up capped` 경고**(아래) |
| 급락 | 증가·요동 | 증가 | **A+B 동시** — 서버부터 |
| 정상 | 정상 | 증가 | **제3의 원인** — `Prune`·`SeqGap`을 먼저 본다(`Snap gap`은 부하에서 둔감해진다, 아래) |
| 정상 | 정상 | 정상 | **재현 안 됨** |

**원인 A의 확증은 `lag > 0`이 아니라 `catch-up capped` 경고다.** `lag`은 서버 프레임이 느려지기만 해도
(상한에 안 걸리고 정상적으로 몰아 처리해도) 양수로 자란다 — "못 따라가 영구히 밀림"과 "뭉쳐서 처리함"은
대응 슬라이스가 다르다. 상한에 실제로 걸릴 때만 뜨는
`[TickUpdater] catch-up capped at 8 ticks/frame (behind by N)` 경고가 깨끗한 판별자다.

> **`Snap gap`은 부하에서 오히려 둔감해진다.** 스냅이 청크로 쪼개져 오는데 **한 틱의 청크 중 하나만
> 도착해도** 최신 tick이 전진한다. 부하로 청크 수가 늘수록 "틱을 통째로 놓칠" 확률은 낮아진다.
> 대역폭 의심은 `Prune`·`SeqGap`으로 판단할 것.
>
> **`Cushion`은 100.0ms에서 포화**한다(상한 = 송신간격 × 5). `100.0`이 찍히면 측정값이 아니라 천장이다.

**되돌리기 판정이 우선한다:** 3단계에서 값이 기준선으로 **안 돌아오면** 원인은 엔티티가 아니라 시간 경과·세션 누적이다. 이 경우 위 표의 결론을 쓰지 말고 그 사실을 기록한다.

> 서버 콘솔에 `[TickUpdater] catch-up capped at 8 ticks/frame (behind by N)` 경고가 뜬다면 그것만으로 **원인 A가 사실상 확정**이다. 이 경고는 기존 코드가 이미 남기고 있다.

> **⚠️ 실측으로 바로잡은 기준 두 가지 (T5에서 확인).**
> ① **서버 `lag`의 건강한 기준선은 `0`이 아니라 `-1`**이고, 그게 평평하게 유지되는 것이 정상이다.
> 보는 것은 절대값이 아니라 **`-1`에서 양수 쪽으로 자라는가**다.
> ② **`frameMaxMs`가 `budgetMs`를 넘는 것은 원인 A의 증거가 아니다.** 캐치업 여유가 `8 × interval`
> (≈160ms)이라 유휴 에디터에서도 예산(20ms)을 넘긴다(실측 37~40ms). 이걸 확증으로 쓰면 **기준선에서부터
> 원인 A로 오판**한다.

> **입력 타이밍 4개(`d avg`/`d max`/`Prune`/`SeqGap`)는 리셋 버튼이 지우지 못한다.** 클라가 누적하는
> 값이 아니라 **서버가 보낸 최신 요약을 덮어쓰는** 홀더라 값의 주인이 서버다. 조건별 **절대값이 아니라
> 조건 사이의 차이**로 읽을 것.

> 지표가 전부 정상인데 **육안으로는 튄다**면 그것도 결과다 — "제3의 원인"으로 분류하고,
> 다음 후보로 렌더 보정(`RenderCorrectionSmoother`)을 기록에 남긴다. 시뮬은 멀쩡한데 보이는
> 위치만 흔들리는 경우가 거기다.

- [ ] **Step 6: ROADMAP 기록**

`docs/ROADMAP.md`의 파킹 표에서 "**Recon 엔티티-로드 러버밴딩**" 행을 판정 결과로 갱신한다. 판정이 났으면 파킹에서 내리고 Done 원장에 결과를 적되, **대응은 다음 슬라이스로 남긴다**. "재현 안 됨"이면 그 사실과 근거(측정값)를 적고 항목을 닫는다.

기록에 반드시 포함할 것:
- 세 조건의 실측 수치 표
- 판정과 근거 한 문장
- 다음 슬라이스 후보(B면 AoI, A면 서버 틱 예산 최적화)

- [ ] **Step 7: 커밋 + 머지**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/.claude/worktrees/netcode-recon-diagnostics"
git add docs/ROADMAP.md
git commit -m "docs(roadmap): recon 엔티티-로드 원인 판정 결과"
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git checkout main
git merge --no-ff worktree-netcode-recon-diagnostics -m "Merge netcode-recon-diagnostics: 진단 결과 기록"
```

---

## 완료 조건

- [ ] GameFramework EditMode 전부 green (신규 8개 포함)
- [ ] 클·서 Unity 컴파일 에러 0
- [ ] HUD에 새 지표 5개가 뜨고, 리셋 버튼이 실제로 누적값을 0으로 만든다
- [ ] 서버 인스펙터에서 적을 늘리고 줄일 수 있다
- [ ] 세 조건(기준선·부하·되돌리기) 실측이 끝나고 **원인이 하나로 판정**됐다
- [ ] ROADMAP에 수치와 판정이 기록됐다
