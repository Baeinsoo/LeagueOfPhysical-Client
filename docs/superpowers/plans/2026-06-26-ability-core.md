# Ability 코어 Implementation Plan (Ability/StatusEffect Phase 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** LOP-Shared에 **순수 C# Ability 코어**(`AbilityData` 설정 + `Abilities`/`AbilitySlot` 데이터 컴포넌트 + `AbilitySystem` Grant/CanActivate/TryActivate)를 신설한다. GAS 생명주기(`CanActivate`→`Commit`(코스트+쿨다운)→효과 생성)를 anemic으로 구현하고, **Ability→Effect 이음새**로 Phase 1 `StatusEffectSystem`을 호출한다. MP 코스트를 위해 GameFramework `ManaSystem.Spend`를 추가한다. **순수 추가**(레거시·클·서 런타임 무변경, 입력/AI 미연결) + **EditMode 테스트**.

**Spec:** `docs/superpowers/specs/2026-06-26-ability-statuseffect-world-core-design.md` (Phase 2)
**선행:** Phase 1 StatusEffect 코어 완료(main 머지) — `StatusEffectData`/`StatusEffectSystem` 존재.

**Architecture:** GameFramework(1 메서드 추가) + LOP-Shared(Ability 코어) 추가형. `world.Tick` 실배선·Luban `TbAction→TbAbility`·엔티티 생성 시 컴포넌트 부여·입력/AI/와이어 이전 = **Phase 3(컷오버)**. 이 Phase는 순수 코어 + 단위 테스트만.

**Tech Stack:** Unity / C# / GameFramework.World / UnityMCP(클·서 컴파일 검증).

---

## 레포 / 도구 참조
- **GameFramework:** `C:\Users\re5na\workspace\LOP\GameFramework` (`ManaSystem.Spend` 1개 추가)
- **LOP-Shared:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Shared` (Ability 코어 + 테스트)
- **Client:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client` (문서 커밋 + 컴파일 검증)
- **Server:** 컴파일 검증만(코드 변경 0).
- **UnityMCP:** `mcpforunity://instances`에서 Client(`@de70658b9450cbb4`)/Server(`@f99391fa2dbaaf3c`) id(해시 변동 — 매번 resource 재확인). 모든 호출에 `unity_instance` 명시.

**⚠️ EOL:** mass `sed` 금지. 신규=Write, 편집=Edit.
**.meta:** 신규 `.cs`는 `refresh_unity`가 `.meta` 생성. `.cs`+`.cs.meta` 함께 커밋.
**픽스처 보존:** `git add -A`/`commit -am` 금지. Task 명시 파일만 add.
**컨벤션:** Microsoft C#. 시스템=상태없는 인스턴스 클래스. GAS 생명주기 어휘(`CanActivate`/`Commit`). 네임스페이스 `namespace LOP`(LOP-Shared) / `GameFramework.World`(GameFramework).

---

## 확정된 설계 결정 (spec + 실측 반영)

1. **틱 타입 = `long`**(쿨다운 절대 end-tick). 쿨다운 readiness는 *파생*(`currentTick >= CooldownEndTick`) — **per-tick 갱신 불필요라 `AbilitySystem`에 `Tick` 없음**(캐스트/채널 페이즈가 생길 때 추가).
2. **코스트 = MP만(이번)** — `Mana.Current`에서 `MpCost` 차감. HP 코스트는 DEFER(드묾). 차감은 신규 `ManaSystem.Spend` 경유(StatusEffect가 StatsSystem 거치듯, 시스템 경계 유지).
3. **Ability→Effect 이음새 = 호출자 resolve, 시스템은 primitives-in.** `AbilityData.ProducesEffectIds`(설정)는 *어떤 효과인지*만 가리키고, 실제 적용은 호출자(사이드-로컬, Luban 접근)가 resolve한 `StatusEffectData[]`를 `TryActivate`에 넘기면 `AbilitySystem`이 `StatusEffectSystem.Apply` 호출. LOP-Shared는 MasterData 비참조 유지(토폴로지).
4. **인스턴싱 = InstancedPerActor** — 엔티티당 `AbilitySlot`(슬롯=GAS PerActor 상태). 동시 다중캐스트(PerExecution) DEFER.
5. **타게팅 = enum만(`TargetingMode` Self/Unit/Point/Direction) + Range 저장.** 지오메트리 해소(스킬샷/AoE)·클라 타깃 예측 = DEFER. target은 `TryActivate` 인자로 호출자가 지정.
6. **캐스트/채널/취소 = DEFER**(즉발만). `CastTimeTicks`는 구조로 두되 즉발(=0) 경로만 구현.
7. **배치** — `Runtime/Scripts/Game/Ability/`(StatusEffect 옆). 3파일: `AbilityData.cs`(enum+config), `Abilities.cs`(컴포넌트+슬롯), `AbilitySystem.cs`(로직).
8. **시스템 의존 = `ManaSystem` + `StatusEffectSystem`**(생성자 주입). `StatsSystem`은 `StatusEffectSystem`이 이미 보유.

---

## Task 0: 피처 브랜치 (GameFramework / LOP-Shared / Client)
- [ ] **Step 1: working tree 확인** (모두 main, 픽스처 외 깨끗)
```bash
for r in GameFramework LeagueOfPhysical-Shared LeagueOfPhysical-Client; do echo "== $r =="; git -C C:/Users/re5na/workspace/LOP/$r status --short; echo "branch: $(git -C C:/Users/re5na/workspace/LOP/$r branch --show-current)"; done
```
Expected: GameFramework/LOP-Shared 깨끗(main), Client는 픽스처 + 이번 plan(untracked). 다른 변경 있으면 멈추고 보고.
- [ ] **Step 2: 브랜치 생성**
```bash
for r in GameFramework LeagueOfPhysical-Shared LeagueOfPhysical-Client; do git -C C:/Users/re5na/workspace/LOP/$r checkout -b feature/ability-core; done
```

---

## Task 1: GameFramework — `ManaSystem.Spend`

- [ ] **Step 1: `Runtime/Scripts/World/Systems/ManaSystem.cs`에 `Spend` 추가** (Edit). 기존 `ApplyAuthoritativeState` 위/아래에:
```csharp
        /// <summary>자원을 소비한다. 잔량이 부족하면 차감 없이 false. 성공 시 차감하고 true. (Generation 결정 메서드)</summary>
        public bool Spend(Mana mana, int amount)
        {
            if (amount < 0 || mana.Current < amount)
            {
                return false;
            }
            mana.Current -= amount;
            return true;
        }
```
- [ ] **Step 2: 컴파일 검증** — 클·서 `refresh_unity`(scope=all, force) → `read_console`(error) 0. (GameFramework 변경이 클·서에 전파.)

---

## Task 2: LOP-Shared — `AbilityData` (설정 + enum)

- [ ] **Step 1: `Runtime/Scripts/Game/Ability/AbilityData.cs` 생성** (Write)
```csharp
namespace LOP
{
    /// <summary>어빌리티 타게팅 종류(설정). 지오메트리 해소는 후속.</summary>
    public enum TargetingMode { Self, Unit, Point, Direction }

    /// <summary>
    /// 어빌리티 설정(불변 디자인 데이터). 후속 슬라이스에서 Luban TbAbility로 외부화되며, 코어는 이 구조체만 소비한다.
    /// <para>ProducesEffectIds는 *어떤 효과인지*만 가리킨다 — 실제 StatusEffectData는 호출자(사이드)가 resolve해 TryActivate에 넘긴다.</para>
    /// </summary>
    public readonly struct AbilityData
    {
        public readonly int AbilityId;
        public readonly long CooldownTicks;
        public readonly int MpCost;
        public readonly long CastTimeTicks;     // 0 = 즉발 (캐스트 경로는 후속)
        public readonly TargetingMode TargetingMode;
        public readonly float Range;
        public readonly int[] ProducesEffectIds;

        public AbilityData(int abilityId, long cooldownTicks, int mpCost, long castTimeTicks,
                           TargetingMode targetingMode, float range, int[] producesEffectIds)
        {
            AbilityId = abilityId;
            CooldownTicks = cooldownTicks;
            MpCost = mpCost;
            CastTimeTicks = castTimeTicks;
            TargetingMode = targetingMode;
            Range = range;
            ProducesEffectIds = producesEffectIds;
        }
    }
}
```

---

## Task 3: LOP-Shared — `Abilities` 컴포넌트 + `AbilitySlot`

- [ ] **Step 1: `Runtime/Scripts/Game/Ability/Abilities.cs` 생성** (Write)
```csharp
using System.Collections.Generic;
using GameFramework.World;

namespace LOP
{
    /// <summary>부여된 어빌리티 하나의 런타임 상태(데이터). 로직은 <see cref="AbilitySystem"/>에 둔다(Anemic).</summary>
    public readonly struct AbilitySlot
    {
        public readonly int AbilityId;
        public readonly long CooldownEndTick;   // currentTick >= 이 값이면 ready (초기 0)

        public AbilitySlot(int abilityId, long cooldownEndTick)
        {
            AbilityId = abilityId;
            CooldownEndTick = cooldownEndTick;
        }
    }

    /// <summary>엔티티가 보유한 어빌리티 슬롯 집합(데이터 컴포넌트). AbilityId당 1 슬롯(InstancedPerActor).</summary>
    public class Abilities : Component
    {
        public Dictionary<int, AbilitySlot> Slots { get; } = new Dictionary<int, AbilitySlot>();
    }
}
```

---

## Task 4: LOP-Shared — `AbilitySystem` (Grant / CanActivate / TryActivate)

- [ ] **Step 1: `Runtime/Scripts/Game/Ability/AbilitySystem.cs` 생성** (Write)
```csharp
using GameFramework.World;

namespace LOP
{
    /// <summary>
    /// 어빌리티 로직(상태 없음). GAS 생명주기(CanActivate→Commit(코스트+쿨다운)→효과 생성)를 anemic으로 구현.
    /// 쿨다운은 절대 end-tick(파생 readiness, per-tick 갱신 없음). 효과는 StatusEffectSystem이 적용(이음새).
    /// </summary>
    public class AbilitySystem
    {
        private readonly ManaSystem _manaSystem;
        private readonly StatusEffectSystem _statusEffectSystem;

        public AbilitySystem(ManaSystem manaSystem, StatusEffectSystem statusEffectSystem)
        {
            _manaSystem = manaSystem;
            _statusEffectSystem = statusEffectSystem;
        }

        /// <summary>어빌리티를 엔티티에 부여한다(ready 슬롯 추가).</summary>
        public void Grant(Entity entity, int abilityId)
        {
            var abilities = entity.Get<Abilities>();
            if (abilities == null)
            {
                return;
            }
            abilities.Slots[abilityId] = new AbilitySlot(abilityId, 0);
        }

        /// <summary>발동 가능 여부(GAS CanActivateAbility): 보유 + 쿨다운 ready + 자원 충분. 순수 읽기.</summary>
        public bool CanActivate(Entity caster, in AbilityData data, long currentTick)
        {
            var abilities = caster.Get<Abilities>();
            if (abilities == null || !abilities.Slots.TryGetValue(data.AbilityId, out var slot))
            {
                return false;
            }
            if (currentTick < slot.CooldownEndTick)
            {
                return false;
            }
            if (data.MpCost > 0)
            {
                var mana = caster.Get<Mana>();
                if (mana == null || mana.Current < data.MpCost)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 어빌리티를 발동한다. CanActivate면 Commit(코스트 차감 + 쿨다운 설정) 후 producedEffects를 타깃에 적용하고 true.
        /// 아니면 부수효과 없이 false. producedEffects = 호출자가 data.ProducesEffectIds로 resolve한 효과 설정.
        /// </summary>
        public bool TryActivate(Entity caster, in AbilityData data, Entity target,
                                StatusEffectData[] producedEffects, long currentTick)
        {
            if (!CanActivate(caster, data, currentTick))
            {
                return false;
            }

            // Commit — 코스트 차감 + 쿨다운 설정
            if (data.MpCost > 0)
            {
                _manaSystem.Spend(caster.Get<Mana>(), data.MpCost);
            }
            var abilities = caster.Get<Abilities>();
            abilities.Slots[data.AbilityId] = new AbilitySlot(data.AbilityId, currentTick + data.CooldownTicks);

            // 효과 생성(이음새) — 즉발. 캐스트 경로는 후속.
            if (producedEffects != null)
            {
                foreach (var effect in producedEffects)
                {
                    _statusEffectSystem.Apply(target, effect, caster.Id, currentTick);
                }
            }
            return true;
        }
    }
}
```
> `in AbilityData`를 람다에 캡처하지 않음(CS1628 회피 — Phase 1 교훈). 슬롯 갱신은 struct 교체.

- [ ] **Step 2: 컴파일 검증** — 클·서 `refresh_unity` → `read_console`(error) 0. 신규 3 `.cs.meta` 생성 확인.

---

## Task 5: LOP-Shared — EditMode 테스트

- [ ] **Step 1: `Tests/EditMode/AbilitySystemTests.cs` 생성** (Write). 헬퍼: Abilities+Mana(+Stats+StatusEffects) 단 Entity, `new ManaSystem()`/`new StatsSystem()`/`new StatusEffectSystem(stats)`/`new AbilitySystem(mana, statusEffect)`. 예시 어빌리티(쿨다운 N틱, MpCost M, ProducesEffectIds=[haste]) + producedEffects=[haste StatusEffectData].
  - `Grant_AddsReadySlot`: Grant 후 `CanActivate @0` true.
  - `TryActivate_Success_SpendsMana_SetsCooldown_AppliesEffect`: TryActivate @0 true → Mana.Current == max−M, 슬롯 CooldownEndTick == cd, **타깃에 효과 적용**(StatusEffects.Count==1, Stats 값 변화).
  - `TryActivate_OnCooldown_Fails`: 발동 후 즉시 재발동 @0 → false, Mana 추가차감 0, 효과 추가 0.
  - `TryActivate_AfterCooldown_Succeeds`: cd 경과 후(@cd) 재발동 → true.
  - `CanActivate_InsufficientMana_False`: Mana.Current < MpCost → false, TryActivate도 false(부수효과 0).
  - `TryActivate_NotGranted_False`: Grant 안 한 어빌리티 → false.
  - `TryActivate_NullProducedEffects_StillCommits`: producedEffects=null이어도 코스트·쿨다운은 커밋(true), 효과만 없음.
  - 부동소수 `Within(1e-4f)`.
- [ ] **Step 2: 테스트 실행** — `run_tests`(EditMode, assembly `baegames.LOP.Shared.Tests.EditMode`) green. (StatusEffect/Movement 기존 테스트도 함께 green 유지.)

---

## Task 6: 종합 컴파일 검증
- [ ] **Step 1: 양쪽 재스캔** — Client·Server 각 `refresh_unity`(scope=all, force, wait_for_ready) → `read_console`(error) **0**.
- [ ] **Step 2: 신규 .meta 확인** — GameFramework(없음, Edit) / LOP-Shared `Ability/*.cs.meta` + `AbilitySystemTests.cs.meta` 생성.

---

## Task 7: 커밋
- [ ] **Step 1: GameFramework** — `git add Runtime/Scripts/World/Systems/ManaSystem.cs`
```
feat(world): ManaSystem.Spend — affordable resource deduction

Add Spend(mana, amount): returns false (no change) if insufficient,
else deducts and returns true. Used by AbilitySystem cost commit.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 2: LOP-Shared** — `git add Runtime/Scripts/Game/Ability.meta Runtime/Scripts/Game/Ability/ Tests/EditMode/AbilitySystemTests.cs*`
```
feat(world): Ability core — anemic data + system (LOP-Shared)

Add pure-C# Ability core: AbilityData config, Abilities/AbilitySlot
data component, AbilitySystem (Grant/CanActivate/TryActivate). GAS
lifecycle: CanActivate (cooldown ready + MP affordable) -> Commit
(spend mana, set absolute cooldown end-tick) -> produce effects via
StatusEffectSystem.Apply (the ability->effect seam). Cast/channel,
targeting geometry, HP cost deferred. Cooldown is derived (no per-tick
Tick). EditMode tests cover grant/activate/cooldown/cost/seam.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 3: Client 문서** — `git add docs/superpowers/plans/2026-06-26-ability-core.md`
```
docs(world): Ability core Phase 2 plan

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

---

## Task 8: 검증 + 머지 (사용자)
- [ ] **Step 1: 최종 컴파일** — 양쪽 `read_console` 에러 0. Ability + StatusEffect + Movement 테스트 전부 green.
- [ ] **Step 2: 머지/푸시(사용자 요청 시)** — GameFramework → LOP-Shared → Client 순 `--no-ff`(file: 의존 순서). 런타임 거동 0(순수 추가·미배선)이라 플레이 검증 불요. 머지·push는 사용자 요청 시에만.

---

## Self-Review (작성자 체크)
- **Spec 커버리지(Phase 2):** ManaSystem.Spend=Task1 / AbilityData=Task2 / 컴포넌트=Task3 / AbilitySystem+이음새=Task4 / 테스트=Task5 / 검증=Task6 / 커밋=Task7. ✅
- **순수 추가:** GameFramework 1 메서드(기존 무변경) + LOP-Shared 신규 타입. 클·서 코드 0줄, 신규 타입 미사용 → 런타임 거동 0. ✅
- **anemic:** 컴포넌트(`Abilities`/`AbilitySlot`)=데이터만, 로직 전부 `AbilitySystem`. ✅
- **이음새 정합:** `TryActivate`가 `StatusEffectSystem.Apply` 호출(GAS ApplyGameplayEffectToTarget). 효과는 다출처·단일적용. ✅
- **토폴로지:** LOP-Shared가 MasterData 비참조 유지 — producedEffects를 호출자가 resolve해 넘김(primitives-in). ✅
- **결정론:** 쿨다운=절대 end-tick 비교(난수 없음), per-tick 갱신 불필요라 Tick 없음. ✅
- **시스템 경계:** 코스트 차감은 `ManaSystem.Spend` 경유(AbilitySystem이 Mana.Current 직접 안 만짐). ✅
- **범위 정직(YAGNI):** 캐스트/채널/취소·타게팅 지오메트리·HP코스트·클라 예측·Luban·world.Tick 배선·엔티티 생성·컷오버 = 명시 제외(Phase 3/Stage④). ✅
- **CS1628:** `in` 파라미터를 람다 캡처 안 함(Phase 1 교훈). ✅
- **EOL/.meta/픽스처:** Write=신규/Edit=기존, mass sed 없음, .cs+.meta 함께, 픽스처 제외. ✅
```
