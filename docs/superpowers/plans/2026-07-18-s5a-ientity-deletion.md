# S5a — `IEntity` 삭제 + 모션 접근자 통일 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** GameFramework의 얇은 파사드 인터페이스 `IEntity`를 삭제하고, 엔티티 소비처를 콘크리트 `LOPActor`로 재타입하며, 모션(pos/rot/vel)의 유니티↔순수숫자 변환을 GameFramework 익스텐션 한 벌로 통일한다.

**Architecture:** strangler(점진 교체). 새 모션 익스텐션을 먼저 추가(additive)하고 소비처를 그것으로 옮긴 뒤, LOP 소비처를 콘크리트 타입으로 재타입하고, 마지막에 GF 제약을 `MonoBehaviour`로 바꾼 후 `IEntity`를 삭제한다. **각 태스크 끝에서 3레포(GameFramework·Client·Server) 컴파일이 그린**이어야 한다. `IEntity` 삭제 자체는 원자적이지만, 중간 단계는 "LOPActor는 IEntity이자 MonoBehaviour"라는 사실 + 매니저 내부 캐스트로 그린을 유지한다.

**Tech Stack:** Unity(C#), VContainer(DI), MessagePipe(pub/sub), GameFramework(`com.baegames.gameframework`, file: 패키지), 순수 C# World Core(`GameFramework.World`, `System.Numerics`).

## Global Constraints

- **3레포 동시 작업:** GameFramework(`C:/Users/re5na/workspace/LOP/GameFramework`), Client(`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client`), Server(`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server`). GameFramework는 클·서가 함께 참조하는 file: 패키지 → GF 변경은 양쪽 동시 컴파일 통과 필요.
- **피처 브랜치에서만:** 각 레포에 `feature/entity-s5a-ientity-deletion` 브랜치. main 직접 커밋 금지. Client 레포엔 이미 이 브랜치 존재(spec 커밋됨).
- **컴파일 검증(UnityMCP):** 파일 수정 후 `refresh_unity` → `read_console`로 에러 확인. **모든 UnityMCP 호출에 `unity_instance` 명시**(client·server 각각). client id는 `mcpforunity://instances`에서 `name == "LeagueOfPhysical-Client"`인 인스턴스의 `id`(`Name@hash`), server는 `LeagueOfPhysical-Server`. GF는 코드가 클·서 양쪽 에디터에서 컴파일되므로 두 인스턴스 모두 확인.
- **.meta 커밋:** 새 파일 생성/삭제 시 Unity가 만든 `.meta`도 함께 `git add`/`git rm`(직접 생성·수정 금지).
- **네임스페이스 컨벤션:** LOP 측 파일은 `using GameFramework.World;`를 추가하지 않고 World 타입을 풀 네임스페이스로 한정(`GameFramework.World.Entity` 등). 단 이미 짧게 쓰는 파일(예: `Reconciler`는 `GameFramework.World.Entity`를 이미 풀 한정)은 기존 스타일 유지.
- **인게임 검증은 사용자:** 컴파일·EditMode는 에이전트(UnityMCP), 플레이 스모크는 사용자. 동작 무변화가 기대치(값 동치 리팩터).
- **어휘 rename 금지(범위 밖):** `entity`→`actor` 식별자 rename은 S5a에서 하지 않는다. 이 플랜은 타입/시그니처만 바꾸고 식별자 이름은 그대로 둔다.

---

## 파일 구조 (무엇을 만들고 고치나)

**GameFramework (생성):**
- `Runtime/Scripts/Extensions/EntityMotion.Extensions.cs` — `World.Entity`의 유니티-Vector3 모션 접근자(Get/Set × pos/rot/vel). 변환+동등성 가드를 여기 한 곳에.
- `Tests/EditMode/.../EntityMotionExtensionsTests.cs` — 접근자 roundtrip + 가드 EditMode 테스트.

**GameFramework (수정 — IEntity 제거):**
- `Runtime/Scripts/Entity/IEntity.cs` — **삭제**.
- `Runtime/Scripts/Entity/IEntityManager.cs` — `where TEntity : IEntity` → `MonoBehaviour`, 비제네릭 `IEntity` 반환 → `MonoBehaviour`.
- `Runtime/Scripts/Entity/IEntityFactory.cs` / `IEntityCreator.cs` / `EntityFactory.cs` — `where TEntity : IEntity` → `MonoBehaviour`.

**Client (수정):**
- `Assets/Scripts/Entity/LOPActor.cs` — 파사드/LinkWorldMotion/모션필드/`: IEntity` 제거, `IsGrounded` 이전.
- `Assets/Scripts/Entity/LOPEntityView.cs` — 모션 읽기를 registry+익스텐션으로, `IsGrounded` 수신.
- `Assets/Scripts/Entity/RemoteEntityInterpolator.cs` — 파사드 쓰기를 `worldEntity` + 익스텐션으로(+`worldEntity` 필드).
- `Assets/Scripts/Entity/Reconciler.cs` — 파사드 읽/쓰기를 익스텐션으로.
- `Assets/Scripts/Entity/Event.Entity.cs` — `EntityCreated.entity` : `IEntity`→`LOPActor`.
- `Assets/Scripts/Entity/LOPEntityManager.cs` — `entityMap` 타입 + 시그니처 + 캐스트.
- `Assets/Scripts/EntityCreator/CharacterCreator.cs` / `ItemCreator.cs` — `interpolator.worldEntity` 세팅, `LinkWorldMotion` 호출 제거.
- `Assets/Scripts/Game/EntityBinder.cs` / `PlayerHudCoordinator.cs` — `EntityCreated.entity`(이제 LOPActor) 소비 정리.

**Server (수정 — 클라 대응 미러):**
- `Assets/Scripts/Entity/LOPActor.cs`, `LOPEntityView.cs`, `LOPEntityManager.cs`, `Event.Entity.cs`
- `Assets/Scripts/AI/EnemyBrain.cs`, `IBrain.cs`
- `Assets/Scripts/EntityCreator/CharacterCreator.cs`, `ItemCreator.cs`
- `Assets/Scripts/EntityCreationDataFactory/EntityCreationDataFactory.cs`, `IEntityCreationDataFactory.cs`, `IEntityCreationDataCreator.cs`, `CharacterCreationDataCreator.cs`, `ItemCreationDataCreator.cs`
- `Assets/Scripts/Game/LOPRunner.cs`(있다면 `GetEntities` 사용부 확인)

---

### Task 1: 모션 접근자 익스텐션 + EditMode 테스트 (GameFramework, additive)

순수 추가 태스크 — 아무 것도 안 깨고 새 익스텐션만 더한다. TDD.

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Extensions/EntityMotion.Extensions.cs`
- Test: GameFramework EditMode 테스트 폴더에 `EntityMotionExtensionsTests.cs`(기존 World 관련 EditMode 테스트가 사는 곳에 맞춰 배치 — `Tests/EditMode/` 아래, 기존 테스트 asmdef 사용)

**Interfaces:**
- Produces (GameFramework, `namespace GameFramework.World`, static class `EntityMotionExtensions`):
  - `Vector3 GetPosition(this Entity e)` / `void SetPosition(this Entity e, Vector3 value)`
  - `Vector3 GetRotation(this Entity e)` (euler) / `void SetRotation(this Entity e, Vector3 eulerValue)`
  - `Vector3 GetVelocity(this Entity e)` / `void SetVelocity(this Entity e, Vector3 value)`
  - 각 Set은 동등성 가드(`Vector3EqualityComparer.instance`)로 동일값이면 쓰지 않음.

- [ ] **Step 1: 실패 테스트 작성**

기존 GameFramework EditMode 테스트 파일을 하나 열어 asmdef/네임스페이스/`[Test]` 패턴을 확인한 뒤, 같은 폴더에 아래 테스트 파일을 만든다. (`World.Entity`에 `Transform`/`Velocity` 컴포넌트를 붙여 roundtrip 검증.)

```csharp
using NUnit.Framework;
using UnityEngine;
using GameFramework.World;

namespace GameFramework.Tests
{
    public class EntityMotionExtensionsTests
    {
        private static Entity MakeEntity()
        {
            var e = new Entity("test-1");
            e.Add(new Transform());
            e.Add(new Velocity());
            return e;
        }

        [Test]
        public void SetPosition_then_GetPosition_roundtrips()
        {
            var e = MakeEntity();
            e.SetPosition(new Vector3(1f, 2f, 3f));
            Assert.AreEqual(new Vector3(1f, 2f, 3f), e.GetPosition());
            // 진실원본(numerics)에도 반영됐는지 확인
            Assert.AreEqual(1f, e.Get<Transform>().Position.X, 1e-4f);
        }

        [Test]
        public void SetVelocity_then_GetVelocity_roundtrips()
        {
            var e = MakeEntity();
            e.SetVelocity(new Vector3(-4f, 0f, 5f));
            Assert.AreEqual(new Vector3(-4f, 0f, 5f), e.GetVelocity());
        }

        [Test]
        public void SetRotation_writes_quaternion_and_GetRotation_returns_euler()
        {
            var e = MakeEntity();
            e.SetRotation(new Vector3(0f, 90f, 0f));
            // GetRotation은 eulerAngles(래핑 있음) → y≈90 확인
            Assert.AreEqual(90f, e.GetRotation().y, 0.5f);
        }

        [Test]
        public void SetPosition_with_equal_value_is_noop_on_reference()
        {
            var e = MakeEntity();
            e.SetPosition(new Vector3(1f, 2f, 3f));
            var before = e.Get<Transform>().Position;
            e.SetPosition(new Vector3(1f, 2f, 3f)); // 동일값 → 가드로 스킵
            Assert.AreEqual(before, e.Get<Transform>().Position);
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패(컴파일 에러)하는지 확인**

UnityMCP `run_tests`(EditMode, 두 인스턴스 중 GF 컴파일하는 쪽) 또는 `read_console`.
Expected: `EntityMotionExtensions` / `GetPosition` 미정의로 컴파일 실패.

- [ ] **Step 3: 익스텐션 구현**

```csharp
using UnityEngine;

namespace GameFramework.World
{
    /// <summary>
    /// World.Entity의 모션(Transform/Velocity)을 UnityEngine.Vector3로 읽고 쓰는 경계 접근자.
    /// 코어는 System.Numerics(엔진 비의존)라 유니티↔순수숫자 변환이 필요한데, 그 변환과
    /// 불필요-쓰기 스킵(값이 같으면 안 씀)을 여기 한 곳에 모은다.
    /// </summary>
    public static class EntityMotionExtensions
    {
        public static Vector3 GetPosition(this Entity e) => e.Get<Transform>().Position.ToUnity();

        public static void SetPosition(this Entity e, Vector3 value)
        {
            var t = e.Get<Transform>();
            if (Vector3EqualityComparer.instance.Equals(t.Position.ToUnity(), value)) return;
            t.Position = value.ToNumerics();
        }

        public static Vector3 GetRotation(this Entity e) => e.Get<Transform>().Rotation.ToUnity().eulerAngles;

        public static void SetRotation(this Entity e, Vector3 eulerValue)
        {
            var t = e.Get<Transform>();
            if (Vector3EqualityComparer.instance.Equals(t.Rotation.ToUnity().eulerAngles, eulerValue)) return;
            t.Rotation = Quaternion.Euler(eulerValue).ToNumerics();
        }

        public static Vector3 GetVelocity(this Entity e) => e.Get<Velocity>().Linear.ToUnity();

        public static void SetVelocity(this Entity e, Vector3 value)
        {
            var v = e.Get<Velocity>();
            if (Vector3EqualityComparer.instance.Equals(v.Linear.ToUnity(), value)) return;
            v.Linear = value.ToNumerics();
        }
    }
}
```

> 주: `ToUnity`/`ToNumerics`(GameFramework `Numerics.Extensions.cs`)와 `Vector3EqualityComparer`(GameFramework `Property.Extensions.cs`)는 `namespace GameFramework`에 있고, 이 파일 `namespace GameFramework.World`는 상위 네임스페이스라 별도 `using` 없이 보인다. 컴파일 에러 시 `using GameFramework;` 추가.

- [ ] **Step 4: 테스트 통과 확인**

UnityMCP `run_tests`(EditMode). Expected: 4 테스트 PASS + 기존 GF EditMode(154) 유지.

- [ ] **Step 5: 커밋 (GameFramework 레포)**

```bash
cd C:/Users/re5na/workspace/LOP/GameFramework
git checkout -b feature/entity-s5a-ientity-deletion   # 없으면 생성
git add Runtime/Scripts/Extensions/EntityMotion.Extensions.cs Runtime/Scripts/Extensions/EntityMotion.Extensions.cs.meta Tests/
git commit -m "feat(entity): World.Entity 모션 유니티 접근자 익스텐션 추가 (S5a Task1)"
```

---

### Task 2: 클라 모션 사이트 → 익스텐션 (Client)

파사드 프로퍼티를 그대로 둔 채, 모션을 읽/쓰던 클라 사이트를 새 익스텐션으로 옮긴다. `IEntity`·GF 제약 무변 → 컴파일 그린, 동작 무변화.

**Files:**
- Modify: `Assets/Scripts/Entity/Reconciler.cs:67,85-87,150`
- Modify: `Assets/Scripts/Entity/RemoteEntityInterpolator.cs:16-17,67-76`
- Modify: `Assets/Scripts/Entity/LOPEntityView.cs:96,157-158`
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs:65-76`, `Assets/Scripts/EntityCreator/ItemCreator.cs:46-49`

**Interfaces:**
- Consumes: `EntityMotionExtensions.{GetPosition,SetPosition,GetRotation,SetRotation,GetVelocity,SetVelocity}` (Task 1).
- Produces: `RemoteEntityInterpolator.worldEntity { get; set; }` (`GameFramework.World.Entity`), 크리에이터가 세팅.

- [ ] **Step 1: `RemoteEntityInterpolator`에 `worldEntity` 필드 추가 + Apply를 익스텐션으로**

`RemoteEntityInterpolator.cs`에서 필드 추가(16-17 인근):

```csharp
        public LOPActor entity { get; set; }
        public GameFramework.World.Entity worldEntity { get; set; }
        public LOPEntityView entityView { get; set; }
```

`Apply`(67-76)를 교체:

```csharp
        private void Apply(Vector3 pos, Quaternion rot)
        {
            worldEntity.SetPosition(pos);
            worldEntity.SetRotation(rot.eulerAngles);
            if (entityView.visualGameObject != null)
            {
                entityView.visualGameObject.transform.position = pos;
                entityView.visualGameObject.transform.rotation = rot;
            }
        }
```

파일 상단에 `using GameFramework.World;`가 없으면, 익스텐션 호출을 위해 추가하거나 `EntityMotionExtensions.SetPosition(worldEntity, pos)` 형태로 정적 호출. (익스텐션 메서드는 `GameFramework.World` 네임스페이스 `using` 필요.)

- [ ] **Step 2: 크리에이터가 `interpolator.worldEntity` 세팅**

`CharacterCreator.cs`의 else 분기(72-76)와 `ItemCreator.cs`(46-49)에서 `RemoteEntityInterpolator` 배선에 한 줄 추가. `worldEntity`는 이미 로컬 변수로 존재.

CharacterCreator (else 분기):
```csharp
            else
            {
                RemoteEntityInterpolator interpolator = entity.gameObject.AddComponent<RemoteEntityInterpolator>();
                objectResolver.Inject(interpolator);
                interpolator.entity = entity;
                interpolator.worldEntity = worldEntity;
                interpolator.entityView = view;
            }
```

ItemCreator:
```csharp
            RemoteEntityInterpolator interpolator = entity.gameObject.AddComponent<RemoteEntityInterpolator>();
            objectResolver.Inject(interpolator);
            interpolator.entity = entity;
            interpolator.worldEntity = worldEntity;
            interpolator.entityView = view;
```

- [ ] **Step 3: `Reconciler` 모션 읽/쓰기를 익스텐션으로**

`Reconciler.cs`는 이미 `worldEntity`를 손에 쥔다(line 60). 교체:
- line 67: `Vector3 preCorrectionPos = entity.position;` → `Vector3 preCorrectionPos = worldEntity.GetPosition();`
- line 85-87:
```csharp
            worldEntity.SetPosition(snap.position);
            worldEntity.SetRotation(snap.rotation);
            worldEntity.SetVelocity(snap.velocity);
```
- line 150: `renderCorrectionSmoother.OnCorrection(preCorrectionPos.ToNumerics(), entity.position.ToNumerics());` → `... entity.position.ToNumerics()`를 `worldEntity.GetPosition().ToNumerics()`로.

익스텐션 호출을 위해 `using GameFramework.World;`가 스코프에 있는지 확인(없으면 정적 호출 `EntityMotionExtensions.GetPosition(worldEntity)`).

- [ ] **Step 4: 클라 `LOPEntityView` 모션 읽기를 registry+익스텐션으로**

`LOPEntityView`는 이미 `entityRegistry` 주입됨(line 17). `IsGrounded`는 아직 `LOPActor`에 있으니 이 태스크에선 그대로 호출(이전은 Task 6).
- line 96 `entity.velocity`:
```csharp
            var worldEntity = entityRegistry.Get(entity.entityId);
            Vector3 v = worldEntity != null ? worldEntity.GetVelocity() : Vector3.zero;
            float horizontalSpeedSquared = v.x * v.x + v.z * v.z;
            animator.SetBool("Run", horizontalSpeedSquared > walkThreshold * walkThreshold && entity.IsGrounded());
```
- line 157-158 (`UpdateVisual`, `entity.position`/`entity.rotation`):
```csharp
            var worldEntity = entityRegistry.Get(entity.entityId);
            if (worldEntity != null)
            {
                visualGameObject.transform.position = worldEntity.GetPosition();
                visualGameObject.transform.rotation = Quaternion.Euler(worldEntity.GetRotation());
            }
```
`using GameFramework.World;` 필요 시 추가.

- [ ] **Step 5: 컴파일 확인**

UnityMCP `refresh_unity` + `read_console`(unity_instance=Client). Expected: 에러 0. (파사드 프로퍼티는 아직 존재하나 이 사이트들에선 미사용.)

- [ ] **Step 6: 커밋 (Client 레포)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Entity/Reconciler.cs Assets/Scripts/Entity/RemoteEntityInterpolator.cs Assets/Scripts/Entity/LOPEntityView.cs Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/EntityCreator/ItemCreator.cs
git commit -m "refactor(entity): 클라 모션 사이트를 World.Entity 익스텐션으로 (S5a Task2)"
```

- [ ] **Step 7: 인게임 스모크(사용자 요청)** — 스폰·이동·원격 보간·롤백 reconcile 무변화 확인. (파사드→익스텐션은 값 동치.)

---

### Task 3: 서버 모션 사이트 → 익스텐션 (Server)

클라 Task 2의 미러. 서버 파일은 먼저 읽고, 아래 규칙대로 파사드 접근을 익스텐션으로 바꾼다. `IEntity`·GF 제약 무변 → 그린.

**Files:**
- Modify: `Assets/Scripts/AI/EnemyBrain.cs` (server)
- Modify: `Assets/Scripts/Entity/LOPEntityManager.cs` (server, `GetAllEntitySnaps`)
- Modify: `Assets/Scripts/Entity/LOPEntityView.cs` (server)
- Modify: `Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs`, `ItemCreationDataCreator.cs` (server)
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs`, `ItemCreator.cs` (server — `interpolator.worldEntity` 세팅. 서버에 RemoteEntityInterpolator가 있으면)

**Interfaces:**
- Consumes: `EntityMotionExtensions.*` (Task 1). `RemoteEntityInterpolator.worldEntity`(서버에 해당 컴포넌트가 있으면 Task 2와 동일한 필드 추가).

- [ ] **Step 1: 변환 규칙 (모든 서버 사이트 공통)**

`LOPActor`/`IEntity` 변수의 파사드 접근을 아래로 치환. `worldEntity`는 `entityRegistry.Get(id)`로 얻는다(대상 클래스가 `entityRegistry`를 이미 주입/보유; `EnemyBrain`·`LOPEntityManager`·CreationDataCreator 모두 registry 접근 가능).

| 현재 (파사드) | 후 (익스텐션) |
|---|---|
| `x.position` (읽기) | `entityRegistry.Get(x.entityId).GetPosition()` |
| `x.rotation` (읽기) | `entityRegistry.Get(x.entityId).GetRotation()` |
| `x.velocity` (읽기) | `entityRegistry.Get(x.entityId).GetVelocity()` |
| `x.position = v` | `entityRegistry.Get(x.entityId).SetPosition(v)` |
| `x.rotation = v` | `entityRegistry.Get(x.entityId).SetRotation(v)` |
| `x.velocity = v` | `entityRegistry.Get(x.entityId).SetVelocity(v)` |

`EnemyBrain`: line 44 `entity.rotation = ...` → Set; line 57 `entity.velocity = new Vector3(vx, entity.velocity.y, vz)` — `entity.velocity.y` 읽기도 익스텐션으로. 반복 조회를 줄이려면 메서드 시작에 `var worldEntity = entityRegistry.Get(entity.entityId);` 한 번 얻어 재사용. target/others의 `.position` 읽기도 동일 규칙(각 대상은 `entityRegistry.Get(e.entityId).GetPosition()`).

`LOPEntityManager.GetAllEntitySnaps`(server): `GetEntities()` 비제네릭 → `GetEntities<LOPActor>()`로 바꾸고, 각 `entity.position/rotation/velocity`를 `entityRegistry.Get(entity.entityId).Get*()`로. (매니저는 `entityRegistry` 주입 보유.)

`Character/ItemCreationDataCreator`(server): `Create(LOPActor lopEntity)` 내부의 `lopEntity.position/rotation/velocity` 읽기 → registry로. (creator가 registry를 보유하는지 확인, 없으면 factory가 넘겨주는 값 사용 — 파일 읽고 판단.)

`LOPEntityView`(server): 클라 Task 2 Step 4와 동일(단 서버 뷰엔 `IsGrounded`가 없으니 velocity/position/rotation 읽기만).

- [ ] **Step 2: 서버 RemoteEntityInterpolator worldEntity 세팅(해당 시)**

서버에 `RemoteEntityInterpolator`가 존재하면(클라와 동일 클래스인지 서버 사본인지 확인) Task 2 Step 1·2와 동일하게 `worldEntity` 필드 추가 + 서버 크리에이터에서 세팅. 서버에 없으면 스킵.

- [ ] **Step 3: 컴파일 확인**

UnityMCP `refresh_unity` + `read_console`(unity_instance=Server). Expected: 에러 0.

- [ ] **Step 4: 커밋 (Server 레포)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/entity-s5a-ientity-deletion   # 없으면 생성
git add Assets/Scripts/AI/EnemyBrain.cs Assets/Scripts/Entity/LOPEntityManager.cs Assets/Scripts/Entity/LOPEntityView.cs Assets/Scripts/EntityCreationDataFactory/ Assets/Scripts/EntityCreator/
git commit -m "refactor(entity): 서버 모션 사이트를 World.Entity 익스텐션으로 (S5a Task3)"
```

- [ ] **Step 5: 인게임 스모크(사용자)** — 서버 AI 이동·스냅 송신·테스트렌더 무변화.

---

### Task 4: LOP 소비처 IEntity→LOPActor 재타입 (Client + Server)

GF 제약은 아직 `IEntity`인 채로, LOP 콘크리트 소비처를 `LOPActor`로 재타입한다. 제네릭 흐름에서 `IEntity`가 들어오는 지점은 매니저에서 `(LOPActor)` 캐스트. 그린 유지(LOPActor는 IEntity라 캐스트 유효).

**Files (Client):** `Assets/Scripts/Entity/Event.Entity.cs:103-110`, `Assets/Scripts/Entity/LOPEntityManager.cs`, `Assets/Scripts/Game/EntityBinder.cs`, `Assets/Scripts/Game/PlayerHudCoordinator.cs`
**Files (Server):** `Assets/Scripts/Entity/Event.Entity.cs`, `Assets/Scripts/Entity/LOPEntityManager.cs`, `Assets/Scripts/AI/IBrain.cs`, `Assets/Scripts/AI/EnemyBrain.cs`, `Assets/Scripts/EntityCreationDataFactory/IEntityCreationDataFactory.cs`, `IEntityCreationDataCreator.cs`, `EntityCreationDataFactory.cs`, `CharacterCreationDataCreator.cs`, `ItemCreationDataCreator.cs`

**Interfaces:**
- Produces: `EntityCreated.entity` 타입 = `LOPActor` (양쪽). `IBrain<T>`만 남고 비제네릭 `IBrain.Think(IEntity)` 제거(서버). CreationData creator `Create(LOPActor)`.

- [ ] **Step 1: `EntityCreated.entity` → LOPActor (클·서)**

Client `Event.Entity.cs:103-110`:
```csharp
    public struct EntityCreated
    {
        public LOPActor entity;
        public EntityCreated(LOPActor entity)
        {
            this.entity = entity;
        }
    }
```
Server `Event.Entity.cs`도 동일(`GameFramework.IEntity` → `LOPActor`).

- [ ] **Step 2: 매니저 `entityMap` 타입 + 캐스트 (클·서)**

Client `LOPEntityManager.cs`:
- line 27: `private Dictionary<string, LOPActor> entityMap = new Dictionary<string, LOPActor>();`
  - 주석 추가: `// id→뷰 앵커 인덱스. World EntityRegistry(id→데이터 진실원본)와 별개 축.`
- `CreateEntity`(72-83): `entity`는 `TEntity`(제약 아직 IEntity). 캐스트로 entityId·EntityCreated 처리:
```csharp
        public TEntity CreateEntity<TEntity, TCreationData>(TCreationData creationData)
            where TEntity : IEntity
            where TCreationData : struct, IEntityCreationData
        {
            var entity = entityFactory.CreateEntity<TEntity, TCreationData>(creationData);

            var actor = (LOPActor)(object)entity;
            entityMap[actor.entityId] = actor;

            entityCreatedPublisher.Publish(new EntityCreated(actor));

            return entity;
        }
```
  - `GetEntities()` 비제네릭(62-65): 반환은 `IEnumerable<IEntity>` 유지(제약 아직 IEntity) — `entityMap.Values`가 `LOPActor`라 `.Cast<IEntity>().ToList()` 또는 그대로(LOPActor:IEntity 공변) 반환. `return entityMap.Values.Cast<IEntity>().ToList();`
  - `GetEntities<T>()`(67-70): `entityMap.Values.Cast<T>().ToList()` 그대로(LOPActor→T).
  - `GetEntity`/`TryGetEntity`: `entityMap[id]`가 LOPActor → IEntity 반환 암시 변환 OK. 그대로.
Server `LOPEntityManager.cs`도 동일 변경 + 서버 전용 `GetAllEntitySnaps`/`GetAllEntityCreationDatas`/`userEntityMap` 흐름은 `GetEntities<LOPActor>()`로 정합(Task 3에서 일부 처리됨).

- [ ] **Step 3: 서버 `IBrain` 비제네릭 오버로드 제거**

`IBrain.cs`: 비제네릭 `void Think(IEntity entity, double)` 제거, `IBrain<T> where T : IEntity`는 유지(제약 아직 IEntity). `EnemyBrain.cs`: `Think(IEntity)` 오버로드(61-71) 삭제. 호출부(AIController 등) 확인 — `Think(LOPActor)` 제네릭 경로만 남는지 grep. 남은 `Think(IEntity)` 호출 있으면 `Think(lopActor)`로.

- [ ] **Step 4: 서버 CreationData creator `Create(IEntity)` → `Create(LOPActor)`**

`IEntityCreationDataFactory.cs`/`IEntityCreationDataCreator.cs`/`EntityCreationDataFactory.cs`/`CharacterCreationDataCreator.cs`/`ItemCreationDataCreator.cs`에서 `Create(IEntity)` 시그니처 → `Create(LOPActor)`, 내부 `if (entity is LOPActor lopEntity)` 다운캐스트 제거(이미 LOPActor). `IEntityCreationDataCreator`의 `where TEntity : IEntity`는 유지(다음 태스크에서 MonoBehaviour로). factory가 `GetEntities()`로 얻은 것을 넘길 때 `LOPActor`로 넘어가는지 확인(Task 3/Step2에서 `<LOPActor>` 사용).

- [ ] **Step 5: EntityCreated 소비처 정리 (클라)**

`EntityBinder.cs:38` `if (entityCreated.entity is not LOPActor entity)` — 이제 `entity`가 `LOPActor`라 패턴 매칭 불필요:
```csharp
            LOPActor entity = entityCreated.entity;
            if (entity == null) return;
```
(이후 `entity.gameObject`/`entity.entityId` 그대로.) `PlayerHudCoordinator.cs`도 `entityCreated.entity`(LOPActor) 사용부 타입 정리.

- [ ] **Step 6: 3레포 컴파일 확인**

UnityMCP `refresh_unity`+`read_console` (Client, Server 각각). GF 변경 없음. Expected: 에러 0.

- [ ] **Step 7: 커밋 (Client, Server 각각)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client && git add -A && git commit -m "refactor(entity): 클라 IEntity 소비처를 LOPActor로 재타입 (S5a Task4)"
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server && git add -A && git commit -m "refactor(entity): 서버 IEntity 소비처를 LOPActor로 재타입 (S5a Task4)"
```

---

### Task 5: GF 엔티티 추상 제약 IEntity→MonoBehaviour (GameFramework + 매니저 impl)

GF 제네릭 제약을 `MonoBehaviour`로 바꾸고 매니저 impl 시그니처를 맞춘다. Task 4의 캐스트가 있어 그린 유지. **원자적 3레포 컴파일.**

**Files (GameFramework):** `IEntityManager.cs`, `IEntityFactory.cs`, `IEntityCreator.cs`, `EntityFactory.cs`
**Files (Client/Server):** 각 `LOPEntityManager.cs` (impl 시그니처)

**Interfaces:**
- Produces: GF 추상 제약 `where TEntity : MonoBehaviour`. `IEntityManager` 비제네릭 반환 `MonoBehaviour`.

- [ ] **Step 1: GF 인터페이스/팩토리 제약 교체**

`IEntityManager.cs`: 모든 `where TEntity : IEntity` → `where TEntity : MonoBehaviour`; 비제네릭 `IEntity GetEntity(string)` → `MonoBehaviour GetEntity(string)`; `bool TryGetEntity(string, out IEntity)` → `out MonoBehaviour`; `IEnumerable<IEntity> GetEntities()` → `IEnumerable<MonoBehaviour>`. (`using UnityEngine;` 이미 있음.)
`IEntityFactory.cs`·`IEntityCreator.cs`·`EntityFactory.cs`: `where TEntity : IEntity` → `where TEntity : MonoBehaviour`. `IEntityCreator.cs`는 이미 `using UnityEngine;` 있음. `EntityFactory.cs`는 `using UnityEngine;` 추가 필요할 수 있음.

- [ ] **Step 2: 매니저 impl 시그니처 맞춤 (클·서)**

`LOPEntityManager.cs`(클·서): `where T : IEntity` → `where T : MonoBehaviour`; 비제네릭 `GetEntity(string)` 반환 `MonoBehaviour`; `TryGetEntity(out MonoBehaviour)`; `GetEntities()` 반환 `IEnumerable<MonoBehaviour>` (본문 `entityMap.Values.Cast<MonoBehaviour>().ToList()` 또는 `entityMap.Values.ToList<MonoBehaviour>()` — LOPActor는 MonoBehaviour). `CreateEntity` 제약도 `MonoBehaviour`. `(LOPActor)(object)entity` 캐스트는 그대로 유효(MonoBehaviour→LOPActor). `GetEntityByUserId<TEntity>`(서버) 제약도 MonoBehaviour.

- [ ] **Step 3: 3레포 컴파일 확인**

UnityMCP: GF 변경이 클·서 양쪽에서 컴파일되는지 두 인스턴스 모두 `read_console`. Expected: 에러 0. (`IEntity`는 아직 존재 — LOPActor `: IEntity`가 남아 있음.)

- [ ] **Step 4: 커밋 (GameFramework, Client, Server)**

```bash
cd C:/Users/re5na/workspace/LOP/GameFramework && git add -A && git commit -m "refactor(entity): 엔티티 추상 제약 IEntity→MonoBehaviour (S5a Task5)"
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client && git add -A && git commit -m "refactor(entity): 매니저 impl 시그니처 MonoBehaviour 정합 (S5a Task5)"
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server && git add -A && git commit -m "refactor(entity): 매니저 impl 시그니처 MonoBehaviour 정합 (S5a Task5)"
```

---

### Task 6: LOPActor 슬림화 + IsGrounded 이전 (Client + Server)

`LOPActor`에서 파사드/모션 백킹/`: IEntity`를 제거해 순수 신원 앵커로. `IsGrounded`(클라)는 뷰로 이전. 크리에이터의 `LinkWorldMotion` 호출 제거. 이후 `IEntity`는 자기 파일 외 참조 0.

**Files (Client):** `Assets/Scripts/Entity/LOPActor.cs`, `Assets/Scripts/Entity/LOPEntityView.cs`, `Assets/Scripts/EntityCreator/CharacterCreator.cs`, `ItemCreator.cs`
**Files (Server):** `Assets/Scripts/Entity/LOPActor.cs`, `Assets/Scripts/EntityCreator/CharacterCreator.cs`, `ItemCreator.cs`

- [ ] **Step 1: 클라 `LOPActor` 슬림화**

`LOPActor.cs`(client) 전체를:
```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPActor : MonoBehaviour
    {
        public string entityId { get; private set; }

        public virtual void Initialize<TEntityCreationData>(TEntityCreationData creationData)
            where TEntityCreationData : struct, IEntityCreationData
        {
            entityId = creationData.entityId;
        }
    }
}
```
(파사드 프로퍼티·`LinkWorldMotion`·`worldTransform/worldVelocity`·`: IEntity`·`IsGrounded`·`using System.Linq` 제거.)

- [ ] **Step 2: 클라 뷰로 `IsGrounded` 이전**

`LOPEntityView.cs`에 접지 판정을 옮긴다(Task 2에서 `worldEntity`를 이미 registry로 얻음). `UpdateRunAnimation`에서:
```csharp
            var worldEntity = entityRegistry.Get(entity.entityId);
            Vector3 v = worldEntity != null ? worldEntity.GetVelocity() : Vector3.zero;
            float horizontalSpeedSquared = v.x * v.x + v.z * v.z;
            bool grounded = worldEntity != null && IsGrounded(worldEntity.GetPosition());
            animator.SetBool("Run", horizontalSpeedSquared > walkThreshold * walkThreshold && grounded);
```
그리고 뷰에 private 헬퍼 추가(구 LOPActor.IsGrounded 이식):
```csharp
        // TODO: 고도화 필요! (접지 판정 — 구 LOPActor에서 이전)
        private static bool IsGrounded(Vector3 position)
        {
            Vector3 checkPosition = position + Vector3.down * 0.2f;
            Collider[] colliders = Physics.OverlapSphere(checkPosition, 0.4f);
            return System.Linq.Enumerable.Any(colliders, col => col.gameObject.name == "Plane");
        }
```

- [ ] **Step 3: 크리에이터 `LinkWorldMotion` 호출 제거 (클·서)**

`CharacterCreator.cs`(client) 42-47을:
```csharp
            LOPActor entity = root.AddComponent<LOPActor>();
            objectResolver.Inject(entity);
            entity.Initialize(creationData);
```
78행 주석 "Transform/Velocity는 위에서 생성(파사드 백킹)"에서 "(파사드 백킹)" 제거. `ItemCreator.cs`(client) 30-35도 동일(LinkWorldMotion 3줄 제거). 서버 `CharacterCreator`/`ItemCreator`도 동일하게 `LinkWorldMotion` 호출 제거.

- [ ] **Step 4: 서버 `LOPActor` 슬림화**

서버 `LOPActor.cs`를 클라 Step 1과 동일하게(단 서버엔 `IsGrounded`가 원래 없음). `: IEntity`·파사드·LinkWorldMotion·필드 제거, `entityId`+`Initialize`만.

- [ ] **Step 5: 3레포 컴파일 확인**

UnityMCP (Client, Server). GF 변경 없음. Expected: 에러 0. 이제 `IEntity`는 `IEntity.cs` 외에서 참조 0(grep `\bIEntity\b`로 확인 — GF `IEntity.cs`만 남아야).

- [ ] **Step 6: 커밋 (Client, Server)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client && git add -A && git commit -m "refactor(entity): LOPActor를 순수 신원 앵커로 슬림화 + IsGrounded 뷰 이전 (S5a Task6)"
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server && git add -A && git commit -m "refactor(entity): LOPActor를 순수 신원 앵커로 슬림화 (S5a Task6)"
```

---

### Task 7: IEntity.cs 삭제 + 최종 검증 (GameFramework)

**Files:** Delete `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Entity/IEntity.cs` (+`.meta`)

- [ ] **Step 1: 잔여 참조 0 확인**

3레포에서 `\bIEntity\b` grep → `IEntity.cs` 자기 자신 외 매치 0 확인. (`IEntityManager`/`IEntityFactory`/`IEntityCreator`/`IEntityCreationData*`는 whole-word라 매치 안 됨.)

- [ ] **Step 2: 파일 삭제**

```bash
cd C:/Users/re5na/workspace/LOP/GameFramework
git rm Runtime/Scripts/Entity/IEntity.cs Runtime/Scripts/Entity/IEntity.cs.meta
```

- [ ] **Step 3: 3레포 컴파일 + EditMode**

UnityMCP `refresh_unity`(scope 넓게)+`read_console` (Client, Server). GF EditMode `run_tests` — 기존 154 + Task1 신규 green. Expected: 에러 0, 테스트 green.

- [ ] **Step 4: 커밋 (GameFramework)**

```bash
git commit -m "refactor(entity): IEntity 파사드 인터페이스 삭제 (S5a Task7)"
```

- [ ] **Step 5: 최종 인게임 스모크(사용자)** — 스폰·이동·충돌·아이템 줍기·넉백/데미지·롤백 reconcile·원격 보간·서버 테스트렌더 전부 무변화 확인.

- [ ] **Step 6: 최종 whole-branch 리뷰** — `superpowers:requesting-code-review`로 3레포 교차 리뷰(DI 실파일 대조). Critical/Important 0 목표.

---

## Self-Review (스펙 대비)

- **spec §1 모션 익스텐션** → Task 1. ✓
- **spec §2 LOPActor 슬림 + IsGrounded 이전** → Task 6. ✓
- **spec §3 IEntity 삭제 + 소비처 재배선(GF 패밀리 5종 + 모션 6사이트 + 타입 재타입)** → Task 2/3(모션), Task 4(타입), Task 5(GF 제약), Task 7(삭제). ✓
- **spec §4 IEntityManager 유지+재타입(RunnerBase 무변)** → Task 5(제약만, RunnerBase 미변경). ✓
- **spec §5 entityMap 재규정(뷰-인덱스)** → Task 4 Step 2(타입 + 주석). ✓
- **spec §6 어휘 rename 범위 밖** → Global Constraints에 명시, 이 플랜은 rename 안 함. ✓
- **spec 테스트/그린 판정** → Task 1 EditMode + 각 태스크 컴파일 + Task 2/3/7 인게임 스모크. ✓
- **위험: GF 원자 변경** → Task 5/7이 3레포 동시 컴파일 확인. ✓
- **위험: 모션 값 동치** → Task 1 EditMode 가드 테스트 + 인게임 스모크. ✓
- **위험: worldEntity 배선 누락** → Task 2/3에서 크리에이터가 세팅, 스폰 스모크. ✓
- **위험: 비제네릭 GetEntities 잔여** → Task 3에서 `<LOPActor>` 전환, Task 7 grep. ✓
- **추가 발견(플랜 중):** 제네릭 매니저 `entity.entityId`가 MonoBehaviour 제약 하에 깨짐 → Task 4에서 `(LOPActor)` 캐스트 선투입, Task 5 제약 변경 시 그린 유지. Self-review로 순서 매듭 해소 확인. ✓

## 상태

플랜 작성 완료(2026-07-18). 실행 = subagent-driven(권장). 상태 원장은 `docs/ROADMAP.md`.
spec: `docs/superpowers/specs/2026-07-18-s5a-ientity-deletion-design.md`.
