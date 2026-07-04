# Stage④ 슬라이스 ③ — 롤백/재조정 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버 스냅 도착 시 내 캐릭을 그 틱으로 하드 복원하고 저장된 입력을 현재까지 이동·물리 재생해 예측 오차를 보정한다(기존 `SnapReconciler` delta-replay 대체).

**Architecture:** 재생용 `InputHistory`(LOP-Shared 링)와 순수 판정 `ReconcileGate`(GameFramework.Netcode)를 깔고, 클라 호스트 서비스 `Reconciler`가 `LOPRunner` 틱 앞에서 [errorGate → 하드 복원 → MovementSystem+물리 재생 → 스냅 재기록]을 수행한다. 기존 `SnapReconciler`는 지연 렌더링 전용 `LocalEntityInterpolator`로 축소·개명한다.

**Tech Stack:** Unity 6000.3 (PhysX `SimulationMode.Script`, `autoSyncTransforms=false`), VContainer, System.Numerics, NUnit(EditMode), UnityMCP(컴파일 검증).

## Global Constraints

- **클라 + LOP-Shared + GameFramework 변경.** 서버 프로젝트(`LeagueOfPhysical-Server`)·와이어·프로토콜 **무변경**.
- **재생 범위 = 이동+물리만**: 재생 루프는 `MovementSystem.Tick`만 부른다(어빌리티/상태이상 이중 진행 방지 — `world.Tick` 금지). 어빌리티/상태이상 재생은 후속(B).
- **sim = 하드 복원+재생, 뷰 = 기존 지연 렌더링 유지.** sim SmoothDamp 제거. 전용 보정-스무싱은 후속.
- **errorGate**: 예측 vs 서버 위치 차이 ≤ threshold(초기 **0.06**)면 롤백 스킵.
- **원격 = 현재 포즈 정적 장애물 유지**(slice① kinematic). 원격 틱별 정합 안 함.
- 링 용량 = **128**. 재생 틱 상한 = **128**(초과 시 텔레포트 폴백).
- **UnityMCP**: 모든 호출에 `unity_instance="LeagueOfPhysical-Client@<hash>"` (client 인스턴스, `mcpforunity://instances`에서 조회). 서버 인스턴스 조작 금지.
- **커밋**: 클라 피처 브랜치 `feature/stage4-rollback-reconcile`(spec 이미 커밋). LOP-Shared/GameFramework은 각 레포에서 동명 브랜치. main 직접 금지. Unity `.meta`를 `.cs`와 함께 커밋.
- **네임스페이스 풀 한정**: 클라 파일에서 World/Netcode 타입은 `GameFramework.World.*`/`GameFramework.Netcode.*`로 풀 한정(`using GameFramework.World;` 금지 — `UnityEngine.Component` 충돌).

---

### Task 1: `InputHistory` 링 (LOP-Shared, TDD)

재생용 "틱→적용 입력" 저장소. `InputBuffer.Commands`는 소비 시 제거되어 재생 재료가 없으므로 별도 보관.

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/InputHistory.cs`
- Test: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/InputHistoryTests.cs`

**Interfaces:**
- Consumes: `LOP.InputCommand`(기존, LOP-Shared).
- Produces: `class LOP.InputHistory { InputHistory(int capacity); void Record(long tick, InputCommand command); bool TryGet(long tick, out InputCommand command); }`

- [ ] **Step 1: 실패 테스트 작성**

Create `.../Tests/EditMode/InputHistoryTests.cs`:

```csharp
using NUnit.Framework;

namespace LOP.Tests
{
    public class InputHistoryTests
    {
        private static InputCommand Cmd(long seq) => new InputCommand { SequenceNumber = seq };

        [Test]
        public void Record_ThenTryGet_ReturnsSameCommand()
        {
            var history = new InputHistory(4);
            var c = Cmd(10);
            history.Record(10, c);

            Assert.IsTrue(history.TryGet(10, out var got));
            Assert.AreSame(c, got);
        }

        [Test]
        public void TryGet_UnknownTick_ReturnsFalse()
        {
            var history = new InputHistory(4);
            history.Record(10, Cmd(10));
            Assert.IsFalse(history.TryGet(9, out _));
        }

        [Test]
        public void Empty_TryGet_ReturnsFalse()
        {
            var history = new InputHistory(4);
            Assert.IsFalse(history.TryGet(0, out _));
        }

        [Test]
        public void Exceeding_Capacity_EvictsOldestTick()
        {
            var history = new InputHistory(4);
            for (long t = 0; t <= 4; t++) history.Record(t, Cmd(t));

            Assert.IsFalse(history.TryGet(0, out _), "가장 오래된 tick 0은 밀려나야 한다");
            Assert.IsTrue(history.TryGet(1, out _));
            Assert.IsTrue(history.TryGet(4, out _));
        }

        [Test]
        public void UnrecordedTickThatMapsToDefaultSlot_ReturnsFalse()
        {
            var history = new InputHistory(4);
            history.Record(3, Cmd(3));   // latest=3, 윈도우가 tick 0 포함
            Assert.IsFalse(history.TryGet(0, out _), "기록 안 된 tick 0은 sentinel과 구분돼 false");
        }
    }
}
```

- [ ] **Step 2: 실패 확인 (컴파일 에러 = `InputHistory` 미정의)**

`mcpforunity://instances`에서 client id 확보 →
`refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true, unity_instance=<client>)` →
`read_console(action="get", types=["error"], unity_instance=<client>)` → `LOP.InputHistory` 미정의 에러.

- [ ] **Step 3: `InputHistory` 구현**

Create `.../Runtime/Scripts/Game/InputHistory.cs`:

```csharp
using System;

namespace LOP
{
    /// <summary>
    /// 틱 → 그 틱에 적용된 입력 커맨드. 클라 롤백 재생(replay)용 입력 로그.
    /// 슬롯 = tick % capacity. 같은 슬롯의 오래된 틱은 덮여 자동 eviction. (slice② SnapshotHistory 링과 동형 —
    /// InputCommand는 참조형이라 tick 판별용 병렬 배열 + sentinel을 둔다.)
    /// </summary>
    public class InputHistory
    {
        private const long EmptyTick = long.MinValue;

        private readonly long[] _ticks;
        private readonly InputCommand[] _commands;
        private readonly int _capacity;
        private long _latestTick;
        private bool _hasAny;

        public InputHistory(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _capacity = capacity;
            _ticks = new long[capacity];
            _commands = new InputCommand[capacity];
            for (int i = 0; i < capacity; i++)
            {
                _ticks[i] = EmptyTick;
            }
        }

        /// <summary>틱에 적용된 입력을 기록한다(무입력 틱은 0-커맨드를 넣는다).</summary>
        public void Record(long tick, InputCommand command)
        {
            int slot = Slot(tick);
            _ticks[slot] = tick;
            _commands[slot] = command;
            if (!_hasAny || tick > _latestTick)
            {
                _latestTick = tick;
            }
            _hasAny = true;
        }

        /// <summary>틱의 입력을 조회한다. 최근 capacity틱 윈도우 밖이거나 미기록이면 false.</summary>
        public bool TryGet(long tick, out InputCommand command)
        {
            if (_hasAny && tick <= _latestTick && tick > _latestTick - _capacity)
            {
                int slot = Slot(tick);
                if (_ticks[slot] == tick)
                {
                    command = _commands[slot];
                    return true;
                }
            }

            command = null;
            return false;
        }

        private int Slot(long tick) => (int)(((tick % _capacity) + _capacity) % _capacity);
    }
}
```

- [ ] **Step 4: 통과 확인**

`refresh_unity(... unity_instance=<client>)` → `read_console(types=["error"])` 0 →
`run_tests(mode="EditMode", test_filter="InputHistoryTests", unity_instance=<client>)` → 5개 PASS.

- [ ] **Step 5: 커밋 (LOP-Shared 레포)**

```bash
S="C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git -C "$S" checkout -b feature/stage4-rollback-reconcile
git -C "$S" add Runtime/Scripts/Game/InputHistory.cs Tests/EditMode/InputHistoryTests.cs
git -C "$S" commit -m "$(cat <<'EOF'
feat(netcode): InputHistory ring for rollback replay

틱→적용 입력 링버퍼(재생 재료). InputBuffer.Commands는 소비 시 제거되므로 별도 보관. EditMode 테스트 5종.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

> `.meta` 누락 시 `refresh_unity` 후 `git add`에 포함.

---

### Task 2: `ReconcileGate` 순수 판정 (GameFramework.Netcode, TDD)

예측 위치 vs 서버 위치 차이가 임계값 초과인지 판정하는 순수 함수(errorGate).

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Netcode/ReconcileGate.cs`
- Test: `C:/Users/re5na/workspace/LOP/GameFramework/Tests/Runtime/Netcode/ReconcileGateTests.cs`

**Interfaces:**
- Produces: `static class GameFramework.Netcode.ReconcileGate { static bool ShouldReconcile(System.Numerics.Vector3 predicted, System.Numerics.Vector3 authoritative, float threshold); }`

- [ ] **Step 1: 실패 테스트 작성**

Create `.../Tests/Runtime/Netcode/ReconcileGateTests.cs`:

```csharp
using System.Numerics;
using GameFramework.Netcode;
using NUnit.Framework;

namespace GameFramework.Tests.Netcode
{
    public class ReconcileGateTests
    {
        [Test]
        public void WithinThreshold_DoesNotReconcile()
        {
            var a = new Vector3(0, 0, 0);
            var b = new Vector3(0.05f, 0, 0);
            Assert.IsFalse(ReconcileGate.ShouldReconcile(a, b, 0.06f));
        }

        [Test]
        public void BeyondThreshold_Reconciles()
        {
            var a = new Vector3(0, 0, 0);
            var b = new Vector3(0.2f, 0, 0);
            Assert.IsTrue(ReconcileGate.ShouldReconcile(a, b, 0.06f));
        }

        [Test]
        public void ExactlyAtThreshold_DoesNotReconcile()
        {
            var a = new Vector3(0, 0, 0);
            var b = new Vector3(0.06f, 0, 0);
            Assert.IsFalse(ReconcileGate.ShouldReconcile(a, b, 0.06f));
        }
    }
}
```

- [ ] **Step 2: 실패 확인** — `refresh_unity`/`read_console`로 `ReconcileGate` 미정의 컴파일 에러.

- [ ] **Step 3: `ReconcileGate` 구현**

Create `.../Runtime/Scripts/Netcode/ReconcileGate.cs`:

```csharp
using System.Numerics;

namespace GameFramework.Netcode
{
    /// <summary>예측 위치와 서버 권위 위치의 차이가 임계값을 넘으면 롤백해야 하는지 판정한다(순수).</summary>
    public static class ReconcileGate
    {
        public static bool ShouldReconcile(Vector3 predicted, Vector3 authoritative, float threshold)
        {
            return Vector3.Distance(predicted, authoritative) > threshold;
        }
    }
}
```

- [ ] **Step 4: 통과 확인** — `run_tests(mode="EditMode", test_filter="ReconcileGateTests", unity_instance=<client>)` → 3개 PASS.

- [ ] **Step 5: 커밋 (GameFramework 레포)**

```bash
G="C:/Users/re5na/workspace/LOP/GameFramework"
git -C "$G" checkout -b feature/stage4-rollback-reconcile
git -C "$G" add Runtime/Scripts/Netcode/ReconcileGate.cs Tests/Runtime/Netcode/ReconcileGateTests.cs
git -C "$G" commit -m "$(cat <<'EOF'
feat(netcode): ReconcileGate — rollback decision by position error threshold

예측 vs 서버 위치 차이 > threshold일 때만 롤백(순수 판정, DOTS error detection). EditMode 테스트 3종.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: 입력 히스토리 기록 + DI (`PlayerInputManager`)

매 틱 적용된 입력을 `InputHistory`에 기록하고, 사라질 `SnapReconciler` 입력-시퀀스 결합을 끊는다.

**Files:**
- Modify: `Assets/Scripts/Game/PlayerInputManager.cs`
- Modify: `Assets/Scripts/Game/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `LOP.InputHistory`(Task 1).
- Produces: 런타임 계약 — "매 틱 `InputHistory`에 그 틱 적용 입력(무입력=0-커맨드)이 기록된다"(Task 4 재생이 의존).

- [ ] **Step 1: `InputHistory`를 DI 등록**

`GameLifetimeScope.cs`에서 `builder.Register(_ => new GameFramework.Netcode.SnapshotHistory(128), Lifetime.Singleton);`(slice②로 추가된 줄) **다음**에 추가:

```csharp
            builder.Register(_ => new InputHistory(128), Lifetime.Singleton);
```

- [ ] **Step 2: `PlayerInputManager`에 주입 + 기록**

생성자에 `InputHistory` 추가. 현재 생성자/필드:

```csharp
        private InputBufferSystem inputBufferSystem;

        public PlayerInputManager(IRunner runner, IPlayerContext playerContext, AbilityActivator abilityActivator,
            GameFramework.World.EntityRegistry entityRegistry, InputBufferSystem inputBufferSystem)
        {
            this.runner = runner;
            this.playerContext = playerContext;
            this.abilityActivator = abilityActivator;
            this.entityRegistry = entityRegistry;
            this.inputBufferSystem = inputBufferSystem;

            this.runner.AddListener(this);
        }
```

를 아래로 교체(필드 + 파라미터 + 대입 추가):

```csharp
        private InputBufferSystem inputBufferSystem;
        private InputHistory inputHistory;

        public PlayerInputManager(IRunner runner, IPlayerContext playerContext, AbilityActivator abilityActivator,
            GameFramework.World.EntityRegistry entityRegistry, InputBufferSystem inputBufferSystem,
            InputHistory inputHistory)
        {
            this.runner = runner;
            this.playerContext = playerContext;
            this.abilityActivator = abilityActivator;
            this.entityRegistry = entityRegistry;
            this.inputBufferSystem = inputBufferSystem;
            this.inputHistory = inputHistory;

            this.runner.AddListener(this);
        }
```

- [ ] **Step 3: `ProcessInput`에서 기록 + `AddLocalInputSequence` 제거**

`ProcessInput`의 `if (captured != null)` 분기에서, 아래 블록을 제거:

```csharp
                playerContext.entity.GetComponent<SnapReconciler>().AddLocalInputSequence(new InputSequence
                {
                    Tick = tick,
                    Sequence = captured.SequenceNumber,
                });

                captured = null;
```

를 아래로 교체(입력 히스토리 기록으로 대체):

```csharp
                inputHistory.Record(tick, captured);

                captured = null;
```

그리고 `else`(무입력) 분기의 `inputBufferSystem.SetCurrent(buffer, new InputCommand());` 를 아래로 교체(0-커맨드를 히스토리에도 기록 — 재생 시 수평 제동을 재현):

```csharp
                var noInput = new InputCommand();
                inputBufferSystem.SetCurrent(buffer, noInput);
                inputHistory.Record(tick, noInput);
```

- [ ] **Step 4: 컴파일 검증** — `refresh_unity(... unity_instance=<client>)` → `read_console(types=["error"])` 0건.

- [ ] **Step 5: 커밋 (클라)**

```bash
C="C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git -C "$C" add Assets/Scripts/Game/PlayerInputManager.cs Assets/Scripts/Game/GameLifetimeScope.cs
git -C "$C" commit -m "$(cat <<'EOF'
feat(stage4): record applied input per tick into InputHistory

PlayerInputManager가 매 틱 적용 입력(무입력=0-커맨드)을 InputHistory에 기록. 재생 재료 확보.
SnapReconciler AddLocalInputSequence 결합 제거(앵커는 tick 기반 복원으로 대체).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: `Reconciler` 서비스 (하드 복원 + 이동·물리 재생)

롤백 핵심. 이 태스크에서는 서비스만 만들고 DI 등록까지 — **아직 `LOPRunner`가 호출하지 않는다**(dead code, 컴파일만). 배선은 Task 5.

**Files:**
- Create: `Assets/Scripts/Entity/Reconciler.cs`
- Modify: `Assets/Scripts/Game/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `LOP.InputHistory`(T1), `GameFramework.Netcode.ReconcileGate`(T2), `GameFramework.Netcode.SnapshotHistory`(slice②), `IPlayerContext`, `GameFramework.World.EntityRegistry`, `MovementSystem`, `GameFramework.IPhysicsSimulator`, `LOP.EntitySnap`, `LOPEntity.{position,rotation,velocity,PushMotionToPhysics,SyncPhysics}`.
- Produces: `class LOP.Reconciler { void AddServerSnap(EntitySnap snap); void Reconcile(long currentTick, float deltaTime); }`

- [ ] **Step 1: `Reconciler` 구현**

Create `Assets/Scripts/Entity/Reconciler.cs`:

```csharp
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 내 캐릭 롤백 재조정(호스트 서비스). 서버 스냅이 도착하면 그 틱으로 하드 복원하고,
    /// 저장된 입력으로 이동·물리를 현재 직전 틱까지 재생해 예측 오차를 보정한다.
    /// 어빌리티/상태이상은 재생하지 않는다(이동만) — 확장은 후속 슬라이스.
    /// </summary>
    public class Reconciler
    {
        private const float Threshold = 0.06f;     // 이 이하 오차는 롤백 스킵(예측 정확)
        private const long MaxReplayTicks = 128;   // 격차가 이보다 크면 텔레포트 폴백(재생 생략)

        [Inject] private IPlayerContext playerContext;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private GameFramework.Netcode.SnapshotHistory snapshotHistory;
        [Inject] private InputHistory inputHistory;
        [Inject] private MovementSystem movementSystem;
        [Inject] private GameFramework.IPhysicsSimulator physicsSimulator;

        private EntitySnap latestSnap;
        private bool hasPending;

        /// <summary>서버 스냅 수신(내 캐릭). 가장 최신 틱만 남긴다.</summary>
        public void AddServerSnap(EntitySnap snap)
        {
            if (!hasPending || snap.tick > latestSnap.tick)
            {
                latestSnap = snap;
                hasPending = true;
            }
        }

        /// <summary>틱 앞에서 호출. 대기 스냅이 있고 예측이 어긋났으면 복원+재생.</summary>
        public void Reconcile(long currentTick, float deltaTime)
        {
            if (!hasPending)
            {
                return;
            }
            hasPending = false;

            EntitySnap snap = latestSnap;
            long anchorTick = snap.tick;

            LOPEntity entity = playerContext.entity;
            if (entity == null)
            {
                return;
            }
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entity.entityId);
            if (worldEntity == null)
            {
                return;
            }

            // errorGate: 예측이 서버와 충분히 가까우면 아무것도 안 함.
            if (snapshotHistory.TryGet(anchorTick, out var predicted) &&
                !GameFramework.Netcode.ReconcileGate.ShouldReconcile(
                    predicted.Position, snap.position.ToNumerics(), Threshold))
            {
                return;
            }

            // 하드 복원: 내 캐릭을 서버 스냅(anchorTick) 상태로. reactive 경로가 rigidbody에 반영되므로
            // PhysX가 새 포즈를 보도록 수동 SyncTransforms(autoSyncTransforms=false).
            entity.position = snap.position;
            entity.rotation = snap.rotation;
            entity.velocity = snap.velocity;
            Physics.SyncTransforms();

            // 격차가 과도하면 재생 생략(텔레포트) — 입력/스냅 히스토리 밖이라 재생 불가.
            if (currentTick - anchorTick > MaxReplayTicks)
            {
                return;
            }

            // 재생: 이미 예측했던 과거 틱(anchor+1 ~ currentTick-1)을 이동+물리로 재구성.
            var buffer = worldEntity.Get<InputBuffer>();
            for (long t = anchorTick + 1; t < currentTick; t++)
            {
                buffer.Current = inputHistory.TryGet(t, out var cmd) ? cmd : null;

                movementSystem.Tick(worldEntity, deltaTime);
                entity.PushMotionToPhysics();
                physicsSimulator.Simulate(deltaTime);
                entity.SyncPhysics();

                // 보정값으로 스냅 히스토리 갱신(다음 비교가 stale값을 안 보도록).
                var transform = worldEntity.Get<GameFramework.World.Transform>();
                var velocity = worldEntity.Get<GameFramework.World.Velocity>();
                snapshotHistory.Record(new GameFramework.Netcode.EntitySnapshot(
                    t, transform.Position, transform.Rotation, velocity.Linear));
            }
        }
    }
}
```

- [ ] **Step 2: `Reconciler`를 DI 등록**

`GameLifetimeScope.cs`에서 Task 3에서 추가한 `builder.Register(_ => new InputHistory(128), Lifetime.Singleton);` **다음**에 추가:

```csharp
            builder.Register<Reconciler>(Lifetime.Singleton);
```

- [ ] **Step 3: 컴파일 검증** — `refresh_unity(... unity_instance=<client>)` → `read_console(types=["error"])` 0건. (아직 아무도 `Reconciler`를 호출/주입하지 않아 동작 변화 없음.)

- [ ] **Step 4: 커밋 (클라)**

```bash
C="C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git -C "$C" add Assets/Scripts/Entity/Reconciler.cs Assets/Scripts/Game/GameLifetimeScope.cs
git -C "$C" commit -m "$(cat <<'EOF'
feat(stage4): Reconciler service — hard restore + movement/physics replay

서버 스냅 도착 시 anchor 틱으로 하드 복원 후 InputHistory로 이동·물리 재생(errorGate로 어긋날 때만,
과도 격차는 텔레포트 폴백). 아직 미배선 — 호출은 다음 커밋.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: 컷오버 — 배선 + `SnapReconciler` 축소·개명 (플레이 검증)

`Reconciler`를 루프에 꽂고, 스냅 수신을 재연결하고, 낡은 `SnapReconciler`를 지연 렌더링 전용으로 축소·개명한다. **동작이 바뀌는 원자적 전환** — 함께 착지.

**Files:**
- Modify: `Assets/Scripts/Game/LOPRunner.cs`
- Modify: `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs`
- Rename+rewrite: `Assets/Scripts/Entity/SnapReconciler.cs` → `Assets/Scripts/Entity/LocalEntityInterpolator.cs`
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs`

**Interfaces:**
- Consumes: `Reconciler.{AddServerSnap, Reconcile}`(T4).
- Produces: (동작 계약 — 스냅 도착 시 하드 복원+재생, 뷰는 지연 렌더링.)

- [ ] **Step 1: `LOPRunner`에 `Reconciler` 주입 + 틱 앞 호출**

`LOPRunner.cs`에서 slice②로 추가된 `[Inject] private GameFramework.Netcode.SnapshotHistory snapshotHistory;` **다음**에 추가:

```csharp
        [Inject] private Reconciler reconciler;
```

그리고 `UpdateRunner()`의 `ProcessNetworkMessage();` **다음 줄**(=`ProcessInput();` 앞)에 삽입:

```csharp
            reconciler.Reconcile(Runner.Time.tick, (float)tickUpdater.interval);
```

- [ ] **Step 2: 스냅 수신을 `Reconciler`로 재연결**

`Game.Entity.MessageHandler.cs`에 주입 추가 — 기존 `[Inject] private GameFramework.World.StatsSystem statsSystem;` **다음**:

```csharp
        [Inject] private Reconciler reconciler;
```

`OnEntitySnapsToC`의 내 캐릭 분기(현재):

```csharp
                if (playerContext.entity.entityId == entity.entityId)
                {
                    entity.GetComponent<SnapReconciler>().AddServerEntitySnap(entitySnap);
                }
```

를 아래로 교체:

```csharp
                if (playerContext.entity.entityId == entity.entityId)
                {
                    reconciler.AddServerSnap(entitySnap);
                }
```

- [ ] **Step 3: `SnapReconciler` → `LocalEntityInterpolator` 개명·축소**

기존 `Assets/Scripts/Entity/SnapReconciler.cs`를 삭제하고 `Assets/Scripts/Entity/LocalEntityInterpolator.cs`를 아래 내용으로 생성(재조정·앵커·SmoothDamp 전부 제거, 지연 렌더링만 잔존):

```csharp
using GameFramework;
using LOP.Event.LOPRunner.Update;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 내 캐릭터의 지연 렌더링. 진짜 위치(sim)는 <see cref="Reconciler"/>가 하드 보정하고,
    /// 이 컴포넌트는 보이는 메시(visualGameObject)만 저장된 틱 스냅 사이를 보간해 부드럽게 그린다
    /// (틱/프레임 주기차 흡수). 게임 로직·물리는 건드리지 않는다.
    /// </summary>
    public class LocalEntityInterpolator : MonoBehaviour, ICleanup
    {
        [Inject] private IRunner runner;

        public LOPEntity entity { get; set; }
        public LOPEntityView entityView { get; set; }

        private BoundedDictionary<long, EntityTransform> entityTransformSnaps;

        private void Awake()
        {
            entityTransformSnaps = new BoundedDictionary<long, EntityTransform>(20);
        }

        private void Start()
        {
            runner.AddListener(this);
        }

        public void Cleanup()
        {
            runner.RemoveListener(this);
        }

        [RunnerListen(typeof(End))]
        private void OnEnd()
        {
            entityTransformSnaps[Runner.Time.tick] = new EntityTransform
            {
                position = entity.position,
                rotation = entity.rotation,
                velocity = entity.velocity,
            };
        }

        private void LateUpdate()
        {
            if (entityView.visualGameObject == null || entityTransformSnaps.Count < 2)
            {
                return;
            }

            double tickInterval = Runner.Time.tickInterval;
            double renderTime = Runner.Time.elapsedTime - tickInterval;

            long tickPrev = (long)(renderTime / tickInterval);
            long tickNext = tickPrev + 1;
            float t = (float)((renderTime % tickInterval) / tickInterval);

            // 역산 틱이 버퍼에 없으면 이번 프레임 보간 스킵(직전 위치 유지).
            if (entityTransformSnaps.TryGetValue(tickPrev, out var prev) == false ||
                entityTransformSnaps.TryGetValue(tickNext, out var next) == false)
            {
                return;
            }

            entityView.visualGameObject.transform.position = Vector3.Lerp(prev.position, next.position, t);
            entityView.visualGameObject.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(prev.rotation), Quaternion.Euler(next.rotation), t);
        }
    }
}
```

- [ ] **Step 4: `CharacterCreator` 참조 교체**

`CharacterCreator.cs`의 내 캐릭 분기(현재):

```csharp
                SnapReconciler snapReconciler = entity.gameObject.AddComponent<SnapReconciler>();
                objectResolver.Inject(snapReconciler);
                snapReconciler.entity = entity;
                snapReconciler.entityView = view;
```

를 아래로 교체:

```csharp
                LocalEntityInterpolator interpolator = entity.gameObject.AddComponent<LocalEntityInterpolator>();
                objectResolver.Inject(interpolator);
                interpolator.entity = entity;
                interpolator.entityView = view;
```

- [ ] **Step 5: 컴파일 검증**

`refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true, unity_instance=<client>)` → `read_console(action="get", types=["error"], unity_instance=<client>)`.

Expected: 에러 0건. 특히 `SnapReconciler`/`AddServerEntitySnap`/`AddLocalInputSequence`/`InputSequence` 잔존 참조 에러가 없어야 함(모두 제거·재연결됨). 만약 `InputSequence` 미사용 타입이 어디선가 참조되면 그 참조도 정리.

- [ ] **Step 6: 플레이 검증 (사용자 구동)**

클라·서버 두 에디터로 룸 접속 후 관찰(에이전트 플레이 불가 → 체크리스트 제시):

1. **지상 이동/정지/방향전환** 정상, 러버밴딩 없음.
2. **공중 낙하 중 점프** — RTT 50/150/300ms(Latency Simulation) × 손실 20%에서 러버밴딩 육안 소멸/수용. `ReconciliationStats` HUD의 distance가 delta-replay 때 대비 안정.
3. **대시** — 발동/이동 정상(재생 구간 대시 오차는 알려진 한계 — 크게 튀지 않으면 OK).
4. **원격 플레이어** 이동/충돌(장애물) 정상.
5. **콘솔 신규 에러 0**, `Snap count`/`Snap tick`(slice② HUD) 정상.

Expected: 1~5 정상(3은 알려진 한계 내). 문제 시 어느 항목인지로 국소화.

- [ ] **Step 7: 커밋 (클라)**

```bash
C="C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git -C "$C" add Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs Assets/Scripts/Entity/SnapReconciler.cs Assets/Scripts/Entity/SnapReconciler.cs.meta Assets/Scripts/Entity/LocalEntityInterpolator.cs Assets/Scripts/Entity/LocalEntityInterpolator.cs.meta Assets/Scripts/EntityCreator/CharacterCreator.cs
git -C "$C" commit -m "$(cat <<'EOF'
feat(stage4): cut over to Reconciler; SnapReconciler -> LocalEntityInterpolator

LOPRunner 틱 앞에서 Reconciler.Reconcile 호출, 내 캐릭 스냅 수신을 Reconciler로 재연결.
낡은 SnapReconciler(delta-replay)를 지연 렌더링 전용 LocalEntityInterpolator로 축소·개명.
sim=하드 복원+재생 / 뷰=지연 렌더링.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

> `git rm` 대신 위처럼 삭제된 `SnapReconciler.cs`(+`.meta`)와 신규 `LocalEntityInterpolator.cs`(+`.meta`)를 함께 `git add`하면 rename으로 커밋된다. `.meta`는 Unity가 갱신하므로 `refresh_unity` 후 스테이징.

---

## Self-Review

**1. Spec coverage:**
- 틱 앞 한 패스 복원+재생 → Task 4(Reconcile) + Task 5(배선). ✅
- `InputHistory`(LOP-Shared) → Task 1. ✅
- errorGate 순수+테스트 → Task 2. ✅
- sim 하드 / 뷰 지연렌더 유지 → Task 4(하드 복원) + Task 5(`LocalEntityInterpolator`). ✅
- 이동만 재생(`MovementSystem.Tick`, not `world.Tick`) → Task 4 재생 루프. ✅
- 텔레포트 폴백(MaxReplayTicks) → Task 4. ✅
- 스냅 재기록 → Task 4 재생 루프. ✅
- `SnapReconciler` 재조정 제거·지연렌더 유지 → Task 5. ✅
- 스냅 수신 재연결 → Task 5 Step 2. ✅
- 입력-시퀀스 결합 제거 → Task 3 Step 3. ✅
- 무변경(서버/WorldBase/MovementSystem/어빌리티) → 편집 대상에 없음. ✅

**2. Placeholder scan:** "TBD/TODO" 없음. 모든 코드 스텝이 완전 블록. ✅

**3. Type consistency:** `InputHistory(int)`·`Record(long,InputCommand)`·`TryGet(long,out InputCommand)`(T1↔T3,T4 일치). `ReconcileGate.ShouldReconcile(Vector3,Vector3,float)` numerics(T2↔T4, `snap.position.ToNumerics()`/`predicted.Position` 둘 다 numerics). `Reconciler.{AddServerSnap(EntitySnap), Reconcile(long,float)}`(T4↔T5 일치). `MovementSystem.Tick(Entity,float)`·`LOPEntity.{PushMotionToPhysics,SyncPhysics,position,rotation,velocity}`·`Transform.Position/Rotation`·`Velocity.Linear`·`EntitySnap.{tick,position,rotation,velocity}`·`Physics.Simulate/SyncTransforms` 전부 기존 시그니처. ✅

## Execution Handoff

(작성 완료 후 사용자에게 실행 방식 제시. Task 1=LOP-Shared, Task 2=GameFramework, Task 3~5=클라 — 태스크 경계 명확, Subagent-Driven 권장. Task 5는 플레이 검증 필요 = 사용자 구동.)
