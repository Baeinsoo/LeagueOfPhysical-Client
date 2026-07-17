# 전투 히트 해소 Part 1 — 닷지 on-hit 게이트 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 공격의 히트를 1회 해소해 "명중(닷지 안 된) 대상"만 on-hit 효과(넉백)에 넘겨, 닷지한 대상이 넉백당하는 버그를 표준 구조(구조 ②b)로 고친다.

**Architecture:** 데미지 효과가 "히트 정의자" — 대상 탐색 + per-attack 닷지 판정으로 명중 대상을 `AttackHitContext`(발동당 1개)에 기록. 넉백 효과는 "on-hit 라이더" — 그 명중 대상만 밀고, 자기 overlap/닷지 재확인을 하지 않는다. 전부 서버권위 경로라 클라 예측 무영향.

**Tech Stack:** C# (LOP-Shared 순수 concrete), GameFramework.World, VContainer(서버 DI), NUnit EditMode.

## Global Constraints

- **범위 = Part 1만** (닷지 on-hit 게이트). 크리/회피 상수 MasterData 승격(Part 2)은 별도 plan.
- **작업 저장소**: `LeagueOfPhysical-Shared`(핵심 코드+테스트), `LeagueOfPhysical-Client`·`LeagueOfPhysical-Server`(AbilityDataProvider 매핑 2줄). 피처 브랜치 `feature/combat-hit-resolution`(클라는 이미 있음; Shared/Server는 생성).
- **크리/회피 clamp 상수는 이 plan에서 하드코딩 유지** (0.05/0.95 dodge, 0.05/0.50 crit, 1.25/1.75 mult) — Part 2에서 config로 승격.
- **넉백 Luban 데이터(Range/Angle)는 이 plan에서 안 건드림** — C# `KnockbackEffect`가 두 필드를 버리고 매퍼가 안 읽을 뿐, Luban bean은 vestigial로 남긴다(Part 2 MasterData 작업 때 정리). 바이너리 Excel 편집 없음.
- **결정론**: 닷지 seed = `hash(matchSeed, tick, attacker, target)`(effectIndex 없음), 크리 seed = `hash(..., effectIndex)`(별도 스트림). 클·서 공유 concrete라 자동 일치.
- **effect 순서 규약**: 어빌리티 effect 리스트에서 **DamageEffect가 KnockbackEffect보다 앞**(라이더가 정의자 결과를 읽음). 코드 주석으로 박제.
- **EditMode 실행**: UnityMCP `run_tests`(EditMode), `unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4"`(클라 에디터가 Shared 패키지 테스트 실행). 서버 에디터는 이 세션에서 미연결 → 서버 컴파일/플레이 검증은 Task 4에서 deferred 처리.

---

## 파일 구조

**LOP-Shared (`Runtime/Scripts/Game/`):**
- Create `Ability/AttackHitContext.cs` — 발동당 명중 대상 집합
- Modify `Ability/AbilityEffectContext.cs` — `AttackHitContext HitContext` 필드 추가
- Modify `Ability/AbilityEffectExecutor.cs` — 발동당 `AttackHitContext` 생성·전파
- Modify `LOPCombatSystem.cs` — `IsDodged`(per-attack) 추출 + `Attack`가 명중 기록(hitContext 파라미터)
- Modify `Ability/AbilityEffect.cs` — `KnockbackEffect`에서 `Range`/`Angle` 제거
- Modify `DamageEffectHandler.cs` — `Attack`에 `ctx.HitContext` 전달
- Modify `KnockbackEffectHandler.cs` — 라이더화(명중 대상만, `IOverlapQuery` 제거)

**LOP-Shared 테스트 (`Tests/EditMode/`):**
- Create `AttackHitContextTests.cs`
- Modify `LOPCombatSystemTests.cs` (새 `Attack` 시그니처 + `IsDodged`/명중 기록 테스트)
- Rewrite `KnockbackEffectHandlerTests.cs` (라이더 기반)
- Modify `DamageEffectHandlerTests.cs` (새 ctx 시그니처)

**LOP-Client / LOP-Server:**
- Modify `Assets/Scripts/Game/AbilityDataProvider.cs` (양쪽) — `KnockbackEffect` 생성에서 Range/Angle 제거

---

## Task 1: AttackHitContext + 컨텍스트/executor 배선

발동당 "명중 대상 집합"을 담는 컨테이너를 만들고, `AbilityEffectContext`에 실어 executor가 발동당 1개 생성·전파한다.

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AttackHitContext.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilityEffectContext.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilityEffectExecutor.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/AttackHitContextTests.cs`

**Interfaces:**
- Produces: `LOP.AttackHitContext` { `void MarkLanded(string)`, `bool Landed(string)`, `IReadOnlyCollection<string> LandedTargets` }; `AbilityEffectContext` 5-인자 ctor `(Entity caster, Entity target, long currentTick, int effectIndex, AttackHitContext hitContext)` + 읽기전용 필드 `HitContext`.

- [ ] **Step 1: Shared/Server 브랜치 생성**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared && git checkout -b feature/combat-hit-resolution
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server && git checkout -b feature/combat-hit-resolution
```
Expected: 각 `Switched to a new branch`.

- [ ] **Step 2: 실패 테스트 작성 (AttackHitContext)**

Create `LeagueOfPhysical-Shared/Tests/EditMode/AttackHitContextTests.cs`:
```csharp
using System.Linq;
using NUnit.Framework;

namespace LOP.Tests
{
    public class AttackHitContextTests
    {
        [Test]
        public void Landed_false_until_marked()
        {
            var hit = new AttackHitContext();
            Assert.IsFalse(hit.Landed("B"));
        }

        [Test]
        public void MarkLanded_records_target()
        {
            var hit = new AttackHitContext();
            hit.MarkLanded("B");
            Assert.IsTrue(hit.Landed("B"));
            Assert.IsFalse(hit.Landed("C"));
        }

        [Test]
        public void LandedTargets_enumerates_marked_unique()
        {
            var hit = new AttackHitContext();
            hit.MarkLanded("B");
            hit.MarkLanded("B");
            hit.MarkLanded("C");
            Assert.AreEqual(new[] { "B", "C" }, hit.LandedTargets.OrderBy(x => x).ToArray());
        }
    }
}
```

- [ ] **Step 3: 테스트 실패 확인**

UnityMCP `run_tests`(EditMode, 필터 `AttackHitContextTests`, client 인스턴스).
Expected: FAIL — `AttackHitContext` 타입 없음(컴파일 에러).

- [ ] **Step 4: AttackHitContext 구현**

Create `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AttackHitContext.cs`:
```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 한 어빌리티 발동(공격)에서 "명중한(닷지 안 된) 대상 id 집합". 히트 정의자(데미지)가 기록하고
    /// on-hit 라이더(넉백 등)가 읽는다 — 효과들이 같은 히트 결과를 공유하게 하는 발동당 컨텍스트.
    /// </summary>
    public sealed class AttackHitContext
    {
        private readonly HashSet<string> _landed = new HashSet<string>();

        public void MarkLanded(string targetId) => _landed.Add(targetId);
        public bool Landed(string targetId) => _landed.Contains(targetId);
        public IReadOnlyCollection<string> LandedTargets => _landed;
    }
}
```

- [ ] **Step 5: AbilityEffectContext에 HitContext 필드 추가**

Replace `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilityEffectContext.cs` body with:
```csharp
using GameFramework.World;

namespace LOP
{
    /// <summary>
    /// effect 핸들러에 넘기는 발동 맥락(누가/누구에게/언제 + 발동당 히트 결과). 핸들러가 필요한 것만 읽는다.
    /// <see cref="HitContext"/>는 히트 정의자(데미지)가 명중 대상을 기록하고 on-hit 라이더(넉백)가 읽는 공유 채널.
    /// </summary>
    public readonly struct AbilityEffectContext
    {
        public readonly Entity Caster;
        public readonly Entity Target;
        public readonly long CurrentTick;
        public readonly int EffectIndex;   // 효과 리스트 내 위치 — 크리 RNG sub-stream 구분용
        public readonly AttackHitContext HitContext;

        public AbilityEffectContext(Entity caster, Entity target, long currentTick, int effectIndex, AttackHitContext hitContext)
        {
            Caster = caster;
            Target = target;
            CurrentTick = currentTick;
            EffectIndex = effectIndex;
            HitContext = hitContext;
        }
    }
}
```

- [ ] **Step 6: Executor가 발동당 AttackHitContext 생성·전파**

In `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilityEffectExecutor.cs`:

`DriveActiveEntity`의 ctx 생성부를 교체 — 발동당 `AttackHitContext` 하나 생성:
```csharp
        public void DriveActiveEntity(Entity caster, long currentTick)
        {
            var active = caster?.Get<Abilities>()?.ActiveAbility;
            if (active == null || active.Value.Phase != AbilityPhase.Active)
            {
                return;
            }
            var hit = new AttackHitContext();   // 이 발동의 명중 대상 공유 채널
            var ctx = new AbilityEffectContext(caster, active.Value.Target, currentTick, 0, hit);
            if (currentTick == active.Value.StartupEndTick)
            {
                OnActiveEnter(ctx, active.Value.Effects);   // 진입 1회 — 데미지(히트 정의) → 넉백(라이더)
            }
            OnActiveTick(ctx, active.Value.Effects);         // 매 틱 — 대시 push(지속)
        }
```

`OnActiveEnter`·`OnActiveTick`의 per-effect ctx 생성부에 `ctx.HitContext` 전파(두 메서드 동일 패턴):
```csharp
                    var effectCtx = new AbilityEffectContext(
                        ctx.Caster, ctx.Target, ctx.CurrentTick, i, ctx.HitContext);
```
(두 메서드 각각의 `new AbilityEffectContext(...)` 를 위 5-인자 형태로.)

- [ ] **Step 7: 기존 AbilityEffectContext 생성처(테스트) 컴파일 수복**

`ctx` 를 직접 만드는 기존 테스트가 4-인자 ctor를 쓰면 컴파일이 깨진다. 확인:
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
grep -rn "new AbilityEffectContext(" Tests Runtime | grep -v ".meta"
```
찾은 각 호출을 5-인자로 갱신 — 히트 컨텍스트가 무관한 테스트는 `new AttackHitContext()`를 새로 넘긴다. (예: `KnockbackEffectHandlerTests`/`DamageEffectHandlerTests`/`StatusEffectApplyEffectHandlerTests`의 `Ctx(...)` 헬퍼 — 해당 테스트를 손대는 Task 2·3에서 최종 형태로 다시 씀. 이 스텝에선 *컴파일만* 통과하도록 각 호출에 `new AttackHitContext()` 인자 추가.)

- [ ] **Step 8: 테스트 통과 확인**

UnityMCP `run_tests`(EditMode, 필터 `AttackHitContextTests`, client 인스턴스) + 전체 EditMode 그린(컴파일 수복 확인).
Expected: `AttackHitContextTests` 3 PASS, 기존 테스트 회귀 없음.

- [ ] **Step 9: Commit (+.meta)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/Ability/AttackHitContext.cs Runtime/Scripts/Game/Ability/AttackHitContext.cs.meta \
        Runtime/Scripts/Game/Ability/AbilityEffectContext.cs \
        Runtime/Scripts/Game/Ability/AbilityEffectExecutor.cs \
        Tests/EditMode/AttackHitContextTests.cs Tests/EditMode/AttackHitContextTests.cs.meta \
        Tests/EditMode/*.cs
git commit -m "feat(combat): AttackHitContext + 발동당 히트결과 배선(효과 공유 채널)"
```

---

## Task 2: LOPCombatSystem 히트 정의자 (per-attack 닷지 + 명중 기록) + DamageEffectHandler 전달

닷지를 `effectIndex` 없는 per-attack 판정으로 추출하고, `Attack`이 명중(닷지 안 된) 대상을 `AttackHitContext`에 기록하도록 한다. 데미지 핸들러는 `ctx.HitContext`를 `Attack`에 넘긴다.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LOPCombatSystem.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/DamageEffectHandler.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/LOPCombatSystemTests.cs`, `DamageEffectHandlerTests.cs`

**Interfaces:**
- Consumes: `AttackHitContext`(Task 1), `AbilityEffectContext.HitContext`.
- Produces: `LOPCombatSystem.IsDodged(Entity attacker, Entity target, long tick, ulong matchSeed) → bool`; `LOPCombatSystem.Attack(Entity attacker, Entity target, int baseDamage, long tick, int effectIndex, ulong matchSeed, AttackHitContext hitContext)` — 명중 시 `hitContext.MarkLanded(target.Id)`.

- [ ] **Step 1: 실패 테스트 작성 (IsDodged + 명중 기록)**

Add to `LeagueOfPhysical-Shared/Tests/EditMode/LOPCombatSystemTests.cs` (파일 상단 `NewCombat`/`Player` 헬퍼 재사용):
```csharp
        [Test]
        public void IsDodged_is_deterministic_and_effectIndex_independent()
        {
            var a = NewCombat();
            var atk = Player("A", a.stats, 10, 10);
            var tgt = Player("B", a.stats, 10, 10, 100);
            bool d1 = a.combat.IsDodged(atk, tgt, 7, 999UL);
            bool d2 = a.combat.IsDodged(atk, tgt, 7, 999UL);
            Assert.AreEqual(d1, d2);   // 같은 입력 = 같은 결과 (effectIndex 파라미터 자체가 없음)
        }

        [Test]
        public void Attack_marks_landed_iff_not_dodged()
        {
            var (buf, combat, stats) = NewCombat();
            var atk = Player("A", stats, 20, 10);
            var tgt = Player("B", stats, 10, 10, 100);
            var hit = new AttackHitContext();
            combat.Attack(atk, tgt, 10, 5, 0, 12345UL, hit);

            var evt = (DamageDealtEvent)buf.Snapshot[0];
            Assert.AreEqual(!evt.isDodged, hit.Landed("B"));   // 명중 기록 == 닷지 아님
        }
```

- [ ] **Step 2: 기존 Attack 호출부 시그니처 갱신 (컴파일 실패 유도)**

`LOPCombatSystemTests.cs`의 기존 `combat.Attack(...)` 호출(6-인자) 전부에 마지막 인자 `new AttackHitContext()` 추가(7-인자). 예:
```csharp
            combat.Attack(Player("A", stats, 20, 10), target, 10, 5, 0, 12345UL, new AttackHitContext());
```
(파일 내 모든 `.Attack(` 호출 — `grep -n "\.Attack(" Tests/EditMode/LOPCombatSystemTests.cs` 로 확인.)

- [ ] **Step 3: 테스트 실패 확인**

UnityMCP `run_tests`(EditMode, 필터 `LOPCombatSystemTests`, client 인스턴스).
Expected: FAIL — `IsDodged` 없음 / `Attack` 7-인자 오버로드 없음(컴파일 에러).

- [ ] **Step 4: LOPCombatSystem 구현**

In `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LOPCombatSystem.cs`:

(a) seed 헬퍼 추가(클래스 내부, private static):
```csharp
        // 공격-대상 당 결정론 seed(effectIndex 없음). 닷지는 이걸, 크리는 여기에 effectIndex를 더한 seed를 쓴다.
        private static ulong AttackSeed(ulong matchSeed, long tick, string attackerId, string targetId)
            => GameFramework.Hashing.Combine(
                   GameFramework.Hashing.Combine(
                       GameFramework.Hashing.Combine(matchSeed, (ulong)tick),
                       GameFramework.Hashing.Fnv1a64(attackerId)),
                   GameFramework.Hashing.Fnv1a64(targetId));
```

(b) `IsDodged`(per-attack, dex를 stats에서 읽음) 추가 — 기존 `IsDodge(int,int,ref rng)`는 삭제:
```csharp
        /// <summary>이 공격이 대상에게 회피당하는가(공격당 1회, effectIndex 무관). 결정론.</summary>
        public bool IsDodged(GameFramework.World.Entity attacker, GameFramework.World.Entity target, long tick, ulong matchSeed)
        {
            int attackerDex = DexOf(attacker);
            int targetDex = DexOf(target);
            var rng = new GameFramework.DeterministicRandom(AttackSeed(matchSeed, tick, attacker.Id, target.Id));
            float dodgeChance = (float)targetDex / (attackerDex + targetDex);
            dodgeChance = Mathf.Clamp(dodgeChance, 0.05f, 0.95f);
            return rng.Range(0.0f, 1.0f) < dodgeChance;
        }

        private int DexOf(GameFramework.World.Entity e)
        {
            var s = e?.Get<GameFramework.World.Stats>();
            return s != null ? Mathf.RoundToInt(statsSystem.GetValue(s, (int)GameFramework.World.EntityStatType.Dexterity)) : 0;
        }
```

(c) `Attack`을 새 시그니처로 — 닷지=IsDodged, 크리=별도 seed, 명중 기록:
```csharp
        public void Attack(GameFramework.World.Entity attacker, GameFramework.World.Entity target,
                           int baseDamage, long tick, int effectIndex, ulong matchSeed, AttackHitContext hitContext)
        {
            bool attackerIsPlayer = attacker?.Has<GameFramework.World.Ownership>() == true;
            bool targetIsPlayer = target?.Has<GameFramework.World.Ownership>() == true;
            if (!attackerIsPlayer && !targetIsPlayer)
            {
                return;
            }

            GameFramework.World.Health health = target?.Get<GameFramework.World.Health>();
            if (health == null)
            {
                Debug.LogWarning($"[World] Attack: Health not found for entity {target?.Id}");
                return;
            }
            if (health.IsDead)
            {
                Debug.LogWarning($"Target {target.Id} is already dead.");
                return;
            }

            bool isDodged = IsDodged(attacker, target, tick, matchSeed);

            int attackerStrength = StrengthOf(attacker);
            int targetStrength = StrengthOf(target);
            int damage = (baseDamage + attackerStrength) * 3;   // 밸런스: 데미지 3배(크리 배수 적용 전)

            bool isCritical = false;
            if (!isDodged)
            {
                // 크리는 per-hit(effectIndex 포함 seed) — 닷지와 다른 스트림
                var critRng = new GameFramework.DeterministicRandom(
                    GameFramework.Hashing.Combine(AttackSeed(matchSeed, tick, attacker.Id, target.Id), (ulong)effectIndex));
                isCritical = IsCritical(attackerStrength, targetStrength, ref critRng);
                if (isCritical)
                {
                    damage = Mathf.RoundToInt(damage * critRng.Range(1.25f, 1.75f));
                }
                healthSystem.TakeDamage(health, damage);
                hitContext?.MarkLanded(target.Id);   // 명중 = on-hit 라이더 대상
            }

            int dealtAmount = isDodged ? 0 : damage;
            worldEventBuffer.Append(new GameFramework.World.DamageDealtEvent(
                targetId:   target.Id,
                attackerId: attacker.Id,
                amount:     dealtAmount,
                isCritical: isCritical,
                isDodged:   isDodged));

            if (health.IsDead)
            {
                worldEventBuffer.Append(new GameFramework.World.DeathEvent(
                    victimId:   target.Id,
                    attackerId: attacker.Id));
            }
        }

        private int StrengthOf(GameFramework.World.Entity e)
        {
            var s = e?.Get<GameFramework.World.Stats>();
            return s != null ? Mathf.RoundToInt(statsSystem.GetValue(s, (int)GameFramework.World.EntityStatType.Strength)) : 0;
        }
```
`IsCritical(int,int,ref rng)`는 현행 유지(하드코딩 clamp 0.05/0.50).

- [ ] **Step 5: DamageEffectHandler가 hitContext 전달**

In `LeagueOfPhysical-Shared/Runtime/Scripts/Game/DamageEffectHandler.cs`, `Attack` 호출부를 교체:
```csharp
                combatSystem.Attack(ctx.Caster, target, effect.Amount, ctx.CurrentTick, ctx.EffectIndex, matchSeed.Value, ctx.HitContext);
```

- [ ] **Step 6: DamageEffectHandlerTests ctx 시그니처 갱신**

`DamageEffectHandlerTests.cs`의 `AbilityEffectContext` 생성(Task 1 Step 7에서 임시 통과시킨 것)을, 명중 기록을 검증하도록 확정 — 최소한 컴파일되게 `new AttackHitContext()` 인자가 들어가 있는지 확인. (데미지 핸들러의 명중 기록은 이미 `Attack`이 하므로 별도 신규 테스트는 Task 3의 통합에서.)

- [ ] **Step 7: 테스트 통과 확인**

UnityMCP `run_tests`(EditMode, 필터 `LOPCombatSystemTests`, client 인스턴스) + 전체 그린.
Expected: 신규 2 PASS + 기존 회귀 없음.

- [ ] **Step 8: Commit**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/LOPCombatSystem.cs Runtime/Scripts/Game/DamageEffectHandler.cs Tests/EditMode/*.cs
git commit -m "feat(combat): 데미지=히트 정의자 — IsDodged(per-attack) + 명중 대상 기록"
```

---

## Task 3: KnockbackEffect 라이더화 (버그 픽스)

넉백을 on-hit 라이더로 — `AttackHitContext`의 명중 대상만 밀고, 자기 overlap/부채꼴/닷지 판정을 제거한다. `KnockbackEffect`에서 `Range`/`Angle`을 뺀다.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilityEffect.cs` (KnockbackEffect)
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/KnockbackEffectHandler.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/AbilityDataProvider.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/AbilityDataProvider.cs`
- Test: Rewrite `LeagueOfPhysical-Shared/Tests/EditMode/KnockbackEffectHandlerTests.cs`

**Interfaces:**
- Consumes: `AttackHitContext.LandedTargets`(Task 1), `AbilityEffectContext.HitContext`.
- Produces: `KnockbackEffect(float strength, int durationTicks, float decayPerTick)`; `KnockbackEffectHandler(EntityRegistry entityRegistry)`.

- [ ] **Step 1: 실패 테스트 작성 (라이더 기반)**

Replace `LeagueOfPhysical-Shared/Tests/EditMode/KnockbackEffectHandlerTests.cs` with:
```csharp
using System.Numerics;
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class KnockbackEffectHandlerTests
    {
        private static Entity Caster(string id, EntityRegistry reg)
        {
            var e = new Entity(id);
            e.Add(new GameFramework.World.Transform { Position = Vector3.Zero, Rotation = Quaternion.Identity });
            e.Add(new MotionContributions());
            reg.Add(e);
            return e;
        }

        private static Entity Target(string id, EntityRegistry reg, Vector3 pos)
        {
            var e = new Entity(id);
            e.Add(new GameFramework.World.Transform { Position = pos, Rotation = Quaternion.Identity });
            e.Add(new MotionContributions());
            reg.Add(e);
            return e;
        }

        private static AbilityEffectContext Ctx(Entity caster, AttackHitContext hit)
            => new AbilityEffectContext(caster, null, 5L, 0, hit);

        private static KnockbackEffect Effect() => new KnockbackEffect(5f, 12, 0.8f);   // strength, durationTicks, decayPerTick

        [Test]
        public void Pushes_only_landed_targets()
        {
            var reg = new EntityRegistry();
            var caster = Caster("A", reg);
            var hitTarget = Target("B", reg, new Vector3(0, 0, 3));
            var missTarget = Target("C", reg, new Vector3(0, 0, 3));
            var hit = new AttackHitContext();
            hit.MarkLanded("B");   // B만 명중, C는 닷지

            new KnockbackEffectHandler(reg).OnActiveEnter(Ctx(caster, hit), Effect());

            Assert.AreEqual(1, hitTarget.Get<MotionContributions>().Items.Count);   // 명중 → 넉백
            Assert.AreEqual(0, missTarget.Get<MotionContributions>().Items.Count);  // 닷지 → 넉백 없음
        }

        [Test]
        public void No_landed_targets_pushes_nothing()
        {
            var reg = new EntityRegistry();
            var caster = Caster("A", reg);
            var target = Target("B", reg, new Vector3(0, 0, 3));
            new KnockbackEffectHandler(reg).OnActiveEnter(Ctx(caster, new AttackHitContext()), Effect());
            Assert.AreEqual(0, target.Get<MotionContributions>().Items.Count);
        }

        [Test]
        public void Null_hitContext_pushes_nothing()
        {
            var reg = new EntityRegistry();
            var caster = Caster("A", reg);
            Assert.DoesNotThrow(() =>
                new KnockbackEffectHandler(reg).OnActiveEnter(
                    new AbilityEffectContext(caster, null, 5L, 0, null), Effect()));
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

UnityMCP `run_tests`(EditMode, 필터 `KnockbackEffectHandlerTests`, client 인스턴스).
Expected: FAIL — `KnockbackEffect` 3-인자 ctor 없음 / `KnockbackEffectHandler(reg)` 1-인자 ctor 없음.

- [ ] **Step 3: KnockbackEffect에서 Range/Angle 제거**

In `LeagueOfPhysical-Shared/Runtime/Scripts/Game/Ability/AbilityEffect.cs`, `KnockbackEffect`를 교체:
```csharp
    /// <summary>대상을 공격자 반대 방향으로 민다(넉백) — on-hit 라이더. 히트 형상은 데미지 효과가 정의하고,
    /// 이 효과는 명중 대상(AttackHitContext)에만 작용한다(자체 타게팅 없음). 서버 핸들러가 Additive 기여 등록.</summary>
    public sealed class KnockbackEffect : AbilityEffect
    {
        public readonly float Strength;      // 초기 세기 v0
        public readonly int DurationTicks;   // 밀림 지속(활성 창)
        public readonly float DecayPerTick;  // 지수 감쇠(0<k<=1)

        public KnockbackEffect(float strength, int durationTicks, float decayPerTick)
        {
            Strength = strength;
            DurationTicks = durationTicks;
            DecayPerTick = decayPerTick;
        }
    }
```

- [ ] **Step 4: KnockbackEffectHandler 라이더화**

Replace `LeagueOfPhysical-Shared/Runtime/Scripts/Game/KnockbackEffectHandler.cs` with:
```csharp
namespace LOP
{
    /// <summary>
    /// <see cref="KnockbackEffect"/> 핸들러(공유 구현, 서버만 등록) — on-hit 라이더. 히트 정의자(데미지)가
    /// <see cref="AttackHitContext"/>에 기록한 명중 대상만 공격자 반대 방향으로 미는 Additive 기여를 등록한다.
    /// 자체 범위 탐색/닷지 판정 없음(히트 형상·명중 여부는 데미지가 정함). 위치는 World.Transform(진실원본).
    /// 클라 미등록 → 클라는 스냅샷으로 결과 수신(넉백 = 서버권위).
    /// </summary>
    public class KnockbackEffectHandler : AbilityEffectHandler<KnockbackEffect>
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public KnockbackEffectHandler(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        protected override void OnActiveEnter(AbilityEffectContext ctx, KnockbackEffect effect)
        {
            AttackHitContext hit = ctx.HitContext;
            if (hit == null)
            {
                return;
            }
            GameFramework.World.Transform casterTransform = ctx.Caster?.Get<GameFramework.World.Transform>();
            if (casterTransform == null)
            {
                return;
            }

            foreach (string id in hit.LandedTargets)
            {
                GameFramework.World.Entity target = entityRegistry.Get(id);
                GameFramework.World.Transform targetTransform = target?.Get<GameFramework.World.Transform>();
                if (targetTransform == null)
                {
                    continue;
                }
                MotionContributions contributions = target.Get<MotionContributions>();
                if (contributions == null)
                {
                    continue;
                }
                contributions.Items.Add(MotionContributionSystem.CreateRadialKnockback(
                    casterTransform.Position, targetTransform.Position,
                    effect.Strength, effect.DurationTicks, effect.DecayPerTick, ctx.CurrentTick));
            }
        }
    }
}
```

- [ ] **Step 5: 양쪽 AbilityDataProvider 매핑 갱신**

In BOTH `LeagueOfPhysical-Client/Assets/Scripts/Game/AbilityDataProvider.cs` AND `LeagueOfPhysical-Server/Assets/Scripts/Game/AbilityDataProvider.cs`, `MapEffects`의 KnockbackEffect case를 교체(Range/Angle 미전달 — Luban bean은 vestigial로 남음):
```csharp
                    case LOP.MasterData.KnockbackEffect k:
                        result.Add(new KnockbackEffect(k.Strength, k.DurationTicks, k.DecayPerTick));
                        break;
```

- [ ] **Step 6: 테스트 통과 확인**

UnityMCP `run_tests`(EditMode, 필터 `KnockbackEffectHandlerTests`, client 인스턴스) + 전체 그린.
Expected: 신규 3 PASS + 기존 회귀 없음. (클라 AbilityDataProvider 변경으로 클라도 컴파일 — `refresh_unity` 후 `read_console` 0 에러 확인.)

- [ ] **Step 7: Commit (Shared + Client)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/Ability/AbilityEffect.cs Runtime/Scripts/Game/KnockbackEffectHandler.cs Tests/EditMode/KnockbackEffectHandlerTests.cs
git commit -m "fix(combat): 넉백을 on-hit 라이더로 — 명중 대상만 밀기(닷지 게이트). Range/Angle 제거"
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/AbilityDataProvider.cs
git commit -m "fix(combat): 클라 AbilityDataProvider — KnockbackEffect Range/Angle 미전달"
```

---

## Task 4: 서버 배선 확인 + 회귀 검증

서버 컴파일(핸들러 새 ctor DI 해소)과 플레이 회귀를 확인한다. 서버 코드 변경은 `AbilityDataProvider` 한 줄뿐.

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/AbilityDataProvider.cs` (Task 3 Step 5에서 이미 편집)

- [ ] **Step 1: 서버 AbilityDataProvider 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/AbilityDataProvider.cs
git commit -m "fix(combat): 서버 AbilityDataProvider — KnockbackEffect Range/Angle 미전달"
```

- [ ] **Step 2: 서버 컴파일 확인 (서버 에디터)**

서버 Unity 에디터가 연결돼 있으면 `refresh_unity` + `read_console`(server 인스턴스)로 0 에러 확인. **연결 안 돼 있으면 deferred** — `KnockbackEffectHandler`의 ctor가 `IOverlapQuery`를 뺐지만 VContainer `Register<KnockbackEffectHandler>()`는 새 ctor(EntityRegistry만)를 자동 해소하므로 DI 명시 변경 불필요. `DamageEffectHandler`/`LOPCombatSystem` 등록도 무변경. 정적으로 컴파일 예상.
Expected: 0 에러(또는 deferred 표기).

- [ ] **Step 3: 플레이 회귀 검증 (서버 에디터 + 사람)**

클·서 동시 Play, 공격 반복:
- **닷지 뜬 대상** → 데미지 0 **그리고 넉백 없음**(밀리지 않음) ← 이 슬라이스의 핵심 수정
- **명중 대상** → 데미지 + 넉백 정상
- 여러 대상 중 일부만 닷지 → 명중 대상만 넉백
- 콘솔 예외/경고 없음
Expected: 회귀 없음 + 닷지→넉백 버그 소멸. (서버 에디터/사람 필요 → 이 세션에서 안 되면 최종 human 게이트.)

---

## 완료 기준 (Definition of Done)

- [ ] `AttackHitContextTests` 3 + `LOPCombatSystemTests` 신규 2 + `KnockbackEffectHandlerTests` 3 green, 전체 Shared EditMode 회귀 없음.
- [ ] 닷지한 대상은 넉백 안 당함(명중 대상만 `MotionContributions` 등록) — EditMode + 플레이 확인.
- [ ] 크리/회피 clamp 상수는 아직 하드코딩(Part 2 대상).
- [ ] 넉백 Luban bean의 Range/Angle은 vestigial로 남음(C#·매퍼 미사용) — Part 2에서 정리.
- [ ] 클·서 클린 컴파일.

## 통합 / 머지

3 저장소(Shared/Server/Client) 각 `feature/combat-hit-resolution`를 `--no-ff`로 main에 머지(Shared→Server→Client). 플레이 게이트 통과 후.

## 후속 — Part 2 (별도 plan)

크리/회피 상수 → `TbCombatConfig` MasterData 승격 + `CombatConfig` struct/provider + DI + 넉백 Luban Range/Angle 정리. **선행: Luban 단일-행 config 테이블 저작 규약 + `__tables__`/`__beans__` 편집 방식 조사** (바이너리 Excel — `_gen_scratch/author_*.py` 패턴 확장). 별도 brainstorm 불필요(설계는 spec Part 2에 확정), 별도 plan만.
