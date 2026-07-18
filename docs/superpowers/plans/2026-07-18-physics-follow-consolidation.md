# 물리 rb-follow 경로 통합(③) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** rb가 World.Transform을 따라가는 3중복 경로를 호스트 단일 패스(`LOPRunner`에서 `PhysicsBody` 가진 모든 엔티티에 `MotionBridge.PushMotion`)로 통합하고, 레거시 경로(`LOPEntityController`/`LOPEntity.SyncPhysics`·`PushMotionToPhysics`/`PhysicsFollower.OnPropertyChange`)를 삭제한다.

**Architecture:** strangler. Task 1=호스트 패스 신설 + 아이템 `PhysicsBody` 추가 + Reconciler 스왑(레거시 공존, idempotent라 동작 무변화). Task 2=레거시 경로 삭제(호스트 패스가 단독으로 rb 팔로우 — behavioral 순간, 플레이 검증). 클·서만(GameFramework·LOP-Shared 무변경 — `MotionBridge.PushMotion` 기존).

**Tech Stack:** Unity 6 (C#), GameFramework.World(`EntityRegistry`/`IMotionBridge`), VContainer DI, MessagePipe, UnityMCP.

## Global Constraints

- **매 태스크 끝에 게임이 그대로 동작**(strangler). 클·서 editor 컴파일 에러 0.
- **UnityMCP는 컨트롤러가 수행**(구현 서브에이전트는 코드+git만). 인스턴스: client `LeagueOfPhysical-Client@de70658b9450cbb4`, server `LeagueOfPhysical-Server@f99391fa2dbaaf3c`(해시는 `mcpforunity://instances`로 확인).
- World 타입은 **풀 네임스페이스 한정**(`GameFramework.World.IMotionBridge` 등, CLAUDE.md).
- **서버 do-not-commit 픽스처**(`Entrance/EntranceComponent/ConfigureRoomComponent.cs`·`Game/GameRuleSystem.cs`·`DefaultVolumeProfile.asset`·`GraphicsSettings.asset`) 스테이징 금지 — 명시 `git add`만.
- 파일 삭제 시 `.meta`도(`git rm` 명시).
- 브랜치: client `feature/physics-follow-consolidation`(존재), server는 생성. 커밋 끝 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- **Assembly-CSharp라 유닛 불가** — 검증=컴파일 + 인게임 플레이.

---

## File Structure

- **Client:** Modify `Assets/Scripts/Game/LOPRunner.cs`, `Assets/Scripts/Entity/Reconciler.cs`, `Assets/Scripts/EntityCreator/ItemCreator.cs`(T1); `Assets/Scripts/EntityCreator/CharacterCreator.cs`, `Assets/Scripts/EntityCreator/ItemCreator.cs`, `Assets/Scripts/Entity/LOPEntity.cs`, `Assets/Scripts/Component/PhysicsFollower.cs`(T2); Delete `Assets/Scripts/Entity/LOPEntityController.cs`(T2).
- **Server:** Modify `Assets/Scripts/Game/LOPRunner.cs`, `Assets/Scripts/EntityCreator/ItemCreator.cs`(T1); `Assets/Scripts/EntityCreator/CharacterCreator.cs`, `Assets/Scripts/EntityCreator/ItemCreator.cs`, `Assets/Scripts/Entity/LOPEntity.cs`, `Assets/Scripts/Component/PhysicsFollower.cs`(T2); Delete `Assets/Scripts/Entity/LOPEntityController.cs`(T2).

---

## Task 1: 호스트 follow 패스 신설 (레거시 공존)

**Files:**
- Client: Modify `Assets/Scripts/Game/LOPRunner.cs`, `Assets/Scripts/Entity/Reconciler.cs`, `Assets/Scripts/EntityCreator/ItemCreator.cs`.
- Server: Modify `Assets/Scripts/Game/LOPRunner.cs`, `Assets/Scripts/EntityCreator/ItemCreator.cs`.

**Interfaces:**
- Consumes: `GameFramework.World.IMotionBridge.PushMotion(GameFramework.World.Entity)`, `GameFramework.World.EntityRegistry.All`, `LOP.PhysicsBody(Rigidbody, CapsuleCollider)`.

- [ ] **Step 1: 서버 브랜치 생성**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server checkout -b feature/physics-follow-consolidation
```

- [ ] **Step 2: 클라 `LOPRunner`에 follow 패스 + `IMotionBridge` 주입**

`Assets/Scripts/Game/LOPRunner.cs`(클라):
① 주입 필드 추가(`[Inject] private GameFramework.World.EntityRegistry entityRegistry;` 아래):
```csharp
        [Inject] private GameFramework.World.IMotionBridge motionBridge;
```
② `SimulatePhysics()` 교체:
```csharp
        private void SimulatePhysics()
        {
            DispatchEvent<BeforePhysicsSimulation>();

            // World.Transform → rb 팔로우: PhysicsBody 가진 모든 엔티티(내 캐릭=예측, 남·아이템=보간).
            // Simulated는 world.Tick서 이미 밀렸으나 idempotent. per-entity LOPEntityController 대체.
            foreach (var entity in entityRegistry.All)
            {
                motionBridge.PushMotion(entity);
            }

            physicsSimulator.Simulate((float)tickUpdater.interval);

            DispatchEvent<AfterPhysicsSimulation>();
        }
```

- [ ] **Step 3: 서버 `LOPRunner`에 follow 패스 + `IMotionBridge` 주입**

`Assets/Scripts/Game/LOPRunner.cs`(서버): 클라 Step 2와 동일 —
① `[Inject] private GameFramework.World.IMotionBridge motionBridge;` 추가(`entityRegistry` 주입 아래).
② `SimulatePhysics()`를 Step 2와 동일 본문으로 교체.

- [ ] **Step 4: 클·서 `ItemCreator`에 `PhysicsBody` 추가**

`Assets/Scripts/EntityCreator/ItemCreator.cs`(클·서 동일): `physicsFollower.Initialize(worldEntity, true, true);` 바로 다음 줄에:
```csharp
            worldEntity.Add(new PhysicsBody(physicsFollower.entityRigidbody, (CapsuleCollider)physicsFollower.entityColliders[0]));
```
(이로써 통합 패스가 아이템 rb를 따라가게 함. `LOPEntityController` 배선은 Task 2에서 제거.)

- [ ] **Step 5: 클라 `Reconciler` 롤백-후 push 스왑**

`Assets/Scripts/Entity/Reconciler.cs`:
① 주입 필드 추가(`[Inject] private GameFramework.World.IWorld world;` 아래):
```csharp
        [Inject] private GameFramework.World.IMotionBridge motionBridge;
```
② `entity.PushMotionToPhysics();`(하드복원 블록, `entity.velocity = snap.velocity;` 다음) → 교체:
```csharp
            motionBridge.PushMotion(worldEntity);
```
(`Physics.SyncTransforms();` 그다음 줄은 유지. `worldEntity`는 이 메서드에 이미 있음.)

- [ ] **Step 6: 커밋 (컨트롤러가 컴파일+플레이 검증 — 동작 무변화 기대)**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Entity/Reconciler.cs Assets/Scripts/EntityCreator/ItemCreator.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "feat(physics): 호스트 단일 rb-follow 패스 신설 + 아이템 PhysicsBody + Reconciler 스왑 (레거시 공존)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/EntityCreator/ItemCreator.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "feat(physics): 호스트 단일 rb-follow 패스 신설 + 아이템 PhysicsBody (레거시 공존)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 레거시 경로 삭제

**Files:**
- Client: Delete `Assets/Scripts/Entity/LOPEntityController.cs`; Modify `Assets/Scripts/EntityCreator/CharacterCreator.cs`, `Assets/Scripts/EntityCreator/ItemCreator.cs`, `Assets/Scripts/Entity/LOPEntity.cs`, `Assets/Scripts/Component/PhysicsFollower.cs`.
- Server: Delete `Assets/Scripts/Entity/LOPEntityController.cs`; Modify `Assets/Scripts/EntityCreator/CharacterCreator.cs`, `Assets/Scripts/EntityCreator/ItemCreator.cs`, `Assets/Scripts/Entity/LOPEntity.cs`, `Assets/Scripts/Component/PhysicsFollower.cs`.

**Interfaces:**
- Consumes: Task 1의 호스트 패스(단독으로 rb 팔로우).

- [ ] **Step 1: 4개 창조자에서 `LOPEntityController` 배선 제거**

`CharacterCreator.cs`·`ItemCreator.cs`(클·서 총 4파일) 각각에서 아래 3줄 블록 삭제:
```csharp
            LOPEntityController controller = root.CreateChildWithComponent<LOPEntityController>();
            objectResolver.Inject(controller);
            controller.SetEntity(entity);
```

- [ ] **Step 2: `LOPEntityController.cs` 삭제 (클·서)**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client rm Assets/Scripts/Entity/LOPEntityController.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server rm Assets/Scripts/Entity/LOPEntityController.cs
```
(`.cs.meta` 미스테이징 시 명시 `git rm`.)

- [ ] **Step 3: `LOPEntity`에서 `SyncPhysics`/`PushMotionToPhysics` 삭제 (클·서)**

`Assets/Scripts/Entity/LOPEntity.cs`(클·서 각각): `public void SyncPhysics() { … }`와 `public void PushMotionToPhysics() { … }` 두 메서드 전체 삭제(주석 포함).

- [ ] **Step 4: `PhysicsFollower`에서 reactive 경로 제거 (클라)**

`Assets/Scripts/Component/PhysicsFollower.cs`(클라) 전체를 아래로 교체(`ICleanup`/구독/`OnPropertyChange` 제거):
```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// World.Transform을 따라가는 물리 바디(Rigidbody/캡슐 콜라이더)를 소유하는 프레젠테이션 컴포넌트.
    /// rb 팔로우는 호스트 단일 패스(LOPRunner)가 MotionBridge.PushMotion으로 구동한다.
    /// </summary>
    public class PhysicsFollower : MonoBehaviour
    {
        private GameObject physicsGameObject;

        public Rigidbody entityRigidbody { get; private set; }
        public Collider[] entityColliders { get; private set; }

        public void Initialize(GameFramework.World.Entity worldEntity, bool isKinematic, bool isTrigger)
        {
            var worldTransform = worldEntity.Get<GameFramework.World.Transform>();
            var worldVelocity = worldEntity.Get<GameFramework.World.Velocity>();

            GameObject physics = transform.parent.Find("Physics").gameObject;
            physicsGameObject = physics.CreateChild("PhysicsGameObject");
            physicsGameObject.layer = LayerMask.NameToLayer("Character");

            entityRigidbody = physicsGameObject.AddComponent<Rigidbody>();
            entityRigidbody.linearDamping = 0f;
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
    }
}
```

- [ ] **Step 5: `PhysicsFollower`에서 reactive 경로 제거 (서버, 트리거 유지)**

`Assets/Scripts/Component/PhysicsFollower.cs`(서버) 전체를 아래로 교체(`ICleanup`/구독/`OnPropertyChange`만 제거, `worldEntity`/트리거/주입 유지):
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
    /// World.Transform을 따라가는 물리 바디(Rigidbody/캡슐 콜라이더)를 소유하는 프레젠테이션 컴포넌트.
    /// rb 팔로우는 호스트 단일 패스(LOPRunner)가 MotionBridge.PushMotion으로 구동한다.
    /// 서버는 트리거로 아이템 접촉(ItemTouch)을 감지한다.
    /// </summary>
    public class PhysicsFollower : MonoBehaviour
    {
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private IPublisher<ItemTouch> itemTouchPublisher;

        private GameFramework.World.Entity worldEntity;
        private GameObject physicsGameObject;

        public Rigidbody entityRigidbody { get; private set; }
        public Collider[] entityColliders { get; private set; }

        public void Initialize(GameFramework.World.Entity worldEntity, bool isKinematic, bool isTrigger)
        {
            this.worldEntity = worldEntity;
            var worldTransform = worldEntity.Get<GameFramework.World.Transform>();
            var worldVelocity = worldEntity.Get<GameFramework.World.Velocity>();

            GameObject physics = transform.parent.Find("Physics").gameObject;
            physicsGameObject = physics.CreateChild("PhysicsGameObject");
            physicsGameObject.layer = LayerMask.NameToLayer("Character");

            entityRigidbody = physicsGameObject.AddComponent<Rigidbody>();
            entityRigidbody.linearDamping = 0f;
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

- [ ] **Step 6: (기회적) 죽은 PropertyChange 발행 정리**

Grep 클·서 `Assets/Scripts`에서 `GetSubscriber<string, PropertyChange>` = 0 확인. **0이면** `LOPEntity.cs`(클·서)에서:
- `position` setter의 `RaisePropertyChanged(this, new PropertyChangedEventArgs(nameof(position)));` 줄 삭제,
- `public void RaisePropertyChanged(object sender, PropertyChangedEventArgs e) { … }` 메서드 삭제,
- 미사용 `using System.ComponentModel;`/`using MessagePipe;` 정리(컴파일 경고만이면 선택).
**0이 아니면**(예상 밖 구독자) 이 스텝 건너뛰고 DONE_WITH_CONCERNS로 보고.

- [ ] **Step 7: 참조 0 확인 + 커밋 (컨트롤러가 컴파일+플레이 검증)**

Grep 클·서: `LOPEntityController`·`SyncPhysics`·`PushMotionToPhysics`·`OnPropertyChange`(PhysicsFollower) 참조 0.
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "refactor(physics): 레거시 rb-follow 경로 삭제 (LOPEntityController/SyncPhysics/PushMotionToPhysics/reactive)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Entity/LOPEntityController.cs Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/EntityCreator/ItemCreator.cs Assets/Scripts/Entity/LOPEntity.cs Assets/Scripts/Component/PhysicsFollower.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(physics): 레거시 rb-follow 경로 삭제 (LOPEntityController/SyncPhysics/PushMotionToPhysics/reactive)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
(서버는 do-not-commit 픽스처 회피 위해 명시 `git add` 경로만.)

---

## Task 3: 통합 검증 + ROADMAP

**Files:** `docs/ROADMAP.md`(client)

- [ ] **Step 1: 클·서 동시 플레이 검증(핵심=물리 동작)**

두 editor 플레이. 확인:
- 내 캐릭 이동·점프·중력·**캐릭터끼리 충돌**(단단한 벽) 정상.
- **원격 캐릭터**(2nd 클라/AI)가 내 화면에서 위치+**회전** 따라오고 충돌 정상.
- **아이템 줍기**(트리거) 정상.
- **넉백·전역공격(G)·Reconciler 롤백**(고RTT/손실 주입) 후 위치 튐 없음.
- 클·서 콘솔 신규 예외 0(기존 파킹 NRE만).

- [ ] **Step 2: 참조 0 재확인**

Grep(클·서): `LOPEntityController`/`SyncPhysics`/`PushMotionToPhysics` 0.

- [ ] **Step 3: ROADMAP 갱신 + 커밋**

`docs/ROADMAP.md`에 물리 통합 완료 한 줄 추가 + 다음(S4). 커밋:
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add docs/ROADMAP.md
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "docs(roadmap): 물리 rb-follow 경로 통합(③) 완료 반영

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage:** ✅ 호스트 단일 패스(T1 S2·S3) · 아이템 PhysicsBody(T1 S4) · Reconciler 스왑(T1 S5) · LOPEntityController 삭제+배선 제거(T2 S1·S2) · SyncPhysics/PushMotionToPhysics 삭제(T2 S3) · PhysicsFollower reactive 제거(T2 S4·S5) · 죽은 PropertyChange 정리(T2 S6) · 파리티 위치(BeforePhysicsSimulation) · 검증(T3). 스펙 요구 전부 매핑.

**Placeholder scan:** 코드 스텝 전부 실제 코드/전체 파일. TODO/TBD 없음.

**Type consistency:** `motionBridge.PushMotion(GameFramework.World.Entity)` — `entityRegistry.All` 원소 타입과 일치. `LOPRunner`/`Reconciler` 둘 다 `IMotionBridge` 주입. 아이템 `PhysicsBody(entityRigidbody, (CapsuleCollider)entityColliders[0])` = CharacterCreator와 동일 시그니처.

**주의(구현자):** ① Task 1은 레거시 공존이라 동작 무변화 기대(rb 이중 push=idempotent). ② `IMotionBridge`는 DI 등록됨(GameLifetimeScope, `MotionBridge` 구체). ③ 서버 `LOPRunner`도 `entityRegistry` 주입돼 있음(follow 루프 가능). ④ Task 2 Step 6은 grep 조건부(구독자 0일 때만 발행 제거).
