# S2 — PhysicsComponent → PhysicsFollower Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 레거시 `PhysicsComponent : LOPComponent`(엔티티-컴포넌트)를 순수 MonoBehaviour `PhysicsFollower`로 전환해, S1 후 유일하게 남은 LOPComponent를 제거하고 `LOPEntity.components` 병렬 리스트를 비운다.

**Architecture:** 클·서의 `PhysicsComponent`는 각자 복제된 별개 파일이라 두 사이드를 독립 전환한다(공유 코드 없음). 각 사이드: 새 `PhysicsFollower.cs` 생성(`worldEntity` 보유, `Depenetrate` 죽은코드 삭제, `physicsGameObject` reactive→필드) → 옛 `PhysicsComponent.cs` 삭제 → 창조자 2개 + `LOPEntity` 브릿지 재배선. 물리 루프·`MotionBridge`는 안 건드린다(strangler; 경로 통합은 S3).

**Tech Stack:** Unity 6 (C#), GameFramework.World(순수 C# 코어), VContainer DI, MessagePipe(GlobalMessagePipe keyed pub/sub), UnityMCP(컴파일/콘솔 검증).

## Global Constraints

- **매 태스크 끝에 게임이 그대로 동작해야 함**(strangler). 클·서 각 editor 컴파일 에러 0.
- `PhysicsFollower`는 **순수 MonoBehaviour**(엔티티-컴포넌트 아님, `LOPComponent`/`MonoComponent` 상속 안 함). `ICleanup` 구현해 파괴 스윕이 구독을 해제.
- **World 타입은 풀 네임스페이스 한정**(`GameFramework.World.Transform` 등) — `UnityEngine`과 충돌 회피(CLAUDE.md).
- **물리 루프·`MotionBridge`/`PhysicsBody`·`LOPEntityController` 무변경.** `LOPEntity.SyncPhysics`/`PushMotionToPhysics`는 룩업만 `GetComponent<PhysicsFollower>()`로 바꾸고 로직 유지.
- **UnityMCP 호출은 컨트롤러가 수행**(구현 서브에이전트는 코드+git만). 인스턴스: client `LeagueOfPhysical-Client@de70658b9450cbb4`, server `LeagueOfPhysical-Server@f99391fa2dbaaf3c`(해시는 세션마다 `mcpforunity://instances`로 확인).
- 새 `.cs` 생성·삭제 시 `.meta`도 함께(`git rm`이 `.cs.meta`를 자동 스테이징 안 하면 명시 `git rm`; 생성 `.meta`는 controller가 refresh 후 커밋).
- 브랜치: client는 `feature/entity-s2-physics-follower`(이미 존재), server는 이 브랜치 생성. 커밋 끝에 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- **Assembly-CSharp라 유닛 테스트 불가** — 검증은 컴파일 + 인게임 플레이.

---

## File Structure

**Client:** Create `Assets/Scripts/Component/PhysicsFollower.cs`; Delete `Assets/Scripts/Component/PhysicsComponent.cs`; Modify `Assets/Scripts/EntityCreator/CharacterCreator.cs`, `Assets/Scripts/EntityCreator/ItemCreator.cs`, `Assets/Scripts/Entity/LOPEntity.cs`.

**Server:** Create `Assets/Scripts/Component/PhysicsFollower.cs`; Delete `Assets/Scripts/Component/PhysicsComponent.cs`; Modify `Assets/Scripts/EntityCreator/CharacterCreator.cs`, `Assets/Scripts/EntityCreator/ItemCreator.cs`, `Assets/Scripts/Entity/LOPEntity.cs`. (Server `PhysicsFollower` additionally keeps the `TriggerDetector`/`ItemTouch` wiring the client lacks.)

---

## Task 1: 클라이언트 — PhysicsComponent → PhysicsFollower

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Component/PhysicsFollower.cs`
- Delete: `LeagueOfPhysical-Client/Assets/Scripts/Component/PhysicsComponent.cs` (+.meta)
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs`, `Assets/Scripts/EntityCreator/ItemCreator.cs`, `Assets/Scripts/Entity/LOPEntity.cs`

**Interfaces:**
- Produces: `PhysicsFollower : MonoBehaviour, ICleanup` with `void Initialize(GameFramework.World.Entity worldEntity, bool isKinematic, bool isTrigger)`, `Rigidbody entityRigidbody { get; }`, `Collider[] entityColliders { get; }`.
- Consumes: `GameFramework.World.Entity`(worldEntity, 창조자 로컬), `GlobalMessagePipe`, `KinematicDepenetration`(삭제 대상이라 미사용).

- [ ] **Step 1: `PhysicsFollower.cs` 생성 (클라)**

Create `LeagueOfPhysical-Client/Assets/Scripts/Component/PhysicsFollower.cs`:
```csharp
using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// World.Transform을 따라가는 물리 바디(Rigidbody/캡슐 콜라이더)를 소유·구동하는 프레젠테이션 컴포넌트.
    /// 엔티티-컴포넌트가 아닌 순수 MonoBehaviour — 시뮬(World.Transform)이 권위, Rigidbody는 팔로워.
    /// </summary>
    public class PhysicsFollower : MonoBehaviour, ICleanup
    {
        private System.IDisposable propertyChangeSubscription;
        private GameFramework.World.Entity worldEntity;

        private GameObject physicsGameObject;

        public Rigidbody entityRigidbody { get; private set; }
        public Collider[] entityColliders { get; private set; }

        public void Initialize(GameFramework.World.Entity worldEntity, bool isKinematic, bool isTrigger)
        {
            this.worldEntity = worldEntity;
            var worldTransform = worldEntity.Get<GameFramework.World.Transform>();
            var worldVelocity = worldEntity.Get<GameFramework.World.Velocity>();

            propertyChangeSubscription = GlobalMessagePipe.GetSubscriber<string, PropertyChange>().Subscribe(worldEntity.Id, OnPropertyChange);

            GameObject physics = transform.parent.Find("Physics").gameObject;
            physicsGameObject = physics.CreateChild("PhysicsGameObject");
            physicsGameObject.layer = LayerMask.NameToLayer("Character");

            entityRigidbody = physicsGameObject.AddComponent<Rigidbody>();
            entityRigidbody.linearDamping = 0f;   // 수평 정지는 이동 모터가 0으로 제동. 수직=순수 중력.
            entityRigidbody.angularDamping = 0.05f;
            entityRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            entityRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            entityRigidbody.position = worldTransform.Position.ToUnity();
            entityRigidbody.rotation = worldTransform.Rotation.ToUnity();
            entityRigidbody.linearVelocity = worldVelocity.Linear.ToUnity();
            entityRigidbody.isKinematic = isKinematic;

            CapsuleCollider capsuleCollider = physicsGameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = 0.35f;
            capsuleCollider.height = 1.5f;
            capsuleCollider.center = new Vector3(0, capsuleCollider.height * 0.5f, 0);
            capsuleCollider.isTrigger = isTrigger;
            entityColliders = new Collider[] { capsuleCollider };
        }

        public void Cleanup()
        {
            propertyChangeSubscription?.Dispose();
        }

        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(LOPEntity.position):
                    entityRigidbody.position = worldEntity.Get<GameFramework.World.Transform>().Position.ToUnity();
                    break;

                // velocity·rotation은 BeforePhysicsSimulation 브릿지(LOPEntity.PushMotionToPhysics)가 담당.
            }
        }
    }
}
```

- [ ] **Step 2: 옛 `PhysicsComponent.cs` 삭제 (클라)**

Run:
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client rm Assets/Scripts/Component/PhysicsComponent.cs
```
`.cs.meta`가 함께 스테이징 안 되면: `git -C ... rm Assets/Scripts/Component/PhysicsComponent.cs.meta`.

- [ ] **Step 3: `CharacterCreator.cs` 재배선 (클라)**

`Assets/Scripts/EntityCreator/CharacterCreator.cs`에서 PhysicsComponent 3블록 교체:
```csharp
            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            objectResolver.Inject(physicsComponent);
            // 모든 캐릭터 kinematic — 우리가 직접 이동시킨다. 내 캐릭=예측(KinematicMoveSystem), 남=스냅 팔로워.
            physicsComponent.Initialize(true, false);
```
→
```csharp
            PhysicsFollower physicsFollower = entity.gameObject.AddComponent<PhysicsFollower>();
            objectResolver.Inject(physicsFollower);
            // 모든 캐릭터 kinematic — 우리가 직접 이동시킨다. 내 캐릭=예측(KinematicMoveSystem), 남=스냅 팔로워.
            physicsFollower.Initialize(worldEntity, true, false);
```
그리고 PhysicsBody 공유 줄:
```csharp
            worldEntity.Add(new PhysicsBody(physicsComponent.entityRigidbody, (CapsuleCollider)physicsComponent.entityColliders[0]));
```
→
```csharp
            worldEntity.Add(new PhysicsBody(physicsFollower.entityRigidbody, (CapsuleCollider)physicsFollower.entityColliders[0]));
```

- [ ] **Step 4: `ItemCreator.cs` 재배선 (클라)**

`Assets/Scripts/EntityCreator/ItemCreator.cs`에서:
```csharp
            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            objectResolver.Inject(physicsComponent);
            physicsComponent.Initialize(true, true);
```
→
```csharp
            PhysicsFollower physicsFollower = entity.gameObject.AddComponent<PhysicsFollower>();
            objectResolver.Inject(physicsFollower);
            physicsFollower.Initialize(worldEntity, true, true);
```
(ItemCreator는 PhysicsBody를 안 만드므로 그 줄 없음 — 확인만.)

- [ ] **Step 5: `LOPEntity.cs` 브릿지 룩업 교체 (클라)**

`Assets/Scripts/Entity/LOPEntity.cs`의 `SyncPhysics()`·`PushMotionToPhysics()` 두 메서드에서:
```csharp
            PhysicsComponent physicsComponent = this.GetEntityComponent<PhysicsComponent>();

            if (physicsComponent == null)
```
→ (각 메서드 첫 두 줄)
```csharp
            PhysicsFollower physicsFollower = GetComponent<PhysicsFollower>();

            if (physicsFollower == null)
```
그리고 각 메서드 본문의 `physicsComponent.entityRigidbody` → `physicsFollower.entityRigidbody`(SyncPhysics 3곳: `.isKinematic`/`.position`/`.rotation`/`.linearVelocity`; PushMotionToPhysics: `Rigidbody rigidbody = physicsFollower.entityRigidbody;`). **로직·조건은 그대로.**

- [ ] **Step 6: 커밋 (클라)**

컴파일 검증은 컨트롤러가 수행. 편집 후:
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts/Component/PhysicsFollower.cs Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/EntityCreator/ItemCreator.cs Assets/Scripts/Entity/LOPEntity.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "refactor(entity): 클라 PhysicsComponent → 순수 MonoBehaviour PhysicsFollower

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
(`.meta` 삭제분이 Step 2에서 스테이징됐으면 이 커밋에 포함. 새 `PhysicsFollower.cs.meta`는 controller가 refresh 후 커밋.)

---

## Task 2: 서버 — PhysicsComponent → PhysicsFollower

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Component/PhysicsFollower.cs`
- Delete: `LeagueOfPhysical-Server/Assets/Scripts/Component/PhysicsComponent.cs` (+.meta)
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs`, `Assets/Scripts/EntityCreator/ItemCreator.cs`, `Assets/Scripts/Entity/LOPEntity.cs`

**Interfaces:**
- Produces: 클라와 동일 시그니처의 `PhysicsFollower`(추가로 `TriggerDetector`+`ItemTouch` 발행 보유).

- [ ] **Step 1: 서버 브랜치 생성**

Run:
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server checkout -b feature/entity-s2-physics-follower
```

- [ ] **Step 2: `PhysicsFollower.cs` 생성 (서버)**

Create `LeagueOfPhysical-Server/Assets/Scripts/Component/PhysicsFollower.cs`:
```csharp
using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using Unity.VisualScripting;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// World.Transform을 따라가는 물리 바디(Rigidbody/캡슐 콜라이더)를 소유·구동하는 프레젠테이션 컴포넌트.
    /// 엔티티-컴포넌트가 아닌 순수 MonoBehaviour — 시뮬(World.Transform)이 권위, Rigidbody는 팔로워.
    /// 서버는 트리거로 아이템 접촉(ItemTouch)을 감지한다.
    /// </summary>
    public class PhysicsFollower : MonoBehaviour, ICleanup
    {
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private IPublisher<ItemTouch> itemTouchPublisher;

        private System.IDisposable propertyChangeSubscription;
        private GameFramework.World.Entity worldEntity;

        private GameObject physicsGameObject;

        public Rigidbody entityRigidbody { get; private set; }
        public Collider[] entityColliders { get; private set; }

        public void Initialize(GameFramework.World.Entity worldEntity, bool isKinematic, bool isTrigger)
        {
            this.worldEntity = worldEntity;
            var worldTransform = worldEntity.Get<GameFramework.World.Transform>();
            var worldVelocity = worldEntity.Get<GameFramework.World.Velocity>();

            propertyChangeSubscription = GlobalMessagePipe.GetSubscriber<string, PropertyChange>().Subscribe(worldEntity.Id, OnPropertyChange);

            GameObject physics = transform.parent.Find("Physics").gameObject;
            physicsGameObject = physics.CreateChild("PhysicsGameObject");
            physicsGameObject.layer = LayerMask.NameToLayer("Character");

            entityRigidbody = physicsGameObject.AddComponent<Rigidbody>();
            entityRigidbody.linearDamping = 0f;   // 수평 정지는 이동 모터가 0으로 제동. 수직=순수 중력.
            entityRigidbody.angularDamping = 0.05f;
            entityRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            entityRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            entityRigidbody.position = worldTransform.Position.ToUnity();
            entityRigidbody.rotation = worldTransform.Rotation.ToUnity();
            entityRigidbody.linearVelocity = worldVelocity.Linear.ToUnity();
            entityRigidbody.isKinematic = isKinematic;

            CapsuleCollider capsuleCollider = physicsGameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = 0.35f;
            capsuleCollider.height = 1.5f;
            capsuleCollider.center = new Vector3(0, capsuleCollider.height * 0.5f, 0);
            capsuleCollider.isTrigger = isTrigger;
            entityColliders = new Collider[] { capsuleCollider };

            TriggerDetector triggerDetector = physicsGameObject.GetOrAddComponent<TriggerDetector>();
            triggerDetector.onTriggerEnter += OnTriggerEnter;
            triggerDetector.onTriggerStay += OnTriggerStay;
            triggerDetector.onTriggerExit += OnTriggerExit;
        }

        public void Cleanup()
        {
            propertyChangeSubscription?.Dispose();
        }

        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(LOPEntity.position):
                    entityRigidbody.position = worldEntity.Get<GameFramework.World.Transform>().Position.ToUnity();
                    break;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            LOPEntity otherEntity = other.transform.parent?.parent?.GetComponentInChildren<LOPEntity>();
            if (otherEntity == null)
            {
                Debug.LogWarning($"Trigger detected with non-entity object: {other.name}");
                return;
            }

            if (entityRegistry.Get(otherEntity.entityId)?.Has<GameFramework.World.Ownership>() == true)
            {
                itemTouchPublisher.Publish(new ItemTouch(worldEntity.Id, otherEntity.entityId));
            }
        }

        private void OnTriggerStay(Collider other)
        {
        }

        private void OnTriggerExit(Collider other)
        {
        }
    }
}
```

- [ ] **Step 3: 옛 `PhysicsComponent.cs` 삭제 (서버)**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server rm Assets/Scripts/Component/PhysicsComponent.cs
```
`.cs.meta` 미스테이징 시 명시 `git rm`.

- [ ] **Step 4: `CharacterCreator.cs` 재배선 (서버)**

`Assets/Scripts/EntityCreator/CharacterCreator.cs`:
```csharp
            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            objectResolver.Inject(physicsComponent);
            physicsComponent.Initialize(true, false);   // kinematic, non-trigger — 우리가 직접 이동시킴
```
→
```csharp
            PhysicsFollower physicsFollower = entity.gameObject.AddComponent<PhysicsFollower>();
            objectResolver.Inject(physicsFollower);
            physicsFollower.Initialize(worldEntity, true, false);   // kinematic, non-trigger — 우리가 직접 이동시킴
```
그리고 PhysicsBody 공유 줄 `physicsComponent.entityRigidbody`/`.entityColliders[0]` → `physicsFollower.…`.

- [ ] **Step 5: `ItemCreator.cs` 재배선 (서버)**

`Assets/Scripts/EntityCreator/ItemCreator.cs`:
```csharp
            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            objectResolver.Inject(physicsComponent);
            physicsComponent.Initialize(true, true);
```
→
```csharp
            PhysicsFollower physicsFollower = entity.gameObject.AddComponent<PhysicsFollower>();
            objectResolver.Inject(physicsFollower);
            physicsFollower.Initialize(worldEntity, true, true);
```

- [ ] **Step 6: `LOPEntity.cs` 브릿지 룩업 교체 (서버)**

`Assets/Scripts/Entity/LOPEntity.cs`의 `SyncPhysics()`·`PushMotionToPhysics()`에서 `this.GetEntityComponent<PhysicsComponent>()` → `GetComponent<PhysicsFollower>()`, 변수·널체크·`.entityRigidbody` 참조를 `physicsFollower`로(Task 1 Step 5와 동일 패턴). 로직 그대로.

- [ ] **Step 7: 커밋 (서버, 픽스처 제외)**

서버 repo엔 커밋 금지 로컬 픽스처(`ConfigureRoomComponent.cs`·`GameRuleSystem.cs`·`DefaultVolumeProfile.asset`·`GraphicsSettings.asset`)가 있으니 **명시 `git add`만** 사용:
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Component/PhysicsFollower.cs Assets/Scripts/Component/PhysicsComponent.cs Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/EntityCreator/ItemCreator.cs Assets/Scripts/Entity/LOPEntity.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(entity): 서버 PhysicsComponent → 순수 MonoBehaviour PhysicsFollower

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
(`PhysicsComponent.cs`의 삭제를 add에 포함시켜 스테이징. `.meta`는 Step 3에서 처리.)

---

## Task 3: 통합 검증 + ROADMAP

**Files:** `docs/ROADMAP.md`(client)

- [ ] **Step 1: 클·서 동시 플레이 검증**

두 editor 플레이. 확인:
- 캐릭터 이동·점프·중력 안착 정상.
- **캐릭터끼리 충돌**(단단한 벽, 안 겹침) 정상.
- 스폰 시 지면 관통 없음(Depenetrate 경로=MotionBridge).
- **아이템(exp 마블) 줍기**(서버 트리거→ItemTouch) 정상.
- 전역 공격(G)·넉백 등 기존 동작 무변화.
- 클·서 콘솔 CS 에러 0, 신규 예외 0(기존 파킹 NRE만).

- [ ] **Step 2: `entity.components`가 빈 것 확인**

Grep: 클·서 `Assets/Scripts`에서 `AddEntityComponent<` 호출 0건, `PhysicsComponent` 참조 0건. (`GetEntityComponent`는 다른 용도로 남아있을 수 있으나 `PhysicsComponent` 대상은 0.)

- [ ] **Step 3: ROADMAP 갱신 + 커밋**

`docs/ROADMAP.md` umbrella 관련 섹션에 S2 완료 한 줄 추가 + S3를 다음으로 표시. 커밋:
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add docs/ROADMAP.md
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "docs(roadmap): 엔티티 재구조화 S2(PhysicsComponent→PhysicsFollower) 완료 반영

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage:** ✅ `PhysicsFollower` 생성(클 T1/서 T2) · `Depenetrate` 삭제(새 파일에 없음) · `physicsGameObject` 필드 강등(새 파일) · `worldEntity` 보유(Initialize) · 창조자 재배선(T1·T2) · `LOPEntity` 브릿지 `GetComponent`(T1S5·T2S6) · 서버 트리거 유지(T2) · `entity.components` 빔 확인(T3S2) · 인게임 검증(T3S1). 스펙 요구 전부 매핑.

**Placeholder scan:** 코드 스텝 전부 실제 코드. TODO/TBD 없음.

**Type consistency:** `PhysicsFollower.Initialize(GameFramework.World.Entity, bool, bool)` · `entityRigidbody`/`entityColliders` 시그니처가 정의(T1S1/T2S2)와 소비(창조자·LOPEntity)에서 일치. `OnPropertyChange`의 `nameof(LOPEntity.position)`="position"이 발행자(LOPEntity.position setter)와 일치.

**주의(구현자):** ① `PhysicsFollower`는 `entity.gameObject.AddComponent`로 LOPEntity GameObject에 얹힘 → `transform.parent.Find("Physics")`와 `LOPEntity.GetComponent<PhysicsFollower>()`가 성립. ② 서버 `PhysicsFollower`는 `objectResolver.Inject` 필요(entityRegistry/itemTouchPublisher). ③ `ToUnity()`는 `using GameFramework;`의 벡터 확장(원본 사용처와 동일).
