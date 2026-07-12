# A2.2b — 히트 판정 공유화 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버 전용 히트 판정(`Physics.OverlapSphere`+부채꼴)을 LOP-Shared 공유 concrete로 옮기고, 엔진 물리만 사이드별 `IOverlapQuery` 포트로 뺀다.

**Architecture:** 판정 드라이버(부채꼴·자기제외·Attack 루프)를 `DamageEffectHandler`(LOP-Shared 공유)로, 엔진 broad-phase(`Physics.OverlapSphere`+collider→entityId 매핑)를 `IOverlapQuery`(GameFramework 포트) 뒤로. 씨앗은 `IMatchSeed`(LOP-Shared)로 주입. 판정 위치는 `World.Transform`(진실원본, System.Numerics)에서 읽어 엔진 프리.

**Tech Stack:** Unity(멀티 레포 file: 패키지), C#, VContainer DI, NUnit(EditMode), System.Numerics.

## Global Constraints

- **레포 4곳** — GameFramework(`com.baegames.gameframework`), LeagueOfPhysical-Shared(`com.baegames.lop.shared`), LeagueOfPhysical-Server, LeagueOfPhysical-Client. 각각 독립 git.
- **브랜치**: 각 레포에서 `feature/a2-2b-hit-detection-shared` 피처 브랜치로 작업(main 직접 커밋 금지). 완료 후 `--no-ff` 머지.
- **네임스페이스**: 새 타입은 전부 `namespace LOP`(shared/side) 또는 `namespace GameFramework`(포트). LOP 측 파일은 World 타입을 **풀 네임스페이스로 한정**(`GameFramework.World.Entity` 등) — `using GameFramework.World;` 추가 금지(`Component` 모호성 회피).
- **키 RNG 값 불변**: `Hashing.Combine(matchSeed,tick,attacker.Id,target.Id,effectIndex)` 조립·값은 A2.2a와 동일(서버 동작 무변경). 이 슬라이스는 판정 경로만 바꾸고 해소(`LOPCombatSystem`)는 손대지 않는다.
- **.meta 커밋**: Unity가 생성한 `.meta`를 반드시 함께 커밋(직접 생성/수정 금지).
- **패키지 .cs 추가/삭제 후 재스캔**: GameFramework/LOP-Shared 패키지에 파일을 추가·삭제하면 클·서 두 에디터가 재컴파일해야 한다 — `mcp__UnityMCP__refresh_unity(scope="all", force=true, unity_instance=<client id>)` 후 `read_console`로 컴파일 확인.
- **UnityMCP instance**: 이 프로젝트는 클라이언트다. 모든 UnityMCP 호출에 `unity_instance`를 클라 id로 명시(`mcpforunity://instances`에서 `LeagueOfPhysical-Client@<hash>` 해석). 서버 인스턴스는 사용자 명시 없이 건드리지 않는다.
- **EditMode 테스트 실행처**: LOP-Shared EditMode 테스트는 클라 에디터(manifest `testables`에 `com.baegames.lop.shared` 포함)에서 `mcp__UnityMCP__run_tests(mode="EditMode", ...)`로 돌린다.

---

## File Structure

**GameFramework** (`Runtime/Scripts/Game/`)
- Create: `IOverlapQuery.cs` — 오버랩 포트(`ICollisionQuery` 짝). `string[] OverlapSphere(numerics center, radius)`.

**LeagueOfPhysical-Shared** (`Runtime/Scripts/Game/`)
- Create: `IMatchSeed.cs` — `ulong Value { get; }` 얇은 인터페이스.
- Create: `DamageEffectHandler.cs` — 공유 판정 드라이버(서버에서 이동, numerics 부채꼴로 재작성).
- Create test: `Tests/EditMode/DamageEffectHandlerTests.cs`.

**LeagueOfPhysical-Server** (`Assets/Scripts/Game/`)
- Create: `LOPOverlapQuery.cs` — `IOverlapQuery` 서버 구체(엔진 물리 + collider→entityId).
- Delete: `DamageEffectHandler.cs` — shared로 이동됨(중복 타입 제거).
- Modify: `MatchSeed.cs` — `: IMatchSeed` 추가.
- Modify: `GameLifetimeScope.cs` — `IOverlapQuery`/`IMatchSeed` 등록.

**LeagueOfPhysical-Client** (`Assets/Scripts/Game/`)
- Modify: `MatchSeed.cs` — `: IMatchSeed` 추가(대칭; 클라 등록·구체는 A2.4).

---

## Task 1: 추상 — `IOverlapQuery`(GameFramework) + `IMatchSeed`(LOP-Shared)

**Files:**
- Create: `C:\Users\re5na\workspace\LOP\GameFramework\Runtime\Scripts\Game\IOverlapQuery.cs`
- Create: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Shared\Runtime\Scripts\Game\IMatchSeed.cs`

**Interfaces:**
- Produces: `GameFramework.IOverlapQuery` — `string[] OverlapSphere(System.Numerics.Vector3 center, float radius)`.
- Produces: `LOP.IMatchSeed` — `ulong Value { get; }`.

- [ ] **Step 1: 브랜치 준비** — GameFramework, LeagueOfPhysical-Shared 두 레포에서 피처 브랜치 생성.

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework && git checkout -b feature/a2-2b-hit-detection-shared
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared && git checkout -b feature/a2-2b-hit-detection-shared
```

- [ ] **Step 2: `IOverlapQuery` 작성** (GameFramework)

```csharp
namespace GameFramework
{
    /// <summary>
    /// 범위(구) 안에 겹치는 엔티티들의 id를 반환하는 오버랩 쿼리 포트.
    /// <see cref="ICollisionQuery"/>(캡슐 sweep)의 짝. 엔진 물리(Physics.OverlapSphere)에
    /// 직결되지 않도록 주입한다. 구체는 사이드별(LOPOverlapQuery)로, collider→엔티티 매핑을 담당한다.
    /// </summary>
    public interface IOverlapQuery
    {
        /// <summary>중심 <paramref name="center"/>·반지름 <paramref name="radius"/> 구 안에
        /// 겹치는 엔티티들의 id(World.Entity.Id)를 반환한다. 겹치는 게 없으면 빈 배열.</summary>
        string[] OverlapSphere(System.Numerics.Vector3 center, float radius);
    }
}
```

- [ ] **Step 3: `IMatchSeed` 작성** (LOP-Shared)

```csharp
namespace LOP
{
    /// <summary>매치당 결정론 RNG 씨앗을 노출하는 얇은 인터페이스. 양쪽 MatchSeed(서버=생성, 클라=수신 보관)가
    /// 구현해, 공유 전투 핸들러가 사이드 무지로 씨앗을 읽는다.</summary>
    public interface IMatchSeed
    {
        ulong Value { get; }
    }
}
```

- [ ] **Step 4: 컴파일 확인** — 클·서 재스캔 후 콘솔에 에러 없음 확인.

Run: `mcp__UnityMCP__refresh_unity(scope="all", force=true, unity_instance=<client id>)` → `mcp__UnityMCP__read_console(unity_instance=<client id>)`
Expected: 컴파일 에러 없음(새 인터페이스 2개만 추가).

- [ ] **Step 5: 커밋** (각 레포)

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework && git add Runtime/Scripts/Game/IOverlapQuery.cs Runtime/Scripts/Game/IOverlapQuery.cs.meta && git commit -m "feat(port): add IOverlapQuery overlap-sphere port (ICollisionQuery sibling)"
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared && git add Runtime/Scripts/Game/IMatchSeed.cs Runtime/Scripts/Game/IMatchSeed.cs.meta && git commit -m "feat(combat): add IMatchSeed interface for shared seed injection"
```

---

## Task 2: 공유 `DamageEffectHandler` + EditMode 테스트 (TDD)

**Files:**
- Create: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Shared\Runtime\Scripts\Game\DamageEffectHandler.cs`
- Test: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Shared\Tests\EditMode\DamageEffectHandlerTests.cs`

**Interfaces:**
- Consumes: `GameFramework.IOverlapQuery`, `LOP.IMatchSeed` (Task 1); `LOP.LOPCombatSystem`(A2.2a), `GameFramework.World.{EntityRegistry, Entity, Transform, Health, Stats, Ownership, HealthSystem, StatsSystem, WorldEventBuffer, DamageDealtEvent, EntityStatType}`; `LOP.{AbilityEffectContext, AbilityEffectHandler<T>, DamageEffect}`.
- Produces: `LOP.DamageEffectHandler` — ctor `(LOPCombatSystem combatSystem, GameFramework.IOverlapQuery overlapQuery, IMatchSeed matchSeed, GameFramework.World.EntityRegistry entityRegistry)`; base가 노출하는 public `OnActiveEnter(AbilityEffectContext ctx, AbilityEffect effect)`.

> **Interim 참고:** 이 태스크 후 서버 Assembly-CSharp는 `LOP.DamageEffectHandler`가 shared·server 양쪽에 존재해 **중복 타입 컴파일 에러**가 난다. 클라 에디터는 서버 핸들러가 없어 정상 컴파일되므로 테스트는 클라에서 돌린다. 서버 컴파일은 Task 3(서버 사본 삭제)에서 회복.

- [ ] **Step 1: 실패 테스트 작성** — `DamageEffectHandlerTests.cs`

```csharp
using System.Numerics;
using GameFramework;
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class DamageEffectHandlerTests
    {
        private sealed class FakeOverlap : GameFramework.IOverlapQuery
        {
            private readonly string[] ids;
            public FakeOverlap(params string[] ids) { this.ids = ids; }
            public string[] OverlapSphere(Vector3 center, float radius) => ids;
        }

        private sealed class FakeSeed : IMatchSeed
        {
            public ulong Value { get; }
            public FakeSeed(ulong v) { Value = v; }
        }

        private static Entity Player(string id, EntityRegistry reg, StatsSystem stats,
                                     Vector3 pos, int str = 20, int dex = 10, int hp = 1000)
        {
            var e = new Entity(id);
            e.Add(new Ownership("owner-" + id));
            var s = new Stats();
            stats.SetBase(s, (int)EntityStatType.Strength, str);
            stats.SetBase(s, (int)EntityStatType.Dexterity, dex);
            e.Add(s);
            e.Add(new Health(hp));
            e.Add(new GameFramework.World.Transform { Position = pos, Rotation = Quaternion.Identity });
            reg.Add(e);
            return e;
        }

        private static (EntityRegistry reg, WorldEventBuffer buf, StatsSystem stats) World()
            => (new EntityRegistry(), new WorldEventBuffer(), new StatsSystem());

        private static DamageEffectHandler Handler(EntityRegistry reg, WorldEventBuffer buf,
                                                   StatsSystem stats, GameFramework.IOverlapQuery overlap)
        {
            var combat = new LOPCombatSystem(buf, new HealthSystem(), stats);
            return new DamageEffectHandler(combat, overlap, new FakeSeed(12345UL), reg);
        }

        private static AbilityEffectContext Ctx(Entity caster)
            => new AbilityEffectContext(caster, null, 5L, null, 0);

        [Test]
        public void Hits_target_in_front_within_sector()
        {
            var (reg, buf, stats) = World();
            var caster = Player("A", reg, stats, new Vector3(0, 0, 0));
            Player("B", reg, stats, new Vector3(0, 0, 3));   // 정면 +Z, 거리 3
            var h = Handler(reg, buf, stats, new FakeOverlap("B"));

            h.OnActiveEnter(Ctx(caster), new DamageEffect(0, 5f, 90f));

            Assert.AreEqual(1, buf.Count);
            Assert.IsInstanceOf<DamageDealtEvent>(buf.Snapshot[0]);
        }

        [Test]
        public void Skips_self()
        {
            var (reg, buf, stats) = World();
            var caster = Player("A", reg, stats, new Vector3(0, 0, 0));
            var h = Handler(reg, buf, stats, new FakeOverlap("A"));   // 오버랩이 자기만 반환

            h.OnActiveEnter(Ctx(caster), new DamageEffect(0, 5f, 90f));

            Assert.AreEqual(0, buf.Count);
        }

        [Test]
        public void Skips_target_behind_caster()
        {
            var (reg, buf, stats) = World();
            var caster = Player("A", reg, stats, new Vector3(0, 0, 0));
            Player("B", reg, stats, new Vector3(0, 0, -3));   // 뒤 -Z
            var h = Handler(reg, buf, stats, new FakeOverlap("B"));

            h.OnActiveEnter(Ctx(caster), new DamageEffect(0, 5f, 90f));

            Assert.AreEqual(0, buf.Count);
        }

        [Test]
        public void Skips_target_out_of_range()
        {
            var (reg, buf, stats) = World();
            var caster = Player("A", reg, stats, new Vector3(0, 0, 0));
            Player("B", reg, stats, new Vector3(0, 0, 10));   // 정면이지만 range 5 밖
            var h = Handler(reg, buf, stats, new FakeOverlap("B"));

            h.OnActiveEnter(Ctx(caster), new DamageEffect(0, 5f, 90f));

            Assert.AreEqual(0, buf.Count);
        }

        [Test]
        public void Rotation_flips_hit_to_miss()
        {
            var (reg, buf, stats) = World();
            var caster = Player("A", reg, stats, new Vector3(0, 0, 0));
            caster.Get<GameFramework.World.Transform>().Rotation =
                Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)System.Math.PI);   // 180° → forward -Z
            Player("B", reg, stats, new Vector3(0, 0, 3));    // 이제 뒤쪽
            var h = Handler(reg, buf, stats, new FakeOverlap("B"));

            h.OnActiveEnter(Ctx(caster), new DamageEffect(0, 5f, 90f));

            Assert.AreEqual(0, buf.Count);
        }

        [Test]
        public void Skips_unresolvable_id()
        {
            var (reg, buf, stats) = World();
            var caster = Player("A", reg, stats, new Vector3(0, 0, 0));
            var h = Handler(reg, buf, stats, new FakeOverlap("ghost"));   // 레지스트리에 없음

            h.OnActiveEnter(Ctx(caster), new DamageEffect(0, 5f, 90f));

            Assert.AreEqual(0, buf.Count);
        }

        [Test]
        public void End_to_end_applies_damage_to_health()
        {
            var (reg, buf, stats) = World();
            var caster = Player("A", reg, stats, new Vector3(0, 0, 0));
            var target = Player("B", reg, stats, new Vector3(0, 0, 3));
            var h = Handler(reg, buf, stats, new FakeOverlap("B"));

            h.OnActiveEnter(Ctx(caster), new DamageEffect(0, 5f, 90f));

            var evt = (DamageDealtEvent)buf.Snapshot[0];
            Assert.AreEqual("B", evt.targetId);
            if (!evt.isDodged)
                Assert.AreEqual(1000 - evt.amount, target.Get<Health>().Current);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인** — `DamageEffectHandler` 타입 없음.

Run: `mcp__UnityMCP__refresh_unity(scope="all", force=true, unity_instance=<client id>)` → `mcp__UnityMCP__run_tests(mode="EditMode", testFilter="DamageEffectHandlerTests", unity_instance=<client id>)`
Expected: FAIL/컴파일 에러 — `DamageEffectHandler`를 찾을 수 없음.

- [ ] **Step 3: 공유 `DamageEffectHandler` 작성** — `Runtime/Scripts/Game/DamageEffectHandler.cs`

```csharp
namespace LOP
{
    /// <summary>
    /// <see cref="DamageEffect"/> 핸들러(클·서 공유). Active 진입 시 1회, 시전자 정면 부채꼴 안의 대상을 때린다.
    /// 판정 위치는 World.Transform(진실원본, System.Numerics) — 엔진 좌표 대신. 엔진 물리(범위 검색)는
    /// IOverlapQuery(사이드 구체)에 위임하고, 해소(데미지/크리/회피)는 LOPCombatSystem(공유)이 한다.
    /// 클라 등록·예측 소비는 A2.4 — 지금은 서버만 등록(데미지 서버권위).
    /// </summary>
    public class DamageEffectHandler : AbilityEffectHandler<DamageEffect>
    {
        private readonly LOPCombatSystem combatSystem;
        private readonly GameFramework.IOverlapQuery overlapQuery;
        private readonly IMatchSeed matchSeed;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public DamageEffectHandler(LOPCombatSystem combatSystem,
                                   GameFramework.IOverlapQuery overlapQuery,
                                   IMatchSeed matchSeed,
                                   GameFramework.World.EntityRegistry entityRegistry)
        {
            this.combatSystem = combatSystem;
            this.overlapQuery = overlapQuery;
            this.matchSeed = matchSeed;
            this.entityRegistry = entityRegistry;
        }

        protected override void OnActiveEnter(AbilityEffectContext ctx, DamageEffect effect)
        {
            GameFramework.World.Transform casterTransform = ctx.Caster?.Get<GameFramework.World.Transform>();
            if (casterTransform == null)
            {
                return;
            }

            string[] hitIds = overlapQuery.OverlapSphere(casterTransform.Position, effect.Range);
            foreach (string id in hitIds)
            {
                if (id == ctx.Caster.Id)
                {
                    continue;   // 자기제외
                }

                GameFramework.World.Entity target = entityRegistry.Get(id);
                GameFramework.World.Transform targetTransform = target?.Get<GameFramework.World.Transform>();
                if (targetTransform == null)
                {
                    continue;
                }
                if (!IsInAttackSector(casterTransform, targetTransform.Position, effect.Range, effect.Angle))
                {
                    continue;
                }

                combatSystem.Attack(ctx.Caster, target, ctx.CurrentTick, ctx.EffectIndex, matchSeed.Value);
            }
        }

        // 시전자 정면 부채꼴(전체 각 angle) 안이고 range 이내인지. World.Transform(진실원본) 기준, System.Numerics.
        private static bool IsInAttackSector(GameFramework.World.Transform caster,
                                             System.Numerics.Vector3 targetPosition, float range, float angle)
        {
            System.Numerics.Vector3 toTarget = targetPosition - caster.Position;
            if (toTarget.Length() > range)
            {
                return false;
            }

            System.Numerics.Vector3 forward =
                System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ, caster.Rotation);
            float dot = System.Numerics.Vector3.Dot(
                System.Numerics.Vector3.Normalize(forward),
                System.Numerics.Vector3.Normalize(toTarget));
            float targetAngle = (float)System.Math.Acos(System.Math.Clamp(dot, -1.0, 1.0)) * (180f / (float)System.Math.PI);
            return targetAngle <= (angle * 0.5f);
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Run: `mcp__UnityMCP__refresh_unity(scope="all", force=true, unity_instance=<client id>)` → `mcp__UnityMCP__run_tests(mode="EditMode", testFilter="DamageEffectHandlerTests", unity_instance=<client id>)`
Expected: PASS (7 테스트). (서버 콘솔의 중복 타입 에러는 Task 3에서 해소되므로 여기선 무시 — 클라 테스트 결과만 본다.)

- [ ] **Step 5: 커밋** (LOP-Shared)

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime/Scripts/Game/DamageEffectHandler.cs Runtime/Scripts/Game/DamageEffectHandler.cs.meta \
        Tests/EditMode/DamageEffectHandlerTests.cs Tests/EditMode/DamageEffectHandlerTests.cs.meta
git commit -m "feat(combat): move DamageEffectHandler to LOP-Shared (numerics sector, IOverlapQuery port) + EditMode tests"
```

---

## Task 3: 서버 재배선 — 사본 삭제 + `LOPOverlapQuery` + DI

**Files:**
- Delete: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server\Assets\Scripts\Game\DamageEffectHandler.cs` (+ `.meta`)
- Create: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server\Assets\Scripts\Game\LOPOverlapQuery.cs`
- Modify: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server\Assets\Scripts\Game\MatchSeed.cs`
- Modify: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server\Assets\Scripts\Game\GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `GameFramework.IOverlapQuery`(Task 1), `LOP.IMatchSeed`(Task 1), `LOP.DamageEffectHandler`(Task 2, now from shared), `LOP.LOPEntity`(사이드).
- Produces: `LOP.LOPOverlapQuery : GameFramework.IOverlapQuery` (서버 구체).

- [ ] **Step 1: 브랜치 준비** (서버)

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server && git checkout -b feature/a2-2b-hit-detection-shared
```

- [ ] **Step 2: 서버 `DamageEffectHandler` 사본 삭제**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git rm Assets/Scripts/Game/DamageEffectHandler.cs Assets/Scripts/Game/DamageEffectHandler.cs.meta
```

- [ ] **Step 3: `LOPOverlapQuery` 작성** — `Assets/Scripts/Game/LOPOverlapQuery.cs`

```csharp
using System.Collections.Generic;
using System.Linq;
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>IOverlapQuery 서버 구체 — Physics.OverlapSphere로 범위 검색 후 collider를 LOPEntity로 매핑해
    /// entityId를 반환. LOPEntity(사이드 타입)를 알아야 해서 각 레포에 존재(의도적 사이드 분기).
    /// 레거시 DamageEffectHandler의 OverlapSphere+매핑 그대로 이식. Plane 등 엔티티 없는 콜라이더는 자연 제외.</summary>
    public sealed class LOPOverlapQuery : GameFramework.IOverlapQuery
    {
        public string[] OverlapSphere(System.Numerics.Vector3 center, float radius)
        {
            LayerMask layerMask = LayerMask.GetMask("Default");
            Collider[] hits = Physics.OverlapSphere(center.ToUnity(), radius, layerMask);

            var ids = new HashSet<string>();   // 한 엔티티 다중 콜라이더 → 중복 제거(키 기반 RNG라 순서·중복 무관)
            foreach (var hit in hits)
            {
                var entity = hit.transform.parent?.parent?.GetComponentInChildren<LOPEntity>();
                if (entity != null)
                {
                    ids.Add(entity.entityId);
                }
            }
            return ids.ToArray();
        }
    }
}
```

- [ ] **Step 4: 서버 `MatchSeed`에 `IMatchSeed` 구현 추가** — `MatchSeed.cs`

기존:
```csharp
    public class MatchSeed
    {
        public ulong Value { get; }
```
변경:
```csharp
    public class MatchSeed : IMatchSeed
    {
        public ulong Value { get; }
```

- [ ] **Step 5: DI 등록 수정** — `GameLifetimeScope.cs`

`builder.Register<MatchSeed>(Lifetime.Singleton);` (line 34) 를 다음으로 교체(`Game.Info.MessageHandler`가 concrete `MatchSeed`를 주입하므로 self+interface 둘 다 노출):
```csharp
            builder.Register<MatchSeed>(Lifetime.Singleton).AsSelf().As<IMatchSeed>();
```

`builder.Register<GameFramework.ICollisionQuery, GameFramework.UnityCollisionQuery>(Lifetime.Singleton);` (line 50) 바로 아래에 오버랩 포트 등록 추가:
```csharp
            builder.Register<GameFramework.IOverlapQuery, LOPOverlapQuery>(Lifetime.Singleton);
```

`builder.Register<DamageEffectHandler>(Lifetime.Singleton).As<IAbilityEffectHandler>();` (line 44) 는 **그대로 유지** — 타입은 이제 shared 어셈블리에서 오지만 등록 형태는 동일하고, 주입 4종(`LOPCombatSystem`·`IOverlapQuery`·`IMatchSeed`·`EntityRegistry`)이 모두 이 스코프에 등록돼 있다.

- [ ] **Step 6: 서버 컴파일 확인** — 재스캔 후 서버 콘솔에 에러 없음(중복 타입 해소, 새 등록 resolve 성공).

Run: `mcp__UnityMCP__refresh_unity(scope="all", force=true, unity_instance=<client id>)` → `mcp__UnityMCP__read_console(unity_instance=<server id>)`
Expected: 서버 컴파일 에러 없음. (서버 인스턴스 콘솔은 읽기만 — 사용자 명시 없이 서버 상태를 바꾸지 않는다.)

- [ ] **Step 7: 플레이 검증** — 서버·클라 실행, 공격 시 정면 부채꼴 대상에 데미지/크리/사망이 레거시와 동일하게 동작하는지 육안 확인(전투 회귀 없음). 특히 부채꼴 경계·등 뒤 대상 미명중, 사거리 밖 미명중.

Expected: 공격→명중/데미지/경험치 정상. rubberband·예외 없음.

- [ ] **Step 8: 커밋** (서버)

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/Game/LOPOverlapQuery.cs Assets/Scripts/Game/LOPOverlapQuery.cs.meta \
        Assets/Scripts/Game/MatchSeed.cs Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "refactor(combat): wire shared DamageEffectHandler — LOPOverlapQuery port + IMatchSeed, remove server copy"
```

---

## Task 4: 클라 `MatchSeed`에 `IMatchSeed` 구현 (대칭)

**Files:**
- Modify: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client\Assets\Scripts\Game\MatchSeed.cs`

**Interfaces:**
- Consumes: `LOP.IMatchSeed`(Task 1).

> 클라는 이 슬라이스에서 `DamageEffectHandler`/`LOPOverlapQuery`를 등록·사용하지 않는다(A2.4). `IMatchSeed` 구현만 미리 붙여 양쪽 대칭을 맞춘다(무비용, A2.4 배선 준비).

- [ ] **Step 1: 브랜치 준비** (클라)

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client && git checkout -b feature/a2-2b-hit-detection-shared
```

- [ ] **Step 2: 클라 `MatchSeed`에 인터페이스 추가**

기존:
```csharp
    public class MatchSeed
    {
        public ulong Value { get; private set; }
```
변경:
```csharp
    public class MatchSeed : IMatchSeed
    {
        public ulong Value { get; private set; }
```

- [ ] **Step 3: 클라 컴파일 확인**

Run: `mcp__UnityMCP__refresh_unity(scope="all", force=true, unity_instance=<client id>)` → `mcp__UnityMCP__read_console(unity_instance=<client id>)`
Expected: 컴파일 에러 없음.

- [ ] **Step 4: 커밋** (클라)

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/MatchSeed.cs
git commit -m "feat(combat): implement IMatchSeed on client MatchSeed (A2.4 symmetry)"
```

---

## Task 5: 문서 갱신 + 브랜치 머지

**Files:**
- Modify: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client\docs\ROADMAP.md`
- Modify: A2.2b spec 진행 체크박스.

- [ ] **Step 1: ROADMAP + spec 갱신** — Done 원장에 A2.2b 행 추가(spec/plan 링크), Next #1 A의 "남은 A2"에서 A2.2b 제거(A2.4/A2.5만 남김). spec 진행 체크박스 완료 표기.

- [ ] **Step 2: 문서 커밋** (클라, 같은 브랜치)

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add docs/ROADMAP.md docs/superpowers/specs/2026-07-12-a2-2b-hit-detection-shared-design.md docs/superpowers/plans/2026-07-12-a2-2b-hit-detection-shared.md
git commit -m "docs: A2.2b 히트 판정 공유화 완료 반영"
```

- [ ] **Step 3: 4레포 `--no-ff` 머지** — 각 레포에서 main 체크아웃 후 피처 브랜치 머지.

```bash
for repo in GameFramework LeagueOfPhysical-Shared LeagueOfPhysical-Server LeagueOfPhysical-Client; do
  cd /c/Users/re5na/workspace/LOP/$repo && git checkout main && git merge --no-ff feature/a2-2b-hit-detection-shared
done
```

- [ ] **Step 4: 머지 후 최종 컴파일 + 전투 회귀 재확인** — 클·서 재스캔, 콘솔 clean, 공격 정상.

---

## Self-Review

**Spec coverage:**
- `IOverlapQuery`(GameFramework, entityId[] 반환) → Task 1 ✅
- `LOPOverlapQuery`(서버 구체, Plane 자연제외, dedup) → Task 3 ✅
- `IMatchSeed`(LOP-Shared) + 양쪽 MatchSeed 구현 → Task 1/3/4 ✅
- `DamageEffectHandler` shared 이동 + numerics 부채꼴 + `ctx.EntityManager` 제거 → Task 2 ✅
- 서버 DI 등록(IOverlapQuery/IMatchSeed) → Task 3 ✅
- EditMode 테스트(자기제외·부채꼴·range·회전·미해결 id·끝-끝) → Task 2 ✅
- 클라 미등록(A2.4) → Task 4 참고 ✅

**Type consistency:** `DamageEffectHandler` ctor 인자 순서(combat, overlap, seed, reg)가 Task 2 테스트 `Handler()`와 Task 3 DI 등록(자동 주입)에서 일치. `IOverlapQuery.OverlapSphere(numerics Vector3, float)` 시그니처가 포트·구체·fake·핸들러 호출에서 일치. `IMatchSeed.Value`가 fake·양쪽 MatchSeed에서 일치.

**Placeholder scan:** 모든 코드 스텝에 실제 코드 포함. "적절한 처리" 류 없음.
