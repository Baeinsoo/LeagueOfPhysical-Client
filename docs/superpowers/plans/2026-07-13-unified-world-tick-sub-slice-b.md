# 통합 World Tick — Sub-slice B (클라 scope 축소 + 물리 흡수) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** 클라 `Simulated` 마커를 내 캐릭만으로 좁히고, 키네마틱 이동을 `IMotionBridge` 포트로 추상화해 `LOPWorld.Tick`의 5번째 페이즈로 흡수한다 — **클·서 양쪽 동작 무변경**, `world.Tick`이 5페이즈 단일 진입점이 된다.

**Architecture:** 키네마틱을 `world.Tick`(Simulated 순회)에 넣으면 클라가 전 캐릭 Simulated(Sub-slice A)라 원격까지 움직인다 → **먼저 클라 scope를 내 캐릭만으로 좁힌다**(무변경 — 원격 틱은 전부 no-op). 그다음 host 결합(`SyncTransforms`/`Depenetrate`/`PushMotion`)을 `IMotionBridge` 포트 뒤로 빼고 `LOPWorld.Tick`이 키네마틱 페이즈를 구동, 양쪽 `LOPRunner`가 `MoveCharacters`/`MoveLocalPlayer`를 버린다.

**Tech Stack:** C# / Unity 6000.3 / VContainer / NUnit EditMode / UnityMCP.

## Global Constraints

- **결정론 = 공유 구체 코드.** I/O 어댑터(포트)만 인터페이스 — `IMotionBridge`는 사이드가 다른 host 물리 브릿지라 포트가 맞다.
- **World 타입 풀 네임스페이스 한정** (`GameFramework.World.Entity` 등). LOP 파일은 `using GameFramework.World;` 금지.
- **`.meta` 커밋** — 새 `.cs`는 Unity 생성 `.meta` 함께.
- **repo별 피처 브랜치** `feature/unified-tick-slice-b` (GameFramework/Shared/Server/Client). main 직접 커밋 금지, 완료 후 `--no-ff`.
- **커밋은 내 파일만 개별 `git add`** — 서버/클라 로컬 픽스처(`ConfigureRoomComponent`/`GameRuleSystem`/`*.asset`/`UIRoot.prefab`/`Art`) 커밋 금지. `git add -A` 금지.
- **UnityMCP 타깃 명시** — 클라 `LeagueOfPhysical-Client@<hash>`, 서버 `LeagueOfPhysical-Server@<hash>`.
- **동작 무변경 = 검증 기준** — 각 태스크 후 Shared EditMode green + 양쪽 컴파일 클린. 최종 플레이 무회귀(서버 전 캐릭 키네마틱 / 클라 내 캐릭만 / 원격 보간).

---

## 사전 준비 (태스크 아님)

```bash
for r in GameFramework LeagueOfPhysical-Shared LeagueOfPhysical-Server LeagueOfPhysical-Client; do
  ( cd "C:/Users/re5na/workspace/LOP/$r" && git checkout main && git checkout -b feature/unified-tick-slice-b )
done
```

---

## Task 1: 클라 `Simulated` 마커를 내 캐릭(`isUserEntity`)에만 부착

원격/NPC를 Simulated에서 빼 클라 시뮬 대상을 예측 엔티티(내 캐릭)로 좁힌다. **무변경** — 원격 틱은 전부 no-op(`AbilitySystem.Tick`은 `ActiveAbility==null`이면 즉시 return이고 원격은 `TryActivate`를 안 받음 / `MovementSystem`은 빈 contributions로 같은 값 재기록 / `StatusEffectSystem`은 빈 목록 no-op / driveeffects는 ActiveAbility 없어 return). 원격은 이미 `MoveLocalPlayer`가 안 건드림(스냅 팔로워).

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/EntityCreator/CharacterCreator.cs` (Sub-slice A에서 넣은 무조건 `Simulated` 부착을 `isUserEntity` 안으로)

- [ ] **Step 1: 마커 부착을 `isUserEntity` 블록으로 이동**

클라 `CharacterCreator.cs`에서 현재:
```csharp
            if (isUserEntity)
            {
                // 입력으로 조종되는 엔티티(내 캐릭)만 — 호스트가 매 틱 커맨드를 채우고 MovementSystem이 읽는다.
                worldEntity.Add(new InputBuffer());
            }
            // Sub-slice A: 현행(전 캐릭 틱) 보존을 위해 전 캐릭 마킹. Sub-slice C에서 내 캐릭만으로 좁힌다.
            worldEntity.Add(new GameFramework.World.Simulated());
            entityRegistry.Add(worldEntity);
```
→ 교체:
```csharp
            if (isUserEntity)
            {
                // 입력으로 조종되는 엔티티(내 캐릭)만 — 호스트가 매 틱 커맨드를 채우고 MovementSystem이 읽는다.
                worldEntity.Add(new InputBuffer());
                // 클라 시뮬 대상 = 예측하는 내 캐릭만. 남/NPC는 Simulated 아님 → 스냅샷 보간 전용.
                worldEntity.Add(new GameFramework.World.Simulated());
            }
            entityRegistry.Add(worldEntity);
```

- [ ] **Step 2: 클라 컴파일 확인**

`refresh_unity(scope=all, compile=request, unity_instance=클라)` → `read_console(types=[error], unity_instance=클라)` = 0건.

- [ ] **Step 3: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/EntityCreator/CharacterCreator.cs
git commit -m "feat: 클라 Simulated 마커를 내 캐릭만으로 좁힘 (남/NPC=보간 전용)"
```

---

## Task 2: `IMotionBridge` 포트 + 클·서 `LOPMotionBridge` 구체 + DI 등록

키네마틱 앞뒤의 host 결합(`Physics.SyncTransforms` / `PhysicsComponent.Depenetrate` / `LOPEntity.PushMotionToPhysics`)을 사이드 무관 포트 뒤로 뺀다. 이 태스크는 배선만 — 아직 `world.Tick`에 안 넣으므로 **동작 무변경**.

**Files:**
- Create: `GameFramework/Runtime/Scripts/Motion/IMotionBridge.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Motion/LOPMotionBridge.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Motion/LOPMotionBridge.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/GameLifetimeScope.cs` (등록)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs` (등록)

**Interfaces:**
- Produces: `GameFramework.IMotionBridge { void SyncTransforms(); void Depenetrate(string entityId); void PushMotion(string entityId); }`. 키네마틱 페이즈가 엔티티별로 Depenetrate→(kinematic)→PushMotion, 페이즈 시작에 SyncTransforms 1회.

- [ ] **Step 1: 포트 인터페이스 작성 (GameFramework)**

`GameFramework/Runtime/Scripts/Motion/IMotionBridge.cs`:
```csharp
namespace GameFramework
{
    /// <summary>
    /// 키네마틱 이동의 host(Unity) 결합을 사이드 무관하게 뺀 포트. 시뮬(world.Tick)이 엔티티별로 호출:
    /// 페이즈 시작 <see cref="SyncTransforms"/> 1회 → 엔티티마다 <see cref="Depenetrate"/> → (KinematicMoveSystem) → <see cref="PushMotion"/>.
    /// 구체는 사이드별(LOPEntity/PhysicsComponent 래핑) — ICollisionQuery/IOverlapQuery와 같은 포트 관용.
    /// </summary>
    public interface IMotionBridge
    {
        void SyncTransforms();
        void Depenetrate(string entityId);
        void PushMotion(string entityId);
    }
}
```

- [ ] **Step 2: 서버 구체 작성**

`LeagueOfPhysical-Server/Assets/Scripts/Motion/LOPMotionBridge.cs`:
```csharp
using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>서버 키네마틱 물리 브릿지 — entityId로 LOPEntity를 잡아 Depenetrate/PushMotion.</summary>
    public class LOPMotionBridge : IMotionBridge
    {
        [Inject] private IEntityManager entityManager;
        private readonly int _layerMask = LayerMask.GetMask("Default");

        public void SyncTransforms() => Physics.SyncTransforms();

        public void Depenetrate(string entityId)
            => entityManager.GetEntity<LOPEntity>(entityId)?.GetEntityComponent<PhysicsComponent>()?.Depenetrate(_layerMask);

        public void PushMotion(string entityId)
            => entityManager.GetEntity<LOPEntity>(entityId)?.PushMotionToPhysics();
    }
}
```

- [ ] **Step 3: 클라 구체 작성**

`LeagueOfPhysical-Client/Assets/Scripts/Motion/LOPMotionBridge.cs` — 서버와 동일 본문(클라 `LOPEntity`/`PhysicsComponent`는 별개 타입):
```csharp
using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>클라 키네마틱 물리 브릿지 — entityId로 LOPEntity를 잡아 Depenetrate/PushMotion. 클라 Simulated=내 캐릭만.</summary>
    public class LOPMotionBridge : IMotionBridge
    {
        [Inject] private IEntityManager entityManager;
        private readonly int _layerMask = LayerMask.GetMask("Default");

        public void SyncTransforms() => Physics.SyncTransforms();

        public void Depenetrate(string entityId)
            => entityManager.GetEntity<LOPEntity>(entityId)?.GetEntityComponent<PhysicsComponent>()?.Depenetrate(_layerMask);

        public void PushMotion(string entityId)
            => entityManager.GetEntity<LOPEntity>(entityId)?.PushMotionToPhysics();
    }
}
```

- [ ] **Step 4: DI 등록 (양쪽 GameLifetimeScope)**

서버·클라 `GameLifetimeScope.cs`의 `Configure`에서 `KinematicMoveSystem` 등록 줄 근처에 추가:
```csharp
            builder.Register<GameFramework.IMotionBridge, LOPMotionBridge>(Lifetime.Singleton);
```
> `LOPMotionBridge`가 `[Inject] IEntityManager`를 받으므로, `RegisterComponent(entityManager).As<IEntityManager>()` 등록(이미 있음)에 의존 — 등록 순서 무관(VContainer는 지연 resolve).

- [ ] **Step 5: 양쪽 컴파일 + meta 확인**

서버·클라 각각 `refresh_unity` + `read_console(types=[error])` 0건. 새 `.cs`의 `.meta` 생성 확인.

- [ ] **Step 6: 커밋 (3 repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git add Runtime/Scripts/Motion/IMotionBridge.cs Runtime/Scripts/Motion/IMotionBridge.cs.meta
# Motion 폴더가 새로 생기면 폴더 .meta도: git add Runtime/Scripts/Motion.meta
git commit -m "feat: IMotionBridge 포트 (키네마틱 host 결합 추상화)"
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/Motion/LOPMotionBridge.cs Assets/Scripts/Motion/LOPMotionBridge.cs.meta Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat: 서버 LOPMotionBridge + DI 등록"
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/Motion/LOPMotionBridge.cs Assets/Scripts/Motion/LOPMotionBridge.cs.meta Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat: 클라 LOPMotionBridge + DI 등록"
```
> 새 폴더(`Motion/`)의 폴더 `.meta`도 함께 `git add`. `git status`로 확인.

---

## Task 3: 키네마틱을 `LOPWorld.Tick`에 흡수 + 양쪽 host 이동 루프 제거

`LOPWorld` ctor에 `KinematicMoveSystem`+`IMotionBridge` 추가, 물리 페이즈(5번째)를 `Mutation` 뒤에 붙인다. **동시에** 양쪽 `LOPRunner`가 `MoveCharacters`/`MoveLocalPlayer`를 제거(안 그러면 이중 키네마틱). 원자적.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/LOPWorld.cs` (ctor + 물리 페이즈)
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs` (`MoveCharacters()` 호출+메서드 제거)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/LOPRunner.cs` (`MoveLocalPlayer()` 호출+메서드 제거)
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/LOPWorldTests.cs` (ctor 갱신 + 물리 페이즈 스파이 테스트)

**Interfaces:**
- Consumes: `GameFramework.IMotionBridge` (Task 2), `KinematicMoveSystem.Tick(Entity, float)` (기존).
- Produces: `LOPWorld` ctor = `(EntityRegistry, WorldEventBuffer, MovementSystem, AbilitySystem, StatusEffectSystem, AbilityEffectExecutor, KinematicMoveSystem, IMotionBridge)`. `world.Tick`이 마지막에 Simulated 전원을 키네마틱 이동.

- [ ] **Step 1: 실패 테스트 작성 (물리 페이즈가 Simulated 엔티티에 bridge 호출)**

`LOPWorldTests.cs`에 스파이 브릿지 + fake query + 테스트 추가(클래스 내 nested):
```csharp
        private class FakeQuery : GameFramework.ICollisionQuery
        {
            public bool CapsuleCast(UnityEngine.Vector3 point1, UnityEngine.Vector3 point2, float radius,
                UnityEngine.Vector3 direction, float maxDistance, int layerMask, out GameFramework.CollisionHit hit)
            { hit = default; return false; }
        }
        private class SpyBridge : GameFramework.IMotionBridge
        {
            public int syncCount; public System.Collections.Generic.List<string> depenetrated = new(); public System.Collections.Generic.List<string> pushed = new();
            public void SyncTransforms() => syncCount++;
            public void Depenetrate(string id) => depenetrated.Add(id);
            public void PushMotion(string id) => pushed.Add(id);
        }

        [Test]
        public void Tick_RunsKinematicPhase_ForSimulatedEntities()
        {
            var registry = new EntityRegistry();
            var bridge = new SpyBridge();
            var world = new LOPWorld(registry, new WorldEventBuffer(),
                new MovementSystem(new StatsSystem(), new MotionContributionSystem()),
                new AbilitySystem(new ManaSystem()), new StatusEffectSystem(new StatsSystem()),
                new AbilityEffectExecutor(null), new KinematicMoveSystem(new FakeQuery(), ~0), bridge);

            var entity = new Entity("e1");
            entity.Add(new Simulated());
            registry.Add(entity);

            world.Tick(0, 0.05f);

            Assert.That(bridge.syncCount, Is.EqualTo(1), "SyncTransforms 페이즈당 1회");
            Assert.That(bridge.depenetrated, Does.Contain("e1"));
            Assert.That(bridge.pushed, Does.Contain("e1"));
        }
```
> `ICollisionQuery.CapsuleCast` 시그니처·`CollisionHit`는 기존 `KinematicMoveSystemTests.cs`의 `FakeCollisionQuery`에서 정확한 형태 확인해 맞출 것(위는 예상형).

- [ ] **Step 2: 테스트 실행 → 실패 (컴파일: ctor 8인자 없음)**

`run_tests(... test_names=["LOP.Tests.LOPWorldTests.Tick_RunsKinematicPhase_ForSimulatedEntities"], unity_instance=클라)` → LOPWorld ctor 6인자라 CS1729 기대.

- [ ] **Step 3: `LOPWorld`에 키네마틱 페이즈 추가**

`LOPWorld.cs`:
```csharp
    public class LOPWorld : GameFramework.World.WorldBase
    {
        private readonly MovementSystem _movementSystem;
        private readonly AbilitySystem _abilitySystem;
        private readonly StatusEffectSystem _statusEffectSystem;
        private readonly AbilityEffectExecutor _abilityEffectExecutor;
        private readonly KinematicMoveSystem _kinematicMoveSystem;
        private readonly GameFramework.IMotionBridge _motionBridge;

        public LOPWorld(
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer eventBuffer,
            MovementSystem movementSystem,
            AbilitySystem abilitySystem,
            StatusEffectSystem statusEffectSystem,
            AbilityEffectExecutor abilityEffectExecutor,
            KinematicMoveSystem kinematicMoveSystem,
            GameFramework.IMotionBridge motionBridge)
            : base(entityRegistry, eventBuffer)
        {
            _movementSystem = movementSystem;
            _abilitySystem = abilitySystem;
            _statusEffectSystem = statusEffectSystem;
            _abilityEffectExecutor = abilityEffectExecutor;
            _kinematicMoveSystem = kinematicMoveSystem;
            _motionBridge = motionBridge;
        }

        protected override void Mutation(long tick, float deltaTime)
        {
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    _movementSystem.Tick(entity, tick, deltaTime);
                }
            }

            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    _abilitySystem.Tick(entity, tick);
                    _statusEffectSystem.Tick(entity, tick);
                }
            }

            // 페이즈 전진 후 active 창 effect 구동.
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    _abilityEffectExecutor.DriveActiveEntity(entity, tick);
                }
            }

            // 키네마틱 이동(중력+collide-and-slide). host 물리 브릿지로 겹침해소·rb 반영을 사이드 무관 호출.
            // 이전엔 host MoveCharacters/MoveLocalPlayer. 서버=전 캐릭(전 Simulated) / 클라=내 캐릭만.
            _motionBridge.SyncTransforms();
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Has<GameFramework.World.Simulated>())
                {
                    _motionBridge.Depenetrate(entity.Id);
                    _kinematicMoveSystem.Tick(entity, deltaTime);
                    _motionBridge.PushMotion(entity.Id);
                }
            }
        }
    }
```

- [ ] **Step 4: 기존 LOPWorldTests ctor에 2인자 추가**

`LOPWorldTests.cs`의 `new LOPWorld(...)` 5곳(기존 4 + Sub-slice A의 driveeffects 테스트) — 마지막에 `new AbilityEffectExecutor(null)` 다음으로 `, new KinematicMoveSystem(new FakeQuery(), ~0), <bridge>` 추가. driveeffects 테스트(`Tick_DrivesActiveAbilityEffect_ViaAbsorbedPhase`)와 나머지는 `new SpyBridge()`(또는 no-op) 전달. 예(무관심 케이스):
```csharp
            var world = new LOPWorld(registry, new WorldEventBuffer(),
                new MovementSystem(new StatsSystem(), new MotionContributionSystem()), abilitySystem, statusEffects,
                new AbilityEffectExecutor(null), new KinematicMoveSystem(new FakeQuery(), ~0), new SpyBridge());
```
> 테스트 엔티티는 Transform/Velocity가 없어 `KinematicMoveSystem.Tick`이 early-return, `SpyBridge`는 no-op 기록만 — 기존 어써션 무영향.

- [ ] **Step 5: 테스트 실행 → green (신규 물리 페이즈 + 기존 전부)**

`run_tests(... assembly_names=["baegames.LOP.Shared.Tests.EditMode"], unity_instance=클라)` — 전부 PASS.

- [ ] **Step 6: 서버 `LOPRunner`에서 `MoveCharacters` 제거**

서버 `LOPRunner.cs` `UpdateRunner`에서 `MoveCharacters();` 삭제 + `MoveCharacters()` 메서드 정의 삭제. 이제 미사용이면 `kinematicMoveSystem` 주입 제거(read_console 경고 확인 — LOPWorld가 대신 씀). `entityRegistry`/`physicsSimulator` 등 다른 데서 쓰면 유지.

- [ ] **Step 7: 클라 `LOPRunner`에서 `MoveLocalPlayer` 제거**

클라 `LOPRunner.cs` `UpdateRunner`에서 `MoveLocalPlayer();` 삭제 + 메서드 삭제. 미사용된 `kinematicMoveSystem` 주입 제거(경고 확인).

- [ ] **Step 8: 양쪽 컴파일 확인**

서버·클라 각각 `refresh_unity` + `read_console(types=[error])` 0건.

- [ ] **Step 9: 커밋 (3 repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared"
git add Runtime/Scripts/Game/LOPWorld.cs Tests/EditMode/LOPWorldTests.cs
git commit -m "feat: 키네마틱을 LOPWorld.Tick 물리 페이즈로 흡수 (IMotionBridge 포트)"
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" && git add Assets/Scripts/Game/LOPRunner.cs && git commit -m "refactor: 서버 MoveCharacters 제거 (world.Tick 흡수)"
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" && git add Assets/Scripts/Game/LOPRunner.cs && git commit -m "refactor: 클라 MoveLocalPlayer 제거 (world.Tick 흡수)"
```

---

## 최종 검증 (Sub-slice B 완료 게이트)

- [ ] **Shared EditMode 전체 green** — `run_tests(mode=EditMode, assembly_names=["baegames.LOP.Shared.Tests.EditMode"], unity_instance=클라)`.
- [ ] **양쪽 컴파일 클린** — 서버·클라 `read_console(types=[error])` 0건.
- [ ] **서버 플레이 무회귀** — 전 캐릭(플레이어·AI) 키네마틱 이동/충돌/넉백 정상.
- [ ] **클라 플레이 무회귀** — 내 캐릭 이동/점프/대시 정상, **원격 캐릭 보간 정상(러버밴딩·이중구동 없음)**, 남 연출 정상.
- [ ] **머지** — 4 repo `feature/unified-tick-slice-b` → main `--no-ff`.

> Sub-slice B 완료 시 `world.Tick`이 5페이즈 단일 진입점. 남은 것 = Sub-slice C(`Reconciler`=`world.Tick` 재생 → `#6` 종결).

---

## Self-Review 결과

- **Spec 커버리지:** 클라 scope 축소(Task1) / IMotionBridge 포트(Task2) / 키네마틱 흡수+host 제거(Task3) 매핑. Reconciler는 Sub-slice C(범위 밖 명시).
- **Placeholder:** 없음. 단 Task3 Step1의 `ICollisionQuery.CapsuleCast`/`CollisionHit` 시그니처는 "기존 `KinematicMoveSystemTests.FakeCollisionQuery`에서 확인해 맞출 것" 지시(정확형은 그 파일이 정본).
- **타입 일관성:** `LOPWorld` ctor 8인자(Task3) ↔ 테스트 5곳 갱신. `IMotionBridge`(Task2)→소비(Task3). `entityManager.GetEntity<LOPEntity>(id)`·`PushMotionToPhysics()`·`Depenetrate(int)` 실제 시그니처 확인됨.
- **동작 보존 순서:** Task1(클라 narrow, 원격 no-op 제거)→Task2(포트 미배선)→Task3(원자적: 페이즈 추가+host 제거). 각 커밋 후 무변경. 서버는 Task1 무영향, Task3서 전 캐릭 키네마틱 순서 보존(world.Tick 마지막 페이즈=예전 MoveCharacters 위치).
