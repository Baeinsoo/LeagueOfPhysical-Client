# A2.2a — 전투 해소를 LOP-Shared 공유 concrete로 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `LOPCombatSystem`(전투 해소)을 서버 → LOP-Shared 공유 concrete로 옮기고, 시그니처를 `Attack(World.Entity, World.Entity, long tick, int effectIndex, ulong matchSeed)`로 바꿔 클·서가 같은 코드로 같은 결과를 내게 한다. `ICombatSystem` 제거. 이동으로 전투 해소가 EditMode 단위테스트 가능해진다.

**Architecture:** LOP-Shared에 concrete `LOPCombatSystem`(deps: `WorldEventBuffer`/`HealthSystem`/`StatsSystem`, 씨앗은 param). 서버 `DamageEffectHandler`가 히트 판정 후 `ctx.Caster`(attacker World.Entity) + `registry.Get(targetId)`(target World.Entity) + `matchSeed.Value`로 공유 resolver 호출. **중복 타입은 서버에서만** 생기므로(클라엔 없음) LOP-Shared 추가(Task 1)는 클라 인스턴스로, 서버 정리(Task 2)는 서버 인스턴스로 검증.

**Tech Stack:** C# / Unity / VContainer / NUnit. LOP-Shared·GameFramework = file: 패키지.

## Global Constraints

- **3개 저장소**, 각각 피처 브랜치 `feature/a2-2a-combat-resolution-shared`(main 직접 금지):
  - LOP-Shared `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared`
  - Server `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server`
  - Client `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client`(문서만 — 코드 무변경; 브랜치 이미 있으면 재사용)
- **UnityMCP `unity_instance` 항상 명시:** 클 `LeagueOfPhysical-Client@de70658b9450cbb4` / 서 `LeagueOfPhysical-Server@f99391fa2dbaaf3c`.
- **동작 보존:** 전투 수식·밸런스·키 값 불변. `World.Entity.Id`는 A2.1 `LOPEntity.entityId`와 **동일 문자열**(등록 키) — 키 값 불변 보장(Step에서 확인).
- `Mathf`/`Debug` 유지(LOP-Shared UnityEngine 참조 가능). World 타입 풀 네임스페이스 한정.
- **Task 1↔2 사이 서버는 일시적으로 컴파일 깨짐**(중복 타입) — 정상. Task 1은 클라 인스턴스로만 검증, Task 2가 서버 정리.
- Client working tree 기존 무관 변경(Art/prefab/GraphicsSettings) 커밋 금지.

---

### Task 1: LOP-Shared — 공유 `LOPCombatSystem` + EditMode 테스트 (TDD)

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LOPCombatSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/LOPCombatSystemTests.cs`

**Interfaces:**
- Produces: `class LOP.LOPCombatSystem` (concrete, no interface). ctor `(WorldEventBuffer, HealthSystem, StatsSystem)`. `void Attack(GameFramework.World.Entity attacker, GameFramework.World.Entity target, long tick, int effectIndex, ulong matchSeed)`. `bool IsDodge/IsCritical(int,int, ref DeterministicRandom)`.

- [ ] **Step 1: 실패 테스트** — `Tests/EditMode/LOPCombatSystemTests.cs`
```csharp
using GameFramework;
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class LOPCombatSystemTests
    {
        private static (WorldEventBuffer buf, LOPCombatSystem combat, StatsSystem stats) NewCombat()
        {
            var buf = new WorldEventBuffer();
            var stats = new StatsSystem();
            var combat = new LOPCombatSystem(buf, new HealthSystem(), stats);
            return (buf, combat, stats);
        }

        private static Entity Player(string id, StatsSystem stats, int str, int dex, int? hp = null)
        {
            var e = new Entity(id);
            e.Add(new Ownership("owner-" + id));
            var s = new Stats();
            stats.SetBase(s, (int)EntityStatType.Strength, str);
            stats.SetBase(s, (int)EntityStatType.Dexterity, dex);
            e.Add(s);
            if (hp.HasValue) e.Add(new Health(hp.Value));
            return e;
        }

        [Test]
        public void Attack_is_deterministic_for_same_inputs()
        {
            var a = NewCombat(); var b = NewCombat();
            a.combat.Attack(Player("A", a.stats, 20, 10), Player("B", a.stats, 10, 10, 100), 5, 0, 12345UL);
            b.combat.Attack(Player("A", b.stats, 20, 10), Player("B", b.stats, 10, 10, 100), 5, 0, 12345UL);

            var da = (DamageDealtEvent)a.buf.Snapshot[0];
            var db = (DamageDealtEvent)b.buf.Snapshot[0];
            Assert.AreEqual(da.amount, db.amount);
            Assert.AreEqual(da.isCritical, db.isCritical);
            Assert.AreEqual(da.isDodged, db.isDodged);
        }

        [Test]
        public void Attack_event_matches_health_change()
        {
            var (buf, combat, stats) = NewCombat();
            var target = Player("B", stats, 10, 10, 100);
            combat.Attack(Player("A", stats, 20, 10), target, 5, 0, 12345UL);

            var evt = (DamageDealtEvent)buf.Snapshot[0];
            var health = target.Get<Health>();
            if (evt.isDodged)
            {
                Assert.AreEqual(0, evt.amount);
                Assert.AreEqual(100, health.Current);
            }
            else
            {
                Assert.Greater(evt.amount, 0);
                Assert.AreEqual(100 - evt.amount, health.Current);
            }
        }

        [Test]
        public void Attack_death_event_iff_target_dies()
        {
            var (buf, combat, stats) = NewCombat();
            var target = Player("B", stats, 10, 10, 1);   // 1 HP
            combat.Attack(Player("A", stats, 20, 10), target, 5, 0, 12345UL);

            var dmg = (DamageDealtEvent)buf.Snapshot[0];
            bool hasDeath = buf.Snapshot.Count > 1 && buf.Snapshot[1] is DeathEvent;
            if (dmg.isDodged)
            {
                Assert.IsFalse(hasDeath);
                Assert.AreEqual(1, target.Get<Health>().Current);
            }
            else
            {
                Assert.IsTrue(hasDeath, "비회피 히트로 1HP 대상이 죽으면 DeathEvent");
                Assert.AreEqual(0, target.Get<Health>().Current);
            }
        }

        [Test]
        public void Attack_noop_when_neither_is_player()
        {
            var (buf, combat, stats) = NewCombat();
            // Ownership 없는 순수 엔티티
            var attacker = new Entity("A"); var s1 = new Stats(); attacker.Add(s1);
            var target = new Entity("B"); target.Add(new Health(100));

            combat.Attack(attacker, target, 5, 0, 12345UL);

            Assert.AreEqual(0, buf.Count);
            Assert.AreEqual(100, target.Get<Health>().Current);
        }
    }
}
```

- [ ] **Step 2: red 확인** — `refresh_unity(mode="force", scope="all", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")` → `run_tests(mode="EditMode", filter="LOPCombatSystemTests", unity_instance=...)`. Expected: 컴파일 실패(`LOPCombatSystem` 없음 in shared).

- [ ] **Step 3: 구현** — `Runtime/Scripts/Game/LOPCombatSystem.cs` (서버 현재 코드를 World.Entity+씨앗 param으로 reshape, `ICombatSystem` 미상속, `EntityRegistry`/`MatchSeed` 제거, `.entityId`→`.Id`)
```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>전투 해소(공유 concrete) — 데미지/크리/회피 계산 + 이벤트 발행. 클·서 동일 코드(결정론).
    /// 씨앗은 caller가 사이드별로 전달(서버 MatchSeed, 클라 예측=보관 시드). 히트 판정은 caller(핸들러) 소관.</summary>
    public class LOPCombatSystem
    {
        private readonly GameFramework.World.WorldEventBuffer worldEventBuffer;
        private readonly GameFramework.World.HealthSystem healthSystem;
        private readonly GameFramework.World.StatsSystem statsSystem;

        public LOPCombatSystem(
            GameFramework.World.WorldEventBuffer worldEventBuffer,
            GameFramework.World.HealthSystem healthSystem,
            GameFramework.World.StatsSystem statsSystem)
        {
            this.worldEventBuffer = worldEventBuffer;
            this.healthSystem = healthSystem;
            this.statsSystem = statsSystem;
        }

        public void Attack(GameFramework.World.Entity attacker, GameFramework.World.Entity target,
                           long tick, int effectIndex, ulong matchSeed)
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

            int damage = 10;
            GameFramework.World.Stats attackerStats = attacker?.Get<GameFramework.World.Stats>();
            GameFramework.World.Stats targetStats = target?.Get<GameFramework.World.Stats>();

            int attackerStrength = attackerStats != null ? Mathf.RoundToInt(statsSystem.GetValue(attackerStats, (int)GameFramework.World.EntityStatType.Strength)) : 0;
            int attackerDexterity = attackerStats != null ? Mathf.RoundToInt(statsSystem.GetValue(attackerStats, (int)GameFramework.World.EntityStatType.Dexterity)) : 0;
            int targetStrength = targetStats != null ? Mathf.RoundToInt(statsSystem.GetValue(targetStats, (int)GameFramework.World.EntityStatType.Strength)) : 0;
            int targetDexterity = targetStats != null ? Mathf.RoundToInt(statsSystem.GetValue(targetStats, (int)GameFramework.World.EntityStatType.Dexterity)) : 0;

            damage += attackerStrength;

            ulong seed = GameFramework.Hashing.Combine(
                GameFramework.Hashing.Combine(
                    GameFramework.Hashing.Combine(
                        GameFramework.Hashing.Combine(matchSeed, (ulong)tick),
                        GameFramework.Hashing.Fnv1a64(attacker.Id)),
                    GameFramework.Hashing.Fnv1a64(target.Id)),
                (ulong)effectIndex);
            var rng = new GameFramework.DeterministicRandom(seed);

            bool isDodged   = IsDodge(attackerDexterity, targetDexterity, ref rng);
            bool isCritical = IsCritical(attackerStrength, targetStrength, ref rng);
            if (isCritical)
            {
                damage = Mathf.RoundToInt(damage * rng.Range(1.25f, 1.75f));
            }

            int dealtAmount = isDodged ? 0 : damage;
            if (!isDodged)
            {
                healthSystem.TakeDamage(health, dealtAmount);
            }
            bool isDead = health.IsDead;

            worldEventBuffer.Append(new GameFramework.World.DamageDealtEvent(
                targetId:   target.Id,
                attackerId: attacker.Id,
                amount:     dealtAmount,
                isCritical: isCritical,
                isDodged:   isDodged));

            if (isDead)
            {
                worldEventBuffer.Append(new GameFramework.World.DeathEvent(
                    victimId:   target.Id,
                    attackerId: attacker.Id));
            }
        }

        public bool IsDodge(int attackerDex, int targetDex, ref GameFramework.DeterministicRandom rng)
        {
            float dodgeChance = (float)targetDex / (attackerDex + targetDex);
            dodgeChance = Mathf.Clamp(dodgeChance, 0.05f, 0.95f);
            double roll = rng.Range(0.0f, 1.0f);
            return roll < dodgeChance;
        }

        public bool IsCritical(int attackerStr, int targetStr, ref GameFramework.DeterministicRandom rng)
        {
            float critChance = (float)attackerStr / (attackerStr + targetStr);
            critChance = Mathf.Clamp(critChance, 0.05f, 0.50f);
            double roll = rng.Range(0.0f, 1.0f);
            return roll < critChance;
        }
    }
}
```

- [ ] **Step 4: green 확인** — 4/4 PASS (클라 인스턴스). (서버 인스턴스는 이 시점 중복 타입으로 깨짐 — 정상, Task 2가 정리.)

- [ ] **Step 5: 커밋** (LOP-Shared)
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feature/a2-2a-combat-resolution-shared
git add Runtime/Scripts/Game/LOPCombatSystem.cs Runtime/Scripts/Game/LOPCombatSystem.cs.meta Tests/EditMode/LOPCombatSystemTests.cs Tests/EditMode/LOPCombatSystemTests.cs.meta
git commit -m "feat(combat): LOPCombatSystem 공유 concrete + EditMode 테스트 (A2.2a)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Server — 중복 제거 + DamageEffectHandler 배선 + DI

**Files:**
- Delete: `LeagueOfPhysical-Server/Assets/Scripts/CombatSystem/LOPCombatSystem.cs` (+ .meta)
- Delete: `LeagueOfPhysical-Server/Assets/Scripts/CombatSystem/ICombatSystem.cs` (+ .meta)
- Modify: `.../Game/DamageEffectHandler.cs`
- Modify: `.../Game/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: shared `LOP.LOPCombatSystem`(Task 1, file: 패키지). `GameFramework.World.EntityRegistry`, `MatchSeed`, `AbilityEffectContext.EffectIndex`.

- [ ] **Step 1: 중복 파일 삭제** — 서버 `LOPCombatSystem.cs`·`ICombatSystem.cs`(및 각 `.meta`) 제거. `git rm`.

- [ ] **Step 2: `World.Entity.Id` == `entityId` 확인** — 엔티티 등록부(creator)에서 `EntityRegistry.Add` 시 `Entity(id)`의 id가 `LOPEntity.entityId`와 같은 문자열인지 확인(같아야 A2.1 키 불변). `grep -rn "new GameFramework.World.Entity(\|entityRegistry.Add" Assets/Scripts`로 확인만.

- [ ] **Step 3: `DamageEffectHandler` 배선** — `ICombatSystem` → concrete `LOPCombatSystem`, `EntityRegistry`·`MatchSeed` 주입. 히트 판정(OverlapSphere/부채꼴)은 그대로. 호출부만 World.Entity로:
```csharp
        private readonly LOPCombatSystem combatSystem;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly MatchSeed matchSeed;

        public DamageEffectHandler(LOPCombatSystem combatSystem,
                                   GameFramework.World.EntityRegistry entityRegistry,
                                   MatchSeed matchSeed)
        {
            this.combatSystem = combatSystem;
            this.entityRegistry = entityRegistry;
            this.matchSeed = matchSeed;
        }
```
호출부(기존 `combatSystem.Attack(attacker, target, ctx.CurrentTick, ctx.EffectIndex)`)를:
```csharp
                var targetWorld = entityRegistry.Get(target.entityId);
                if (targetWorld == null)
                {
                    continue;
                }
                combatSystem.Attack(ctx.Caster, targetWorld, ctx.CurrentTick, ctx.EffectIndex, matchSeed.Value);
```
(attacker는 `ctx.Caster`(World.Entity) 사용 — OverlapSphere용 LOPEntity `attacker`는 히트 판정에 그대로 유지. self-skip 가드 `target.entityId == attacker.entityId`도 유지.)

- [ ] **Step 4: `GameLifetimeScope` 등록** — `builder.Register<ICombatSystem, LOPCombatSystem>(Lifetime.Singleton);` (약 67행) → `builder.Register<LOPCombatSystem>(Lifetime.Singleton);`. (MatchSeed·EntityRegistry는 이미 등록돼 있음.)

- [ ] **Step 5: 서버 컴파일 검증** — `refresh_unity(mode="force", compile="request", scope="all", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")` → `read_console(types=["error"], unity_instance=...)` **0 에러**(중복 타입 해소, ICombatSystem 참조 없음, DamageEffectHandler·DI 해석).
> **플레이 검증(공격→데미지/크리 정상)은 controller/human** — Task 3 뒤 묶어서. 리포트에 pending.

- [ ] **Step 6: 커밋** (Server)
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/a2-2a-combat-resolution-shared
git add -A Assets/Scripts/CombatSystem/ Assets/Scripts/Game/DamageEffectHandler.cs Assets/Scripts/Game/GameLifetimeScope.cs
git status --short   # 삭제 2 + 수정 2만 확인, 로컬 픽스처 제외
git commit -m "refactor(combat): 서버 LOPCombatSystem/ICombatSystem 제거 → 공유 concrete 사용 (A2.2a)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
> 서버 로컬 픽스처(GameRuleSystem/ConfigureRoomComponent/DefaultVolumeProfile/GraphicsSettings)는 `git add`에 넣지 말 것 — 위 4개 경로만.

---

### Task 3: spec/ROADMAP 마무리 + 3저장소 머지

- [ ] **Step 1: spec 진행 체크** (`specs/2026-07-12-a2-2a-combat-resolution-shared-design.md`).
- [ ] **Step 2: ROADMAP** — Done에 A2.2a 한 줄(07-12), Next A 항목에 "A2.2a 완료 → A2.2b(판정 공유 + 오버랩 포트)" 주석.
- [ ] **Step 3: 플레이 검증(human)** — 서버·클라 접속 → 공격 → 데미지/크리/회피 정상(회귀 없음). 통과 후 머지.
- [ ] **Step 4: 머지** — `--no-ff`, 순서 LOP-Shared → Server → Client(문서).
```bash
for R in LeagueOfPhysical-Shared LeagueOfPhysical-Server; do
  cd /c/Users/re5na/workspace/LOP/$R && git checkout main && git merge --no-ff feature/a2-2a-combat-resolution-shared -m "Merge feature/a2-2a-combat-resolution-shared (A2.2a)"
done
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add docs/ && git commit -m "docs: A2.2a 구현 완료 — spec 진행 + ROADMAP Done"
git checkout main && git merge --no-ff feature/a2-2a-combat-resolution-shared -m "Merge feature/a2-2a-combat-resolution-shared: A2.2a (spec/plan/ROADMAP)"
```

---

## Self-Review

**1. Spec coverage:** LOPCombatSystem 이동+reshape→T1 ✓ / ICombatSystem 제거→T2 ✓ / World.Entity+씨앗 param 시그니처→T1 Step3 ✓ / DamageEffectHandler 배선→T2 Step3 ✓ / DI concrete→T2 Step4 ✓ / EditMode 테스트(결정론/데미지/사망/비플레이어)→T1 Step1 ✓ / Mathf·Debug 유지→T1 Step3 코드 ✓ / A2.2b·A2.4·A2.5 Out of Scope→태스크 없음 ✓.

**2. Placeholder scan:** Step 2(Id 확인)·Step 3(플레이)은 확인/실행 단계(코드 placeholder 아님). 코드 스텝 완전. TODO/TBD 없음.

**3. Type consistency:** `LOPCombatSystem(WorldEventBuffer, HealthSystem, StatsSystem)` ctor — T1 정의·T1 테스트·T2 DI 일치 ✓. `Attack(World.Entity, World.Entity, long, int, ulong)` — T1 정의·T1 테스트·T2 호출(`ctx.Caster, targetWorld, ctx.CurrentTick, ctx.EffectIndex, matchSeed.Value`) 일치 ✓. `IsDodge/IsCritical(int,int, ref DeterministicRandom)` 일관 ✓. `Entity.Id`(string)·`Ownership(string)`·`Health(int)`·`StatsSystem.SetBase(Stats,int,float)`·`EntityStatType.Strength/Dexterity` — 실제 API 확인 완료 ✓.

## Execution Handoff

(작성자가 실행 방식 선택 제시 — subagent-driven 권장. Task 1↔2 사이 서버 일시 컴파일 깨짐은 설계된 것[클라 인스턴스로 T1 검증]. 플레이 검증은 T3 앞 human 게이트.)
