# Stage④ 슬라이스 ② — SnapshotHistory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 내 캐릭터의 시뮬 상태(위치·회전·속도)를 매 틱 링버퍼에 기록하는 `SnapshotHistory` 인프라를 만들고, 호스트 루프가 그것을 채우게 한다. 되돌리기는 하지 않는다.

**Architecture:** 순수 컨테이너 `EntitySnapshot`+`SnapshotHistory`를 GameFramework(`GameFramework.Netcode`, System.Numerics)에 두어 클·서 공통·단위 테스트 가능하게 한다. 클라 `LOPRunner`가 매 틱 끝에 내 캐릭의 `World.Entity`를 읽어 `SnapshotHistory.Record`로 채운다(전용 Recorder 컴포넌트 없음 — DOTS `GhostPredictionHistorySystem`/우리 §6.5 스케치와 동일). `DebugHud`로 기록을 관측한다.

**Tech Stack:** Unity 6000.3 (PhysX, `SimulationMode.Script`), VContainer, System.Numerics, NUnit(EditMode), UI Toolkit, UnityMCP(컴파일·테스트 검증).

## Global Constraints

- **클라 + GameFramework 공통 컨테이너만.** 서버 프로젝트(`LeagueOfPhysical-Server`)·와이어·프로토콜 **무변경**.
- `EntitySnapshot`/`SnapshotHistory`는 **`GameFramework.Netcode` 네임스페이스, System.Numerics** 사용. **`GameFramework.World` 타입에 의존하지 않는다**(Runtime asmdef이 World asmdef을 참조 안 함). World 읽기는 클라 `LOPRunner`가 담당.
- **무변경 대상**: `SnapReconciler`, `WorldBase`/`IWorld`, 입력 처리, 서버 스냅 핸들러.
- **되돌리기/롤백/replay 없음** — 이 슬라이스는 기록까지만.
- **프로덕션 버퍼 용량 = 128** (DI 등록 시). 테스트는 작은 용량으로 eviction 검증.
- **UnityMCP**: 모든 호출에 `unity_instance="LeagueOfPhysical-Client@<hash>"` 명시. id는 `mcpforunity://instances`에서 `LeagueOfPhysical-Client` 이름으로 조회. 서버 인스턴스 조작 금지.
- **커밋**: 피처 브랜치 `feature/stage4-snapshot-history`(spec 이미 커밋됨). main 직접 커밋 금지. Unity 생성 `.meta`를 `.cs`와 함께 커밋.
- **네임스페이스 풀 한정**: 클라 파일에서 World 타입은 `GameFramework.World.Entity` 등 풀 네임스페이스로(`using GameFramework.World;` 금지 — `UnityEngine.Component`와 충돌). Netcode 타입도 `GameFramework.Netcode.EntitySnapshot`으로 풀 한정.

---

### Task 1: `EntitySnapshot` + `SnapshotHistory` (GameFramework, TDD)

순수 데이터 + 링버퍼 저장소. 이 태스크가 이 슬라이스의 실제 테스트 사이클을 가진다.

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Netcode/EntitySnapshot.cs`
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Netcode/SnapshotHistory.cs`
- Test: `C:/Users/re5na/workspace/LOP/GameFramework/Tests/Runtime/Netcode/SnapshotHistoryTests.cs`

**Interfaces:**
- Consumes: `System.Numerics.Vector3`, `System.Numerics.Quaternion` (BCL).
- Produces (Task 2·3이 사용):
  - `struct GameFramework.Netcode.EntitySnapshot { long Tick; Vector3 Position; Quaternion Rotation; Vector3 Velocity }`, 생성자 `EntitySnapshot(long tick, Vector3 position, Quaternion rotation, Vector3 velocity)`.
  - `class GameFramework.Netcode.SnapshotHistory`: `SnapshotHistory(int capacity)`, `void Record(EntitySnapshot snapshot)`, `bool TryGet(long tick, out EntitySnapshot snapshot)`, `EntitySnapshot? Latest { get; }`, `int Count { get; }`.

- [ ] **Step 1: 실패하는 테스트 작성**

Create `C:/Users/re5na/workspace/LOP/GameFramework/Tests/Runtime/Netcode/SnapshotHistoryTests.cs`:

```csharp
using System.Numerics;
using GameFramework.Netcode;
using NUnit.Framework;

namespace GameFramework.Tests.Netcode
{
    public class SnapshotHistoryTests
    {
        private static EntitySnapshot Snap(long tick) =>
            new EntitySnapshot(tick, new Vector3(tick, 0, 0), Quaternion.Identity, Vector3.Zero);

        [Test]
        public void Record_ThenTryGet_ReturnsSnapshot()
        {
            var history = new SnapshotHistory(4);
            history.Record(Snap(10));

            Assert.IsTrue(history.TryGet(10, out var got));
            Assert.AreEqual(10, got.Tick);
            Assert.AreEqual(new Vector3(10, 0, 0), got.Position);
        }

        [Test]
        public void TryGet_UnknownTick_ReturnsFalse()
        {
            var history = new SnapshotHistory(4);
            history.Record(Snap(10));

            Assert.IsFalse(history.TryGet(9, out _));
        }

        [Test]
        public void Empty_HasNoLatest_AndZeroCount()
        {
            var history = new SnapshotHistory(4);

            Assert.IsNull(history.Latest);
            Assert.AreEqual(0, history.Count);
            Assert.IsFalse(history.TryGet(0, out _));
        }

        [Test]
        public void Exceeding_Capacity_EvictsOldestTick()
        {
            var history = new SnapshotHistory(4);
            for (long t = 0; t <= 4; t++)   // 5 records into capacity 4
            {
                history.Record(Snap(t));
            }

            Assert.IsFalse(history.TryGet(0, out _), "가장 오래된 tick 0은 밀려나야 한다");
            Assert.IsTrue(history.TryGet(1, out _));
            Assert.IsTrue(history.TryGet(4, out var newest));
            Assert.AreEqual(4, newest.Tick);
        }

        [Test]
        public void Latest_ReturnsMostRecentlyRecorded()
        {
            var history = new SnapshotHistory(4);
            history.Record(Snap(7));
            history.Record(Snap(8));

            Assert.IsNotNull(history.Latest);
            Assert.AreEqual(8, history.Latest.Value.Tick);
        }

        [Test]
        public void Count_CapsAtCapacity()
        {
            var history = new SnapshotHistory(4);
            for (long t = 0; t < 10; t++)
            {
                history.Record(Snap(t));
            }

            Assert.AreEqual(4, history.Count);
        }

        [Test]
        public void TryGet_UnrecordedTickThatMapsToDefaultSlot_ReturnsFalse()
        {
            // 링 슬롯의 초기 sentinel이 실제 tick 0과 충돌하지 않는지 검증(디폴트 Tick=0 함정).
            var history = new SnapshotHistory(4);
            history.Record(Snap(3));   // latest=3, 윈도우가 tick 0을 포함

            Assert.IsFalse(history.TryGet(0, out _), "기록 안 된 tick 0은 sentinel과 구분돼 false여야 한다");
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패(컴파일 에러)하는지 확인**

클라 인스턴스 id 조회 후:
1. `mcpforunity://instances` 읽어 `LeagueOfPhysical-Client@<hash>` 확보.
2. `refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true, unity_instance=<client>)`
3. `read_console(action="get", types=["error"], unity_instance=<client>)`

Expected: `GameFramework.Netcode.EntitySnapshot`/`SnapshotHistory` 미정의로 컴파일 에러(테스트가 참조하는 타입 없음).

- [ ] **Step 3: `EntitySnapshot` 구현**

Create `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Netcode/EntitySnapshot.cs`:

```csharp
using System.Numerics;

namespace GameFramework.Netcode
{
    /// <summary>
    /// 한 틱의 엔티티 시뮬 상태 사진(위치·회전·속도). 엔진 비의존(System.Numerics) 순수 데이터.
    /// 클라 롤백 예측과 서버 lag-compensation이 공유한다.
    /// </summary>
    public readonly struct EntitySnapshot
    {
        public long Tick { get; }
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Velocity { get; }

        public EntitySnapshot(long tick, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            Tick = tick;
            Position = position;
            Rotation = rotation;
            Velocity = velocity;
        }
    }
}
```

- [ ] **Step 4: `SnapshotHistory` 구현**

Create `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Netcode/SnapshotHistory.cs`:

```csharp
using System;

namespace GameFramework.Netcode
{
    /// <summary>
    /// 틱을 키로 최근 N틱의 엔티티 상태를 보관하는 링버퍼. 클라 롤백 예측과 서버
    /// lag-compensation이 공유하는 순수 저장소 — "무엇을 언제 기록/복원할지"(정책)는 사이드가 소유한다.
    /// 슬롯 = tick % capacity. 같은 슬롯의 오래된 틱은 새 틱으로 덮여 자동 eviction된다.
    /// </summary>
    public class SnapshotHistory
    {
        // 초기 슬롯이 실제 tick(특히 0)과 충돌하지 않도록 하는 sentinel.
        private const long EmptyTick = long.MinValue;

        private readonly EntitySnapshot[] _ring;
        private readonly int _capacity;
        private long _latestTick;
        private bool _hasAny;

        public SnapshotHistory(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _capacity = capacity;
            _ring = new EntitySnapshot[capacity];
            for (int i = 0; i < capacity; i++)
            {
                _ring[i] = new EntitySnapshot(EmptyTick, default, default, default);
            }
        }

        /// <summary>보관 중인 스냅샷 수(용량에서 포화).</summary>
        public int Count { get; private set; }

        /// <summary>가장 최근에 기록된 스냅샷. 비어 있으면 null.</summary>
        public EntitySnapshot? Latest => _hasAny ? _ring[Slot(_latestTick)] : (EntitySnapshot?)null;

        /// <summary>스냅샷을 기록한다. 같은 슬롯의 오래된 틱은 덮어써진다.</summary>
        public void Record(EntitySnapshot snapshot)
        {
            _ring[Slot(snapshot.Tick)] = snapshot;
            if (!_hasAny || snapshot.Tick > _latestTick)
            {
                _latestTick = snapshot.Tick;
            }
            _hasAny = true;
            if (Count < _capacity)
            {
                Count++;
            }
        }

        /// <summary>틱으로 스냅샷을 조회한다. 최근 capacity틱 윈도우 밖이거나 미기록이면 false.</summary>
        public bool TryGet(long tick, out EntitySnapshot snapshot)
        {
            if (_hasAny && tick <= _latestTick && tick > _latestTick - _capacity)
            {
                var candidate = _ring[Slot(tick)];
                if (candidate.Tick == tick)
                {
                    snapshot = candidate;
                    return true;
                }
            }

            snapshot = default;
            return false;
        }

        private int Slot(long tick) => (int)(((tick % _capacity) + _capacity) % _capacity);
    }
}
```

- [ ] **Step 5: 테스트 통과 확인**

1. `refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true, unity_instance=<client>)`
2. `read_console(action="get", types=["error"], unity_instance=<client>)` → 에러 0.
3. `run_tests(mode="EditMode", test_filter="SnapshotHistoryTests", unity_instance=<client>)`

Expected: `SnapshotHistoryTests`의 7개 테스트 전부 PASS.

- [ ] **Step 6: 커밋 (GameFramework 레포)**

GameFramework은 별도 git 저장소다. `.meta`는 Unity가 생성했는지 확인 후 함께 커밋.

```bash
G="C:/Users/re5na/workspace/LOP/GameFramework"
git -C "$G" checkout -b feature/stage4-snapshot-history
git -C "$G" add Runtime/Scripts/Netcode/ Tests/Runtime/Netcode/
git -C "$G" commit -m "$(cat <<'EOF'
feat(netcode): EntitySnapshot + SnapshotHistory ring buffer

틱-키 링버퍼로 최근 N틱 엔티티 상태 보관(순수, System.Numerics). 클라 롤백 예측 +
서버 lag-comp 공유 저장소. EditMode 테스트 7종. 기록/복원 정책은 사이드 소유.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

> `.meta` 누락 시: `refresh_unity`로 Unity가 `Netcode/` 폴더와 `.cs`들의 `.meta`를 생성하게 한 뒤 `git add`에 포함.

---

### Task 2: 호스트 기록 배선 (`LOPRunner` + DI)

`LOPRunner`가 매 틱 끝에 내 캐릭 스냅샷을 `SnapshotHistory`에 기록.

**Files:**
- Modify: `Assets/Scripts/Game/LOPRunner.cs` (인젝션 추가 + `EndUpdate` + `RecordLocalSnapshot`)
- Modify: `Assets/Scripts/Game/GameLifetimeScope.cs` (`SnapshotHistory` 등록)

**Interfaces:**
- Consumes: `GameFramework.Netcode.SnapshotHistory`(Task 1), `IPlayerContext.entity`(`LOPEntity`), `GameFramework.World.EntityRegistry.Get(string)`, `GameFramework.World.Entity.Get<T>()`, `GameFramework.World.Transform.{Position,Rotation}`, `GameFramework.World.Velocity.Linear`, `Runner.Time.tick`.
- Produces: 런타임 계약 — "매 틱, 내 캐릭이 존재하면 그 시뮬 상태가 `SnapshotHistory`에 tick 키로 기록된다"(Task 3·슬라이스 ③이 의존).

- [ ] **Step 1: `SnapshotHistory`를 DI에 등록**

`Assets/Scripts/Game/GameLifetimeScope.cs`에서 `builder.Register<LeadState>(Lifetime.Singleton);` (현재 line 85) **다음 줄**에 추가:

```csharp
            builder.Register(_ => new GameFramework.Netcode.SnapshotHistory(128), Lifetime.Singleton);
```

- [ ] **Step 2: `LOPRunner`에 인젝션 추가**

`Assets/Scripts/Game/LOPRunner.cs`에서 `[Inject] private IMapLoader mapLoader;` (현재 line 21) **다음 줄**에 추가:

```csharp
        [Inject] private IPlayerContext playerContext;
        [Inject] private GameFramework.Netcode.SnapshotHistory snapshotHistory;
```

- [ ] **Step 3: `EndUpdate`에서 기록 + `RecordLocalSnapshot` 추가**

같은 파일의 `EndUpdate`(현재 line 174-179)를 아래로 교체:

교체 전:
```csharp
        private void EndUpdate()
        {
            DispatchEvent<End>();

            entityManager.DestroyMarkedEntities();
        }
```

교체 후:
```csharp
        private void EndUpdate()
        {
            RecordLocalSnapshot();

            DispatchEvent<End>();

            entityManager.DestroyMarkedEntities();
        }

        // 내 캐릭의 이번 틱 최종 시뮬 상태를 스냅샷에 남긴다. End 디스패치(=SnapReconciler 스무딩) 전에
        // 찍어 스무딩이 얹히기 전 원본 예측 상태를 포착한다. 되돌리기는 슬라이스 ③.
        private void RecordLocalSnapshot()
        {
            LOPEntity local = playerContext.entity;
            if (local == null)
            {
                return;
            }

            GameFramework.World.Entity worldEntity = entityRegistry.Get(local.entityId);
            if (worldEntity == null)
            {
                return;
            }

            var transform = worldEntity.Get<GameFramework.World.Transform>();
            var velocity = worldEntity.Get<GameFramework.World.Velocity>();
            if (transform == null || velocity == null)
            {
                return;
            }

            snapshotHistory.Record(new GameFramework.Netcode.EntitySnapshot(
                Runner.Time.tick,
                transform.Position,
                transform.Rotation,
                velocity.Linear));
        }
```

- [ ] **Step 4: 클라 컴파일 검증**

1. `refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true, unity_instance=<client>)`
2. `read_console(action="get", types=["error"], unity_instance=<client>)`

Expected: 에러 0건. (`IPlayerContext`·`SnapshotHistory` 미주입/미참조 에러 없음.)

- [ ] **Step 5: 커밋 (클라 레포)**

```bash
C="C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git -C "$C" add Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Game/GameLifetimeScope.cs
git -C "$C" commit -m "$(cat <<'EOF'
feat(stage4): host records local player snapshot each tick

LOPRunner가 EndUpdate에서 내 캐릭 World.Transform/Velocity를 SnapshotHistory에 기록
(End 디스패치=스무딩 전). SnapshotHistory는 게임 스코프 Singleton(용량 128). 되돌리기는 ③.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: DebugHud 관측 (VM + View + UXML)

기록이 실제로 쌓이는지 플레이 중 눈으로 확인할 지표 추가.

**Files:**
- Modify: `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs` (getter 2개)
- Modify: `Assets/Scripts/UI/DebugHud/DebugHudView.cs` (라벨 2개 바인딩)
- Modify: `Assets/UI/DebugHud/DebugHud.uxml` (라벨 2개)

**Interfaces:**
- Consumes: `GameFramework.Netcode.SnapshotHistory.{Count, Latest}`(Task 1).
- Produces: (없음 — 표시 전용.)

- [ ] **Step 1: ViewModel에 지표 추가**

`Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs`에서 `[Inject] private InputTimingStats inputTimingStats;` (현재 line 16-17) **다음**에 인젝션 추가:

```csharp
        [Inject]
        private GameFramework.Netcode.SnapshotHistory snapshotHistory;
```

그리고 `public int TimingSeqGap => inputTimingStats.SeqGapCount;` (현재 line 44) **다음 줄**에 getter 추가:

```csharp

        public int SnapshotCount => snapshotHistory.Count;

        public long SnapshotLatestTick => snapshotHistory.Latest?.Tick ?? -1;
```

- [ ] **Step 2: UXML에 라벨 추가**

`Assets/UI/DebugHud/DebugHud.uxml`에서 `timing-seqgap-text` 라벨(현재 line 16) **다음 줄**에 추가:

```xml
            <ui:Label name="snapshot-count-text" class="debug-text" text="Snap count: 0" />
            <ui:Label name="snapshot-tick-text" class="debug-text" text="Snap tick: -1" />
```

- [ ] **Step 3: View에 라벨 바인딩 추가**

`Assets/Scripts/UI/DebugHud/DebugHudView.cs` 세 곳 수정:

(1) 필드 선언 — `private Label _timingSeqGapText;` (현재 line 25) 다음:
```csharp
        private Label _snapshotCountText;
        private Label _snapshotTickText;
```

(2) `OnOpen`의 Q 조회 — `_timingSeqGapText = Root.Q<Label>("timing-seqgap-text");` (현재 line 51) 다음:
```csharp
            _snapshotCountText = Root.Q<Label>("snapshot-count-text");
            _snapshotTickText = Root.Q<Label>("snapshot-tick-text");
```

(3) `Refresh`의 갱신 — `_timingSeqGapText.text = $"SeqGap: {_viewModel.TimingSeqGap}";` (현재 line 74) 다음:
```csharp
            _snapshotCountText.text = $"Snap count: {_viewModel.SnapshotCount}";
            _snapshotTickText.text = $"Snap tick: {_viewModel.SnapshotLatestTick}";
```

- [ ] **Step 4: 클라 컴파일 검증**

1. `refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true, unity_instance=<client>)`
2. `read_console(action="get", types=["error"], unity_instance=<client>)`

Expected: 에러 0건.

- [ ] **Step 5: 플레이 검증 (사용자 구동)**

클라·서버 에디터로 룸 접속 후 관찰(에이전트는 플레이 불가 → 사용자에게 체크리스트 제시):

1. **DebugHud에 "Snap count"가 0→128까지 오른 뒤 128에서 유지**되는가.
2. **"Snap tick"이 "Client tick"을 (거의) 따라가는가**(매 틱 기록 확인).
3. **이동/점프/대시/정지·방향전환, reconciliation(러버밴딩 없음) 무회귀** — 소비자가 없으므로 게임 동작은 이전과 동일해야 함.
4. **콘솔 신규 에러 0**.

Expected: 1~4 모두 정상.

- [ ] **Step 6: 커밋 (클라 레포)**

```bash
C="C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git -C "$C" add Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs Assets/Scripts/UI/DebugHud/DebugHudView.cs Assets/UI/DebugHud/DebugHud.uxml
git -C "$C" commit -m "$(cat <<'EOF'
feat(stage4): DebugHud shows snapshot count + latest tick

기록이 매 틱 쌓이는지 관측하는 지표. count는 128에서 포화, tick은 client tick 추종.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Self-Review

**1. Spec coverage:**
- `EntitySnapshot` + `SnapshotHistory` (GameFramework.Netcode, numerics, 128, 테스트) → Task 1. ✅
- 호스트 직접 기록(`LOPRunner`, End 전, `IPlayerContext`→`World.Entity`) → Task 2. ✅
- DI Singleton 등록(`ReconciliationStats` 옆) → Task 2 Step 1. ✅
- DebugHud count/tick 표시 → Task 3. ✅
- EditMode 단위 테스트(Record/TryGet/eviction/Latest/Count) → Task 1 Step 1. ✅
- 무변경(SnapReconciler/WorldBase/입력/서버/되돌리기) → 편집 대상에 없음, 자동 충족. ✅

**2. Placeholder scan:** "TBD/TODO/적절히" 없음. 모든 코드 스텝이 완전한 블록. ✅

**3. Type consistency:** `SnapshotHistory(int)`·`Record(EntitySnapshot)`·`TryGet(long,out EntitySnapshot)`·`Latest`(`EntitySnapshot?`)·`Count`(int) — Task 1 정의와 Task 2·3 사용처가 일치. `EntitySnapshot(long,Vector3,Quaternion,Vector3)` 생성자 인자 순서가 Task 1과 Task 2 호출부에서 동일(tick, position, rotation, velocity). `Transform.Position`/`Rotation`·`Velocity.Linear`·`EntityRegistry.Get(string)`·`Entity.Get<T>()`·`LOPEntity.entityId`·`Runner.Time.tick` 전부 기존 시그니처. ✅

## Execution Handoff

(작성 완료 후 사용자에게 실행 방식 제시 — Task 1은 GameFramework, Task 2·3은 클라라 태스크 경계가 명확. Subagent-Driven 권장.)
