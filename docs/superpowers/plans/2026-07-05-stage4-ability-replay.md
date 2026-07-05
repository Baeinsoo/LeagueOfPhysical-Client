# Stage④ 어빌리티/상태이상 예측 재생 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 롤백 재조정 재생 구간에서 이동뿐 아니라 어빌리티/상태이상까지 재현하도록, 풀 예측 상태(어빌리티/상태이상/스탯/마나) 스냅샷을 앵커 틱으로 복원하고 재생 루프를 예측 틱 전체로 확장한다.

**Architecture:** LOP-Shared에 어빌리티/상태이상/스탯/마나 상태의 틱별 스냅샷(`PredictedAbilityState`)과 링(`PredictedAbilityStateHistory`, 기존 `InputHistory` 링과 동형)을 추가한다. 위치/속도용 `EntitySnapshot`/`SnapshotHistory`(GameFramework, slice②)는 손대지 않고 병렬로 둔다 — 매 틱 함께 기록·같은 앵커 틱에서 함께 복원하므로 lockstep. `Reconciler`(LOP-Client)가 앵커에서 두 상태를 복원한 뒤 재생 루프를 발동→이동→어빌리티페이즈→상태이상→효과구동→물리 순서(라이브와 동일)로 돌린다.

**Tech Stack:** Unity, C#, VContainer(DI), NUnit(EditMode 테스트), 순수 C# World Core(GameFramework.World / LOP-Shared).

## Global Constraints

- **브랜치 (main 직접 커밋 금지):** LOP-Client 작업은 워크트리 `C:/Users/re5na/workspace/LOP/wt-stage4-ability-replay`(브랜치 `feature/stage4-ability-replay`)에서. LOP-Shared는 자기 레포 `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared`에서 동명 브랜치 `feature/stage4-ability-replay`를 만들어 진행(아래 Preflight).
- **레포 경계:** `PredictedAbilityState`/`PredictedAbilityStateHistory` + 그 EditMode 테스트는 **LOP-Shared 레포**. `Reconciler`/`LOPRunner`/`GameLifetimeScope` 수정은 **LOP-Client 워크트리**. 각 레포에서 각자 커밋.
- **.meta 커밋:** 새 `.cs`마다 Unity가 생성한 `.meta`를 함께 커밋. `.meta`는 직접 만들지 말고 Unity Editor가 생성한 것만 add.
- **네임스페이스 풀 한정(LOP-Client 파일):** `GameFramework.World.Entity` 등 World 타입은 `using GameFramework.World;` 없이 항상 풀 네임스페이스로. (`Component` 모호성 회피 — 기존 `Reconciler`/`LOPRunner` 컨벤션 유지.)
- **Anemic:** 컴포넌트에 로직을 두지 않는다. 상태 쓰기는 시스템 또는 복원 헬퍼(`PredictedAbilityState.RestoreTo`)가 수행.
- **테스트 배치:** 순수 C#(LOP-Shared)만 EditMode 단위 테스트. LOP-Client 코드는 전부 `Assembly-CSharp`라 asmdef 참조 불가 → **클라 통합은 컴파일 + 플레이 검증**(EditMode 테스트 없음).
- **테스트 실행:** UnityMCP `run_tests`(EditMode), 반드시 클라 인스턴스 지정(`unity_instance="LeagueOfPhysical-Client@<hash>"`, `mcpforunity://instances`에서 해석). LOP-Shared 테스트는 클라 에디터에 패키지로 포함되어 함께 실행됨.
- **커밋 메시지 trailer:** 각 커밋 끝에 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

### Preflight (코드 변경 전 1회)

- [ ] LOP-Shared에 브랜치 생성:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" && git checkout -b feature/stage4-ability-replay
```
- [ ] LOP-Client 워크트리(`feature/stage4-ability-replay`)는 이미 생성됨. 이후 클라 편집·커밋은 `C:/Users/re5na/workspace/LOP/wt-stage4-ability-replay`에서 수행.

---

### Task 1: `PredictedAbilityState` — 캡처 + 복원 (LOP-Shared)

내 캐릭 어빌리티/상태이상/스탯/마나 한 틱 상태를 깊은 복사로 캡처하고, 앵커 복원 시 컴포넌트에 되쓰는 데이터+헬퍼.

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/PredictedAbilityState.cs`
- Test: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/PredictedAbilityStateTests.cs`

**Interfaces:**
- Consumes: `GameFramework.World.Entity`, `Abilities`(`ActiveAbility?`, `Slots`), `StatusEffects`(`Effects`), `GameFramework.World.Stats`(`BaseStats`, `Modifiers`, `UnspentPoints`), `GameFramework.World.Mana`(`Current`, `Max`), `AbilitySlot`, `ActiveEffect`, `GameFramework.World.StatModifier`.
- Produces:
  - `static PredictedAbilityState PredictedAbilityState.Capture(GameFramework.World.Entity entity)`
  - `void PredictedAbilityState.RestoreTo(GameFramework.World.Entity entity)`
  - 읽기 프로퍼티: `ActiveAbility? ActiveAbility`, `Dictionary<int,AbilitySlot> Slots`, `List<ActiveEffect> StatusEffects`, `Dictionary<int,float> BaseStats`, `List<StatModifier> Modifiers`, `int UnspentPoints`, `int ManaCurrent`, `int ManaMax`.

- [ ] **Step 1: 실패 테스트 작성** — 깊은 복사 격리 + 복원.

`Tests/EditMode/PredictedAbilityStateTests.cs`:
```csharp
using System.Collections.Generic;
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class PredictedAbilityStateTests
    {
        private static Entity MakeEntity()
        {
            var e = new Entity("caster");
            e.Add(new Abilities());
            e.Add(new Mana(100));
            var stats = new Stats();
            stats.BaseStats[(int)EntityStatType.Dexterity] = 10f;
            e.Add(stats);
            e.Add(new StatusEffects());
            return e;
        }

        [Test]
        public void Capture_IsDeepCopy_LiveMutationDoesNotLeak()
        {
            var e = MakeEntity();
            e.Get<Abilities>().Slots[7] = new AbilitySlot(7, 42);
            e.Get<StatusEffects>().Effects.Add(new ActiveEffect(3, 50, 1, "src", "se:3"));

            var snap = PredictedAbilityState.Capture(e);

            // 캡처 후 라이브를 바꿔도 스냅은 그대로여야 한다.
            e.Get<Abilities>().Slots[7] = new AbilitySlot(7, 999);
            e.Get<StatusEffects>().Effects.Clear();
            e.Get<Mana>().Current = 0;

            Assert.AreEqual(42, snap.Slots[7].CooldownEndTick);
            Assert.AreEqual(1, snap.StatusEffects.Count);
            Assert.AreEqual(3, snap.StatusEffects[0].EffectId);
            Assert.AreEqual(100, snap.ManaCurrent);
        }

        [Test]
        public void RestoreTo_OverwritesLiveState()
        {
            var e = MakeEntity();
            e.Get<Mana>().Current = 80;
            e.Get<StatusEffects>().Effects.Add(new ActiveEffect(3, 50, 1, "src", "se:3"));
            var snap = PredictedAbilityState.Capture(e);

            // 라이브를 다르게 바꾼 뒤 복원하면 스냅 시점으로 돌아와야 한다.
            e.Get<Mana>().Current = 10;
            e.Get<StatusEffects>().Effects.Clear();
            e.Get<Abilities>().ActiveAbility = new ActiveAbility(1, AbilityPhase.Active, 0, 5, 7, e, new AbilityEffect[0]);

            snap.RestoreTo(e);

            Assert.AreEqual(80, e.Get<Mana>().Current);
            Assert.AreEqual(1, e.Get<StatusEffects>().Effects.Count);
            Assert.IsNull(e.Get<Abilities>().ActiveAbility);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run: UnityMCP `run_tests(mode="EditMode", filter="PredictedAbilityStateTests", unity_instance="LeagueOfPhysical-Client@<hash>")`
Expected: 컴파일 에러(`PredictedAbilityState` 미정의) 또는 FAIL.

- [ ] **Step 3: 최소 구현 작성**

`Runtime/Scripts/Game/PredictedAbilityState.cs`:
```csharp
using System.Collections.Generic;
using GameFramework.World;

namespace LOP
{
    /// <summary>
    /// 내 캐릭 예측 재생용 어빌리티/상태이상/스탯/마나 상태의 한 틱 사진(깊은 복사).
    /// 위치/속도는 별도(GameFramework.Netcode.EntitySnapshot). 롤백 재조정이 앵커 틱으로 복원 후 재생에 쓴다.
    /// </summary>
    public sealed class PredictedAbilityState
    {
        public ActiveAbility? ActiveAbility { get; private set; }
        public Dictionary<int, AbilitySlot> Slots { get; private set; }
        public List<ActiveEffect> StatusEffects { get; private set; }
        public Dictionary<int, float> BaseStats { get; private set; }
        public List<StatModifier> Modifiers { get; private set; }
        public int UnspentPoints { get; private set; }
        public int ManaCurrent { get; private set; }
        public int ManaMax { get; private set; }

        public static PredictedAbilityState Capture(Entity entity)
        {
            var s = new PredictedAbilityState();
            var abilities = entity.Get<Abilities>();
            s.ActiveAbility = abilities?.ActiveAbility;
            s.Slots = abilities != null
                ? new Dictionary<int, AbilitySlot>(abilities.Slots)
                : new Dictionary<int, AbilitySlot>();

            var status = entity.Get<StatusEffects>();
            s.StatusEffects = status != null
                ? new List<ActiveEffect>(status.Effects)
                : new List<ActiveEffect>();

            var stats = entity.Get<Stats>();
            s.BaseStats = stats != null
                ? new Dictionary<int, float>(stats.BaseStats)
                : new Dictionary<int, float>();
            s.Modifiers = stats != null
                ? new List<StatModifier>(stats.Modifiers)
                : new List<StatModifier>();
            s.UnspentPoints = stats?.UnspentPoints ?? 0;

            var mana = entity.Get<Mana>();
            s.ManaCurrent = mana?.Current ?? 0;
            s.ManaMax = mana?.Max ?? 0;
            return s;
        }

        public void RestoreTo(Entity entity)
        {
            var abilities = entity.Get<Abilities>();
            if (abilities != null)
            {
                abilities.ActiveAbility = ActiveAbility;
                abilities.Slots.Clear();
                foreach (var kv in Slots)
                {
                    abilities.Slots[kv.Key] = kv.Value;
                }
            }

            var status = entity.Get<StatusEffects>();
            if (status != null)
            {
                status.Effects.Clear();
                status.Effects.AddRange(StatusEffects);
            }

            var stats = entity.Get<Stats>();
            if (stats != null)
            {
                stats.BaseStats.Clear();
                foreach (var kv in BaseStats)
                {
                    stats.BaseStats[kv.Key] = kv.Value;
                }
                stats.Modifiers.Clear();
                stats.Modifiers.AddRange(Modifiers);
                stats.UnspentPoints = UnspentPoints;
            }

            var mana = entity.Get<Mana>();
            if (mana != null)
            {
                mana.Current = ManaCurrent;
                mana.Max = ManaMax;
            }
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: UnityMCP `run_tests(mode="EditMode", filter="PredictedAbilityStateTests", ...)`
Expected: PASS (2 tests). 먼저 `read_console`로 컴파일 에러 없음 확인.

- [ ] **Step 5: 커밋** (LOP-Shared 레포)

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" && \
git add Runtime/Scripts/Game/PredictedAbilityState.cs Runtime/Scripts/Game/PredictedAbilityState.cs.meta \
        Tests/EditMode/PredictedAbilityStateTests.cs Tests/EditMode/PredictedAbilityStateTests.cs.meta && \
git commit -m "feat(stage4): PredictedAbilityState — capture/restore ability/status/stats/mana"
```

---

### Task 2: `PredictedAbilityStateHistory` 링 (LOP-Shared)

틱→상태 링버퍼. 기존 `InputHistory`와 동형(참조형 페이로드 + 병렬 tick 배열 + sentinel).

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime/Scripts/Game/PredictedAbilityStateHistory.cs`
- Test: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/PredictedAbilityStateHistoryTests.cs`

**Interfaces:**
- Consumes: `PredictedAbilityState`(Task 1).
- Produces:
  - `PredictedAbilityStateHistory(int capacity)`
  - `void Record(long tick, PredictedAbilityState state)`
  - `bool TryGet(long tick, out PredictedAbilityState state)`

- [ ] **Step 1: 실패 테스트 작성**

`Tests/EditMode/PredictedAbilityStateHistoryTests.cs`:
```csharp
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class PredictedAbilityStateHistoryTests
    {
        private static PredictedAbilityState State()
        {
            var e = new Entity("x");
            e.Add(new Abilities());
            e.Add(new Mana(100));
            e.Add(new Stats());
            e.Add(new StatusEffects());
            return PredictedAbilityState.Capture(e);
        }

        [Test]
        public void Record_ThenTryGet_ReturnsSameState()
        {
            var h = new PredictedAbilityStateHistory(4);
            var s = State();
            h.Record(10, s);

            Assert.IsTrue(h.TryGet(10, out var got));
            Assert.AreSame(s, got);
        }

        [Test]
        public void TryGet_UnknownTick_ReturnsFalse()
        {
            var h = new PredictedAbilityStateHistory(4);
            h.Record(10, State());
            Assert.IsFalse(h.TryGet(9, out _));
        }

        [Test]
        public void Exceeding_Capacity_EvictsOldestTick()
        {
            var h = new PredictedAbilityStateHistory(4);
            for (long t = 0; t <= 4; t++) h.Record(t, State());

            Assert.IsFalse(h.TryGet(0, out _), "가장 오래된 tick 0은 밀려나야 한다");
            Assert.IsTrue(h.TryGet(1, out _));
            Assert.IsTrue(h.TryGet(4, out _));
        }

        [Test]
        public void UnrecordedTickThatMapsToDefaultSlot_ReturnsFalse()
        {
            var h = new PredictedAbilityStateHistory(4);
            h.Record(3, State());   // latest=3, 윈도우가 tick 0 포함
            Assert.IsFalse(h.TryGet(0, out _), "기록 안 된 tick 0은 sentinel과 구분돼 false");
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Run: UnityMCP `run_tests(mode="EditMode", filter="PredictedAbilityStateHistoryTests", ...)`
Expected: 컴파일 에러(`PredictedAbilityStateHistory` 미정의).

- [ ] **Step 3: 최소 구현 작성**

`Runtime/Scripts/Game/PredictedAbilityStateHistory.cs`:
```csharp
using System;

namespace LOP
{
    /// <summary>
    /// 틱 → 그 틱의 <see cref="PredictedAbilityState"/>. 클라 롤백 재생용 어빌리티 상태 로그.
    /// 슬롯 = tick % capacity. 같은 슬롯의 오래된 틱은 덮여 자동 eviction. (InputHistory 링과 동형 —
    /// 참조형 페이로드라 tick 판별용 병렬 배열 + sentinel.)
    /// </summary>
    public class PredictedAbilityStateHistory
    {
        private const long EmptyTick = long.MinValue;

        private readonly long[] _ticks;
        private readonly PredictedAbilityState[] _states;
        private readonly int _capacity;
        private long _latestTick;
        private bool _hasAny;

        public PredictedAbilityStateHistory(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _capacity = capacity;
            _ticks = new long[capacity];
            _states = new PredictedAbilityState[capacity];
            for (int i = 0; i < capacity; i++)
            {
                _ticks[i] = EmptyTick;
            }
        }

        public void Record(long tick, PredictedAbilityState state)
        {
            int slot = Slot(tick);
            _ticks[slot] = tick;
            _states[slot] = state;
            if (!_hasAny || tick > _latestTick)
            {
                _latestTick = tick;
            }
            _hasAny = true;
        }

        public bool TryGet(long tick, out PredictedAbilityState state)
        {
            if (_hasAny && tick <= _latestTick && tick > _latestTick - _capacity)
            {
                int slot = Slot(tick);
                if (_ticks[slot] == tick)
                {
                    state = _states[slot];
                    return true;
                }
            }

            state = null;
            return false;
        }

        private int Slot(long tick) => (int)(((tick % _capacity) + _capacity) % _capacity);
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: UnityMCP `run_tests(mode="EditMode", filter="PredictedAbilityStateHistoryTests", ...)`
Expected: PASS (4 tests).

- [ ] **Step 5: 커밋** (LOP-Shared)

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" && \
git add Runtime/Scripts/Game/PredictedAbilityStateHistory.cs Runtime/Scripts/Game/PredictedAbilityStateHistory.cs.meta \
        Tests/EditMode/PredictedAbilityStateHistoryTests.cs Tests/EditMode/PredictedAbilityStateHistoryTests.cs.meta && \
git commit -m "feat(stage4): PredictedAbilityStateHistory ring (tick -> ability state)"
```

---

### Task 3: 결정론 라운드트립 테스트 (LOP-Shared)

이 슬라이스의 안전망. 어빌리티 발동 + 상태이상이 낀 틱 시퀀스를 진행하며 틱별 캡처 → 앵커로 복원 → 재진행 → 재현된 상태가 원본과 일치하는지 단언. 스냅샷이 재생에 필요한 상태를 **빠짐없이** 담는지 못박는다(누락 시 실패). 물리/이동/`AbilityActivator`(클라)는 제외한 순수 코어(발동은 `AbilitySystem.TryActivate` 직접 호출).

**Files:**
- Test: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Tests/EditMode/AbilityReplayDeterminismTests.cs`

**Interfaces:**
- Consumes: `PredictedAbilityState`(Task 1), `AbilitySystem`, `StatusEffectSystem`, `AbilityEffectExecutor`, `ManaSystem`, `StatsSystem`, `StatusEffectApplyEffectHandler`, `AbilityData`, `StatusEffectData`, `StatusEffectApplyEffect`.
- Produces: (테스트만 — 프로덕션 코드 없음)

- [ ] **Step 1: 결정론 테스트 작성**

`Tests/EditMode/AbilityReplayDeterminismTests.cs`:
```csharp
using System.Collections.Generic;
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    // 재생 = 앵커 복원 후 어빌리티/상태이상 시스템을 라이브와 같은 순서로 재진행. 최종 상태가 라이브와 일치해야 한다.
    public class AbilityReplayDeterminismTests
    {
        const int AbilityId = 1;
        const int HasteEffectId = 100;
        const int MpCost = 20;

        private ManaSystem _mana;
        private StatsSystem _stats;
        private StatusEffectSystem _status;
        private AbilityEffectExecutor _executor;
        private AbilitySystem _abilities;

        [SetUp]
        public void SetUp()
        {
            _mana = new ManaSystem();
            _stats = new StatsSystem();
            _status = new StatusEffectSystem(_stats);
            var handler = new StatusEffectApplyEffectHandler(_status, Resolve);
            _executor = new AbilityEffectExecutor(new IAbilityEffectHandler[] { handler });
            _abilities = new AbilitySystem(_mana);
        }

        private static StatusEffectData? Resolve(int id)
            => id == HasteEffectId ? (StatusEffectData?)new StatusEffectData(
                HasteEffectId, DurationPolicy.Duration, 3,
                new[] { new StatusModifierSpec((int)EntityStatType.Dexterity, 0.3f, ModifierType.PercentAdd) },
                StatusStackPolicy.Refresh, 1) : null;

        private static AbilityData Haste()
            => new AbilityData(AbilityId, 10, MpCost, 2, 3, 2,
                               new AbilityEffect[] { new StatusEffectApplyEffect(HasteEffectId) });

        private static Entity MakeEntity()
        {
            var e = new Entity("caster");
            e.Add(new Abilities());
            e.Add(new Mana(100));
            var stats = new Stats();
            stats.BaseStats[(int)EntityStatType.Dexterity] = 10f;
            e.Add(stats);
            e.Add(new StatusEffects());
            return e;
        }

        // 한 틱 진행 = 재조정 재생 루프의 어빌리티/상태 부분과 같은 순서(이동/물리 제외).
        private void AdvanceTick(Entity e, long tick)
        {
            _abilities.Tick(e, tick);
            _status.Tick(e, tick);
            _executor.DriveActiveEntity(e, null, tick);
        }

        private static void AssertStateEqual(PredictedAbilityState expected, Entity actual, long atTick)
        {
            var abilities = actual.Get<Abilities>();
            Assert.AreEqual(expected.ActiveAbility?.Phase, abilities.ActiveAbility?.Phase, $"tick {atTick}: phase");
            Assert.AreEqual(expected.Slots[AbilityId].CooldownEndTick,
                            abilities.Slots[AbilityId].CooldownEndTick, $"tick {atTick}: cooldown");
            Assert.AreEqual(expected.StatusEffects.Count,
                            actual.Get<StatusEffects>().Effects.Count, $"tick {atTick}: status count");
            Assert.AreEqual(expected.ManaCurrent, actual.Get<Mana>().Current, $"tick {atTick}: mana");
            Assert.AreEqual(expected.Modifiers.Count,
                            actual.Get<Stats>().Modifiers.Count, $"tick {atTick}: modifiers");
        }

        [Test]
        public void RestoreThenReplay_ReproducesAbilityAndStatusState()
        {
            const int anchor = 1;
            const int last = 9;

            // 1) 라이브 진행 — 틱 0에 발동, 매 틱 진행 후 상태 캡처.
            var e = MakeEntity();
            _abilities.Grant(e, AbilityId);
            _abilities.TryActivate(e, Haste(), e, 0);

            var recorded = new Dictionary<long, PredictedAbilityState>();
            for (long t = 0; t <= last; t++)
            {
                AdvanceTick(e, t);
                recorded[t] = PredictedAbilityState.Capture(e);
            }

            // 2) 앵커(anchor)로 복원 후 anchor+1..last 재진행.
            recorded[anchor].RestoreTo(e);
            for (long t = anchor + 1; t <= last; t++)
            {
                AdvanceTick(e, t);
                AssertStateEqual(recorded[t], e, t);   // 재현 == 라이브
            }
        }
    }
}
```

- [ ] **Step 2: 테스트 실행 (통과 기대)**

Run: UnityMCP `run_tests(mode="EditMode", filter="AbilityReplayDeterminismTests", ...)`
Expected: PASS. 만약 FAIL이면 `PredictedAbilityState`(Task 1)가 재진행에 필요한 상태를 빠뜨린 것 — 누락 필드를 Task 1에 추가하고 Task 1 테스트부터 다시 통과시킨다.

- [ ] **Step 3: 커밋** (LOP-Shared)

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared" && \
git add Tests/EditMode/AbilityReplayDeterminismTests.cs Tests/EditMode/AbilityReplayDeterminismTests.cs.meta && \
git commit -m "test(stage4): determinism round-trip — restore+replay reproduces ability/status state"
```

---

### Task 4: 매 틱 어빌리티 상태 기록 (LOP-Client)

`LOPRunner`가 매 틱 위치 스냅샷과 **함께** 어빌리티 상태 스냅샷을 기록하도록. 아직 아무도 읽지 않으므로 동작 변화 없음(안전 증분).

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/wt-stage4-ability-replay/Assets/Scripts/Game/GameLifetimeScope.cs:86` (DI 등록 추가)
- Modify: `C:/Users/re5na/workspace/LOP/wt-stage4-ability-replay/Assets/Scripts/Game/LOPRunner.cs:23` (주입) 및 `:191-217` (`RecordLocalSnapshot`)

**Interfaces:**
- Consumes: `PredictedAbilityStateHistory`(Task 2), `PredictedAbilityState.Capture`(Task 1).
- Produces: DI에 `PredictedAbilityStateHistory`(Singleton, capacity 128) 등록. `LOPRunner`가 매 틱 `predictedAbilityStateHistory.Record(tick, Capture(worldEntity))` 수행.

- [ ] **Step 1: DI 등록 추가** — `GameLifetimeScope.cs`의 SnapshotHistory 등록 바로 아래.

기존 (`GameLifetimeScope.cs:86-87`):
```csharp
            builder.Register(_ => new GameFramework.Netcode.SnapshotHistory(128), Lifetime.Singleton);
            builder.Register(_ => new InputHistory(128), Lifetime.Singleton);
```
변경 후:
```csharp
            builder.Register(_ => new GameFramework.Netcode.SnapshotHistory(128), Lifetime.Singleton);
            builder.Register(_ => new PredictedAbilityStateHistory(128), Lifetime.Singleton);
            builder.Register(_ => new InputHistory(128), Lifetime.Singleton);
```

- [ ] **Step 2: `LOPRunner` 주입 필드 추가** — `LOPRunner.cs:23` 아래.

기존:
```csharp
        [Inject] private GameFramework.Netcode.SnapshotHistory snapshotHistory;
        [Inject] private Reconciler reconciler;
```
변경 후:
```csharp
        [Inject] private GameFramework.Netcode.SnapshotHistory snapshotHistory;
        [Inject] private PredictedAbilityStateHistory predictedAbilityStateHistory;
        [Inject] private Reconciler reconciler;
```

- [ ] **Step 3: `RecordLocalSnapshot`에서 어빌리티 상태도 기록** — 기존 `snapshotHistory.Record(...)` 뒤에 한 줄 추가.

기존 (`LOPRunner.cs:212-216`):
```csharp
            snapshotHistory.Record(new GameFramework.Netcode.EntitySnapshot(
                Runner.Time.tick,
                transform.Position,
                transform.Rotation,
                velocity.Linear));
```
변경 후:
```csharp
            snapshotHistory.Record(new GameFramework.Netcode.EntitySnapshot(
                Runner.Time.tick,
                transform.Position,
                transform.Rotation,
                velocity.Linear));

            predictedAbilityStateHistory.Record(Runner.Time.tick, PredictedAbilityState.Capture(worldEntity));
```

- [ ] **Step 4: 컴파일 확인**

Run: UnityMCP `refresh_unity(...)` 후 `read_console(unity_instance="LeagueOfPhysical-Client@<hash>")`
Expected: 컴파일 에러 없음. (`predictedAbilityStateHistory`가 `worldEntity` 스코프 안에서 호출되는지 확인 — `RecordLocalSnapshot`은 `worldEntity`를 이미 지역 변수로 보유.)

- [ ] **Step 5: 커밋** (LOP-Client 워크트리)

```bash
cd "C:/Users/re5na/workspace/LOP/wt-stage4-ability-replay" && \
git add Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Game/LOPRunner.cs && \
git commit -m "feat(stage4): record predicted ability state each tick (client)"
```

---

### Task 5: `Reconciler` 풀 복원 + 예측 틱 전체 재생 (LOP-Client)

앵커에서 어빌리티 상태를 복원하고, 재생 루프를 이동만이 아니라 발동→이동→어빌리티페이즈→상태이상→효과구동→물리로 확장. **버그가 실제로 닫히는 태스크.**

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/wt-stage4-ability-replay/Assets/Scripts/Entity/Reconciler.cs` (주입 + `Reconcile` 시그니처 + 복원 + 재생 루프)
- Modify: `C:/Users/re5na/workspace/LOP/wt-stage4-ability-replay/Assets/Scripts/Game/LOPRunner.cs:90` (`Reconcile` 호출에 `entityManager` 전달)

**Interfaces:**
- Consumes: `PredictedAbilityStateHistory`(Task 2), `PredictedAbilityState`(Task 1), `AbilityDataProvider`(`bool TryGet(int, out AbilityData)`), `AbilitySystem`(`TryActivate(Entity, in AbilityData, Entity, long)`, `Tick(Entity, long)`), `StatusEffectSystem`(`Tick(Entity, long)`), `AbilityEffectExecutor`(`DriveActiveEntity(Entity, GameFramework.IEntityManager, long)`), `GameFramework.IEntityManager`, `InputCommand.AbilityId`.
- Produces: `void Reconciler.Reconcile(long currentTick, float deltaTime, GameFramework.IEntityManager entityManager)` (시그니처 변경 — 3번째 인자 추가).

- [ ] **Step 1: `Reconciler` 주입 필드 추가** — 기존 주입 블록(`Reconciler.cs:17-23`)에 추가.

기존:
```csharp
        [Inject] private IPlayerContext playerContext;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private GameFramework.Netcode.SnapshotHistory snapshotHistory;
        [Inject] private InputHistory inputHistory;
        [Inject] private MovementSystem movementSystem;
        [Inject] private GameFramework.IPhysicsSimulator physicsSimulator;
        [Inject] private ReconciliationStats reconciliationStats;
```
변경 후 (아래 5줄 추가):
```csharp
        [Inject] private IPlayerContext playerContext;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private GameFramework.Netcode.SnapshotHistory snapshotHistory;
        [Inject] private PredictedAbilityStateHistory predictedAbilityStateHistory;
        [Inject] private InputHistory inputHistory;
        [Inject] private MovementSystem movementSystem;
        [Inject] private AbilitySystem abilitySystem;
        [Inject] private StatusEffectSystem statusEffectSystem;
        [Inject] private AbilityEffectExecutor abilityEffectExecutor;
        [Inject] private AbilityDataProvider abilityDataProvider;
        [Inject] private GameFramework.IPhysicsSimulator physicsSimulator;
        [Inject] private ReconciliationStats reconciliationStats;
```

- [ ] **Step 2: `Reconcile` 시그니처 변경 + 어빌리티 상태 복원 + 재생 루프 확장**

기존 시그니처 (`Reconciler.cs:39`):
```csharp
        public void Reconcile(long currentTick, float deltaTime)
```
변경 후:
```csharp
        public void Reconcile(long currentTick, float deltaTime, GameFramework.IEntityManager entityManager)
```

기존 하드 복원 블록 (`Reconciler.cs:73-79`):
```csharp
            // 하드 복원: 내 캐릭을 서버 스냅(anchorTick) 상태로. reactive 경로가 rigidbody에 반영되므로
            // PhysX가 새 포즈를 보도록 수동 SyncTransforms(autoSyncTransforms=false).
            entity.position = snap.position;
            entity.rotation = snap.rotation;
            entity.velocity = snap.velocity;
            entity.PushMotionToPhysics();
            Physics.SyncTransforms();
```
변경 후 (어빌리티 상태 복원 추가):
```csharp
            // 하드 복원: 내 캐릭을 서버 스냅(anchorTick) 상태로. reactive 경로가 rigidbody에 반영되므로
            // PhysX가 새 포즈를 보도록 수동 SyncTransforms(autoSyncTransforms=false).
            entity.position = snap.position;
            entity.rotation = snap.rotation;
            entity.velocity = snap.velocity;
            entity.PushMotionToPhysics();
            Physics.SyncTransforms();

            // 어빌리티/상태이상/스탯/마나도 앵커 틱 상태로 복원 — 재생이 대시 등을 정확히 재현하려면 필요.
            if (predictedAbilityStateHistory.TryGet(anchorTick, out var abilityState))
            {
                abilityState.RestoreTo(worldEntity);
            }
```

기존 재생 루프 (`Reconciler.cs:93-107`):
```csharp
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
```
변경 후 (예측 틱 전체 = 라이브 순서: 발동→이동→어빌리티페이즈→상태이상→효과구동→물리):
```csharp
            for (long t = anchorTick + 1; t < currentTick; t++)
            {
                var cmd = inputHistory.TryGet(t, out var recorded) ? recorded : null;
                buffer.Current = cmd;

                // 발동 재현: 입력에 어빌리티가 있으면 그 틱에 다시 발동한다. AbilityActivator가 아니라
                // AbilitySystem.TryActivate를 직접 부른다 — 연출 cue 이벤트(AbilityActivatedEvent)를 재생 때
                // 중복 송출하지 않기 위해(cue는 원래 라이브 틱에 이미 발화됨).
                if (cmd != null && cmd.AbilityId != 0 &&
                    abilityDataProvider.TryGet(cmd.AbilityId, out var data))
                {
                    abilitySystem.TryActivate(worldEntity, data, worldEntity, t);
                }

                movementSystem.Tick(worldEntity, deltaTime);
                abilitySystem.Tick(worldEntity, t);
                statusEffectSystem.Tick(worldEntity, t);
                abilityEffectExecutor.DriveActiveEntity(worldEntity, entityManager, t);

                entity.PushMotionToPhysics();
                physicsSimulator.Simulate(deltaTime);
                entity.SyncPhysics();

                // 보정값으로 두 히스토리 갱신(다음 비교/재생이 stale값을 안 보도록).
                var transform = worldEntity.Get<GameFramework.World.Transform>();
                var velocity = worldEntity.Get<GameFramework.World.Velocity>();
                snapshotHistory.Record(new GameFramework.Netcode.EntitySnapshot(
                    t, transform.Position, transform.Rotation, velocity.Linear));
                predictedAbilityStateHistory.Record(t, PredictedAbilityState.Capture(worldEntity));
            }
```

- [ ] **Step 3: `LOPRunner`에서 `entityManager` 전달** — `Reconcile` 호출 수정.

기존 (`LOPRunner.cs:90`):
```csharp
            reconciler.Reconcile(Runner.Time.tick, (float)tickUpdater.interval);
```
변경 후:
```csharp
            reconciler.Reconcile(Runner.Time.tick, (float)tickUpdater.interval, entityManager);
```

- [ ] **Step 4: 컴파일 확인**

Run: UnityMCP `refresh_unity(...)` 후 `read_console(unity_instance="LeagueOfPhysical-Client@<hash>")`
Expected: 컴파일 에러 없음. `entityManager`(LOPRunner의 `entityManager` 프로퍼티 = `LOPEntityManager`)가 `GameFramework.IEntityManager`로 전달되는지 확인.

- [ ] **Step 5: 커밋** (LOP-Client 워크트리)

```bash
cd "C:/Users/re5na/workspace/LOP/wt-stage4-ability-replay" && \
git add Assets/Scripts/Entity/Reconciler.cs Assets/Scripts/Game/LOPRunner.cs && \
git commit -m "feat(stage4): reconcile replays full predicted tick (ability/status) + restores ability state"
```

---

### Task 6: 플레이 검증 (LOP-Client — 수동)

자동 단위 테스트가 불가한 클라 통합의 최종 검증. 지연 주입 + 대시 재조정에서 러버밴딩 소멸 및 무회귀 확인.

**Files:** (코드 변경 없음 — 검증만)

**Interfaces:**
- Consumes: Task 5 완료 빌드.
- Produces: 검증 결과(육안 + DebugHud `ReconciliationStats`).

- [ ] **Step 1: 지연 주입 준비** — Mirror LatencySimulation으로 RTT 50/150/300ms를 순서대로 설정(로컬 2-에디터). `docs/netcode-redesign.md §9.6` 참고.

- [ ] **Step 2: 대시 재조정 시나리오** — 대시(어빌리티 id 2)를 이동 중/공중에서 반복 발동하며, 재조정 창에 대시가 걸치도록 유도. 확인:
  - **이전(회귀 기준):** 대시가 재생에서 걷기로 뭉개져 위치가 튀던 현상.
  - **기대:** 대시가 재생에 재현되어 위치 pop/러버밴딩 육안 소멸. `ReconciliationStats`(DebugHud)의 하드 보정 distance가 대시 구간에서 크게 줄어듦.

- [ ] **Step 3: 무회귀 확인** — 지상 이동 / 점프 / 정지 / 대시 각 시나리오에서 이전과 동일하게 동작(대시 없는 구간은 slice③과 동일).

- [ ] **Step 4: (선택) 상태이상 재현** — 상태이상 어빌리티가 있으면 재조정 창에 걸쳐 발동, 재생 후 모디파이어/지속이 서버와 어긋나지 않는지 확인.

- [ ] **Step 5: 결과 기록** — 검증 결과를 slice spec 하단 "구현 후기"에 한 단락으로 남긴다(RTT별 육안 + distance 관찰). 코드 변경 없으므로 커밋은 spec 문서 갱신 시 함께.

---

## 마무리 (모든 태스크 후)

- [ ] LOP-Shared 브랜치 push + LOP-Client 워크트리 브랜치 push.
- [ ] 각 레포 `feature/stage4-ability-replay` → 각 `main` `--no-ff` 머지(사용자 확인 후). 두 레포는 함께 머지(패키지 참조 정합).
- [ ] 워크트리 정리: `git worktree remove ../wt-stage4-ability-replay`.

## 자기 검토 메모 (작성자)

- **스펙 커버리지:** 풀 스냅샷(어빌리티/상태/스탯/마나) = Task 1. 링 = Task 2. 재생 전체 틱 = Task 5. 결정론 테스트 = Task 3. errorGate/뷰 무변경 = 유지(수정 안 함). 레벨 2(velocity 권위)·라이브 단일화 = 범위 밖(미포함, 의도적).
- **cue 중복 회피:** 재생 발동은 `AbilitySystem.TryActivate` 직접 호출(=`AbilityActivator` 우회)로 `AbilityActivatedEvent` 재송출 방지 — Task 5 Step 2 주석에 근거 명시.
- **DRY 노트:** `PredictedAbilityStateHistory`는 `GameFramework.Netcode.SnapshotHistory` 링과 알고리즘이 동형(참조형이라 `InputHistory`와 더 근접). 제네릭 `SnapshotHistory<T>`로 통합하면 slice② 테스트·DebugHud까지 churn이 커져, 지금은 통합하지 않고 병렬 유지(YAGNI). 후속 통합 여지.
- **타입 일관성:** `PredictedAbilityState.Capture`/`RestoreTo`, `PredictedAbilityStateHistory.Record/TryGet`, `Reconcile(long,float,IEntityManager)` — Task 간 시그니처 일치 확인함.
