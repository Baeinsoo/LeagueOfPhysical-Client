# 서버 키네마틱 배선 (슬라이스 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 슬라이스 1의 키네마틱 커널을 **서버에** 배선한다 — 서버 캐릭터를 kinematic으로 바꾸고, 매 틱 중력+collide-and-slide로 이동시켜 다이나믹 PhysX 캐릭터 적분을 대체한다. 클라는 무변경.

**Architecture:** 공유 `KinematicMoveSystem`(LOP-Shared, 중력 수직 스텝 + `KinematicMover` 호출 + World 쓰기)을 만들고, 서버 호스트(`LOPRunner`)가 캐릭터 엔티티에 대해 매 틱 호출한다. 물리 쿼리는 `ICollisionQuery`(DI). 캐릭터는 kinematic rb가 되어 우리가 `World.Transform.Position`으로 직접 이동시키고, `Physics.Simulate`는 다이나믹 물체 전용이 된다.

**Tech Stack:** Unity 6, C#, NUnit(EditMode), VContainer, UnityMCP(컴파일·테스트·플레이), Git.

## Global Constraints

- **작업 브랜치 = `feature/kinematic-server-wiring`** (각 레포에 동명 브랜치 생성). **main 직접 커밋 금지.**
- **레포 분리:** Task 1 = LOP-Shared, Task 2 = GameFramework + Server, Task 3 = Server. 각 커밋은 해당 레포에서.
- **UnityMCP는 매 호출에 `unity_instance` 명시.** 클라 = `LeagueOfPhysical-Client@de70658b9450cbb4`, 서버 = `LeagueOfPhysical-Server@f99391fa2dbaaf3c`(둘 다 해석은 `mcpforunity://instances`로 최신 hash 확인). EditMode 테스트·컴파일 검증은 **클라 인스턴스**에서(공유 패키지가 클라 에디터에서 컴파일됨). 서버 코드(Task 2·3) 컴파일 검증은 **서버 인스턴스**에서.
- **World 타입은 풀 네임스페이스 한정** (`GameFramework.World.Transform`/`Velocity`/`Entity`) — `using GameFramework.World;` 금지(`UnityEngine.Transform`/`Component` 충돌 회피, 프로젝트 컨벤션).
- **`.meta`는 Unity 생성분을 함께 커밋.** 새 `.cs` 작성 후 `refresh_unity`로 생성.
- **중력은 컨트롤러 레이어의 분리된 수직 스텝** (`KinematicMoveSystem` 안). `KinematicMover` 커널은 중력 무지 유지. 넉백류 외력(`MotionContributions`)과 다른 축.
- **커밋 메시지 끝**: `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`

> **⚠️ Task 3 검증은 사람이 플레이로 확인한다.** Task 1(순수 로직)은 EditMode로, Task 2(인프라)는 컴파일로 자동 검증되지만, Task 3(행동 배선 — 걷기/점프/낙하/벽·캐릭터 충돌)은 **자동 테스트 불가 → 사용자 수동 플레이 검증**이 완료 조건이다. 서브에이전트는 코드 작성 + 컴파일 클린까지만 하고, 플레이 검증 체크리스트를 사용자에게 넘긴다.

---

## 파일 구조

- **Create** `LeagueOfPhysical-Shared/Runtime/Scripts/Game/KinematicMoveSystem.cs` — 중력+커널 오케스트레이션(공유).
- **Create** `LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoveSystemTests.cs` — EditMode 테스트(fake 쿼리).
- **Modify** `GameFramework/Runtime/Scripts/Game/UnityCollisionQuery.cs` — sweep이 트리거(아이템)를 무시하도록 `QueryTriggerInteraction.Ignore`.
- **Modify** `LeagueOfPhysical-Server/Assets/Scripts/Game/GameLifetimeScope.cs` — `ICollisionQuery`·`KinematicMoveSystem` DI 등록.
- **Modify** `LeagueOfPhysical-Server/Assets/Scripts/EntityCreator/CharacterCreator.cs:53` — 캐릭터 kinematic 생성.
- **Modify** `LeagueOfPhysical-Server/Assets/Scripts/Entity/LOPEntity.cs` — `PushMotionToPhysics` kinematic 브랜치가 `rb.position`도 밀도록.
- **Modify** `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs` — `KinematicMoveSystem` 주입 + `MoveCharacters()` 틱 스텝.

---

## Task 1: `KinematicMoveSystem` (LOP-Shared) — TDD

중력을 속도에 더한 뒤 `KinematicMover.Move`로 위치를 내고 `World.Transform`/`Velocity`에 쓴다. fake 쿼리로 EditMode 검증. 라이브 미배선(dormant).

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/KinematicMoveSystem.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoveSystemTests.cs`

**Interfaces:**
- Consumes: `GameFramework.ICollisionQuery`/`CollisionHit`(슬라이스1), `LOP.KinematicMover.Move(in KinematicMoveInput, ICollisionQuery)`/`KinematicMoveInput(Vector3 position, Vector3 velocity, float radius, float height, float deltaTime, int layerMask)`/`KinematicMoveResult(Vector3 position, Vector3 velocity, bool grounded)`(슬라이스1), `GameFramework.World.Entity/Transform/Velocity`, `.ToUnity()`/`.ToNumerics()` 확장.
- Produces: `LOP.KinematicMoveSystem(ICollisionQuery query, int layerMask)` + `void Tick(GameFramework.World.Entity entity, float deltaTime)`.

- [ ] **Step 1: 테스트 파일 + 첫 테스트(중력 낙하) 작성**

`LeagueOfPhysical-Shared/Tests/EditMode/KinematicMoveSystemTests.cs`:

```csharp
using System.Collections.Generic;
using GameFramework;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class KinematicMoveSystemTests
    {
        const float Tolerance = 1e-3f;
        const float Dt = 0.1f;
        const float Gravity = -9.81f * 2f;   // KinematicMoveSystem의 중력 상수와 같은 값

        private class FakeCollisionQuery : ICollisionQuery
        {
            public readonly Queue<CollisionHit> Responses = new Queue<CollisionHit>();
            public CollisionHit CapsuleCast(Vector3 p1, Vector3 p2, float radius,
                Vector3 dir, float dist, int mask)
                => Responses.Count > 0 ? Responses.Dequeue() : CollisionHit.None;
        }

        private static GameFramework.World.Entity Entity(Vector3 pos, Vector3 vel)
        {
            var e = new GameFramework.World.Entity("e1");
            e.Add(new GameFramework.World.Transform { Position = pos.ToNumerics() });
            e.Add(new GameFramework.World.Velocity { Linear = vel.ToNumerics() });
            return e;
        }

        [Test]
        public void Gravity_PullsDown_WhenAirborne()
        {
            var sys = new KinematicMoveSystem(new FakeCollisionQuery(), ~0);   // 응답 없음 → 자유낙하
            var e = Entity(new Vector3(0f, 10f, 0f), Vector3.zero);

            sys.Tick(e, Dt);

            var v = e.Get<GameFramework.World.Velocity>().Linear.ToUnity();
            var p = e.Get<GameFramework.World.Transform>().Position.ToUnity();
            Assert.That(v.y, Is.EqualTo(Gravity * Dt).Within(Tolerance), "중력만큼 수직 속도 감소");
            Assert.That(p.y, Is.LessThan(10f), "아래로 이동");
        }
    }
}
```

- [ ] **Step 2: 실패 확인 (시스템 없음)**

클라 인스턴스 id 해석 후:
`run_tests(unity_instance="LeagueOfPhysical-Client@<hash>", mode="EditMode", testFilter="LOP.Tests.KinematicMoveSystemTests")`
Expected: 컴파일 실패 — `KinematicMoveSystem` 미정의.

- [ ] **Step 3: `KinematicMoveSystem.cs` 구현**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/KinematicMoveSystem.cs`:

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 키네마틱 캐릭터 이동: 중력(수직 가속)을 속도에 더한 뒤 collide-and-slide 커널로 위치를 낸다.
    /// World.Transform/Velocity(진실원본)에 쓴다. 클·서 공유 — 호스트가 캐릭터 엔티티에 대해 호출한다.
    /// 물리 쿼리는 ICollisionQuery 포트 뒤. mover 커널은 중력을 모른다 — 중력은 여기서만(분리된 수직 스텝).
    /// </summary>
    public class KinematicMoveSystem
    {
        const float Gravity = -9.81f * 2f;   // 서버 Physics.gravity.y와 같은 값 유지(낙하 가속)
        const float Radius = 0.35f;          // PhysicsComponent 캡슐과 일치
        const float Height = 1.5f;

        private readonly ICollisionQuery _query;
        private readonly int _layerMask;

        public KinematicMoveSystem(ICollisionQuery query, int layerMask)
        {
            _query = query;
            _layerMask = layerMask;
        }

        public void Tick(GameFramework.World.Entity entity, float deltaTime)
        {
            var transform = entity.Get<GameFramework.World.Transform>();
            var velocity = entity.Get<GameFramework.World.Velocity>();
            if (transform == null || velocity == null)
            {
                return;
            }

            Vector3 vel = velocity.Linear.ToUnity();
            vel.y += Gravity * deltaTime;   // 중력 = 분리된 수직 스텝(컨트롤러 레이어). mover는 이걸 모름.

            var result = KinematicMover.Move(new KinematicMoveInput(
                transform.Position.ToUnity(), vel, Radius, Height, deltaTime, _layerMask), _query);

            transform.Position = result.position.ToNumerics();
            velocity.Linear = result.velocity.ToNumerics();
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

`run_tests(unity_instance="LeagueOfPhysical-Client@<hash>", mode="EditMode", testFilter="LOP.Tests.KinematicMoveSystemTests")`
Expected: `Gravity_PullsDown_WhenAirborne` PASS.

- [ ] **Step 5: 커밋**

```bash
# LeagueOfPhysical-Shared 저장소에서:
git add Runtime/Scripts/Game/KinematicMoveSystem.cs Runtime/Scripts/Game/KinematicMoveSystem.cs.meta \
        Tests/EditMode/KinematicMoveSystemTests.cs Tests/EditMode/KinematicMoveSystemTests.cs.meta
git commit -m "feat(movement): KinematicMoveSystem — 중력+collide-and-slide 오케스트레이션

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 6: 바닥 접지 테스트 추가**

`KinematicMoveSystemTests`에 추가:

```csharp
        [Test]
        public void Ground_StopsFall_ZeroesVerticalVelocity()
        {
            var q = new FakeCollisionQuery();
            q.Responses.Enqueue(new CollisionHit(true, 0.05f, new Vector3(0f, 1f, 0f), Vector3.zero)); // 바닥(법선 위)
            var sys = new KinematicMoveSystem(q, ~0);
            var e = Entity(new Vector3(0f, 0.1f, 0f), Vector3.zero);

            sys.Tick(e, Dt);

            var v = e.Get<GameFramework.World.Velocity>().Linear.ToUnity();
            Assert.That(v.y, Is.EqualTo(0f).Within(Tolerance), "바닥 접지 시 수직 속도 소멸");
        }
```

- [ ] **Step 7: 통과 확인**

`run_tests(...testFilter="LOP.Tests.KinematicMoveSystemTests")`
Expected: 2개 PASS (중력이 vel.y를 음수로 만들고 → 바닥 법선에 투영돼 0).

- [ ] **Step 8: 수평 이동 테스트 추가**

```csharp
        [Test]
        public void HorizontalVelocity_MovesPosition_WhenClear()
        {
            var sys = new KinematicMoveSystem(new FakeCollisionQuery(), ~0);   // 무충돌
            var e = Entity(Vector3.zero, new Vector3(5f, 0f, 0f));

            sys.Tick(e, Dt);

            var p = e.Get<GameFramework.World.Transform>().Position.ToUnity();
            Assert.That(p.x, Is.EqualTo(0.5f).Within(Tolerance), "수평 5 × dt 0.1 = 0.5 이동");
        }
```

- [ ] **Step 9: null-safe 테스트 추가**

```csharp
        [Test]
        public void NoTransformOrVelocity_DoesNotThrow()
        {
            var sys = new KinematicMoveSystem(new FakeCollisionQuery(), ~0);
            var e = new GameFramework.World.Entity("e2");   // Transform/Velocity 없음
            Assert.DoesNotThrow(() => sys.Tick(e, Dt));
        }
```

- [ ] **Step 10: 통과 확인 (4개)**

`run_tests(...testFilter="LOP.Tests.KinematicMoveSystemTests")`
Expected: 4개 모두 PASS.

- [ ] **Step 11: 커밋**

```bash
git add Runtime/Scripts/Game/KinematicMoveSystem.cs Tests/EditMode/KinematicMoveSystemTests.cs
git commit -m "test(movement): KinematicMoveSystem 접지·수평이동·null-safe 테스트

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 인프라 배선 — 트리거 무시 쿼리 + 서버 DI (GameFramework + Server)

sweep이 아이템(트리거)에 안 막히게 하고, 서버 컨테이너에 `ICollisionQuery`·`KinematicMoveSystem`을 등록한다. 아직 틱에서 호출하지 않음 — 컴파일만 검증.

**Files:**
- Modify: `GameFramework/Runtime/Scripts/Game/UnityCollisionQuery.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `LOP.KinematicMoveSystem(ICollisionQuery, int)`(Task 1), `GameFramework.ICollisionQuery`/`UnityCollisionQuery`(슬라이스1).
- Produces: 서버 컨테이너에서 `KinematicMoveSystem` resolve 가능(Task 3가 주입).

- [ ] **Step 1: `UnityCollisionQuery`가 트리거를 무시하도록 수정**

`GameFramework/Runtime/Scripts/Game/UnityCollisionQuery.cs`의 `CapsuleCast` 본문 교체(마지막 인자 `QueryTriggerInteraction.Ignore` 추가):

```csharp
        public CollisionHit CapsuleCast(Vector3 point1, Vector3 point2, float radius,
            Vector3 direction, float distance, int layerMask)
        {
            // 이동 sweep은 트리거(아이템 픽업 등)에 막히면 안 된다 → 트리거 무시.
            if (Physics.CapsuleCast(point1, point2, radius, direction, out RaycastHit hit,
                    distance, layerMask, QueryTriggerInteraction.Ignore))
            {
                return new CollisionHit(true, hit.distance, hit.normal, hit.point);
            }
            return CollisionHit.None;
        }
```

- [ ] **Step 2: GameFramework 컴파일 확인**

`refresh_unity(unity_instance="LeagueOfPhysical-Client@<hash>")` → `read_console(unity_instance="LeagueOfPhysical-Client@<hash>", types=["error"])`.
Expected: 신규 에러 0.

- [ ] **Step 3: 서버 DI 등록 추가**

`LeagueOfPhysical-Server/Assets/Scripts/Game/GameLifetimeScope.cs`에서 `IPhysicsSimulator` 등록 줄(현재 line 48) **바로 다음**에 추가:

```csharp
            builder.Register<GameFramework.ICollisionQuery, GameFramework.UnityCollisionQuery>(Lifetime.Singleton);
            builder.Register<KinematicMoveSystem>(c => new KinematicMoveSystem(
                c.Resolve<GameFramework.ICollisionQuery>(), LayerMask.GetMask("Default")), Lifetime.Singleton);
```

> `LayerMask.GetMask("Default")` — 코드베이스 관례(DamageEffectHandler/KnockbackEffectHandler와 동일). 캐릭터·환경 콜라이더가 모두 Default 레이어라 이 마스크로 벽·바닥·다른 캐릭터를 감지. 아이템(트리거)은 Step 1에서 무시.

- [ ] **Step 4: 서버 컴파일 확인**

서버 인스턴스 id 해석 후:
`refresh_unity(unity_instance="LeagueOfPhysical-Server@<hash>")` → `read_console(unity_instance="LeagueOfPhysical-Server@<hash>", types=["error"])`.
Expected: 신규 에러 0. `KinematicMoveSystem`(LOP-Shared)·`ICollisionQuery`(GameFramework) 해석됨.

- [ ] **Step 5: 커밋 (두 저장소 각각)**

```bash
# GameFramework 저장소에서:
git add Runtime/Scripts/Game/UnityCollisionQuery.cs
git commit -m "feat(physics): 이동 sweep이 트리거 무시(QueryTriggerInteraction.Ignore)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"

# LeagueOfPhysical-Server 저장소에서:
git add Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(game): 서버 DI에 ICollisionQuery + KinematicMoveSystem 등록

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: 서버 행동 배선 — 캐릭터 kinematic + MoveCharacters 틱 스텝 (Server) — 사람 플레이 검증

캐릭터를 kinematic으로 만들고, 매 틱 `KinematicMoveSystem`으로 이동시킨다. **이 Task의 완료 조건은 사용자 플레이 검증**(자동 테스트 불가).

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/EntityCreator/CharacterCreator.cs:53`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/LOPEntity.cs` (`PushMotionToPhysics`)
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs`

**Interfaces:**
- Consumes: `KinematicMoveSystem.Tick`(Task 1·2 DI), `EntityTypeComponent.entityType`, `entityRegistry.Get(id)`, `entityManager.GetEntities<LOPEntity>()`, `entity.PushMotionToPhysics()`.

- [ ] **Step 1: 캐릭터를 kinematic으로 생성**

`CharacterCreator.cs:53` 교체:

```csharp
            physicsComponent.Initialize(true, false);   // kinematic, non-trigger — 우리가 직접 이동시킴
```

- [ ] **Step 2: `PushMotionToPhysics` kinematic 브랜치가 위치도 밀도록**

`LeagueOfPhysical-Server/Assets/Scripts/Entity/LOPEntity.cs`의 `PushMotionToPhysics` kinematic 분기 교체:

```csharp
            // kinematic 바디(캐릭터·아이템)는 velocity를 못 받는다(Unity 경고). 우리가 이동시키므로
            // World 위치·회전을 rb에 직접 밀어넣는다.
            if (rigidbody.isKinematic)
            {
                rigidbody.position = position;
                rigidbody.rotation = Quaternion.Euler(rotation);
                return;
            }
```

> `position`/`rotation`은 LOPEntity 파사드(World.Transform 백킹). `KinematicMoveSystem`이 World.Transform.Position을 직접 써서 파사드 이벤트가 안 뜨므로, 여기서 명시적으로 rb에 반영한다.

- [ ] **Step 3: `LOPRunner`에 `MoveCharacters()` 스텝 추가**

`LeagueOfPhysical-Server/Assets/Scripts/Game/LOPRunner.cs` 상단 주입 필드에 추가(기존 `[Inject]` 블록 옆):

```csharp
        [Inject] private KinematicMoveSystem kinematicMoveSystem;
```

`UpdateRunner()`에서 `DriveAbilityEffects();` **다음**, `SimulatePhysics();` **앞**에 호출 추가:

```csharp
            world.Tick(Runner.Time.tick, (float)tickUpdater.interval);
            DriveAbilityEffects();

            MoveCharacters();

            SimulatePhysics();
```

그리고 `DriveAbilityEffects()` 메서드 아래에 추가:

```csharp
        // 캐릭터를 키네마틱 이동(중력+collide-and-slide)시켜 World.Transform에 쓰고 rb에 반영한다.
        // 다이나믹 PhysX 캐릭터 적분을 대체 — 이후 SimulatePhysics는 다이나믹 물체만 처리.
        private void MoveCharacters()
        {
            Physics.SyncTransforms();   // 캐스트가 최신 콜라이더 포즈를 보도록(autoSyncTransforms=false)
            float dt = (float)tickUpdater.interval;
            foreach (var entity in entityManager.GetEntities<LOPEntity>())
            {
                if (entity.GetEntityComponent<EntityTypeComponent>()?.entityType != EntityType.Character)
                {
                    continue;   // 캐릭터만 — 아이템(kinematic-trigger)은 이동 안 함
                }
                kinematicMoveSystem.Tick(entityRegistry.Get(entity.entityId), dt);
                entity.PushMotionToPhysics();   // 새 World 위치·회전을 rb에 반영
            }
        }
```

- [ ] **Step 4: 서버 컴파일 확인**

서버 인스턴스 id 해석 후:
`refresh_unity(unity_instance="LeagueOfPhysical-Server@<hash>")` → `read_console(unity_instance="LeagueOfPhysical-Server@<hash>", types=["error"])`.
Expected: 신규 에러 0.

- [ ] **Step 5: 커밋**

```bash
# LeagueOfPhysical-Server 저장소에서:
git add Assets/Scripts/EntityCreator/CharacterCreator.cs \
        Assets/Scripts/Entity/LOPEntity.cs \
        Assets/Scripts/Game/LOPRunner.cs
git commit -m "feat(game): 서버 캐릭터 키네마틱 이동 배선 (MoveCharacters 틱 스텝)

캐릭터 kinematic 생성 + 매 틱 KinematicMoveSystem(중력+collide-and-slide)으로
이동. PhysX 캐릭터 적분 대체, Physics.Simulate는 다이나믹 물체만. 클라 무변경.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 6: 사람 플레이 검증 (완료 조건 — 사용자가 수행)**

서버+클라 실행 후 확인 (콘솔은 `read_console`로 양쪽 확인):

1. **걷기** — 캐릭터가 입력 방향으로 정상 이동, 정지 시 칼정지(드리프트 없음).
2. **낙하** — 공중에서 중력으로 떨어지고, **바닥에서 멈춘다**(바닥에 가라앉거나 위에 뜨지 않음).
3. **점프** — 점프로 올라갔다 중력으로 내려옴.
4. **벽 막힘** — 벽으로 걸어가면 뚫지 않고 막힌다(각진 벽은 미끄러짐).
5. **캐릭터끼리 충돌** — 다른 캐릭터를 뚫지 않는다(sweep이 상대 캡슐 감지).
6. **아이템** — 아이템 픽업이 이동을 막지 않는다(트리거 무시).
7. **콘솔** — 서버·클라 신규 에러/경고 0 (특히 kinematic velocity 경고 없음).

> **예상되는 정상 현상(버그 아님):** 클라는 아직 다이나믹 예측(슬라이스 3 전)이라 서버(키네마틱)와 약간의 reconciliation 보정이 보일 수 있다. 충돌 지점·낙하에서 미세 보정은 수용 — **심한 러버밴드·관통·바닥 통과가 아니면 통과.** 심한 발산이 보이면 보고(레이어 마스크/SyncTransforms/self-overlap 재점검 지점).

---

## Self-Review

**1. Spec coverage (슬라이스 2):**
- 캐릭터 kinematic 생성 → Task 3 Step 1. ✓
- KinematicMoveSystem(중력 분리 수직 스텝 + 커널) → Task 1. ✓ (중력=컨트롤러 레이어, mover 무지 — Global Constraints·코드 주석에 명시.)
- ICollisionQuery DI + 트리거 무시 → Task 2. ✓
- 호스트 틱 배선(MoveCharacters, SyncTransforms, EntityType gate) → Task 3 Step 3. ✓
- World→rb 위치 반영(파사드 우회 문제) → Task 3 Step 2(PushMotionToPhysics 위치 push). ✓
- grounded World 컴포넌트 신설 안 함 → 커널 반환 velocity의 y-투영으로 처리(Task 1 접지 테스트). ✓
- 클라 무변경 → Task 1·2·3 모두 서버/공유만, 클라 코드 미변경. ✓ (공유 LOP-Shared 추가는 클라도 컴파일하지만 클라 틱은 호출 안 함 → 행동 무변경.)

**2. Placeholder scan:** 모든 스텝에 실제 코드·명령·기대결과. Task 3 Step 6은 사용자 플레이 체크리스트(구체 7항목) — 자동화 불가 명시. "TBD"/"적절히" 없음. ✓

**3. Type consistency:**
- `KinematicMoveSystem(ICollisionQuery, int)` / `.Tick(GameFramework.World.Entity, float)` — Task 1 정의, Task 2 DI 팩토리·Task 3 호출과 일치. ✓
- `KinematicMoveInput(Vector3,Vector3,float,float,float,int)` / `KinematicMoveResult(Vector3,Vector3,bool)` — 슬라이스1과 일치. ✓
- `CollisionHit(bool,float,Vector3,Vector3)` fake·assert — 슬라이스1과 일치. ✓
- `EntityTypeComponent.entityType`(프로퍼티) / `EntityType.Character` — 실측 확인됨. ✓
- 중력 상수 `-9.81f*2f` = 서버 `Physics.gravity=(0,-9.81f*2,0)`와 일치. ✓

이상 없음.

---

## 다음 (슬라이스 3 예고 — 이 plan 범위 아님)

클라 호스트(`LOPRunner`)와 `Reconciler` replay 루프의 PhysX 캐릭터 적분(`PushMotionToPhysics`+`Simulate`+`SyncPhysics`)을 **같은 `KinematicMoveSystem`으로 교체** → 내 캐릭 예측이 서버 권위와 일치 → reconciliation 격차·러버밴드 감소. 별도 plan.
