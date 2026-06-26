# StatusEffect 코어 Implementation Plan (Ability/StatusEffect Phase 1)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** LOP-Shared에 **순수 C# StatusEffect 코어**(`StatusEffectData` 설정 구조체 + `StatusEffects`/`ActiveEffect` 데이터 컴포넌트 + `StatusEffectSystem` Apply/Tick/Remove)를 신설하고, 기존 `StatsSystem`(`StatModifier` + `RemoveModifiersBySourceId`)에 연동한다. **순수 추가**(레거시·클·서 런타임 무변경) + **EditMode 단위 테스트**로 모디파이어 생명주기를 검증. Luban·인게임 배선·이동→Stats·Ability는 모두 후속.

**Spec:** `docs/superpowers/specs/2026-06-26-ability-statuseffect-world-core-design.md` (Phase 1)

**Architecture:** LOP-Shared 단일 repo 추가형. 신규 순수 타입만 추가(기존 컴파일 비파괴). `GameFramework.World`(Entity/Component/Stats/StatsSystem/StatModifier/ModifierType) 위에 LOP 도메인 시스템을 올린다 — 이동 커널과 같은 자리(`Runtime/Scripts/Game/`). 클·서는 LOP-Shared 전파로 컴파일만 검증(코드 변경 0).

**Tech Stack:** Unity / C# / GameFramework.World (이미 LOP-Shared.Runtime이 참조) / UnityMCP(클·서 컴파일 검증).

---

## 레포 / 도구 참조
- **LOP-Shared:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Shared` (코드 + 테스트)
- **Client:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client` (문서 커밋 + 컴파일 검증)
- **Server:** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server` (컴파일 검증만, 코드 변경 0)
- **GameFramework:** 이번 Phase **무변경**(StatusEffect는 LOP 도메인 → LOP-Shared).
- **UnityMCP:** `mcpforunity://instances`에서 Client/Server id(해시 변동). 모든 호출에 `unity_instance` 명시.

**⚠️ EOL:** mass `sed` 금지(전체 CRLF→LF 오염). 신규 파일은 Write, 편집은 Edit.
**.meta:** 신규 `.cs`는 `refresh_unity` 시 Unity가 `.meta` 생성. `.cs`+`.cs.meta` 함께 커밋. 수기 .meta 금지.
**픽스처 보존:** `git add -A`/`commit -am` 금지. Task 명시 파일만 add. Client/Server 픽스처(Art/Room.unity/ProjectSettings 등) 커밋 금지.
**컨벤션:** Microsoft C#. 시스템은 상태 없는 인스턴스 클래스(`StatsSystem`/`HealthSystem` 짝). 명명은 산업 표준(GAS GameplayEffect 생명주기 어휘). 네임스페이스 `namespace LOP`(LOP-Shared 규약).

---

## 확정된 설계 결정 (spec Open Question 해소 + 실측 반영)

1. **틱 타입 = `long`** — `Runner.Time.tick`/`world.Tick(long,...)`과 일치. `ExpireTick`/`currentTick` 모두 long.
2. **SourceId 스킴(결정론) = `"se:" + effectId`** — 타깃당 effectId 1 인스턴스(AggregateByTarget 기본). Guid/난수 미사용(결정론). 모디파이어 일괄제거 키와 동일.
3. **집계 = AggregateByTarget** — 같은 effectId는 타깃당 1 `ActiveEffect`(여러 출처여도 병합). Source/Target 분리 집계 = DEFER.
4. **스택 = Refresh + StackMagnitude만.** Refresh=`ExpireTick` 리셋. StackMagnitude=`StackCount`(≤`MaxStacks`) 증가 + 모디파이어를 `value × stackCount`로 재적용(`RemoveModifiersBySourceId` 후 재-add) + 지속도 리프레시.
5. **Instant = 영구 베이스 변경(순수 Stats)** — `StatsSystem.AddBase`로 1회 적용, `ActiveEffect` 미저장·모디파이어 미사용. (ModifierType는 Instant에선 flat base로 취급.)
6. **periodic/DoT는 Phase 1 제외(YAGNI)** — DoT는 데미지(HealthSystem) 의존이라 코어를 순수 Stats로 유지하려 보류. `PeriodTicks`/`PeriodicAmount`/`NextPeriodTick` 필드도 **이번엔 만들지 않음**(DoT 슬라이스에서 추가). 따라서 Phase 1 = Duration/Infinite + 모디파이어 + 스택.
7. **헤이스트 검증 대상 스탯** — 인게임 MoveSpeed 배선은 후속(spec Open Question)이라, EditMode 테스트는 기존 `EntityStatType`(예: `Dexterity`)으로 *모디파이어 생명주기*를 검증. "헤이스트=이동속도 +X%"의 의미는 배선 후 MoveSpeed로 이어짐.
8. **배치** — `Runtime/Scripts/Game/StatusEffect/`(이동 커널 옆, 어빌리티는 후속 자기 폴더). 3파일: `StatusEffectData.cs`(enum+spec+config), `StatusEffects.cs`(컴포넌트+ActiveEffect), `StatusEffectSystem.cs`(로직).
9. **시스템 의존 = `StatsSystem` 1개**(생성자 주입). `EntityRegistry` 불필요(메서드가 `Entity`를 직접 받음).

---

## Task 0: LOP-Shared / Client 피처 브랜치
- [ ] **Step 1: working tree 확인**
```bash
for r in LeagueOfPhysical-Shared LeagueOfPhysical-Client; do echo "== $r =="; git -C C:/Users/re5na/workspace/LOP/$r status --short; git -C C:/Users/re5na/workspace/LOP/$r branch --show-current; done
```
Expected: LOP-Shared 깨끗(main), Client는 픽스처 + 이번 spec/plan 문서(untracked) — 정상. 다른 변경 있으면 멈추고 보고.
- [ ] **Step 2: 브랜치 생성** (Server는 Phase 1 무변경이라 제외)
```bash
for r in LeagueOfPhysical-Shared LeagueOfPhysical-Client; do git -C C:/Users/re5na/workspace/LOP/$r checkout -b feature/ability-statuseffect-world-core; done
```

---

## Task 1: LOP-Shared — `StatusEffectData` (설정 + enum)

- [ ] **Step 1: `Runtime/Scripts/Game/StatusEffect/StatusEffectData.cs` 생성** (Write)
```csharp
using GameFramework.World;

namespace LOP
{
    /// <summary>효과 지속 정책. Instant=즉시 1회(영구 베이스 변경), Duration=한시, Infinite=명시 제거까지.</summary>
    public enum DurationPolicy { Instant, Duration, Infinite }

    /// <summary>재적용 시 스택 규칙. Refresh=지속만 리셋, StackMagnitude=스택 증가로 효과 배율.</summary>
    public enum StatusStackPolicy { Refresh, StackMagnitude }

    /// <summary>효과가 부여하는 스탯 모디파이어 하나(설정값). 적용 시 SourceId가 붙어 <see cref="StatModifier"/>가 된다.</summary>
    public readonly struct StatusModifierSpec
    {
        public readonly int StatType;
        public readonly float Value;
        public readonly ModifierType Type;

        public StatusModifierSpec(int statType, float value, ModifierType type)
        {
            StatType = statType;
            Value = value;
            Type = type;
        }
    }

    /// <summary>
    /// 상태이상 설정(불변 디자인 데이터). 후속 슬라이스에서 Luban 테이블로 외부화되며, 코어는 이 구조체만 소비한다.
    /// </summary>
    public readonly struct StatusEffectData
    {
        public readonly int EffectId;
        public readonly DurationPolicy DurationPolicy;
        public readonly long DurationTicks;
        public readonly StatusModifierSpec[] Modifiers;
        public readonly StatusStackPolicy StackPolicy;
        public readonly int MaxStacks;

        public StatusEffectData(int effectId, DurationPolicy durationPolicy, long durationTicks,
                                StatusModifierSpec[] modifiers, StatusStackPolicy stackPolicy, int maxStacks)
        {
            EffectId = effectId;
            DurationPolicy = durationPolicy;
            DurationTicks = durationTicks;
            Modifiers = modifiers;
            StackPolicy = stackPolicy;
            MaxStacks = maxStacks;
        }
    }
}
```

---

## Task 2: LOP-Shared — `StatusEffects` 컴포넌트 + `ActiveEffect`

- [ ] **Step 1: `Runtime/Scripts/Game/StatusEffect/StatusEffects.cs` 생성** (Write)
```csharp
using System.Collections.Generic;
using GameFramework.World;

namespace LOP
{
    /// <summary>활성 상태이상 인스턴스(런타임 데이터). 로직은 <see cref="StatusEffectSystem"/>에 둔다(Anemic).</summary>
    public readonly struct ActiveEffect
    {
        public readonly int EffectId;
        public readonly long ExpireTick;        // Duration 한정. Infinite면 -1
        public readonly int StackCount;
        public readonly string SourceEntityId;  // 귀속(instigator)
        public readonly string SourceId;        // "se:{EffectId}" — StatModifier.SourceId 링크

        public ActiveEffect(int effectId, long expireTick, int stackCount, string sourceEntityId, string sourceId)
        {
            EffectId = effectId;
            ExpireTick = expireTick;
            StackCount = stackCount;
            SourceEntityId = sourceEntityId;
            SourceId = sourceId;
        }
    }

    /// <summary>엔티티에 적용 중인 상태이상 컬렉션(데이터 컴포넌트).</summary>
    public class StatusEffects : Component
    {
        public List<ActiveEffect> Effects { get; } = new List<ActiveEffect>();
    }
}
```

---

## Task 3: LOP-Shared — `StatusEffectSystem` (Apply / Tick / Remove)

- [ ] **Step 1: `Runtime/Scripts/Game/StatusEffect/StatusEffectSystem.cs` 생성** (Write)
```csharp
using GameFramework.World;

namespace LOP
{
    /// <summary>
    /// 상태이상 로직(상태 없음). GAS GameplayEffect 생명주기(Apply→Tick(만료)→Remove)를 anemic으로 구현.
    /// 모디파이어는 효과 인스턴스 SourceId로 달고, 만료/제거 시 그 SourceId로 일괄 해제한다.
    /// </summary>
    public class StatusEffectSystem
    {
        private readonly StatsSystem _statsSystem;

        public StatusEffectSystem(StatsSystem statsSystem)
        {
            _statsSystem = statsSystem;
        }

        private static string SourceIdFor(int effectId) => "se:" + effectId;

        /// <summary>효과를 타깃에 적용한다(GAS ApplyGameplayEffectToTarget). 스택/지속/모디파이어 해소.</summary>
        public void Apply(Entity target, in StatusEffectData data, string sourceEntityId, long currentTick)
        {
            var effects = target.Get<StatusEffects>();
            if (effects == null)
            {
                return;
            }
            var stats = target.Get<Stats>();

            // Instant = 영구 베이스 변경. 추적/모디파이어 없음.
            if (data.DurationPolicy == DurationPolicy.Instant)
            {
                if (stats != null && data.Modifiers != null)
                {
                    foreach (var m in data.Modifiers)
                    {
                        _statsSystem.AddBase(stats, m.StatType, m.Value);
                    }
                }
                return;
            }

            string sourceId = SourceIdFor(data.EffectId);
            long expire = data.DurationPolicy == DurationPolicy.Duration ? currentTick + data.DurationTicks : -1L;

            int idx = effects.Effects.FindIndex(e => e.EffectId == data.EffectId);
            if (idx < 0)
            {
                effects.Effects.Add(new ActiveEffect(data.EffectId, expire, 1, sourceEntityId, sourceId));
                AddModifiers(stats, data, sourceId, 1);
                return;
            }

            // 재적용: 지속 리프레시(+ StackMagnitude면 스택 증가·모디파이어 재배율)
            var active = effects.Effects[idx];
            int stack = active.StackCount;
            if (data.StackPolicy == StatusStackPolicy.StackMagnitude && stack < data.MaxStacks)
            {
                stack++;
                _statsSystem.RemoveModifiersBySourceId(stats, sourceId);
                AddModifiers(stats, data, sourceId, stack);
            }
            effects.Effects[idx] = new ActiveEffect(active.EffectId, expire, stack, active.SourceEntityId, sourceId);
        }

        /// <summary>만료된(Duration) 효과를 제거하고 모디파이어를 해제한다. 매 틱 호출.</summary>
        public void Tick(Entity entity, long currentTick)
        {
            var effects = entity.Get<StatusEffects>();
            if (effects == null)
            {
                return;
            }
            var stats = entity.Get<Stats>();

            for (int i = effects.Effects.Count - 1; i >= 0; i--)
            {
                var e = effects.Effects[i];
                if (e.ExpireTick >= 0 && currentTick >= e.ExpireTick)
                {
                    if (stats != null)
                    {
                        _statsSystem.RemoveModifiersBySourceId(stats, e.SourceId);
                    }
                    effects.Effects.RemoveAt(i);
                }
            }
        }

        /// <summary>효과를 명시적으로 제거한다(디스펠 등). 모디파이어도 함께 해제.</summary>
        public bool Remove(Entity entity, int effectId)
        {
            var effects = entity.Get<StatusEffects>();
            if (effects == null)
            {
                return false;
            }
            int idx = effects.Effects.FindIndex(e => e.EffectId == effectId);
            if (idx < 0)
            {
                return false;
            }

            var stats = entity.Get<Stats>();
            if (stats != null)
            {
                _statsSystem.RemoveModifiersBySourceId(stats, effects.Effects[idx].SourceId);
            }
            effects.Effects.RemoveAt(idx);
            return true;
        }

        private void AddModifiers(Stats stats, in StatusEffectData data, string sourceId, int stackCount)
        {
            if (stats == null || data.Modifiers == null)
            {
                return;
            }
            foreach (var m in data.Modifiers)
            {
                _statsSystem.AddModifier(stats, new StatModifier(m.StatType, m.Value * stackCount, m.Type, sourceId));
            }
        }
    }
}
```

- [ ] **Step 2: 컴파일 검증** — 클·서 양쪽 인스턴스 `refresh_unity`(scope=all, force) → idle → `read_console`(types=error) 0. 신규 3 `.cs.meta` 생성 확인.

---

## Task 4: LOP-Shared — EditMode 테스트

- [ ] **Step 1: `Tests/EditMode/StatusEffectSystemTests.cs` 생성** (Write). 헬퍼: Stats(base 세팅)+StatusEffects를 단 Entity 생성, `new StatsSystem()`/`new StatusEffectSystem(stats)`.
  - `Apply_Duration_AddsModifier_IncreasesValue`: base Dex=10, haste(Duration 5틱, [{Dex,+0.3,PercentAdd}]) @tick0 → `GetValue(Dex)≈13`, Effects.Count==1.
  - `Tick_BeforeExpire_StaysActive`: 위에서 Tick @tick4 → 여전히 ≈13, count 1.
  - `Tick_AtExpire_RemovesModifier_Reverts`: Tick @tick5 → `GetValue(Dex)==10`, count 0, `stats.Modifiers` 비어 있음.
  - `Apply_Refresh_ExtendsExpire`: @0 적용(expire 5) → @3 재적용(Refresh, expire 8) → Tick@5 활성 → Tick@8 제거.
  - `Apply_StackMagnitude_ScalesAndCaps`: StackMagnitude, MaxStacks=3, [{Strength,+2,Flat}], base Str=10. 적용×1→12, ×2→14, ×3→16, ×4→16(캡), StackCount==3.
  - `Remove_Explicit_CleansUp`: Infinite 버프 적용 → `Remove(effectId)` true → 값 복귀, count 0.
  - `Apply_Instant_PermanentBase_NoTracking`: Instant [{Strength,+5,Flat}] → base Str 10→15, `GetValue==15`, Effects.Count==0.
  - 부동소수 비교는 `Is.EqualTo(...).Within(1e-4f)`. statType은 `(int)EntityStatType.Dexterity`/`.Strength`.
- [ ] **Step 2: 테스트 실행** — `run_tests`(EditMode, LOP-Shared 필터) 또는 Test Runner로 전부 green. `.cs`+`.cs.meta` 생성 확인.

---

## Task 5: 종합 컴파일 검증
- [ ] **Step 1: 양쪽 재스캔** — Client·Server 각 `refresh_unity`(scope=all, force, wait_for_ready) → idle → `read_console`(types=error) **0**. (LOP-Shared 추가가 클·서에 전파되어도 신규 타입은 미사용이라 무영향 확인.)
- [ ] **Step 2: 신규 .meta 확인** — `StatusEffectData.cs.meta`/`StatusEffects.cs.meta`/`StatusEffectSystem.cs.meta` + `StatusEffectSystemTests.cs.meta` 생성됨.

---

## Task 6: 커밋
- [ ] **Step 1: LOP-Shared 코드+테스트** — `git add Runtime/Scripts/Game/StatusEffect/ Tests/EditMode/StatusEffectSystemTests.cs*`
```
feat(world): StatusEffect core — anemic data + system (LOP-Shared)

Add pure-C# StatusEffect core: StatusEffectData config struct,
StatusEffects/ActiveEffect data component, StatusEffectSystem
(Apply/Tick/Remove). Effects grant Stats modifiers tagged by a
deterministic per-effect SourceId and are bulk-removed on expiry via
StatsSystem.RemoveModifiersBySourceId. Duration/Infinite + Refresh/
StackMagnitude stacking; Instant = permanent base change. Periodic
DoT + Luban + in-game wiring deferred. EditMode tests cover the
apply/expire/stack/remove lifecycle.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```
- [ ] **Step 2: Client 문서** — `git add docs/superpowers/specs/2026-06-26-ability-statuseffect-world-core-design.md docs/superpowers/plans/2026-06-26-statuseffect-core.md`
```
docs(world): Ability/StatusEffect World Core spec + StatusEffect Phase 1 plan

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
```

---

## Task 7: 검증 + 머지 (사용자)
- [ ] **Step 1: 최종 컴파일** — 양쪽 `read_console` 에러 0. StatusEffect 테스트 green.
- [ ] **Step 2: 머지/푸시(사용자 요청 시)** — LOP-Shared → Client 순 `--no-ff`. 런타임 거동 변화 0(순수 추가, 미배선)이라 플레이 검증 불요 — 테스트 green이 검증. 머지·push는 사용자 요청 시에만.

---

## Self-Review (작성자 체크)
- **Spec 커버리지(Phase 1):** 설정=Task1 / 컴포넌트=Task2 / 시스템=Task3 / 테스트=Task4 / 검증=Task5 / 커밋=Task6. ✅
- **순수 추가:** LOP-Shared 신규 타입만, 클·서 코드 0줄. 신규 타입 미사용이라 런타임 거동 0 변화. ✅
- **anemic:** 컴포넌트(`StatusEffects`/`ActiveEffect`)=데이터만, 로직 전부 `StatusEffectSystem`. ✅
- **Stats 연동 정합:** 실측 `StatModifier(int,float,ModifierType,string)` + `RemoveModifiersBySourceId(string)` 그대로 사용. Source enum 없음 — SourceId가 추적키. ✅
- **결정론:** SourceId=`"se:{effectId}"`(난수/Guid 없음), 틱=long 비교. ✅
- **범위 정직(YAGNI):** periodic/DoT·Instant 데미지(Health 의존)·Luban·이동→Stats·인게임 배선·Ability = 명시 제외(spec Out of Scope/Open Question). Phase 1=순수 Stats 모디파이어 생명주기만. ✅
- **테스트 가치:** 순수 C#이라 Unity 없이 apply/expire/stack/refresh/remove/instant 검증 — 레거시 MonoBehaviour Status론 불가했던 것. ✅
- **EOL/.meta/픽스처:** Write=신규, mass sed 없음, .cs+.meta 함께, 픽스처 제외. ✅
- **헤이스트 caveat:** 테스트는 Dexterity로 생명주기 검증, 인게임 "빨라짐"은 MoveSpeed 배선 후(spec Open Question)임을 결정 7에 명시. ✅
