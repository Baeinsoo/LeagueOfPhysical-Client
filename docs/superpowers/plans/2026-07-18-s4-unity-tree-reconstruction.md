# S4 — Unity 트리 재구성 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 엔티티 Unity 트리를 업계 표준으로 재구성한다 — 빈 루트를 `LOPActor` 앵커 겸 시뮬 바디(kinematic rb+콜라이더)로, 모든 behavior를 루트 컴포넌트로, 모델 인스턴스를 루트 직속 렌더 바디 자식으로, 문자열/구조 순회 배선을 `GetComponent`/`GetComponentInParent`로 교체한다.

**Architecture:** 클·서 2레포. `LOPEntity`→`LOPActor` rename(Task 1, 순수)을 먼저 격리한 뒤, 각 사이드 트리를 통째로 표준 형태로 reshape(Task 2=클, Task 3=서). 트리 reshape는 크리에이터+PhysicsFollower+View+binder+매니저가 서로 물려 있어 사이드별 원자 커밋. 와이어/World.Entity 데이터는 불변이라 "reshaped 클라 ↔ old 서버"도 상호운용(각 사이드 독립 검증 가능).

**Tech Stack:** Unity 6 (C#), UnityMCP(컴파일/콘솔), Assembly-CSharp(클·서 각자 — EditMode 불가, 검증=컴파일+인게임).

## Global Constraints

- **매 태스크 끝에 게임이 그대로 동작**(strangler). 클·서 editor 컴파일 에러 0.
- **rename는 whole-word `\bLOPEntity\b`만** — `LOPEntityView`/`LOPEntityManager`는 **손대지 말 것**(부분 문자열). `GameFramework.World.Entity`·`IEntity`도 무관.
- **`IEntity` 인터페이스(GameFramework 공유)는 유지**(S5). rename은 concrete `LOPEntity`→`LOPActor`만. GameFramework는 **변경 없음**(LOPEntity는 LOP 측 per-side 타입).
- **`#if !UNITY_SERVER` 프로덕션 렌더 제외는 범위 밖** — 서버 뷰는 유지(테스트 렌더).
- **UnityMCP는 컨트롤러가 수행**(구현 서브에이전트는 코드+git만). 인스턴스: client `LeagueOfPhysical-Client@de70658b9450cbb4`, server `LeagueOfPhysical-Server@f99391fa2dbaaf3c`(해시는 `mcpforunity://instances`로 확인).
- 파일 rename/삭제 시 `.meta`도 함께(`git mv`가 `.cs`와 `.cs.meta` 각각 필요할 수 있음 — 확인).
- **서버 do-not-commit 픽스처**(`Entrance/ConfigureRoomComponent.cs`·`Game/GameRuleSystem.cs`·`DefaultVolumeProfile.asset`·`GraphicsSettings.asset`·테스트 auth/playerList) 스테이징 금지 — 명시 `git add`만.
- 브랜치: client `feature/entity-s4-tree`(존재), server는 생성. 커밋 끝 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- **렌더 바디 파렌팅:** 모델 인스턴스는 루트(rb 있음) 직속 자식. 인터폴레이터가 매 LateUpdate에 world position을 직접 세팅하므로 부모(root) 이동과 무관(LateUpdate가 마지막이라 렌더 위치 권위). 모델 프리팹은 콜라이더 없음 전제(루트 rb가 자식 콜라이더를 claim하지 않게) — Task 2 검증.

---

## File Structure

- **Client** `Assets/Scripts/`:
  - Rename: `Entity/LOPEntity.cs`→`Entity/LOPActor.cs`(+`.meta`). Modify(rename 참조): `EntityCreator/CharacterCreator.cs`, `EntityCreator/ItemCreator.cs`, `Entity/LOPEntityView.cs`, `Entity/LocalEntityInterpolator.cs`, `Entity/RemoteEntityInterpolator.cs`, `Entity/LOPEntityManager.cs`, `Game/EntityBinder.cs`, `UI/WorldSpace/CharacterNameplate.cs`, `UI/WorldSpace/DamageFloaterEmitter.cs`, `Game/MessageHandler/Game.Entity.MessageHandler.cs`, `Component/PhysicsFollower.cs`, `IPlayerContext`/`PlayerContext`(있으면).
  - Reshape(Task 2): `CharacterCreator.cs`, `ItemCreator.cs`, `Component/PhysicsFollower.cs`, `Entity/LOPEntityView.cs`, `Game/EntityBinder.cs`, `UI/WorldSpace/CharacterNameplate.cs`, `UI/WorldSpace/DamageFloaterEmitter.cs`, `Entity/LOPEntityManager.cs`.
- **Server** `Assets/Scripts/`:
  - Rename: `Entity/LOPEntity.cs`→`Entity/LOPActor.cs`. Modify(참조): `EntityCreator/CharacterCreator.cs`, `EntityCreator/ItemCreator.cs`, `Entity/LOPEntityView.cs`, `Entity/LOPAIController.cs`, `Entity/LOPEntityManager.cs`, `Component/PhysicsFollower.cs`, `Game/LOPOverlapQuery.cs`, message handlers.
  - Reshape(Task 3): `CharacterCreator.cs`, `ItemCreator.cs`, `Component/PhysicsFollower.cs`, `Entity/LOPEntityView.cs`, `Game/LOPOverlapQuery.cs`, `Entity/LOPEntityManager.cs`.
- **GameFramework:** 변경 없음.

---

## Task 1: rename LOPEntity → LOPActor (클·서)

**Files:** 위 "Rename" 항목(클·서). GameFramework 무관.

**Interfaces:**
- Produces: concrete `LOPActor : MonoBehaviour, IEntity`(내용 무변화, 이름만). 모든 `LOPEntity` 타입 참조가 `LOPActor`로.

- [ ] **Step 1: 서버 브랜치 생성**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server checkout -b feature/entity-s4-tree
```

- [ ] **Step 2: 클라 파일 rename + 클래스명 변경**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client mv Assets/Scripts/Entity/LOPEntity.cs Assets/Scripts/Entity/LOPActor.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client mv Assets/Scripts/Entity/LOPEntity.cs.meta Assets/Scripts/Entity/LOPActor.cs.meta
```
`Assets/Scripts/Entity/LOPActor.cs`에서 `public class LOPEntity` → `public class LOPActor`.

- [ ] **Step 3: 클라 전 참조 whole-word rename**

클라 `Assets/Scripts` 전체에서 정규식 `\bLOPEntity\b`(→ `LOPActor`)로 치환. **`LOPEntityView`/`LOPEntityManager`는 매치 안 됨**(뒤에 단어문자 이어짐 = 워드바운더리 없음). 확인 대상 파일(각 `LOPEntity` 타입 사용처):
`CharacterCreator.cs`(`IEntityCreator<LOPEntity,...>` + 지역변수), `ItemCreator.cs`(동일), `LOPEntityView.cs`(`public LOPEntity entity`, `SetEntity(LOPEntity)`), `LocalEntityInterpolator.cs`/`RemoteEntityInterpolator.cs`(`public LOPEntity entity`), `LOPEntityManager.cs`(`GetEntity<LOPEntity>`, `DestroyMarkedEntities`의 `LOPEntity lopEntity`), `EntityBinder.cs`(`is not LOPEntity entity`), `CharacterNameplate.cs`/`DamageFloaterEmitter.cs`(`public LOPEntity entity`, `SetEntity`), `Game.Entity.MessageHandler.cs`(`TryGetEntity<LOPEntity>` ×4), `PhysicsFollower.cs`(클라는 참조 없음 — 확인), `IPlayerContext`/`PlayerContext`(있으면 `LOPEntity entity` 필드).

- [ ] **Step 4: 클라 컴파일 검증(컨트롤러)**

컨트롤러가 `refresh_unity` + `read_console`(client instance)로 CS 에러 0 확인. `LOPActor` 미해결/`LOPEntity` 잔존 참조 없음.

- [ ] **Step 5: 서버 파일 rename + 클래스명 + 전 참조 rename**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server mv Assets/Scripts/Entity/LOPEntity.cs Assets/Scripts/Entity/LOPActor.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server mv Assets/Scripts/Entity/LOPEntity.cs.meta Assets/Scripts/Entity/LOPActor.cs.meta
```
`LOPActor.cs`에서 `public class LOPEntity` → `public class LOPActor`. 서버 `Assets/Scripts` 전체에서 `\bLOPEntity\b`→`LOPActor`. 대상: `CharacterCreator.cs`, `ItemCreator.cs`, `LOPEntityView.cs`(`public LOPEntity entity`, `SetEntity`), `LOPAIController.cs`(`public LOPEntity entity`, `SetEntity`), `LOPEntityManager.cs`, `PhysicsFollower.cs`(`OnTriggerEnter`의 `LOPEntity otherEntity`), `LOPOverlapQuery.cs`(`GetComponentInChildren<LOPEntity>`), message handlers.

- [ ] **Step 6: 서버 컴파일 검증(컨트롤러)** — server instance CS 에러 0.

- [ ] **Step 7: 커밋(클·서)**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "refactor(entity): LOPEntity → LOPActor rename (클라, 순수)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(entity): LOPEntity → LOPActor rename (서버, 순수)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
(서버 do-not-commit 픽스처가 `Assets/Scripts`에 섞이면 명시 파일 `git add`로 대체.)

---

## Task 2: 클라 트리 reshape (표준 앵커 + 두 바디)

**Files:** Modify: `EntityCreator/CharacterCreator.cs`, `EntityCreator/ItemCreator.cs`, `Component/PhysicsFollower.cs`, `Entity/LOPEntityView.cs`, `Game/EntityBinder.cs`, `UI/WorldSpace/CharacterNameplate.cs`, `UI/WorldSpace/DamageFloaterEmitter.cs`, `Entity/LOPEntityManager.cs`.

**Interfaces:**
- Consumes: Task 1의 `LOPActor`.
- Produces: 클라 엔티티 트리 = `Actor_{id}`(루트: LOPActor + rb+콜라이더 + PhysicsFollower + interpolator + LOPEntityView + nameplate + floater) + 모델 인스턴스(자식). Find/GetComponentInChildren 배선 제거.

- [ ] **Step 1: `PhysicsFollower` — rb+콜라이더를 루트(자기 GameObject)에**

`Assets/Scripts/Component/PhysicsFollower.cs` 전체 교체:
```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// World.Transform을 따라가는 물리 바디(Rigidbody/캡슐 콜라이더)를 소유하는 프레젠테이션 컴포넌트.
    /// 앵커 루트(LOPActor)와 같은 GameObject에 rb+콜라이더를 둔다(시뮬 바디 = 루트).
    /// rb 팔로우는 호스트 단일 패스(LOPRunner)가 MotionBridge.PushMotion으로 구동한다.
    /// </summary>
    public class PhysicsFollower : MonoBehaviour
    {
        public Rigidbody entityRigidbody { get; private set; }
        public Collider[] entityColliders { get; private set; }

        public void Initialize(GameFramework.World.Entity worldEntity, bool isKinematic, bool isTrigger)
        {
            var worldTransform = worldEntity.Get<GameFramework.World.Transform>();
            var worldVelocity = worldEntity.Get<GameFramework.World.Velocity>();

            gameObject.layer = LayerMask.NameToLayer("Character");

            entityRigidbody = gameObject.AddComponent<Rigidbody>();
            entityRigidbody.linearDamping = 0f;
            entityRigidbody.angularDamping = 0.05f;
            entityRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            entityRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            entityRigidbody.position = worldTransform.Position.ToUnity();
            entityRigidbody.rotation = worldTransform.Rotation.ToUnity();
            entityRigidbody.linearVelocity = worldVelocity.Linear.ToUnity();
            entityRigidbody.isKinematic = isKinematic;

            CapsuleCollider capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = 0.35f;
            capsuleCollider.height = 1.5f;
            capsuleCollider.center = new Vector3(0, capsuleCollider.height * 0.5f, 0);
            capsuleCollider.isTrigger = isTrigger;
            entityColliders = new Collider[] { capsuleCollider };
        }
    }
}
```

- [ ] **Step 2: `LOPEntityView` — 모델을 루트 밑에 직접 로드**

`Assets/Scripts/Entity/LOPEntityView.cs`의 `UpdateVisual` 끝부분:
```csharp
            GameObject visual = transform.parent.Find("Visual").gameObject;

            visualGameObject = Instantiate(asyncOperationHandle.Task.Result, visual.transform);
```
을
```csharp
            visualGameObject = Instantiate(asyncOperationHandle.Task.Result, transform);
```
로 교체(View가 루트 컴포넌트라 `transform` = 루트). 나머지(`visualGameObject.transform.position = entity.position;` 등) 유지.

- [ ] **Step 3: `CharacterNameplate`/`DamageFloaterEmitter` — 형제 View를 GetComponent로**

`Assets/Scripts/UI/WorldSpace/CharacterNameplate.cs:51`:
```csharp
            _entityView = transform.parent.GetComponentInChildren<LOPEntityView>();
```
→
```csharp
            _entityView = GetComponent<LOPEntityView>();
```
`Assets/Scripts/UI/WorldSpace/DamageFloaterEmitter.cs:69` 동일 교체.

- [ ] **Step 4: `EntityBinder` — 루트에 AddComponent**

`Assets/Scripts/Game/EntityBinder.cs`의 `OnEntityCreated` 본문에서:
```csharp
            GameObject root = entity.transform.parent.gameObject;

            DamageFloaterEmitter damageFloaterEmitter = root.CreateChildWithComponent<DamageFloaterEmitter>();
            objectResolver.Inject(damageFloaterEmitter);
            damageFloaterEmitter.SetEntity(entity);

            CharacterNameplate nameplate = root.CreateChildWithComponent<CharacterNameplate>();
            objectResolver.Inject(nameplate);
            nameplate.SetEntity(entity);
```
을
```csharp
            GameObject root = entity.gameObject;

            DamageFloaterEmitter damageFloaterEmitter = root.AddComponent<DamageFloaterEmitter>();
            objectResolver.Inject(damageFloaterEmitter);
            damageFloaterEmitter.SetEntity(entity);

            CharacterNameplate nameplate = root.AddComponent<CharacterNameplate>();
            objectResolver.Inject(nameplate);
            nameplate.SetEntity(entity);
```
로 교체(`entity`는 이제 `LOPActor` = 루트 컴포넌트).

- [ ] **Step 5: `LOPEntityManager.DestroyMarkedEntities` — 루트 참조 교정**

`Assets/Scripts/Entity/LOPEntityManager.cs`의 `DestroyMarkedEntities`에서:
```csharp
                LOPActor lopEntity = GetEntity<LOPActor>(entityId);

                foreach (var cleanup in lopEntity.transform.parent.GetComponentsInChildren<ICleanup>(true))
                {
                    cleanup.Cleanup();
                }
```
및
```csharp
                Destroy(lopEntity.transform.parent.gameObject);
```
을 각각
```csharp
                LOPActor lopActor = GetEntity<LOPActor>(entityId);

                foreach (var cleanup in lopActor.transform.GetComponentsInChildren<ICleanup>(true))
                {
                    cleanup.Cleanup();
                }
```
및
```csharp
                Destroy(lopActor.gameObject);
```
로 교체(변수명 `lopEntity`→`lopActor`, `transform.parent`→`transform`/`gameObject`).

- [ ] **Step 6: `CharacterCreator` — 루트 앵커 + 컴포넌트 조립**

`Assets/Scripts/EntityCreator/CharacterCreator.cs`의 트리 조립부. 상단:
```csharp
            GameObject root = new GameObject($"Character_{creationData.entityId}");
            GameObject visual = root.CreateChild("Visual");
            GameObject physics = root.CreateChild("Physics");
```
을
```csharp
            GameObject root = new GameObject($"Actor_{creationData.entityId}");
```
로. 이어 `LOPActor entity = root.CreateChildWithComponent<LOPActor>();` → `LOPActor entity = root.AddComponent<LOPActor>();`. `PhysicsFollower physicsFollower = entity.gameObject.AddComponent<PhysicsFollower>();`는 그대로(entity.gameObject=root). `LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();` → `LOPEntityView view = root.AddComponent<LOPEntityView>();`. 인터폴레이터의 `entity.gameObject.AddComponent<...>()`는 그대로(루트). 나머지(World Core 등록, Grant 등) 무변경.

- [ ] **Step 7: `ItemCreator` — 동일 조립**

`Assets/Scripts/EntityCreator/ItemCreator.cs`:
```csharp
            GameObject root = new GameObject($"Item_{creationData.entityId}");
            GameObject visual = root.CreateChild("Visual");
            GameObject physics = root.CreateChild("Physics");
```
→
```csharp
            GameObject root = new GameObject($"Actor_{creationData.entityId}");
```
`LOPActor entity = root.CreateChildWithComponent<LOPActor>();` → `root.AddComponent<LOPActor>();`. `LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();` → `root.AddComponent<LOPEntityView>();`. `RemoteEntityInterpolator interpolator = entity.gameObject.AddComponent<...>()` 그대로. 나머지 무변경.

- [ ] **Step 8: 컴파일 검증(컨트롤러) + 모델 콜라이더 확인**

컨트롤러 `refresh_unity`+`read_console`(client) CS 에러 0. **모델 프리팹에 Collider가 있으면** 루트 rb가 claim → 확인 필요(캐릭터/아이템 비주얼 프리팹은 렌더 전용 전제; 콜라이더 있으면 컨트롤러가 보고 → 대응 결정).

- [ ] **Step 9: 인게임 검증(사용자)**

클라 플레이(서버는 old도 무방 — 와이어 동일). 확인: 스폰·모델 로드·이동·점프·**캐릭터끼리 충돌**·아이템 줍기·넉백·데미지 플로터·**네임플레이트 HP바**·롤백·원격 보간 정상. 콘솔 신규 예외 0.

- [ ] **Step 10: 커밋**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add Assets/Scripts
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "refactor(entity): 클라 트리 표준화 — LOPActor 루트 앵커 + rb 루트 + 컴포넌트 co-location

빈 루트→Actor 루트(시뮬 바디: kinematic rb+콜라이더). PhysicsFollower/interpolator/
View/nameplate/floater 전부 루트 컴포넌트. 모델=루트 직속 렌더 바디 자식. Visual/Physics
컨테이너 제거. Find/GetComponentInChildren→GetComponent, 매니저 destroy 루트 참조 교정.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: 서버 트리 reshape (동일 표준 + 매핑 교정)

**Files:** Modify: `EntityCreator/CharacterCreator.cs`, `EntityCreator/ItemCreator.cs`, `Component/PhysicsFollower.cs`, `Entity/LOPEntityView.cs`, `Game/LOPOverlapQuery.cs`, `Entity/LOPEntityManager.cs`.

**Interfaces:**
- Consumes: Task 1의 `LOPActor`.
- Produces: 서버 엔티티 트리 = `Actor_{id}`(루트: LOPActor + rb+콜라이더+TriggerDetector + PhysicsFollower + LOPEntityView + AIController) + 모델 인스턴스(자식). 콜라이더→엔티티 = `GetComponentInParent<LOPActor>()`.

- [ ] **Step 1: 서버 `PhysicsFollower` — rb+콜라이더+TriggerDetector 루트에 + 트리거 매핑 교정**

`Assets/Scripts/Component/PhysicsFollower.cs`(서버)에서 `Initialize`의 트리 부분:
```csharp
            GameObject physics = transform.parent.Find("Physics").gameObject;
            physicsGameObject = physics.CreateChild("PhysicsGameObject");
            physicsGameObject.layer = LayerMask.NameToLayer("Character");

            entityRigidbody = physicsGameObject.AddComponent<Rigidbody>();
```
을
```csharp
            gameObject.layer = LayerMask.NameToLayer("Character");

            entityRigidbody = gameObject.AddComponent<Rigidbody>();
```
로, 이하 `physicsGameObject.AddComponent<CapsuleCollider>()`/`GetOrAddComponent<TriggerDetector>()`를 `gameObject.AddComponent`/`gameObject.GetOrAddComponent`로. `private GameObject physicsGameObject;` 필드 삭제. 그리고 `OnTriggerEnter`:
```csharp
            LOPActor otherEntity = other.transform.parent?.parent?.GetComponentInChildren<LOPActor>();
```
을
```csharp
            LOPActor otherEntity = other.GetComponentInParent<LOPActor>();
```
로 교체.

- [ ] **Step 2: 서버 `LOPOverlapQuery` — 콜라이더 매핑 교정**

`Assets/Scripts/Game/LOPOverlapQuery.cs`의:
```csharp
                var entity = hit.transform.parent?.parent?.GetComponentInChildren<LOPActor>();
```
을
```csharp
                var entity = hit.GetComponentInParent<LOPActor>();
```
로 교체.

- [ ] **Step 3: 서버 `LOPEntityView` — 모델을 루트 밑에 직접 로드**

`Assets/Scripts/Entity/LOPEntityView.cs`(서버)의:
```csharp
            GameObject visual = transform.parent.Find("Visual").gameObject;

            visualGameObject = Instantiate(asyncOperationHandle.Task.Result, visual.transform);
```
을
```csharp
            visualGameObject = Instantiate(asyncOperationHandle.Task.Result, transform);
```
로 교체.

- [ ] **Step 4: 서버 `LOPEntityManager.DestroyMarkedEntities` — 루트 참조 교정**

Task 2 Step 5와 동일하게(서버 파일): `LOPActor lopEntity = GetEntity<LOPActor>(entityId);` → `LOPActor lopActor = ...`, `lopActor.transform.parent.GetComponentsInChildren<ICleanup>(true)` → `lopActor.transform.GetComponentsInChildren<ICleanup>(true)`, `Destroy(lopActor.transform.parent.gameObject)` → `Destroy(lopActor.gameObject)`. (서버 매니저의 해당 블록을 열어 동일 패턴 적용 — 클라와 동형.)

- [ ] **Step 5: 서버 `CharacterCreator` — 루트 앵커 조립**

`Assets/Scripts/EntityCreator/CharacterCreator.cs`(서버):
```csharp
            GameObject root = new GameObject($"Character_{creationData.entityId}");
            GameObject visual = root.CreateChild("Visual");
            GameObject physics = root.CreateChild("Physics");
```
→
```csharp
            GameObject root = new GameObject($"Actor_{creationData.entityId}");
```
`LOPActor entity = root.CreateChildWithComponent<LOPActor>();` → `root.AddComponent<LOPActor>();`. `PhysicsFollower ... = entity.gameObject.AddComponent<PhysicsFollower>();` 그대로. `LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();` → `root.AddComponent<LOPEntityView>();`. `LOPAIController aiController = root.CreateChildWithComponent<LOPAIController>();` → `root.AddComponent<LOPAIController>();`. 나머지 무변경.

- [ ] **Step 6: 서버 `ItemCreator` — 동일 조립**

`Assets/Scripts/EntityCreator/ItemCreator.cs`(서버): `root = new GameObject($"Actor_{...}")` (Visual/Physics CreateChild 제거), `LOPActor entity = root.AddComponent<LOPActor>();`, `LOPEntityView view = root.AddComponent<LOPEntityView>();`. 나머지 무변경.

- [ ] **Step 7: 컴파일 검증(컨트롤러)** — server instance CS 에러 0. 모델 프리팹 콜라이더 확인(Task 2 Step 8과 동일).

- [ ] **Step 8: 인게임 검증(사용자, 클·서 동시)**

클·서 둘 다 reshaped. 확인: 스폰·이동·점프·**캐릭터끼리 충돌**·**AI 이동/공격**·**아이템 줍기(서버 trigger)**·**넉백/데미지 판정(서버 OverlapSphere)**·전역공격(G)·데미지 플로터·네임플레이트·롤백·원격 보간. 서버 Scene 뷰에 모델 렌더됨. 콘솔(클·서) 신규 예외 0(기존 파킹 NRE만).

- [ ] **Step 9: 커밋**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(entity): 서버 트리 표준화 — LOPActor 루트 앵커 + rb 루트 + 콜라이더 매핑 교정

빈 루트→Actor 루트(시뮬 바디). PhysicsFollower/View/AIController 루트 컴포넌트,
모델=루트 직속 자식(테스트 렌더 유지). 콜라이더→엔티티 = GetComponentInParent<LOPActor>
(trigger·OverlapQuery), 매니저 destroy 루트 참조 교정. Visual/Physics 컨테이너 제거.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
(do-not-commit 픽스처 섞이면 명시 `git add`.)

---

## Task 4: 통합 검증 + ROADMAP + 메모리

**Files:** `docs/ROADMAP.md`(client), 메모리 `entity-unity-layer-rearchitecture.md`.

- [ ] **Step 1: 배선 잔재 0 확인**

Grep(클·서): `Find("Visual")`·`Find("Physics")`·`parent?.parent?.GetComponentInChildren`·`transform.parent.GetComponentInChildren<LOPEntityView>`·`CreateChild("Visual")`/`CreateChild("Physics")` 0. `\bLOPEntity\b`(타입) 0(View/Manager 제외). `PhysicsGameObject`/`physicsGameObject` 참조 0.

- [ ] **Step 2: 최종 whole-branch 리뷰(선택 — 컨트롤러)**

`requesting-code-review` 또는 `/code-review`로 클·서 두 브랜치 교차 리뷰. Critical/Important 0 목표.

- [ ] **Step 3: ROADMAP 갱신**

`docs/ROADMAP.md` "한 일" 원장에 S4 완료 한 줄 추가(트리 표준화: Actor 루트+rb 루트+컴포넌트 co-location+모델 렌더 바디 자식+배선 typed, LOPEntity→LOPActor, 서버 뷰 유지·#if 후속) + "다음" 절 S4→S5로. 커밋:
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add docs/ROADMAP.md docs/superpowers/plans/2026-07-18-s4-unity-tree-reconstruction.md
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "docs(roadmap): 엔티티 재구조화 S4(Unity 트리 표준화) 완료 반영

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 4: 메모리 갱신**

`entity-unity-layer-rearchitecture.md`에 S4 완료(LOPActor 루트 앵커·rb 루트·컴포넌트 co-location·모델 렌더 바디 자식·배선 typed·서버 뷰 유지+#if 후속) + gotcha(rename whole-word, 모델 콜라이더 claim 주의, self-제외는 콜라이더 참조/기하 start-overlap이라 이동 안전) 반영. 다음=S5.

---

## Self-Review

**Spec coverage:** ✅ rename(T1) · 빈 루트→Actor 루트(T2·T3) · rb+콜라이더 루트로(T2·T3 PhysicsFollower) · behavior 스크립트 루트 컴포넌트로(T2 View/nameplate/floater/interpolator, T3 View/AIController) · Physics/Visual 컨테이너 제거(T2·T3) · 모델=루트 직속 자식(T2·T3 View) · 콜라이더→엔티티 매핑 교정(T3 trigger·OverlapQuery) · 매니저 destroy 배선(T2·T3) · binder AddComponent(T2) · GetComponentInChildren→GetComponent(T2) · 서버 뷰 유지(T3) · 검증/roadmap/메모리(T4). 스펙 요구 전부 매핑.

**Placeholder scan:** 코드 스텝 전부 실제 코드/정확한 위치. TODO/TBD 없음.

**Type consistency:** `LOPActor`(T1) = 모든 후속 타입 참조 대상. `LOPEntityView`/`LOPEntityManager`는 rename 비대상(부분 문자열)이라 그대로 — T2/T3에서 `GetComponent<LOPEntityView>()`·`GetEntity<LOPActor>` 혼용이 정합. `PhysicsFollower.entityRigidbody`/`entityColliders`(T2·T3) = 크리에이터의 `PhysicsBody(...)` 인자와 일치(무변경). `EntityBinder`의 `entity`(=LOPActor)·`entity.gameObject`(루트) 정합.

**주의(구현자):** ① rename는 반드시 whole-word `\bLOPEntity\b`(View/Manager 보존). ② 모델 프리팹 콜라이더 없음 확인(루트 rb claim 회피) — 있으면 컨트롤러 보고 후 대응. ③ 서버 do-not-commit 픽스처 명시 add. ④ 클·서 각각 컴파일=컨트롤러 UnityMCP, 인게임=사용자. ⑤ Task 2(클 reshaped)는 old 서버와도 상호운용(와이어 불변)이라 단독 플레이 검증 가능.
