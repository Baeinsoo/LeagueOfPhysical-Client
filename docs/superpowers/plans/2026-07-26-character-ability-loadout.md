# 캐릭터별 어빌리티 소유 + 슬롯 장착 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 어빌리티를 캐릭터별로 분리하고, 입력·AI가 어빌리티 id 대신 슬롯으로 참조하게 한다.

**Architecture:** 어빌리티 3층(정의 / 부여 / 진행 중)의 가운데·끝 이름을 바로잡고, 부여 기록에 **슬롯**(장착 자리)을 추가한다. 슬롯 해소는 코어(순수 조회)와 side-local(설정 조회+발동)로 나눈다 — 코어는 MasterData를 참조하지 않기 때문. 마지막에 `TbCharacterLoadout` 표로 부여를 데이터화해 캐릭터마다 다른 공격을 갖게 한다.

**Tech Stack:** C# / Unity 6 / Luban 마스터데이터 / VContainer / NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-07-26-character-ability-loadout-design.md`

## Global Constraints

- **저장소 5개**를 건드린다: `LeagueOfPhysical-Shared` · `LeagueOfPhysical-Client` · `LeagueOfPhysical-Server` · `infrastructure` · `LeagueOfPhysical-MasterData-Client`/`-Server`. **각 저장소 main 체크아웃에서 피처 브랜치를 파고 작업하며, main 직접 커밋 금지.** 워크트리를 쓰지 않는다 — 패키지가 `file:` 참조라 워크트리에서는 Unity 에디터가 변경을 보지 못해 EditMode 테스트가 불가능하다.
- **UnityMCP 호출에는 항상 `unity_instance`를 명시**한다. 클라 `LeagueOfPhysical-Client@de70658b9450cbb4`, 서버 `LeagueOfPhysical-Server@f99391fa2dbaaf3c`.
- **EditMode 기준선은 318개 통과**(클라 에디터 전체 실행 기준). 각 태스크 커밋 전에 회귀가 없는지 확인한다.
- **LOP 측 파일에서 World 타입은 항상 풀 네임스페이스로 한정**한다(`GameFramework.World.Entity`). `using GameFramework.World;`를 추가하지 않는다 — `Component`가 `UnityEngine.Component`와 충돌한다. **단 LOP-Shared의 `Runtime/Scripts/Game/Ability/` 파일들은 이미 `using GameFramework.World;`를 쓰고 있으므로 그 파일 안에서는 기존 방식을 유지**한다.
- **`.meta` 파일은 반드시 함께 커밋**한다. 새 `.cs`를 만든 뒤 Unity가 생성한 `.meta`를 같이 add. 직접 만들거나 편집하지 않는다.
- **`git add -A` 금지.** 파일을 명시적으로 지정한다. 무관한 기존 미커밋 변경이 있다: 클라 `Assets/Art`, `ProjectSettings/PackageManagerSettings.asset` / 서버 `Assets/DefaultVolumeProfile.asset`, `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`, `Assets/Scripts/Game/GameRuleSystem.cs`. **커밋하거나 되돌리지 말 것.**
- **TDD 증거 규칙**: RED는 **구현이 아직 존재하지 않는 상태**에서 나와야 한다. 구현을 먼저 쓴 뒤 파일을 삭제·이동해 실패 장면을 만들어 TDD 증거로 제시하는 것은 **금지**. 순서를 못 지킨 사정이 있으면 보고서에 사실대로 쓴다.
- **슬롯 번호 배정** (고정): `1`=기본 공격, `2`=대시, `3`=헤이스트, `4`=전역 공격(테스트, 플레이어 전용). `0`=입력에 붙지 않음.
- **어빌리티 id**: `1`=haste, `2`=dash, `3`=attack(Knight), `4`=global_attack, **`5`=necro_attack(신규)**, **`6`=archer_attack(신규)**.
- **캐릭터 코드**: `character_001`(플레이어/Knight), `monster_001`(Necromancer), `monster_002`(Archer).
- **Luban Excel-embedded 형식**: 1행 `##var`+컬럼명, 2행 `##type`, 3행 `##group`(컬럼별 `c`/`s`/빈칸), 4행 `##`, 5행부터 데이터(**첫 칸은 항상 빈 문자열**). `__tables__.xlsx`는 헤더 1행(`##var full_name value_type read_schema_from_file input index mode group comment tags output`) + 2행부터 데이터. `.xlsx`는 **openpyxl 3.1.5로 편집**한다(Excel 수작업 아님).

---

# 슬라이스 1 — 이름 정리 (동작 무변화)

## Task 1: `AbilitySlot` → `GrantedAbility`

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/Abilities.cs:6-17,82-89`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilitySystem.cs` (2곳)
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/PredictedAbilityState.cs` (3곳)
- Modify: `LeagueOfPhysical-Shared/Tests/EditMode/PredictedAbilityStateTests.cs` (2곳)

**Interfaces:**
- Consumes: 없음
- Produces: `LOP.GrantedAbility` (구 `AbilitySlot`) — `readonly struct`, 필드 `int AbilityId` / `long CooldownEndTick`, 생성자 `GrantedAbility(int abilityId, long cooldownEndTick)`. `LOP.Abilities.Granted` — `Dictionary<int, GrantedAbility>` (구 `Slots`).

- [ ] **Step 1: 타입과 프로퍼티 이름 변경**

`Abilities.cs`의 struct 선언을 교체한다 (내용은 그대로, 이름과 XML 주석만):

```csharp
    /// <summary>
    /// 이 엔티티가 부여받은 어빌리티 하나의 런타임 상태(데이터). 기록의 존재 자체가 보유 증명이다.
    /// GAS의 FGameplayAbilitySpec 대응. 로직은 <see cref="AbilitySystem"/>에 둔다(Anemic).
    /// </summary>
    public readonly struct GrantedAbility
    {
        public readonly int AbilityId;
        public readonly long CooldownEndTick;   // currentTick >= 이 값이면 ready (초기 0)

        public GrantedAbility(int abilityId, long cooldownEndTick)
        {
            AbilityId = abilityId;
            CooldownEndTick = cooldownEndTick;
        }
    }
```

같은 파일의 `Abilities` 컴포넌트:

```csharp
    /// <summary>엔티티가 부여받은 어빌리티 집합(데이터 컴포넌트). AbilityId당 1개.</summary>
    public class Abilities : Component
    {
        public Dictionary<int, GrantedAbility> Granted { get; } = new Dictionary<int, GrantedAbility>();

        /// <summary>진행 중인 발동(없으면 null=Ready). 엔티티당 동시 1 — busy 판정.</summary>
        public ActiveAbility? ActiveAbility { get; set; }
    }
```

- [ ] **Step 2: 나머지 참조 3파일 갱신**

`AbilitySystem.cs`, `PredictedAbilityState.cs`, `PredictedAbilityStateTests.cs`에서
`AbilitySlot` → `GrantedAbility`, `.Slots` → `.Granted` 로 바꾼다.

전수 확인 명령:
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
grep -rn "AbilitySlot\|\.Slots" --include=*.cs Runtime Tests
```
기대: 출력 없음.

- [ ] **Step 3: 컴파일 + 전체 스위트 확인**

UnityMCP `refresh_unity`(mode=force, compile=request, wait_for_ready=true) → `read_console`로 에러 0 → `run_tests`(mode=EditMode) → `get_test_job` 폴링.
기대: **318/318 통과** (순수 리네임이므로 개수 불변).

- [ ] **Step 4: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feature/ability-loadout
git add Runtime/Scripts/Game/Ability/Abilities.cs \
        Runtime/Scripts/Game/Ability/AbilitySystem.cs \
        Runtime/Scripts/Game/PredictedAbilityState.cs \
        Tests/EditMode/PredictedAbilityStateTests.cs
git commit -m "refactor(ability): AbilitySlot → GrantedAbility — 슬롯이 아니라 부여 기록"
```

---

## Task 2: `ActiveAbility` → `AbilityActivation`

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/Abilities.cs:19-89`
- Modify: LOP-Shared의 나머지 13개 `.cs` (아래 전수 명령으로 찾는다)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs`

**Interfaces:**
- Consumes: `LOP.GrantedAbility` (Task 1)
- Produces: `LOP.AbilityActivation` (구 `ActiveAbility`) — 필드·생성자 시그니처 그대로. `WithPhase(AbilityPhase)` → `AbilityActivation` 반환. `static AbilityActivation ForPresentation(int abilityId, long startupEndTick, long activeEndTick, long recoveryEndTick)`. `LOP.Abilities.Current` — `AbilityActivation?` (구 `ActiveAbility`).

- [ ] **Step 1: 타입 이름과 주석 교체**

`Abilities.cs`에서 `ActiveAbility` struct를 `AbilityActivation`으로 바꾼다. 필드·생성자 본문은 그대로 두고, 선언과 XML 주석만 바꾼다:

```csharp
    /// <summary>어빌리티 발동의 시간 페이즈(격투 frame data). null ⇔ Ready; <see cref="AbilityActivation"/>은 항상 Startup/Active/Recovery.</summary>
    public enum AbilityPhase { Ready, Startup, Active, Recovery }

    /// <summary>
    /// 진행 중인 어빌리티 발동 하나(transient). 엔티티당 동시 1. 페이즈 경계는 발동 시 절대 틱으로 확정.
    /// 데이터만 — 전진/적용 로직은 <see cref="AbilitySystem.Tick"/>.
    /// </summary>
    public readonly struct AbilityActivation
    {
```

생성자·`WithPhase`·`ForPresentation`의 타입 이름도 함께 바꾼다:

```csharp
        public AbilityActivation(int abilityId, AbilityPhase phase, long startupEndTick, long activeEndTick,
                                 long recoveryEndTick, Entity target, AbilityEffect[] effects,
                                 float startupMoveScale = 1f, float activeMoveScale = 1f,
                                 float recoveryMoveScale = 1f, bool blockJump = false)

        public AbilityActivation WithPhase(AbilityPhase phase)
            => new AbilityActivation(AbilityId, phase, StartupEndTick, ActiveEndTick, RecoveryEndTick, Target, Effects,
                                     StartupMoveScale, ActiveMoveScale, RecoveryMoveScale, BlockJump);

        public static AbilityActivation ForPresentation(int abilityId, long startupEndTick,
                                                        long activeEndTick, long recoveryEndTick)
        {
            return new AbilityActivation(abilityId, AbilityPhase.Startup,
                startupEndTick, activeEndTick, recoveryEndTick,
                null, System.Array.Empty<AbilityEffect>(), 1f, 1f, 1f, false);
        }
```

컴포넌트의 프로퍼티:

```csharp
        /// <summary>진행 중인 발동(없으면 null=Ready). 엔티티당 동시 1 — busy 판정.</summary>
        public AbilityActivation? Current { get; set; }
```

- [ ] **Step 2: 전 저장소 참조 갱신**

먼저 대상을 나열한다:

```bash
cd /c/Users/re5na/workspace/LOP
grep -rln "ActiveAbility" LeagueOfPhysical-Shared/Runtime LeagueOfPhysical-Shared/Tests \
  LeagueOfPhysical-Client/Assets/Scripts LeagueOfPhysical-Server/Assets/Scripts --include=*.cs
```

각 파일에서 타입 `ActiveAbility` → `AbilityActivation`, 프로퍼티 접근 `.ActiveAbility` → `.Current` 로 바꾼다. XML 주석의 `<see cref="ActiveAbility"/>`도 함께 갱신한다.

**주의**: `AbilityPhase.Active`는 **바꾸지 않는다** — 페이즈 값 이름이며 이 리네임 대상이 아니다.

전수 확인:
```bash
grep -rn "ActiveAbility" LeagueOfPhysical-Shared/Runtime LeagueOfPhysical-Shared/Tests \
  LeagueOfPhysical-Client/Assets/Scripts LeagueOfPhysical-Server/Assets/Scripts --include=*.cs
```
기대: 출력 없음.

- [ ] **Step 3: 양쪽 컴파일 + 전체 스위트**

클·서 각각 `refresh_unity` → `read_console` 에러 0. 클라에서 `run_tests`(EditMode).
기대: **318/318 통과**.

- [ ] **Step 4: 커밋 (3개 저장소)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime Tests
git commit -m "refactor(ability): ActiveAbility → AbilityActivation — 진행 중인 발동임을 이름으로"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git checkout -b feature/ability-loadout
git add Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs
git commit -m "refactor(ability): ActiveAbility → AbilityActivation 참조 갱신"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/ability-loadout
git add Assets/Scripts/Game/TickSystems/EntitySnapshotBroadcastSystem.cs
git commit -m "refactor(ability): ActiveAbility → AbilityActivation 참조 갱신"
```

---

# 슬라이스 2 — 슬롯 도입 (동작 무변화)

## Task 3: 부여 기록에 슬롯 추가 + 슬롯 조회 (코어)

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/Abilities.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilitySystem.cs` (`Grant`, `TryActivate`의 쿨다운 갱신부)
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/AbilitySlotResolutionTests.cs` (신규)

**Interfaces:**
- Consumes: `LOP.GrantedAbility` (Task 1), `LOP.AbilityActivation` (Task 2)
- Produces:
  - `GrantedAbility(int abilityId, int slot, long cooldownEndTick)` — 필드 `AbilityId`/`Slot`/`CooldownEndTick`
  - `AbilitySystem.Grant(Entity entity, int abilityId, int slot)`
  - `AbilitySystem.TryGetAbilityIdBySlot(Entity caster, int slot, out int abilityId) → bool`

- [ ] **Step 1: 실패하는 테스트 작성**

`LeagueOfPhysical-Shared/Tests/EditMode/AbilitySlotResolutionTests.cs`:

```csharp
using NUnit.Framework;
using GameFramework.World;

namespace LOP.Tests
{
    public class AbilitySlotResolutionTests
    {
        private static Entity MakeCaster()
        {
            var entity = new Entity("caster");
            entity.Add(new Abilities());
            return entity;
        }

        private static AbilitySystem MakeSystem() => new AbilitySystem(new ManaSystem());

        [Test]
        public void GrantStoresSlot()
        {
            var entity = MakeCaster();
            MakeSystem().Grant(entity, abilityId: 3, slot: 1);

            Assert.AreEqual(1, entity.Get<Abilities>().Granted[3].Slot);
        }

        [Test]
        public void ResolvesAbilityIdFromSlot()
        {
            var entity = MakeCaster();
            var system = MakeSystem();
            system.Grant(entity, abilityId: 3, slot: 1);
            system.Grant(entity, abilityId: 2, slot: 2);

            Assert.IsTrue(system.TryGetAbilityIdBySlot(entity, 2, out int abilityId));
            Assert.AreEqual(2, abilityId);
        }

        [Test]
        public void UnboundSlotResolvesToFalse()
        {
            var entity = MakeCaster();
            var system = MakeSystem();
            system.Grant(entity, abilityId: 3, slot: 1);

            Assert.IsFalse(system.TryGetAbilityIdBySlot(entity, 4, out _));
        }

        [Test]
        public void SlotZeroIsNotResolvable()
        {
            var entity = MakeCaster();
            var system = MakeSystem();
            system.Grant(entity, abilityId: 7, slot: 0);   // 입력에 붙지 않는 부여

            Assert.IsFalse(system.TryGetAbilityIdBySlot(entity, 0, out _));
        }

        [Test]
        public void RegrantUpdatesSlot()
        {
            var entity = MakeCaster();
            var system = MakeSystem();
            system.Grant(entity, abilityId: 3, slot: 1);
            system.Grant(entity, abilityId: 3, slot: 4);

            Assert.AreEqual(4, entity.Get<Abilities>().Granted[3].Slot);
        }
    }
}
```

> `AbilitySystem`의 생성자는 `AbilitySystem(ManaSystem manaSystem)` 하나뿐이다.

- [ ] **Step 2: 테스트가 실패하는지 확인**

`refresh_unity` 후 `run_tests`(EditMode, filter `AbilitySlotResolutionTests`).
기대: **컴파일 실패** — `Grant`가 인자 2개짜리뿐이고 `TryGetAbilityIdBySlot`·`Slot`이 없음.

- [ ] **Step 3: `GrantedAbility`에 슬롯 추가**

`Abilities.cs`:

```csharp
    /// <summary>
    /// 이 엔티티가 부여받은 어빌리티 하나의 런타임 상태(데이터). 기록의 존재 자체가 보유 증명이다.
    /// GAS의 FGameplayAbilitySpec 대응 — 어빌리티 참조 + 입력 바인딩(<see cref="Slot"/>) + 런타임 상태(쿨다운).
    /// 로직은 <see cref="AbilitySystem"/>에 둔다(Anemic).
    /// </summary>
    public readonly struct GrantedAbility
    {
        public readonly int AbilityId;

        /// <summary>장착 자리 번호(입력 바인딩). 0이면 입력에 붙지 않음 — GAS의 InputID = INDEX_NONE 대응.</summary>
        public readonly int Slot;

        public readonly long CooldownEndTick;   // currentTick >= 이 값이면 ready (초기 0)

        public GrantedAbility(int abilityId, int slot, long cooldownEndTick)
        {
            AbilityId = abilityId;
            Slot = slot;
            CooldownEndTick = cooldownEndTick;
        }
    }
```

- [ ] **Step 4: `Grant`와 슬롯 조회 구현**

`AbilitySystem.cs`의 `Grant`를 교체:

```csharp
        /// <summary>어빌리티를 부여한다(GAS GiveAbility). slot=0이면 입력에 붙지 않는 부여.</summary>
        public void Grant(Entity entity, int abilityId, int slot)
        {
            var abilities = entity.Get<Abilities>();
            if (abilities == null)
            {
                return;
            }
            abilities.Granted[abilityId] = new GrantedAbility(abilityId, slot, 0);
        }

        /// <summary>슬롯에 장착된 어빌리티 id를 찾는다(순수 읽기). 슬롯 0은 입력 대상이 아니라 항상 false.</summary>
        public bool TryGetAbilityIdBySlot(Entity caster, int slot, out int abilityId)
        {
            abilityId = 0;
            if (slot <= 0)
            {
                return false;
            }
            var abilities = caster?.Get<Abilities>();
            if (abilities == null)
            {
                return false;
            }
            foreach (var granted in abilities.Granted.Values)
            {
                if (granted.Slot == slot)
                {
                    abilityId = granted.AbilityId;
                    return true;
                }
            }
            return false;
        }
```

- [ ] **Step 5: 쿨다운 갱신이 슬롯을 지우지 않게 수정 (중요)**

`AbilitySystem.TryActivate`의 Commit 구간이 현재 이렇게 부여 기록을 **통째로 덮어쓴다**:

```csharp
            abilities.Slots[data.AbilityId] = new AbilitySlot(data.AbilityId, currentTick + data.CooldownTicks);
```

이대로 두면 **발동할 때마다 슬롯이 0으로 지워진다.** 기존 슬롯을 보존하도록 바꾼다:

```csharp
            // 쿨다운만 갱신 — 슬롯(장착 자리)은 보존해야 한다.
            int grantedSlot = abilities.Granted[data.AbilityId].Slot;
            abilities.Granted[data.AbilityId] =
                new GrantedAbility(data.AbilityId, grantedSlot, currentTick + data.CooldownTicks);
```

> `CanActivate`가 이미 `Granted`에 기록이 있는지 확인한 뒤에야 여기 도달하므로 인덱서 접근이 안전하다.

- [ ] **Step 6: 슬롯 보존 회귀 테스트 추가**

`AbilitySlotResolutionTests.cs`에 케이스를 더한다. `AbilityData` 생성자는
`AbilityData(int abilityId, long cooldownTicks, int mpCost, long startupTicks, long activeTicks, long recoveryTicks, AbilityEffect[] effects, float startupMoveScale = 1f, float activeMoveScale = 1f, float recoveryMoveScale = 1f, bool blockJump = false)` 이다.

```csharp
        [Test]
        public void ActivationPreservesSlot()
        {
            var entity = MakeCaster();
            entity.Add(new Mana(100));
            var system = MakeSystem();
            system.Grant(entity, abilityId: 3, slot: 1);

            var data = new AbilityData(3, cooldownTicks: 10, mpCost: 0,
                startupTicks: 1, activeTicks: 1, recoveryTicks: 1,
                effects: System.Array.Empty<AbilityEffect>());

            Assert.IsTrue(system.TryActivate(entity, data, entity, currentTick: 100));
            Assert.AreEqual(1, entity.Get<Abilities>().Granted[3].Slot,
                "발동 시 쿨다운만 갱신되어야 하고 슬롯은 보존되어야 한다");
        }
```

- [ ] **Step 7: 기존 `Grant` 호출부 인자 보정**

`Grant`가 3인자가 되면서 기존 2인자 호출이 깨진다. LOP-Shared 안의 호출은 **전부 테스트 파일
15곳**이며(프로덕션 호출부는 클·서 `CharacterCreator`뿐), 다음 5개 파일에 있다:

| 파일 | 호출 |
|---|---|
| `Tests/EditMode/AbilitySystemTests.cs` | 10곳 |
| `Tests/EditMode/LOPWorldTests.cs` | 2곳 |
| `Tests/EditMode/AbilityEffectExecutorTests.cs` | 1곳 |
| `Tests/EditMode/AbilityMovementPolicyTests.cs` | 1곳 |
| `Tests/EditMode/AbilityReplayDeterminismTests.cs` | 1곳 |

이 테스트들은 **슬롯을 검증하지 않으므로 전부 `slot: 0`을 준다**(입력에 연결되지 않은 부여).
예: `system.Grant(e, 7)` → `system.Grant(e, 7, slot: 0)`.

전수 확인:
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
grep -rn "\.Grant([^,]*,[^,)]*)" --include=*.cs Runtime Tests
```
기대: 출력 없음(2인자 호출이 남아 있지 않음).

> 클·서 `CharacterCreator`의 호출은 **Task 4**에서 고친다. 이 태스크에서는 LOP-Shared만 컴파일되면 된다.

- [ ] **Step 8: 테스트 통과 확인**

`run_tests`(EditMode, filter `AbilitySlotResolutionTests`) → 6/6 통과.
이어서 전체 스위트 → **324/324**(기준선 318 + 신규 6). 실패 0.

- [ ] **Step 9: 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/Ability/Abilities.cs \
        Runtime/Scripts/Game/Ability/AbilitySystem.cs \
        Tests/EditMode/AbilitySlotResolutionTests.cs Tests/EditMode/AbilitySlotResolutionTests.cs.meta
git commit -m "feat(ability): 부여 기록에 슬롯(장착 자리) 추가 + 슬롯→id 조회"
```

---

## Task 4: 호출자를 슬롯 기반으로 전환 (클·서)

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/AbilityActivator.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/AbilityActivator.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/PlayerInputManager.cs:174-177`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/UI/GamePad/GamePadViewModel.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/AI/EnemyBrain.cs:10,52`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/CharacterCreator.cs:69-74`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/CharacterCreator.cs:62-67`

**Interfaces:**
- Consumes: `AbilitySystem.Grant(Entity, int abilityId, int slot)`, `AbilitySystem.TryGetAbilityIdBySlot(Entity, int slot, out int abilityId)` (Task 3)
- Produces:
  - `AbilityActivator.TryGetAbilityIdBySlot(string casterEntityId, int slot, out int abilityId) → bool` (클·서 동일)
  - `AbilityActivator.TryActivateSlot(string casterEntityId, int slot, long currentTick) → bool` (클·서 동일)
  - `PlayerInputManager.SetAbilitySlot(int slot)` (클라)

- [ ] **Step 1: `AbilityActivator`에 슬롯 입구 추가 (클·서 동일 코드)**

두 저장소의 `AbilityActivator.cs`에 **똑같이** 메서드 둘을 더한다(기존 `TryActivate`는 그대로):

```csharp
        /// <summary>슬롯에 장착된 어빌리티 id를 찾는다. 입력 캡처가 슬롯을 id로 풀 때 쓴다.</summary>
        public bool TryGetAbilityIdBySlot(string casterEntityId, int slot, out int abilityId)
        {
            abilityId = 0;
            var caster = entityRegistry.Get(casterEntityId);
            if (caster == null)
            {
                return false;
            }
            return abilitySystem.TryGetAbilityIdBySlot(caster, slot, out abilityId);
        }

        /// <summary>슬롯으로 발동. id를 푼 뒤 기존 <see cref="TryActivate"/> 경로로 합류한다.</summary>
        public bool TryActivateSlot(string casterEntityId, int slot, long currentTick)
        {
            if (TryGetAbilityIdBySlot(casterEntityId, slot, out int abilityId) == false)
            {
                return false;
            }
            return TryActivate(casterEntityId, abilityId, currentTick);
        }
```

- [ ] **Step 2: 클라 입력이 슬롯을 받게**

`PlayerInputManager.cs`의 기존 `SetAbilityId`를 **슬롯 버전으로 교체**한다:

```csharp
        /// <summary>슬롯으로 어빌리티 입력을 예약한다. 슬롯을 내 부여 기록으로 풀어 id를 와이어에 싣는다
        /// (서버도 같은 로드아웃을 부여했으므로 같은 id로 해소된다 — 예측 정합).</summary>
        public void SetAbilitySlot(int slot)
        {
            if (abilityActivator.TryGetAbilityIdBySlot(playerContext.entityId, slot, out int abilityId))
            {
                pendingAbilityId = abilityId;
            }
        }
```

> `pendingAbilityId`·`abilityActivator`·`playerContext`는 이미 이 클래스에 있다. 와이어
> (`InputCommand.AbilityId`)는 **바꾸지 않는다.**

- [ ] **Step 3: 게임패드 상수를 슬롯으로**

`GamePadViewModel.cs`의 상수 블록을 교체:

```csharp
        // 버튼은 어빌리티 id가 아니라 장착 자리(슬롯)를 가리킨다 — 캐릭터마다 그 자리의 어빌리티가 다르다.
        private const int AttackSlot = 1;
        private const int DashSlot = 2;
        private const int HasteSlot = 3;
        private const int GlobalAttackSlot = 4;   // 테스트용 광역 공격(플레이어 전용)
```

호출부 4곳을 `SetAbilitySlot`으로 바꾼다:

```csharp
            if (Keyboard.current.hKey.wasPressedThisFrame)
            {
                _playerInputManager.SetAbilitySlot(HasteSlot);
            }

            if (Keyboard.current.gKey.wasPressedThisFrame)
            {
                _playerInputManager.SetAbilitySlot(GlobalAttackSlot);
            }
```

커맨드 메서드 4개(`:90`, `:99`, `:102`, `:105`)도 전부 바꾼다 — 주석의 "로드아웃은 후속" 문구는
이제 실현됐으므로 함께 정리한다:

```csharp
        public void Dash() => _playerInputManager.SetAbilitySlot(DashSlot);

        /// <summary>헤이스트 발동(이동속도 +30%, 한시). 온스크린 버튼/단축키(H)에서 호출.</summary>
        public void Haste() => _playerInputManager.SetAbilitySlot(HasteSlot);

        // 공격 = DamageEffect 어빌리티(서버권위 판정). 슬롯 1에 장착된 것이 캐릭터마다 다르다.
        public void Attack() => _playerInputManager.SetAbilitySlot(AttackSlot);

        // 테스트용 광역 공격(플레이어 로드아웃에만 있음). 온스크린 버튼/단축키(G)에서 호출.
        public void GlobalAttack() => _playerInputManager.SetAbilitySlot(GlobalAttackSlot);
```

파일 안에서 `SetAbilityId` 잔재가 없는지 확인:

```bash
grep -n "SetAbilityId\|AbilityId" /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/UI/GamePad/GamePadViewModel.cs
```
기대: 출력 없음.

- [ ] **Step 4: 서버 AI를 슬롯으로**

`EnemyBrain.cs`:

```csharp
        private const int AttackSlot = 1;   // 기본 공격 자리 — 캐릭터마다 실제 어빌리티가 다르다
```

```csharp
            if (direction.magnitude < 2)
            {
                //  Attack the player — 기본 공격 자리(슬롯 1) 발동. 플레이어와 동일 경로.
                abilityActivator.TryActivateSlot(worldEntity.Id, AttackSlot, tickUpdater.tick);
            }
```

- [ ] **Step 5: `CharacterCreator`의 부여에 슬롯 명시 (클·서 동일)**

두 저장소에서 하드코딩 부여에 슬롯을 붙인다. **부여 대상은 그대로** — 슬롯만 명시한다:

```csharp
            abilitySystem.Grant(worldEntity, 1, slot: 3);   // haste
            abilitySystem.Grant(worldEntity, 2, slot: 2);   // dash
            abilitySystem.Grant(worldEntity, 3, slot: 1);   // attack
```

그리고 내 캐릭 전용 블록:

```csharp
                abilitySystem.Grant(worldEntity, 4, slot: 4);   // 내 캐릭 전용 테스트 툴(G키)
```

- [ ] **Step 6: 양쪽 컴파일 + 스위트**

클·서 각각 `refresh_unity` → `read_console` 에러 0. 클라 `run_tests`(EditMode) → **324/324**.

- [ ] **Step 7: 커밋 (2개 저장소)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/AbilityActivator.cs Assets/Scripts/Game/PlayerInputManager.cs \
        Assets/Scripts/UI/GamePad/GamePadViewModel.cs Assets/Scripts/Entity/CharacterCreator.cs
git commit -m "feat(ability): 입력·부여를 슬롯 기반으로 — 버튼이 자리를 가리킨다"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/AbilityActivator.cs Assets/Scripts/AI/EnemyBrain.cs \
        Assets/Scripts/Entity/CharacterCreator.cs
git commit -m "feat(ability): AI·부여를 슬롯 기반으로"
```

- [ ] **Step 8: 슬라이스 1·2 머지 + 인게임 회귀 확인**

여기까지는 **동작이 하나도 변하지 않아야 한다.** 3개 저장소를 main에 `--no-ff` 머지한 뒤
2에디터로 확인: 공격 / 대시 / 헤이스트 / G키 / 몬스터 공격이 이전과 동일한가. 쿨다운도 정상인가.

---

# 슬라이스 3 — 로드아웃 데이터 (여기서 캐릭터별로 갈린다)

## Task 5: `TbCharacterLoadout` 테이블 + 캐릭터별 공격 행

**Files:**
- Modify (openpyxl): `infrastructure/table/Datas/#Ability.xlsx`
- Create (openpyxl): `infrastructure/table/Datas/#CharacterLoadout.xlsx`
- Modify (openpyxl): `infrastructure/table/Datas/__tables__.xlsx`
- Regenerate: `LeagueOfPhysical-MasterData-Client/Runtime.Generated/**`, `LeagueOfPhysical-MasterData-Server/Runtime.Generated/**`

**Interfaces:**
- Produces: `LOP.MasterData.Tables.TbCharacterLoadout` — `DataList` (`IReadOnlyList<CharacterLoadout>`), 행 타입 `CharacterLoadout { int Id; string CharacterCode; int Slot; int AbilityId; }`. 클·서 **양쪽** 생성.

- [ ] **Step 1: 편집 전 구조 덤프**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table/Datas
python -c "
import openpyxl
for f in ['#Ability.xlsx','__tables__.xlsx']:
    wb = openpyxl.load_workbook(f); ws = wb[wb.sheetnames[0]]
    print('===', f)
    for i, r in enumerate(ws.iter_rows(values_only=True), 1):
        c=[('' if x is None else str(x)) for x in r]
        while c and c[-1]=='': c.pop()
        if c: print(i, c)
"
```

**한글 셀 주의**: 콘솔에서 깨져 보여도 openpyxl은 정상 처리한다. 손대지 않은 셀은 그대로 보존되니
**깨져 보인다고 다시 쓰지 말 것.**

- [ ] **Step 2: `#Ability.xlsx`에 캐릭터별 공격 2행 추가**

기존 `attack` 행(7행)의 값을 그대로 복사하고 `id`/`code`/`name`만 바꾼다. 열 순서는
`##var`행에서 확인한 그대로 쓴다(현재: `id, code, name, description, cooldown_ticks, mp_cost,
startup_ticks, active_ticks, recovery_ticks, cue, effects, startup_move_scale, active_move_scale,
recovery_move_scale, block_jump`, 데이터 행의 첫 칸은 빈 문자열).

```python
import openpyxl
wb = openpyxl.load_workbook('Datas/#Ability.xlsx')
ws = wb[wb.sheetnames[0]]

attack = [ws.cell(row=7, column=c).value for c in range(1, ws.max_column + 1)]

def clone(dst_row, new_id, new_code):
    for c, v in enumerate(attack, start=1):
        ws.cell(row=dst_row, column=c, value=v)
    ws.cell(row=dst_row, column=2, value=str(new_id))   # id
    ws.cell(row=dst_row, column=3, value=new_code)      # code
    ws.cell(row=dst_row, column=4, value=new_code)      # name

clone(9,  5, 'necro_attack')
clone(10, 6, 'archer_attack')
wb.save('Datas/#Ability.xlsx')
```

> `cue` 값도 함께 복사된다(현재 `attack`). 애니 동기화 작업이 재개되면 `cue`는 제거되고
> `TbAbilityView`로 대체되므로, 지금은 복사해 두는 것이 **기존 연출 동작을 보존**한다.

- [ ] **Step 3: `#CharacterLoadout.xlsx` 생성**

```python
import openpyxl
wb = openpyxl.Workbook()
ws = wb.active
rows = [
    ['##var',   'id',  'character_code', 'slot', 'ability_id'],
    ['##type',  'int', 'string',         'int',  'int'],
    ['##group', '',    '',               '',     ''],
    ['##',      'id',  'character_code', 'slot', 'ability_id'],
    ['', '1',  'character_001', '1', '3'],
    ['', '2',  'character_001', '2', '2'],
    ['', '3',  'character_001', '3', '1'],
    ['', '4',  'character_001', '4', '4'],
    ['', '5',  'monster_001',   '1', '5'],
    ['', '6',  'monster_001',   '2', '2'],
    ['', '7',  'monster_001',   '3', '1'],
    ['', '8',  'monster_002',   '1', '6'],
    ['', '9',  'monster_002',   '2', '2'],
    ['', '10', 'monster_002',   '3', '1'],
]
for r in rows:
    ws.append(r)
wb.save('Datas/#CharacterLoadout.xlsx')
```

- [ ] **Step 4: `__tables__.xlsx`에 등록**

기존 `TbAbility` 행과 같은 형태로 한 줄 추가한다. **`group` 칸(8번째)은 빈 문자열** —
클·서 양쪽이 부여에 쓰기 때문이다.

```python
import openpyxl
wb = openpyxl.load_workbook('Datas/__tables__.xlsx')
ws = wb[wb.sheetnames[0]]
ws.append(['', 'TbCharacterLoadout', 'CharacterLoadout', 'TRUE',
           '#CharacterLoadout.xlsx', 'id', 'map', '', 'CharacterLoadout', '', ''])
wb.save('Datas/__tables__.xlsx')
```

- [ ] **Step 5: 재생성 + 양쪽 생성 확인**

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure/table
./gen.sh
ls ../../LeagueOfPhysical-MasterData-Client/Runtime.Generated/Scripts/MasterData/ | grep -i characterloadout
ls ../../LeagueOfPhysical-MasterData-Server/Runtime.Generated/Scripts/MasterData/ | grep -i characterloadout
```

기대: **양쪽 모두**에 `CharacterLoadout.cs`/`TbCharacterLoadout.cs`가 있어야 한다.
한쪽에만 있으면 `group` 칸을 잘못 지정한 것이니 고치고 재생성한다.

- [ ] **Step 6: 컴파일 확인 + 커밋**

클·서 `refresh_unity` → `read_console` 에러 0.

```bash
cd /c/Users/re5na/workspace/LOP/infrastructure
git checkout -b feature/character-loadout
git add table/Datas/
git commit -m "feat(masterdata): TbCharacterLoadout + 캐릭터별 공격 어빌리티 2행"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
git checkout -b feature/character-loadout && git add Runtime.Generated/ \
  && git commit -m "chore(gen): TbCharacterLoadout + 캐릭터별 공격"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server
git checkout -b feature/character-loadout && git add Runtime.Generated/ \
  && git commit -m "chore(gen): TbCharacterLoadout + 캐릭터별 공격"
```

---

## Task 6: 로드아웃 표 기반 부여

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/CharacterLoadoutProvider.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/CharacterLoadoutProvider.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/CharacterCreator.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/CharacterCreator.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `LOP.MasterData.Tables.TbCharacterLoadout` (Task 5), `AbilitySystem.Grant(Entity, int, int)` (Task 3)
- Produces: `LOP.CharacterLoadoutProvider.Get(string characterCode) → IReadOnlyList<(int slot, int abilityId)>`

- [ ] **Step 1: `CharacterLoadoutProvider` 작성 (클·서 동일 코드)**

두 저장소에 **같은 내용**으로 만든다. 기존 `AbilityDataProvider`/`StatusEffectDataProvider`와 같은
자리·같은 패턴(side-local 어댑터)이다.

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// Luban <c>TbCharacterLoadout</c>을 캐릭터별 장착 목록으로 바꾸는 side-local 어댑터.
    /// 표는 int id로 키잉돼 있어 캐릭터 코드로는 못 찾으므로, 생성 시 한 번 색인해 둔다.
    /// </summary>
    public class CharacterLoadoutProvider
    {
        private readonly Dictionary<string, List<(int slot, int abilityId)>> _byCharacter
            = new Dictionary<string, List<(int slot, int abilityId)>>();

        public CharacterLoadoutProvider(LOP.MasterData.LOPMasterData md)
        {
            foreach (var row in md.Tables.TbCharacterLoadout.DataList)
            {
                if (_byCharacter.TryGetValue(row.CharacterCode, out var list) == false)
                {
                    list = new List<(int, int)>();
                    _byCharacter[row.CharacterCode] = list;
                }
                list.Add((row.Slot, row.AbilityId));
            }
        }

        /// <summary>해당 캐릭터의 장착 목록. 없으면 빈 목록.</summary>
        public IReadOnlyList<(int slot, int abilityId)> Get(string characterCode)
        {
            return _byCharacter.TryGetValue(characterCode, out var list)
                ? list
                : System.Array.Empty<(int, int)>();
        }
    }
}
```

- [ ] **Step 2: DI 등록 (클·서)**

두 `GameLifetimeScope.cs`에서 `AbilityDataProvider` 등록 줄(클라 기준 `:43`) 바로 아래에 추가한다.
기존 형태가 `builder.Register<T>(Lifetime.Singleton)`이므로 그대로 따른다:

```csharp
            builder.Register<StatusEffectDataProvider>(Lifetime.Singleton);
            builder.Register<AbilityDataProvider>(Lifetime.Singleton);
            builder.Register<CharacterLoadoutProvider>(Lifetime.Singleton);   // 추가
```

- [ ] **Step 3: `CharacterCreator`가 표로 부여 (클·서)**

두 저장소에서 하드코딩 4줄을 표 순회로 교체한다. `CharacterLoadoutProvider`를 주입받아야 하므로
기존 주입 필드들과 같은 방식으로 추가한다.

```csharp
            foreach (var (slot, abilityId) in characterLoadoutProvider.Get(creationData.characterCode))
            {
                abilitySystem.Grant(worldEntity, abilityId, slot);
            }
```

**`isUserEntity` 분기에 있던 `Grant(worldEntity, 4, slot: 4)`도 제거한다** — 전역 공격은
`character_001` 로드아웃 행으로 표현되고, 몬스터는 그 캐릭터 코드를 쓰지 않으므로 자동으로
플레이어에게만 부여된다. (같은 블록의 `InputBuffer`/`Simulated` 추가는 **그대로 둔다.**)

- [ ] **Step 4: 양쪽 컴파일 + 스위트**

클·서 각각 `refresh_unity` → `read_console` 에러 0. 클라 `run_tests`(EditMode) → **324/324**.

- [ ] **Step 5: 커밋 (2개 저장소)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/CharacterLoadoutProvider.cs Assets/Scripts/Game/CharacterLoadoutProvider.cs.meta \
        Assets/Scripts/Entity/CharacterCreator.cs Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(ability): 로드아웃 표 기반 부여 — 캐릭터마다 자기 공격"

cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/CharacterLoadoutProvider.cs Assets/Scripts/Game/CharacterLoadoutProvider.cs.meta \
        Assets/Scripts/Entity/CharacterCreator.cs Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(ability): 로드아웃 표 기반 부여 — 캐릭터마다 자기 공격"
```

- [ ] **Step 6: 슬라이스 3 머지 + 인게임 검증**

5개 저장소를 main에 `--no-ff` 머지 후 2에디터로 확인:

| 시나리오 | 기대 |
|---|---|
| 플레이어 공격 | id 3 발동 (Knight) |
| Necromancer 공격 | id 5 발동 |
| Archer 공격 | id 6 발동 |
| 대시 / 헤이스트 / G키 | 이전과 동일 |
| 쿨다운 | 발동 후 재발동이 막히는가(슬롯 보존 확인) |
| 롤백 | 손실 환경에서 공격 연타 시 예측 재생 정상 |

발동 id 확인은 서버 콘솔 로그로 한다(`read_console`, `unity_instance` 명시).

---

## 완료 기준

- [ ] 5개 저장소 컴파일 클린
- [ ] EditMode **324/324** 통과 (기준선 318 + 신규 6)
- [ ] 슬라이스 1·2 이후 동작이 **하나도 변하지 않음**
- [ ] 슬라이스 3 이후 세 캐릭터가 각자 다른 공격 어빌리티를 발동
- [ ] `GamePadViewModel`·`EnemyBrain`에 어빌리티 id 상수가 남아 있지 않음(슬롯 상수만)
- [ ] `CharacterCreator`에 `Grant` 하드코딩이 남아 있지 않음
- [ ] 발동해도 슬롯이 지워지지 않음(테스트로 고정)
- [ ] `docs/ROADMAP.md`에 완료 기록 추가
- [ ] 애니 동기화 계획(`2026-07-25-animation-state-sync.md`)의 Task 10 브리프를 갱신 —
  `TbAbilityView`가 `character_code` 없이 어빌리티 id 단일 키로 성립하며, 행은 id 3/5/6
