# 판치기 슬라이스 1~2 (토대) 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 판치기 모드에 입장할 수 있게 만들고, 서버가 굴리는 동전이 두 클라에 똑같이 보이는 데까지 간다.

**Architecture:** 몸을 세우는 설정을 바인더 하드코딩에서 엔티티 데이터(`PhysicsConfig`)로 옮긴 뒤,
그 위에서 판치기 동전을 **다이나믹 몸**으로 세운다. 동전 위치·회전은 기존 `EntitySnap` 파이프라인을
그대로 타고, 클라는 `AllInterpolatedSyncPolicy`로 보기만 한다.

**Tech Stack:** Unity 6000.3.16f1 · VContainer · Mirror · Luban(마스터데이터) · NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-08-24-panchigi-game-mode-design.md`

## Global Constraints

- **범위**: 이 계획은 spec의 **슬라이스 1~2**만 다룬다. 3~5(타격 입력·턴 상태 기계·HUD)는 별도 계획.
- **레포 6개가 걸린다**: `GameFramework` · `LeagueOfPhysical-Shared` · `LeagueOfPhysical-Client` ·
  `LeagueOfPhysical-Server` · `LeagueOfPhysical-MasterData-{Client,Server}` · `infrastructure`.
  모두 `C:/Users/re5na/workspace/LOP/` 아래 형제 폴더다.
- **푸시 규약** (`CLAUDE.md`): 레포마다 피처 브랜치 → `git fetch` → `git rebase --autostash origin/main`
  → `git checkout main` → `git merge --ff-only origin/main` → `git merge --no-ff <feature>` → `git push`.
  **한 줄씩 결과를 확인**하고, `&&`로 이어 붙이지 않는다. `--force` 금지.
- **`git add -A` / `git commit -a` 금지.** 반드시 바꾼 파일만 경로로 지정하고, 커밋 전에
  `git status --short`로 스테이지된 것이 의도한 파일뿐인지 확인한다.
- **로컬 픽스처는 커밋하지 않는다.** 클라: `Assets/AddressableAssetsData/AssetGroups/*.asset`,
  `Assets/Art`, `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`, `ProjectSettings/PackageManagerSettings.asset`.
  서버: `Assets/DefaultVolumeProfile.asset`, `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`,
  `Assets/Scripts/Game/FlapWangRuleSystem.cs`(`SpawnEnemies` 개수).
  그 파일을 **정말 고쳐야 하면** 먼저 `git stash push -m ... <경로>` 로 빼두고 작업 후 `pop`한다.
- **`.meta` 파일은 반드시 함께 커밋한다.** 새 `.cs`를 만들면 Unity가 `.meta`를 만들 때까지 기다린 뒤
  둘을 같이 add한다. `.meta`를 직접 만들지 않는다.
- **컴파일 게이트** (에디터가 떠 있을 때):
  ```bash
  unity command recompile        --project-path "C:/Users/re5na/workspace/LOP/<프로젝트>"
  unity command recompile_status --project-path "C:/Users/re5na/workspace/LOP/<프로젝트>"
  # {"status":"completed","failed":false,"errors":[]} 이어야 통과
  ```
  응답 대기가 30초 상한이라 `recompile`은 타임아웃이 떠도 에디터에선 계속 돈다 — `recompile_status`로 폴링.
  **`file:` 패키지(GameFramework/LOP-Shared/MasterData)를 고치면 CLI로는 재임포트가 안 된다** —
  `Library/ScriptAssemblies/*.dll`의 mtime이 편집 시각보다 이전이면 **사용자에게 에디터 창 포커스를 요청**한다.
- **EditMode 테스트**:
  ```bash
  unity command run_tests   --mode EditMode --async_tests true --project-path "<프로젝트>"
  unity command test_status --project-path "<프로젝트>"
  ```
  클라/서버 **앱 코드**(`Assets/Scripts`)는 asmdef가 없어 테스트를 붙일 수 없다. 순수 로직은 반드시
  `GameFramework` 또는 `LeagueOfPhysical-Shared` 패키지에 두고 그쪽 EditMode로 덮는다.
- **World Core는 `noEngineReferences: true`** — `GameFramework/Runtime/Scripts/World/` 아래 코드는
  `UnityEngine`을 쓸 수 없다. `System.Numerics`를 쓴다.
- **LOP 측 파일에서 World 타입은 풀 네임스페이스로 한정**한다
  (`GameFramework.World.Entity`). `using GameFramework.World;`를 추가하지 않는다 —
  `Component`가 `UnityEngine.Component`와 겹친다.

---

## 파일 구조

### 새로 만드는 파일

| 경로 | 책임 |
|---|---|
| `GameFramework/Runtime/Scripts/World/Components/PhysicsConfig.cs` | 엔진에 몸을 세울 때의 설정(종류·회전고정·트리거) |
| `GameFramework/Runtime/Scripts/World/Components/DiscShape.cs` | 원반 몸의 순수 기하(반지름·두께) |
| `GameFramework/Tests/World/PhysicsConfigTests.cs` | 위 두 컴포넌트 테스트 |
| `GameFramework/Tests/World/DiscShapeTests.cs` | |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/PanchigiWorld.cs` | 판치기 시뮬 — 동전은 PhysX가 굴리므로 비어 있다 |
| `LeagueOfPhysical-Shared/Tests/EditMode/PanchigiWorldTests.cs` | |
| `LeagueOfPhysical-Client/Assets/Scripts/EntitySync/AllInterpolatedSyncPolicy.cs` | 예측 없음 — 전부 보간 |
| `LeagueOfPhysical-Client/Assets/Tests/EditMode/EntitySync/AllInterpolatedSyncPolicyTests.cs` | |
| `LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiLifetimeScope.cs` | 클라 판치기 스코프 |
| `LeagueOfPhysical-Client/Assets/Scripts/Entity/PanchigiPlayerCreator.cs` | 몸 없는 플레이어 엔티티(클) |
| `LeagueOfPhysical-Client/Assets/Scripts/Entity/PanchigiCoinCreator.cs` | 동전 엔티티(클) — kinematic |
| `LeagueOfPhysical-Client/Assets/Scenes/Panchigi.unity` | 클라 판치기 씬 |
| `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiLifetimeScope.cs` | 서버 판치기 스코프 |
| `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiRuleSystem.cs` | 룰 골격 — 이 계획에선 동전 스폰까지만 |
| `LeagueOfPhysical-Server/Assets/Scripts/Entity/PanchigiPlayerCreator.cs` | 몸 없는 플레이어 엔티티(서) |
| `LeagueOfPhysical-Server/Assets/Scripts/Entity/PanchigiCoinCreator.cs` | 동전 엔티티(서) — **dynamic** |
| `LeagueOfPhysical-Server/Assets/Scenes/Panchigi.unity` | 서버 판치기 씬 |
| `infrastructure/table/Datas/#PanchigiConfig.xlsx` | 전역 튜닝(단일 행) |
| `infrastructure/table/Datas/#PanchigiSetup.xlsx` | 인원별 동전 수·대형 |

### 고치는 파일

| 경로 | 무엇 |
|---|---|
| `GameFramework/Runtime/Scripts/World/PhysicsBody.cs` | `Get*` 세 개 추가 |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/UnityPhysicsBody.cs` | `Get*` 구현, 생성자 `Collider`로 확장 |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/PhysicsBodyFactory.cs` | `PhysicsConfig`를 읽어 몸을 세운다 |
| `LeagueOfPhysical-Shared/Runtime/Scripts/Game/EntityType.cs` | `Coin = 5` 추가 |
| `LeagueOfPhysical-{Client,Server}/Assets/Scripts/Entity/EntityBinder.cs` | 인자 없이 팩토리 호출 |
| `LeagueOfPhysical-{Client,Server}/Assets/Scripts/Entity/{Character,FlappyBird,Item}Creator.cs` | `PhysicsConfig` 부착 |
| `LeagueOfPhysical-{Client,Server}/Assets/Scripts/Game/TickSystems/PhysicsSimulationSystem.cs` | 다이나믹 몸 제외 + 되읽기 |
| `infrastructure/table/Datas/#GameMode.xlsx`, `#Map.xlsx`, `__tables__.xlsx` | 판치기 행·테이블 등록 |
| `LeagueOfPhysical-MasterData-{Client,Server}/Runtime/Scripts/LOPMasterData.cs` | `TableFiles`에 새 테이블 2개 |

---

# Phase A — 몸 세우기를 데이터로 (판치기와 무관, 거동 불변)

## Task 1: `PhysicsConfig` 컴포넌트

**Files:**
- Create: `GameFramework/Runtime/Scripts/World/Components/PhysicsConfig.cs`
- Test: `GameFramework/Tests/World/PhysicsConfigTests.cs`

**Interfaces:**
- Consumes: `GameFramework.World.Component`, `GameFramework.World.Entity`
- Produces: `GameFramework.World.BodyKind { Static, Kinematic, Dynamic }`,
  `GameFramework.World.PhysicsConfig(BodyKind kind, bool freezeRotation, bool isTrigger)` with
  `Kind`, `FreezeRotation`, `IsTrigger`, `bool PhysicsOwnsMotion` (== `Kind == BodyKind.Dynamic`)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`GameFramework/Tests/World/PhysicsConfigTests.cs`:

```csharp
using NUnit.Framework;

namespace GameFramework.World.Tests
{
    public class PhysicsConfigTests
    {
        [Test]
        public void AttachesToEntityAndRoundTrips()
        {
            var entity = new Entity("e1");
            entity.Add(new PhysicsConfig(BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            var config = entity.Get<PhysicsConfig>();
            Assert.AreEqual(BodyKind.Kinematic, config.Kind);
            Assert.IsTrue(config.FreezeRotation);
            Assert.IsFalse(config.IsTrigger);
            Assert.AreSame(entity, config.Owner);
        }

        [Test]
        public void DynamicBodyMeansPhysicsOwnsMotion()
        {
            // 이 파생 속성이 "밀어넣을 것인가 읽어올 것인가"를 가른다.
            Assert.IsTrue(new PhysicsConfig(BodyKind.Dynamic, false, false).PhysicsOwnsMotion);
        }

        [Test]
        public void NonDynamicBodiesAreDrivenByUs()
        {
            // Static도 PhysX가 움직이지 않는다 — 우리가 밀어넣는 쪽이다.
            Assert.IsFalse(new PhysicsConfig(BodyKind.Kinematic, true, false).PhysicsOwnsMotion);
            Assert.IsFalse(new PhysicsConfig(BodyKind.Static, true, false).PhysicsOwnsMotion);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```bash
unity command run_tests   --mode EditMode --async_tests true --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
unity command test_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
```
Expected: 컴파일 실패 — `PhysicsConfig`가 없다.

- [ ] **Step 3: 최소 구현**

`GameFramework/Runtime/Scripts/World/Components/PhysicsConfig.cs`:

```csharp
namespace GameFramework.World
{
    /// <summary>물리 엔진이 이 몸을 어떻게 취급할지. Box2D b2BodyType / Unity RigidbodyType2D와 같은 세 값.</summary>
    public enum BodyKind
    {
        Static,
        Kinematic,
        Dynamic,
    }

    /// <summary>
    /// 이 엔티티를 물리 엔진에 어떻게 세울지. 값은 게임이 정한다(엔티티를 만드는 쪽이 붙인다) —
    /// 몸을 만드는 팩토리가 여기서 기본값을 지어내면 시뮬이 쓰는 몸과 어긋난다.
    /// 모양은 별개다(<see cref="CapsuleShape"/>/<see cref="DiscShape"/>) — 그쪽은 엔진을 모르는
    /// 코어 sweep도 같이 읽는 순수 기하다.
    /// </summary>
    public class PhysicsConfig : Component
    {
        public BodyKind Kind { get; }

        /// <summary>회전을 잠글지. 캐릭터는 넘어지면 안 되고, 동전은 굴러야 한다.</summary>
        public bool FreezeRotation { get; }

        /// <summary>막지 않고 통과시키며 접촉만 알리는 몸인지(Box2D의 sensor).</summary>
        public bool IsTrigger { get; }

        /// <summary>물리 엔진이 이 몸의 모션을 소유하는가. 참이면 우리가 밀어넣지 않고 읽어온다.</summary>
        public bool PhysicsOwnsMotion => Kind == BodyKind.Dynamic;

        public PhysicsConfig(BodyKind kind, bool freezeRotation, bool isTrigger)
        {
            Kind = kind;
            FreezeRotation = freezeRotation;
            IsTrigger = isTrigger;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Step 2와 같은 명령. Expected: `PhysicsConfigTests` 3건 PASS, 기존 테스트 회귀 없음.

- [ ] **Step 5: 커밋** (GameFramework 레포)

```bash
cd C:/Users/re5na/workspace/LOP/GameFramework
git checkout -b feat/physics-config
git status --short   # 의도한 4개(.cs + .meta 2쌍)만 있는지 확인
git add Runtime/Scripts/World/Components/PhysicsConfig.cs Runtime/Scripts/World/Components/PhysicsConfig.cs.meta Tests/World/PhysicsConfigTests.cs Tests/World/PhysicsConfigTests.cs.meta
git commit -m "feat(world): 몸을 엔진에 세울 때의 설정을 컴포넌트로"
```

---

## Task 2: `DiscShape` 컴포넌트

**Files:**
- Create: `GameFramework/Runtime/Scripts/World/Components/DiscShape.cs`
- Test: `GameFramework/Tests/World/DiscShapeTests.cs`

**Interfaces:**
- Produces: `GameFramework.World.DiscShape(float radius, float thickness)` with `Radius`, `Thickness`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`GameFramework/Tests/World/DiscShapeTests.cs`:

```csharp
using NUnit.Framework;

namespace GameFramework.World.Tests
{
    public class DiscShapeTests
    {
        [Test]
        public void AttachesToEntityAndRoundTrips()
        {
            var entity = new Entity("coin1");
            entity.Add(new DiscShape(0.15f, 0.02f));

            Assert.AreEqual(0.15f, entity.Get<DiscShape>().Radius, 1e-4f);
            Assert.AreEqual(0.02f, entity.Get<DiscShape>().Thickness, 1e-4f);
            Assert.AreSame(entity, entity.Get<DiscShape>().Owner);
        }

        [Test]
        public void RejectsNonPositiveSize()
        {
            // 0이나 음수 원반은 콜라이더가 아무것도 못 맞히는 몸이 된다 — 만들 때 막는다.
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new DiscShape(0f, 0.02f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new DiscShape(0.15f, -0.02f));
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Task 1 Step 2와 같은 명령. Expected: 컴파일 실패 — `DiscShape`가 없다.

- [ ] **Step 3: 최소 구현**

`GameFramework/Runtime/Scripts/World/Components/DiscShape.cs`:

```csharp
using System;

namespace GameFramework.World
{
    /// <summary>
    /// 원반 몸의 치수(동전처럼 얇고 둥근 것). <see cref="CapsuleShape"/>과 같은 자리 —
    /// 순수 기하만 담고 엔진 설정은 <see cref="PhysicsConfig"/>가 갖는다.
    /// 값은 게임이 정한다(엔티티를 만드는 쪽이 붙인다).
    /// </summary>
    public class DiscShape : Component
    {
        public float Radius { get; }

        /// <summary>원반의 두께(높이).</summary>
        public float Thickness { get; }

        public DiscShape(float radius, float thickness)
        {
            if (radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "원반 반지름은 0보다 커야 한다.");
            }
            if (thickness <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(thickness), thickness, "원반 두께는 0보다 커야 한다.");
            }

            Radius = radius;
            Thickness = thickness;
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

Expected: `DiscShapeTests` 2건 PASS.

- [ ] **Step 5: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/GameFramework
git status --short
git add Runtime/Scripts/World/Components/DiscShape.cs Runtime/Scripts/World/Components/DiscShape.cs.meta Tests/World/DiscShapeTests.cs Tests/World/DiscShapeTests.cs.meta
git commit -m "feat(world): 원반 몸의 기하를 컴포넌트로"
```

---

## Task 3: 물리 몸 포트에 읽기 API를 더한다

지금 `PhysicsBody`는 `Set*`만 있다. 다이나믹 몸의 결과를 World로 되읽으려면 읽기가 필요하다.

**Files:**
- Modify: `GameFramework/Runtime/Scripts/World/PhysicsBody.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/UnityPhysicsBody.cs`

**Interfaces:**
- Consumes: 없음
- Produces: `PhysicsBody.GetPosition() → System.Numerics.Vector3`,
  `PhysicsBody.GetRotation() → System.Numerics.Quaternion`,
  `PhysicsBody.GetVelocity() → System.Numerics.Vector3`.
  `UnityPhysicsBody(Rigidbody rigidbody, Collider collider)` — 생성자 두 번째 인자가
  `CapsuleCollider` → `Collider`로 넓어진다.

- [ ] **Step 1: 포트에 추상 메서드를 더한다**

`GameFramework/Runtime/Scripts/World/PhysicsBody.cs`의 `SetVelocity` 선언 **바로 아래**에 추가:

```csharp
        /// <summary>물리 엔진이 굴리는 몸(다이나믹)의 결과를 World로 되읽을 때 쓴다.</summary>
        public abstract Vector3 GetPosition();

        public abstract Quaternion GetRotation();

        public abstract Vector3 GetVelocity();
```

- [ ] **Step 2: `UnityPhysicsBody`에서 구현하고 생성자를 넓힌다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/UnityPhysicsBody.cs`:

- 필드/생성자의 `CapsuleCollider` 타입을 `Collider`로 바꾼다 (원반은 캡슐이 아니다).
- `SetVelocity` 아래에 다음을 추가:

```csharp
        public override System.Numerics.Vector3 GetPosition()
        {
            return _rigidbody == null ? System.Numerics.Vector3.Zero : _rigidbody.position.ToNumerics();
        }

        public override System.Numerics.Quaternion GetRotation()
        {
            return _rigidbody == null ? System.Numerics.Quaternion.Identity : _rigidbody.rotation.ToNumerics();
        }

        public override System.Numerics.Vector3 GetVelocity()
        {
            return _rigidbody == null ? System.Numerics.Vector3.Zero : _rigidbody.linearVelocity.ToNumerics();
        }
```

- [ ] **Step 3: 컴파일 확인**

```bash
unity command recompile        --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
unity command recompile_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
```
Expected: `{"status":"completed","failed":false,"errors":[]}`
`file:` 패키지를 고쳤으므로 `Library/ScriptAssemblies/baegames.GameFramework.World.dll`의 mtime을 확인하고,
편집 시각보다 이전이면 **사용자에게 에디터 창 포커스를 요청**한다.

- [ ] **Step 4: 서버도 컴파일 확인**

```bash
unity command recompile        --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
unity command recompile_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
```
Expected: 위와 같음.

- [ ] **Step 5: 커밋 (두 레포)**

```bash
cd C:/Users/re5na/workspace/LOP/GameFramework
git status --short
git add Runtime/Scripts/World/PhysicsBody.cs
git commit -m "feat(world): 물리 몸 포트에 읽기 API"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feat/physics-config
git status --short
git add Runtime/Scripts/Game/UnityPhysicsBody.cs
git commit -m "feat(physics): 몸 읽기 구현 + 콜라이더 타입을 캡슐에서 일반으로"
```

---

## Task 4: 팩토리가 `PhysicsConfig`를 읽는다

**Files:**
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/PhysicsBodyFactory.cs`

**Interfaces:**
- Consumes: `PhysicsConfig`, `CapsuleShape`, `DiscShape` (Task 1·2)
- Produces: `PhysicsBodyFactory.Create(GameObject root, GameFramework.World.Entity worldEntity)` —
  **인자 두 개**(`isKinematic`/`isTrigger` 제거)

- [ ] **Step 1: 시그니처와 본문을 바꾼다**

`PhysicsBodyFactory.Create`를 통째로 아래로 교체:

```csharp
        public static UnityPhysicsBody Create(GameObject root, GameFramework.World.Entity worldEntity)
        {
            var config = worldEntity.Get<GameFramework.World.PhysicsConfig>();
            if (config == null)
            {
                // 몸을 어떻게 세울지는 엔티티를 만드는 쪽(게임)이 정한다 — 여기서 기본값을
                // 지어내면 시뮬이 쓰는 몸과 다시 어긋난다(CapsuleShape과 같은 이유).
                throw new System.InvalidOperationException(
                    $"[PhysicsBodyFactory] {worldEntity.Id}에 PhysicsConfig가 없다 — 크리에이터가 붙여야 한다.");
            }

            var capsule = worldEntity.Get<GameFramework.World.CapsuleShape>();
            var disc = worldEntity.Get<GameFramework.World.DiscShape>();
            if (capsule == null && disc == null)
            {
                throw new System.InvalidOperationException(
                    $"[PhysicsBodyFactory] {worldEntity.Id}에 몸 모양이 없다 — CapsuleShape이나 DiscShape을 붙여야 한다.");
            }

            var worldTransform = worldEntity.Get<GameFramework.World.Transform>();
            var worldVelocity = worldEntity.Get<GameFramework.World.Velocity>();

            root.layer = LayerMask.NameToLayer("Character");

            //  루트(시뮬 바디)를 스폰 위치에 즉시 놓는다. kinematic rb의 rb.position은 다음 물리 스텝에야
            //  트랜스폼에 반영돼, 루트가 한 틱 원점에 머물다 점프하면 자식 모델이 끌려가 첫 틱에 순간이동한다.
            root.transform.SetPositionAndRotation(worldTransform.Position.ToUnity(), worldTransform.Rotation.ToUnity());

            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.linearDamping = 0f;   //  수평 정지는 이동 모터가 0으로 제동한다. 수직은 순수 중력.
            rigidbody.angularDamping = 0.05f;
            rigidbody.constraints = config.FreezeRotation
                ? RigidbodyConstraints.FreezeRotation
                : RigidbodyConstraints.None;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rigidbody.position = worldTransform.Position.ToUnity();
            rigidbody.rotation = worldTransform.Rotation.ToUnity();
            rigidbody.linearVelocity = worldVelocity.Linear.ToUnity();
            rigidbody.isKinematic = config.Kind != GameFramework.World.BodyKind.Dynamic;

            Collider collider;
            if (disc != null)
            {
                var cylinder = root.AddComponent<CapsuleCollider>();
                cylinder.direction = 1;              // Y축 — 원반은 눕힌 캡슐이 아니라 납작한 기둥이다
                cylinder.radius = disc.Radius;
                cylinder.height = disc.Thickness;
                cylinder.center = Vector3.zero;
                collider = cylinder;
            }
            else
            {
                var capsuleCollider = root.AddComponent<CapsuleCollider>();
                capsuleCollider.radius = capsule.Radius;
                capsuleCollider.height = capsule.Height;
                capsuleCollider.center = new Vector3(0, capsule.Height * 0.5f, 0);
                collider = capsuleCollider;
            }
            collider.isTrigger = config.IsTrigger;

            return new UnityPhysicsBody(rigidbody, collider);
        }
```

> **알고 두는 한계 두 가지** (spec §4.5의 열린 항목):
> 1. `root.layer`를 여전히 무조건 `Character`로 둔다. 동전도 Character 레이어에 서게 되는데,
>    판치기에는 sweep을 도는 시뮬이 없어 이번 슬라이스에서는 문제가 되지 않는다.
>    **레이어를 `PhysicsConfig`가 들지는 슬라이스 3에서 정한다.**
> 2. `CapsuleCollider`는 지름보다 낮은 height를 구로 클램프한다. 동전의 실제 접지 거동은
> 슬라이스 3에서 튜닝하며 필요하면 메시/박스 콜라이더로 바꾼다 — 지금은 "떨어져 쌓인다"만 보면 된다.
> 이 한계는 Task 12 검증에서 눈으로 확인한다.

- [ ] **Step 2: 컴파일이 깨지는 것을 확인한다**

```bash
unity command recompile        --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
unity command recompile_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
```
Expected: FAIL — `EntityBinder`가 인자 4개로 부르고 있다. **Task 5에서 같이 고친다**(도메인 리로드가
프로젝트 단위라 깨진 채로 두면 다른 패키지 테스트도 낡은 코드로 돈다).

- [ ] **Step 3: 커밋하지 않고 Task 5로 넘어간다**

호출부까지 고쳐야 컴파일이 통과한다. Task 5 끝에서 함께 커밋한다.

---

## Task 5: 크리에이터·바인더 배선 (거동 불변)

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/{CharacterCreator,FlappyBirdCreator,ItemCreator}.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/{CharacterCreator,FlappyBirdCreator,ItemCreator}.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Entity/EntityBinder.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Entity/EntityBinder.cs`

**Interfaces:**
- Consumes: `PhysicsBodyFactory.Create(GameObject, Entity)` (Task 4), `PhysicsConfig` (Task 1)
- Produces: 없음 (배선만)

- [ ] **Step 1: 크리에이터 6곳에 `PhysicsConfig`를 붙인다**

각 크리에이터에서 `CapsuleShape`을 붙이는 줄 **바로 아래**에 추가한다. **지금 동작을 그대로 적는 것이라
거동 변화가 없다.**

`CharacterCreator.cs`, `FlappyBirdCreator.cs` (클·서 각각 — 총 4곳):

```csharp
            // 지금까지 EntityBinder가 하드코딩하던 값을 그대로 옮긴 것 — 거동 변화 없음.
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));
```

`ItemCreator.cs` (클·서 각각 — 총 2곳):

```csharp
            // 아이템만 트리거다 — 줍기 감지가 접촉으로 이뤄진다.
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: true));
```

- [ ] **Step 2: 바인더가 인자 없이 부르게 한다**

**클라** `EntityBinder.cs` — `worldEntity.Add<...PhysicsBody>(...)` 줄을 교체:

```csharp
            worldEntity.Add<GameFramework.World.PhysicsBody>(PhysicsBodyFactory.Create(root, worldEntity));
```

그 위의 `bool isItem = kind.Kind == EntityType.Item;` 줄은 **클라에선 다른 용도가 없으므로 삭제**한다.

**서버** `EntityBinder.cs` — 같은 줄을 교체하고, `isItem`은 `ItemTouchDetector` 부착에 계속 쓰이므로
**남기되 판정 근거를 몸 설정으로 바꾼다**:

```csharp
            worldEntity.Add<GameFramework.World.PhysicsBody>(PhysicsBodyFactory.Create(root, worldEntity));

            //  트리거인 것만 접촉을 감지할 수 있다 — 감지기가 필요한 것도 그것뿐이다.
            //  줍기 판정은 조작 가능성 때문에 서버만 한다(클라엔 이 컴포넌트가 없다).
            if (worldEntity.Get<GameFramework.World.PhysicsConfig>().IsTrigger)
            {
                ItemTouchDetector itemTouchDetector = root.AddComponent<ItemTouchDetector>();
                // ... 기존 배선 그대로
            }
```

기존 `bool isItem = kind.Kind == EntityType.Item;` 줄은 삭제한다.

- [ ] **Step 3: 양쪽 컴파일 확인**

```bash
unity command recompile        --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
unity command recompile_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
unity command recompile        --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
unity command recompile_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
```
Expected: 양쪽 `failed:false, errors:[]`

- [ ] **Step 4: 기존 EditMode 테스트 회귀 확인**

```bash
unity command run_tests   --mode EditMode --async_tests true --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
unity command test_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
```
Expected: `failed: 0`

- [ ] **Step 5: 인게임으로 거동 불변을 확인한다**

FlapWang과 Flappy Race에 각각 입장해 **이동·아이템 줍기**가 그대로인지 본다. 아이템 줍기가 이 태스크의
유일한 실질 위험(트리거 판정 근거가 바뀌었다)이므로 **반드시 아이템을 먹어 본다.**

- [ ] **Step 6: 커밋 (세 레포)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Runtime/Scripts/Game/PhysicsBodyFactory.cs
git commit -m "refactor(physics): 몸 세우기를 엔티티의 PhysicsConfig에서 읽는다"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git checkout -b feat/physics-config
git status --short   # 로컬 픽스처 5개가 섞이지 않았는지 반드시 확인
git add Assets/Scripts/Entity/CharacterCreator.cs Assets/Scripts/Entity/FlappyBirdCreator.cs Assets/Scripts/Entity/ItemCreator.cs Assets/Scripts/Entity/EntityBinder.cs
git commit -m "refactor(entity): 몸 설정을 크리에이터가 정한다"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feat/physics-config
git status --short   # ConfigureRoomComponent.cs / FlapWangRuleSystem.cs 픽스처 제외 확인
git add Assets/Scripts/Entity/CharacterCreator.cs Assets/Scripts/Entity/FlappyBirdCreator.cs Assets/Scripts/Entity/ItemCreator.cs Assets/Scripts/Entity/EntityBinder.cs
git commit -m "refactor(entity): 몸 설정을 크리에이터가 정한다"
```

---

## Task 6: 다이나믹 몸은 밀어넣지 않고 읽어온다

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/TickSystems/PhysicsSimulationSystem.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/TickSystems/PhysicsSimulationSystem.cs`

**Interfaces:**
- Consumes: `PhysicsBody.IsKinematic`, `PhysicsBody.Get*` (Task 3)
- Produces: 없음

> 지금은 다이나믹 몸이 하나도 없으므로 **이 태스크도 거동 불변**이다. 동전이 생기는 Task 12에서 처음 쓰인다.

- [ ] **Step 1: 두 파일의 `Tick`을 같은 내용으로 바꾼다**

```csharp
        public void Tick(long tick, float deltaTime)
        {
            // World.Transform → rb 팔로우: PhysicsBody 가진 모든 엔티티(내 캐릭=예측, 남·아이템=보간).
            // Simulated는 world.Tick서 이미 밀렸으나 idempotent. per-entity LOPEntityController 대체.
            // 단 다이나믹 몸은 제외한다 — 그건 물리 엔진이 굴리므로 rb가 진실원본이고,
            // 밀어넣으면 한 틱 낡은 World 값으로 속도·회전을 매 틱 덮어써 제대로 구르지 못한다.
            foreach (var entity in entityRegistry.All)
            {
                var body = entity.Get<GameFramework.World.PhysicsBody>();
                if (body != null && body.IsKinematic == false)
                {
                    continue;
                }
                motionBridge.PushMotion(entity);
            }

            physicsSimulator.Simulate(deltaTime);

            // 물리 엔진이 굴린 결과를 World로 되읽는다 — 스냅샷은 World만 보기 때문이다.
            foreach (var entity in entityRegistry.All)
            {
                var body = entity.Get<GameFramework.World.PhysicsBody>();
                if (body == null || body.IsKinematic)
                {
                    continue;
                }
                var transform = entity.Get<GameFramework.World.Transform>();
                var velocity = entity.Get<GameFramework.World.Velocity>();
                if (transform == null || velocity == null)
                {
                    continue;
                }
                transform.Position = body.GetPosition();
                transform.Rotation = body.GetRotation();
                velocity.Linear = body.GetVelocity();
            }
        }
```

- [ ] **Step 2: 양쪽 컴파일 확인**

Task 5 Step 3과 같은 명령. Expected: `failed:false`

- [ ] **Step 3: 거동 불변 확인**

FlapWang에 입장해 이동이 그대로인지 본다(다이나믹 몸이 없으므로 두 루프 다 지금과 같은 일을 한다).

- [ ] **Step 4: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Game/TickSystems/PhysicsSimulationSystem.cs
git commit -m "feat(physics): 다이나믹 몸은 밀어넣지 않고 결과를 읽어온다"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Game/TickSystems/PhysicsSimulationSystem.cs
git commit -m "feat(physics): 다이나믹 몸은 밀어넣지 않고 결과를 읽어온다"
```

- [ ] **Step 5: Phase A를 main에 올린다**

레포 4개(GameFramework · LOP-Shared · Client · Server)를 **각각** 푸시 규약대로 올린다.
**의존 순서상 GameFramework → LOP-Shared → Client/Server 순**으로 푸시한다.

```bash
# 레포마다 반복 — 한 줄씩 결과를 확인한다
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feat/physics-config -m "Merge feat/physics-config: 몸 세우기를 데이터로"
git push origin main
```

---

# Phase B — 판치기 모드 배선 (슬라이스 1)

## Task 7: 마스터데이터

**Files:**
- Modify: `infrastructure/table/Datas/#GameMode.xlsx` (행 추가)
- Modify: `infrastructure/table/Datas/#Map.xlsx` (행 추가)
- Create: `infrastructure/table/Datas/#PanchigiConfig.xlsx`
- Create: `infrastructure/table/Datas/#PanchigiSetup.xlsx`
- Modify: `infrastructure/table/Datas/__tables__.xlsx` (테이블 2개 등록)
- Modify: `LeagueOfPhysical-MasterData-Client/Runtime/Scripts/LOPMasterData.cs` (`TableFiles`)
- Modify: `LeagueOfPhysical-MasterData-Server/Runtime/Scripts/LOPMasterData.cs` (`TableFiles`)

**Interfaces:**
- Produces: `md.Tables.TbGameMode.GetOrDefault(7)` (판치기), `md.Tables.TbPanchigiConfig.Get(1)`,
  `md.Tables.TbPanchigiSetup.GetOrDefault(playerCount)`

- [ ] **Step 1: `#GameMode.xlsx`에 판치기 행을 넣는다**

기존 행 형식(`id | code | name | description | min_players | max_players | scene_path`)에 맞춰:

```
7 | Panchigi | 판치기 | 번갈아 쳐서 동전을 모두 뒤집는 사람이 이긴다 | 2 | 4 | Assets/Scenes/Panchigi.unity
```

- [ ] **Step 2: `#Map.xlsx`에 판치기 맵 행을 넣는다**

기존 행의 컬럼 구성을 그대로 따라 `game_mode_id = 7`인 행을 하나 만든다.
**맵이 없으면 `PlayableGameProvider`가 로비 목록에서 제외**하므로 반드시 필요하다.

- [ ] **Step 3: `#PanchigiConfig.xlsx`를 만든다**

`#FlappyConfig.xlsx`를 복사해 헤더 4줄(`##var` / `##type` / `##group` / `##`)을 아래로 바꾸고 값 행 하나를 넣는다.
**숫자는 임시값이다 — 슬라이스 3에서 튜닝한다.**

```
##var  | id  | strike_power_max | rest_speed_epsilon | rest_angular_epsilon | rest_ticks | aim_timeout_sec | match_turn_limit | drop_out_limit
##type | int | float            | float              | float                | int        | float           | int              | int
##group|
##     | id  | strike_power_max | rest_speed_epsilon | rest_angular_epsilon | rest_ticks | aim_timeout_sec | match_turn_limit | drop_out_limit
       | 1   | 3                | 0.05               | 0.1                  | 10         | 20              | 60               | 3
```

- [ ] **Step 4: `#PanchigiSetup.xlsx`를 만든다**

**id = 참가 인원**이다(Luban 관용 `TbLevelExp` 식 조회 테이블).

```
##var  | id  | coin_count | formation
##type | int | int        | string
##group|
##     | id  | coin_count | formation
       | 2   | 4          | FourInLine
       | 3   | 6          | SixInLine
       | 4   | 8          | FourByTwo
```

- [ ] **Step 5: `__tables__.xlsx`에 두 테이블을 등록한다**

기존 `TbFlappyConfig` 행 형식 그대로:

```
TbPanchigiConfig | PanchigiConfig | 1 | #PanchigiConfig.xlsx | id | map | | PanchigiConfig(판치기 튜닝)
TbPanchigiSetup  | PanchigiSetup  | 1 | #PanchigiSetup.xlsx  | id | map | | PanchigiSetup(인원별 판 구성)
```

`group`은 비운다(클·서 공용 — 클라 조준 UI가 세기 상한을 알아야 한다).

- [ ] **Step 6: 생성한다**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
./gen.sh
```
Expected: 에러 없이 종료. `gen.sh`가 `.meta` 삭제를 스스로 되돌리므로 별도 복원은 필요 없다 —
단 `git -C ../../LeagueOfPhysical-MasterData-Client status --short`로 **`.meta`가 삭제로 남지 않았는지**
반드시 확인한다.

- [ ] **Step 7: 로더 목록을 갱신한다**

`LeagueOfPhysical-MasterData-Client/Runtime/Scripts/LOPMasterData.cs`와 서버 쪽 같은 파일의
`TableFiles` 배열에 추가:

```csharp
            "tbgamemode", "tbmap", "tbqueue", "tbflappyconfig",
            "tbpanchigiconfig", "tbpanchigisetup"
```

> 이걸 빼먹으면 `LoadAsync`가 `KeyNotFoundException`으로 죽는다. 양쪽 패키지의
> `TableFileManifestTests`가 누락과 유령 항목을 둘 다 잡는다.

- [ ] **Step 8: 테스트로 확인한다**

```bash
unity command run_tests   --mode EditMode --async_tests true --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
unity command test_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
```
Expected: `TableFileManifestTests` PASS, `failed: 0`

- [ ] **Step 9: 커밋 (세 레포)**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git checkout -b feat/panchigi-masterdata
git status --short
git add table/Datas
git commit -m "feat(table): 판치기 게임 모드·맵·튜닝 테이블"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
git checkout -b feat/panchigi-masterdata
git status --short   # .meta 삭제가 섞이지 않았는지 확인
git add Runtime Runtime.Generated
git commit -m "feat: 판치기 테이블 생성물 + 로더 목록"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server
git checkout -b feat/panchigi-masterdata
git status --short
git add Runtime Runtime.Generated
git commit -m "feat: 판치기 테이블 생성물 + 로더 목록"
```

---

## Task 8: 판치기 시뮬(빈 월드) + `EntityType.Coin`

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/PanchigiWorld.cs`
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/PanchigiWorldTests.cs`
- Modify: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/EntityType.cs`

**Interfaces:**
- Consumes: `GameFramework.World.WorldBase`, `EntityRegistry`, `WorldEventBuffer`
- Produces: `LOP.PanchigiWorld(EntityRegistry, WorldEventBuffer)`, `LOP.EntityType.Coin = 5`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/PanchigiWorldTests.cs`:

```csharp
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class PanchigiWorldTests
    {
        [Test]
        public void TickDoesNotMoveEntities()
        {
            // 판치기 동전은 PhysX가 굴린다 — 우리 시뮬은 아무것도 움직이지 않는다.
            var registry = new EntityRegistry();
            var entity = new Entity("coin1");
            entity.Add(new GameFramework.World.Transform
            {
                Position = new System.Numerics.Vector3(1f, 2f, 3f),
                Rotation = System.Numerics.Quaternion.Identity,
            });
            entity.Add(new Velocity { Linear = new System.Numerics.Vector3(5f, 0f, 0f) });
            entity.Add(new Simulated());
            registry.Add(entity);

            var world = new PanchigiWorld(registry, new WorldEventBuffer());
            world.Tick(1, 0.02f);

            Assert.AreEqual(1f, entity.Get<GameFramework.World.Transform>().Position.X, 1e-4f);
            Assert.AreEqual(2f, entity.Get<GameFramework.World.Transform>().Position.Y, 1e-4f);
            Assert.AreEqual(3f, entity.Get<GameFramework.World.Transform>().Position.Z, 1e-4f);
        }

        [Test]
        public void ExposesRegistryAndEventBuffer()
        {
            var registry = new EntityRegistry();
            var buffer = new WorldEventBuffer();

            var world = new PanchigiWorld(registry, buffer);

            Assert.AreSame(registry, world.EntityRegistry);
            Assert.AreSame(buffer, world.EventBuffer);
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

```bash
unity command run_tests   --mode EditMode --async_tests true --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
unity command test_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
```
Expected: 컴파일 실패 — `PanchigiWorld`가 없다.

- [ ] **Step 3: 구현한다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/PanchigiWorld.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 판치기 시뮬. **비어 있는 것이 맞다** — 동전을 굴리는 것은 우리 시뮬이 아니라 유니티 물리이고,
    /// 그 결과는 PhysicsSimulationSystem이 World로 되읽는다. 플레이어는 아바타가 없어 움직이지 않는다.
    /// 월드 자리가 필요한 이유는 Runner가 매 틱 IWorld.Tick을 부르기 때문이다.
    /// </summary>
    public class PanchigiWorld : GameFramework.World.WorldBase
    {
        public PanchigiWorld(
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer eventBuffer)
            : base(entityRegistry, eventBuffer)
        {
        }

        protected override void Collection(long tick, float deltaTime) { }

        protected override void Mutation(long tick, float deltaTime) { }

        protected override void Detection(long tick, float deltaTime) { }
    }
}
```

> `WorldBase`의 생성자·훅 시그니처가 위와 다르면 `FlappyWorld.cs`를 열어 그 모양에 맞춘다.

`EntityType.cs`에 값을 추가:

```csharp
        Environment = 4,
        Coin = 5,
```

- [ ] **Step 4: 통과 확인**

Step 2와 같은 명령. Expected: `PanchigiWorldTests` 2건 PASS.

- [ ] **Step 5: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feat/panchigi-mode
git status --short
git add Runtime/Scripts/Game/PanchigiWorld.cs Runtime/Scripts/Game/PanchigiWorld.cs.meta Runtime/Scripts/Game/EntityType.cs Tests/EditMode/PanchigiWorldTests.cs Tests/EditMode/PanchigiWorldTests.cs.meta
git commit -m "feat(panchigi): 빈 시뮬 월드 + 동전 엔티티 종류"
```

---

## Task 9: 클라 동기화 정책 — 전부 보간

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/EntitySync/AllInterpolatedSyncPolicy.cs`
- Create: `LeagueOfPhysical-Client/Assets/Tests/EditMode/EntitySync/AllInterpolatedSyncPolicyTests.cs`

**Interfaces:**
- Consumes: `LOP.IEntitySyncPolicy`, `LOP.EntitySyncMode`
- Produces: `LOP.AllInterpolatedSyncPolicy` (인자 없는 생성자)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

기존 `Assets/Tests/EditMode/EntitySync/EntitySyncPolicyTests.cs` 옆에 새 파일:

```csharp
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class AllInterpolatedSyncPolicyTests
    {
        [Test]
        public void EverythingIsInterpolated()
        {
            // 판치기는 서버가 굴린 물리를 보기만 한다 — 클라가 굴릴 규칙이 없다.
            var policy = new AllInterpolatedSyncPolicy();

            var coin = new Entity("coin1");
            coin.Add(new EntityKind(EntityType.Coin));
            var player = new Entity("p1");
            player.Add(new EntityKind(EntityType.Character));
            player.Add(new Ownership("user1"));

            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(coin));
            Assert.AreEqual(EntitySyncMode.Interpolated, policy.For(player));
        }
    }
}
```

- [ ] **Step 2: 실패를 확인한다**

Task 8 Step 2와 같은 명령. Expected: 컴파일 실패 — `AllInterpolatedSyncPolicy`가 없다.

- [ ] **Step 3: 구현한다**

`LeagueOfPhysical-Client/Assets/Scripts/EntitySync/AllInterpolatedSyncPolicy.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 아무것도 예측하지 않는다(판치기). 동전은 서버가 PhysX로 굴리고 클라는 스냅을 보간해 볼 뿐이라
    /// 클라가 굴릴 규칙이 없고, 플레이어는 아바타가 없어 움직이지 않는다.
    /// </summary>
    public class AllInterpolatedSyncPolicy : IEntitySyncPolicy
    {
        public EntitySyncMode For(GameFramework.World.Entity entity)
        {
            return EntitySyncMode.Interpolated;
        }
    }
}
```

- [ ] **Step 4: 통과 확인**

Expected: `AllInterpolatedSyncPolicyTests` PASS, 기존 `EntitySyncPolicyTests` 회귀 없음.

- [ ] **Step 5: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git checkout -b feat/panchigi-mode
git status --short
git add Assets/Scripts/EntitySync/AllInterpolatedSyncPolicy.cs Assets/Scripts/EntitySync/AllInterpolatedSyncPolicy.cs.meta Assets/Tests/EditMode/EntitySync/AllInterpolatedSyncPolicyTests.cs Assets/Tests/EditMode/EntitySync/AllInterpolatedSyncPolicyTests.cs.meta
git commit -m "feat(panchigi): 예측 없는 동기화 정책"
```

---

## Task 10: 플레이어 크리에이터 (몸 없음)

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Entity/PanchigiPlayerCreator.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Entity/PanchigiPlayerCreator.cs`

**Interfaces:**
- Consumes: `ICharacterCreator`, `CharacterCreationData`, `EntityRegistry`
- Produces: `LOP.PanchigiPlayerCreator(GameFramework.World.EntityRegistry)` — `ICharacterCreator` 구현

> **`Transform`+`Velocity`는 필수다.** `EntitySnapshotBroadcastSystem`이 부르는
> `GetVelocity()`/`GetRotation()`에 null 가드가 없다.
> **`PhysicsConfig`+shape도 필수다.** `EntityBinder`가 모든 엔티티에 몸을 만든다.

- [ ] **Step 1: 서버 크리에이터를 쓴다**

`LeagueOfPhysical-Server/Assets/Scripts/Entity/PanchigiPlayerCreator.cs`:

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 판치기 플레이어(서버). 아바타가 없어 돌아다니지 않지만, 누구 차례인지·누가 쳤는지를 잇는
    /// 신원이 필요해 엔티티는 만든다. 몸은 자리만 지키는 최소 크기다.
    /// </summary>
    public class PanchigiPlayerCreator : ICharacterCreator
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public PanchigiPlayerCreator(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        public void Create(CharacterCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            //  스냅샷 빌더가 모든 엔티티에서 속도를 읽는다(널 가드 없음) — 안 움직여도 필요하다.
            worldEntity.Add(new GameFramework.World.Velocity());
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new GameFramework.World.CapsuleShape(0.3f, 1.6f));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            if (string.IsNullOrEmpty(creationData.userId) == false)
            {
                worldEntity.Add(new GameFramework.World.Ownership(creationData.userId));
            }

            //  Simulated을 붙이지 않는다 — 우리 시뮬이 굴릴 것이 없다(아바타가 안 움직인다).
            entityRegistry.Add(worldEntity);

            Debug.Log($"[World] Registered panchigi player {worldEntity.Id}");
        }
    }
}
```

- [ ] **Step 2: 클라 크리에이터를 쓴다**

`LeagueOfPhysical-Client/Assets/Scripts/Entity/PanchigiPlayerCreator.cs` — **위와 같은 내용**을 쓰되,
로그 문구만 `panchigi player (client)`로 둔다. (클라 `FlappyBirdCreator`와 서버 것이 사실상 같은 것과
같은 이유 — 클라는 와이어로 받은 생성 데이터로 같은 엔티티를 세운다.)

- [ ] **Step 3: 양쪽 컴파일 확인**

```bash
unity command recompile        --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
unity command recompile_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
unity command recompile        --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
unity command recompile_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
```
Expected: 양쪽 `failed:false`

- [ ] **Step 4: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Entity/PanchigiPlayerCreator.cs Assets/Scripts/Entity/PanchigiPlayerCreator.cs.meta
git commit -m "feat(panchigi): 몸 없는 플레이어 엔티티"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Entity/PanchigiPlayerCreator.cs Assets/Scripts/Entity/PanchigiPlayerCreator.cs.meta
git commit -m "feat(panchigi): 몸 없는 플레이어 엔티티"
```

---

## Task 11: 씬 + 스코프 — 입장이 된다 (슬라이스 1 완료)

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiLifetimeScope.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scenes/Panchigi.unity`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiLifetimeScope.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiRuleSystem.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scenes/Panchigi.unity`

**Interfaces:**
- Consumes: `PanchigiWorld` (Task 8), `AllInterpolatedSyncPolicy` (Task 9),
  `PanchigiPlayerCreator` (Task 10), `md.Tables.TbPanchigiConfig` (Task 7)
- Produces: `LOP.PanchigiRuleSystem` — `IGameRuleSystem` 구현 (이 계획에선 `ResolveOutcome`이
  전원 동일 등수를 돌려주는 골격)

- [ ] **Step 1: 서버 스코프를 쓴다**

`LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiLifetimeScope.cs`:

```csharp
using VContainer;

namespace LOP
{
    /// <summary>판치기 덩어리(서버) — 빈 월드·아바타 없는 플레이어·턴 룰.</summary>
    public class PanchigiLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<GameFramework.World.IWorld>(c => new PanchigiWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>()), Lifetime.Singleton);
            builder.Register<ICharacterCreator, PanchigiPlayerCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, PanchigiRuleSystem>(Lifetime.Singleton);
        }
    }
}
```

- [ ] **Step 2: 룰 시스템 골격을 쓴다**

`LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiRuleSystem.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// 판치기 룰(서버). 이 슬라이스에서는 판을 세우는 것까지만 하고, 턴 상태 기계와 승패 판정은
    /// 다음 슬라이스에서 붙인다 — 지금은 전원 동일 등수를 돌려준다.
    /// </summary>
    public class PanchigiRuleSystem : IGameRuleSystem
    {
        private readonly IRoomDataStore roomDataStore;

        public PanchigiRuleSystem(IRoomDataStore roomDataStore)
        {
            this.roomDataStore = roomDataStore;
        }

        public void Initialize() { }

        public void Deinitialize() { }

        public MatchOutcome ResolveOutcome()
        {
            //  판이 끝나는 조건은 다음 슬라이스에서 붙는다 — 그때까지는 결과 보고 경로가 끊기지
            //  않도록 전원 동일 등수로 둔다(아직 승자가 정해지지 않는다).
            var outcome = new MatchOutcome();
            foreach (var userId in roomDataStore.match.playerList)
            {
                outcome.placements.Add(new MatchPlacement { userId = userId, placement = 1 });
            }
            return outcome;
        }
    }
}
```

> `MatchOutcome`/`MatchPlacement`의 모양은 `FlappyRaceRuleSystem.ResolveOutcome`
> (`LeagueOfPhysical-Server/Assets/Scripts/Game/FlappyRaceRuleSystem.cs:63`)에서 그대로 가져온 것이다.
> `roomDataStore.match.playerList`는 userId `string[]`이다.

- [ ] **Step 3: 클라 스코프를 쓴다**

`LeagueOfPhysical-Client/Assets/Scripts/Game/PanchigiLifetimeScope.cs`:

```csharp
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>판치기 덩어리(클라) — 빈 월드, 예측 없음, 판을 비추는 카메라.</summary>
    public class PanchigiLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            builder.Register<GameFramework.World.IWorld>(c => new PanchigiWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>()), Lifetime.Singleton);
            builder.Register<ICharacterCreator, PanchigiPlayerCreator>(Lifetime.Singleton);
            builder.Register<IEntitySyncPolicy, AllInterpolatedSyncPolicy>(Lifetime.Singleton);
            builder.Register<IServerCorrectionHandler, NoServerCorrection>(Lifetime.Singleton);
        }
    }
}
```

> `FlappyRaceLifetimeScope.cs`를 열어 **어떤 등록이 필수인지** 대조한다. 위에 없는데 그쪽에 있는
> 등록(예: `IServerCorrectionHandler`)이 더 있으면 같이 넣는다.

- [ ] **Step 4: 씬을 만든다 (클·서)**

`FlappyRace.unity`를 복사해 `Panchigi.unity`로 저장하고:
- 스코프 컴포넌트를 `FlappyRaceLifetimeScope` → `PanchigiLifetimeScope`로 교체
- `runner` 참조가 살아 있는지 확인 (`GameLifetimeScope.runner`는 `[SerializeField]`)
- Flappy 전용 오브젝트(코스 생성기·HUD 등)를 지운다
- 판 역할을 할 **평평한 바닥**(Plane 또는 Cube)을 하나 두고 콜라이더를 켠다
- 클라 씬은 카메라를 판 위에서 내려다보게 두고 `cameraController` 참조를 연결한다

두 씬 모두 **Build Settings의 씬 목록에 추가**한다(`File > Build Settings`).

- [ ] **Step 5: 컴파일 + 테스트**

```bash
unity command recompile        --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
unity command recompile_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
unity command recompile        --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
unity command recompile_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
unity command run_tests   --mode EditMode --async_tests true --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
unity command test_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
```
Expected: 컴파일 `failed:false`, 테스트 `failed: 0`

- [ ] **Step 6: 슬라이스 1 검증 — 로비에 뜨고 입장된다**

두 클라(메인 에디터 + MPPM 클론)를 띄워 매칭한다(`[[driving-both-clients-via-unity-cli]]`).

확인할 것:
1. 로비 게임 목록에 **"판치기"가 보인다** (`PlayableGameProvider`가 자동으로 올린다)
2. 고르고 매칭하면 **`Panchigi.unity`가 로드되고 입장된다**
3. 서버·클라 콘솔에 **에러 0** (`unity command console --params '{"tail":60}'`)
4. 서버 로그에 `[World] Registered panchigi player ...`가 인원수만큼 찍힌다

- [ ] **Step 7: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Game/PanchigiLifetimeScope.cs Assets/Scripts/Game/PanchigiLifetimeScope.cs.meta Assets/Scripts/Game/PanchigiRuleSystem.cs Assets/Scripts/Game/PanchigiRuleSystem.cs.meta Assets/Scenes/Panchigi.unity Assets/Scenes/Panchigi.unity.meta ProjectSettings/EditorBuildSettings.asset
git commit -m "feat(panchigi): 씬과 스코프 — 입장이 된다"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Game/PanchigiLifetimeScope.cs Assets/Scripts/Game/PanchigiLifetimeScope.cs.meta Assets/Scenes/Panchigi.unity Assets/Scenes/Panchigi.unity.meta ProjectSettings/EditorBuildSettings.asset
git commit -m "feat(panchigi): 씬과 스코프 — 입장이 된다"
```

---

# Phase C — 동전 (슬라이스 2)

## Task 12: 동전이 클라로 가는 길 (와이어)

동전은 캐릭터도 아이템도 아니다. 지금 `EntityCreationData`는 그 둘만 담는 `oneof`라
**클라가 동전을 받을 방법이 없다.** 이 태스크가 그 길을 낸다.

**Files:**
- Create: `LeagueOfPhysical-Shared/Protos/CoinCreationData.proto`
- Modify: `LeagueOfPhysical-Shared/Protos/EntityCreationData.proto`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Entity/CoinCreationData.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Entity/CoinCreationData.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/EntityCreationDataFactory/CoinCreationDataCreator.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs:208`
- Modify: `LeagueOfPhysical-{Client,Server}/Assets/Scripts/Entity/EntitySpawner.cs`

**Interfaces:**
- Consumes: `EntityType.Coin` (Task 8), `IEntityCreationDataCreator`
- Produces: proto `CoinCreationData`, C# `LOP.CoinCreationData` 구조체(`entityId`·`position`·
  `rotation`·`velocity`·`visualId`), `EntitySpawner.Spawn(CoinCreationData)`

- [ ] **Step 1: proto를 더한다**

`LeagueOfPhysical-Shared/Protos/CoinCreationData.proto` — `ItemCreationData.proto`를 열어 그 모양을
그대로 따른다(`BaseEntityCreationData`를 어떻게 품는지 포함):

```proto
syntax = "proto3";

import "BaseEntityCreationData.proto";

message CoinCreationData
{
	BaseEntityCreationData base = 1;
	string visual_id = 2;
}
```

`EntityCreationData.proto`의 `oneof`에 한 줄 추가한다. **필드 번호 3은 새 번호여야 한다 — 기존
번호를 재사용하면 와이어가 깨진다.**

```proto
import "CoinCreationData.proto";
...
        CoinCreationData coin_creation_data = 3;
```

- [ ] **Step 2: 생성한다 — MessageId가 밀리지 않는지 확인한다**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
./Scripts/generate_protos.sh
./Scripts/generate_imessage.sh
git diff Runtime.Generated/Scripts/MessageIds.cs
```

> 부모 스크립트가 `MessageIds.cs`를 지워 ID가 통째로 밀리는 사고가 있었다
> (`[[proto-message-id-regen-gotcha]]`). `CoinCreationData`는 최상위 와이어 메시지가 아니라
> `oneof` 안의 payload라 **MessageId가 생기지 않는 것이 정상**이다.
> 위 `git diff`가 **비어 있어야 한다.** 값이 하나라도 바뀌었으면 멈추고 서브스크립트를
> 개별 실행하는 쪽으로 되돌린다.

- [ ] **Step 3: 사이드별 C# 생성 데이터 구조체를 쓴다**

`ItemCreationData.cs`(클·서 각각)를 열어 그 모양을 그대로 따라 `CoinCreationData.cs`를 만든다.
`IEntityCreationData`를 구현하고 `entityId`·`position`·`rotation`·`velocity`·`visualId`를 갖는다.

- [ ] **Step 4: `EntitySpawner`에 오버로드를 더한다 (클·서)**

`Spawn(ItemCreationData)` 바로 아래에:

```csharp
        public void Spawn(CoinCreationData creationData)
        {
            coinCreator.Create(creationData);
            entityCreatedPublisher.Publish(new EntityCreated(creationData.entityId));
        }
```

`coinCreator`를 생성자 주입 필드로 더한다(기존 `itemCreator`와 같은 방식).

> `EntityCreated` 발행을 빼먹으면 **클라에 뷰가 안 생긴다** — `EntityBinder`가 이 신호로 움직인다.

- [ ] **Step 5: 서버 생성 데이터 크리에이터를 쓴다**

`LeagueOfPhysical-Server/Assets/Scripts/EntityCreationDataFactory/`의 아이템용 구현을 열어 그대로
따라 `CoinCreationDataCreator`를 만든다. `EntityType` 프로퍼티는 `EntityType.Coin`을 돌려주고,
World 엔티티에서 `Transform`·`Velocity`·`Appearance`를 읽어 proto를 채운다.

DI 등록은 기존 크리에이터들이 등록되는 자리를 찾아 같은 곳에 추가한다:

```bash
grep -rn "IEntityCreationDataCreator" --include=*.cs Assets/Scripts
```

- [ ] **Step 6: 클라 수신부에 분기를 더한다**

`GameEntityMessageHandler.cs:208`의 `switch (entitySpawnToC.EntityCreationData.CreationDataCase)`에
케이스를 추가한다:

```csharp
                case EntityCreationData.CreationDataOneofCase.CoinCreationData:
                    entitySpawner.Spawn(new CoinCreationData
                    {
                        entityId = entitySpawnToC.EntityCreationData.CoinCreationData.Base.EntityId,
                        position = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.CoinCreationData.Base.Position),
                        rotation = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.CoinCreationData.Base.Rotation),
                        velocity = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.CoinCreationData.Base.Velocity),
                        visualId = entitySpawnToC.EntityCreationData.CoinCreationData.VisualId,
                    });
                    break;
```

> 실제 필드 접근 경로(`.Base.`가 있는지 등)는 같은 `switch`의 `ItemCreationData` 분기가 어떻게
> 쓰는지 보고 맞춘다.

- [ ] **Step 7: 양쪽 컴파일 확인**

```bash
unity command recompile        --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
unity command recompile_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
unity command recompile        --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
unity command recompile_status --project-path "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
```
Expected: 양쪽 `failed:false`

> `EntitySpawner`가 `PanchigiCoinCreator`(Task 13)를 참조하므로 그 파일이 먼저 있어야 컴파일된다.
> **두 태스크는 한 브랜치에서 이어서 하고 커밋만 나눈다** — Task 13 Step 1·2를 먼저 만들어도 된다.

- [ ] **Step 8: 커밋 (세 레포)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git status --short
git add Protos Runtime.Generated
git commit -m "feat(wire): 동전 생성 데이터"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Entity/CoinCreationData.cs Assets/Scripts/Entity/CoinCreationData.cs.meta Assets/Scripts/Entity/EntitySpawner.cs Assets/Scripts/EntityCreationDataFactory
git commit -m "feat(wire): 동전 생성 데이터 송신"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Entity/CoinCreationData.cs Assets/Scripts/Entity/CoinCreationData.cs.meta Assets/Scripts/Entity/EntitySpawner.cs Assets/Scripts/Game/MessageHandler/GameEntityMessageHandler.cs
git commit -m "feat(wire): 동전 생성 데이터 수신"
```

---

## Task 13: 동전을 세우고 판을 차린다 — 동전이 보인다 (슬라이스 2 완료)

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Entity/PanchigiCoinCreator.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Entity/PanchigiCoinCreator.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/PanchigiRuleSystem.cs`
- Modify: `LeagueOfPhysical-{Client,Server}/Assets/Scripts/Game/PanchigiLifetimeScope.cs`

**Interfaces:**
- Consumes: `DiscShape`·`PhysicsConfig` (Task 1·2), `EntityType.Coin` (Task 8),
  `CoinCreationData`·`EntitySpawner.Spawn(CoinCreationData)` (Task 12),
  `md.Tables.TbPanchigiSetup` (Task 7)
- Produces: `LOP.PanchigiCoinCreator.Create(CoinCreationData creationData)`

- [ ] **Step 1: 서버 동전 크리에이터를 쓴다**

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 판치기 동전(서버). 다이나믹 몸이라 유니티 물리가 굴리고, 그 결과를
    /// PhysicsSimulationSystem이 World로 되읽어 스냅샷에 실린다.
    /// Simulated을 붙이지 않는다 — 우리 시뮬이 굴리는 것이 아니다.
    /// </summary>
    public class PanchigiCoinCreator
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public PanchigiCoinCreator(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        public void Create(CoinCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Coin));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new GameFramework.World.DiscShape(0.15f, 0.04f));
            //  회전을 풀어야 뒤집힌다. 다이나믹이라 PhysX가 진실원본이 된다.
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Dynamic, freezeRotation: false, isTrigger: false));

            entityRegistry.Add(worldEntity);
            Debug.Log($"[World] Registered panchigi coin {worldEntity.Id}");
        }
    }
}
```

- [ ] **Step 2: 클라 동전 크리에이터를 쓴다**

**같은 내용**을 클라에 쓰되 `PhysicsConfig` 한 줄만 다르다 — 클라 동전은 **kinematic**이다
(Photon Fusion 프록시 기본값과 같다. 위치는 보간기가 준다):

```csharp
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: false, isTrigger: false));
```

- [ ] **Step 3: 룰 시스템이 판을 차린다**

`PanchigiRuleSystem`의 생성자에 `EntitySpawner entitySpawner`와
`LOP.MasterData.LOPMasterData masterData`를 더하고, `Initialize()`를 아래로 바꾼다:

```csharp
        private const string CoinVisualId = "Assets/Art/Panchigi/Coin.prefab";

        public void Initialize()
        {
            var playerList = roomDataStore.match.playerList;
            var setup = masterData.Tables.TbPanchigiSetup.GetOrDefault(playerList.Length);
            if (setup == null)
            {
                //  조용히 넘기면 판이 빈 채로 시작하고 왜인지 런타임에 추적해야 한다.
                throw new System.InvalidOperationException(
                    $"TbPanchigiSetup에 {playerList.Length}인 구성이 없다 — 테이블을 채워야 한다.");
            }

            //  플레이어는 아바타가 없지만 신원 엔티티는 필요하다(누구 차례인지·누가 쳤는지).
            for (int i = 0; i < playerList.Length; i++)
            {
                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entitySpawner.GenerateEntityId(),
                    visualId = "",
                    characterCode = "",
                    position = Vector3.zero,
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                });
            }

            //  대형(formation) 해석은 슬라이스 3에서 갈라진다. 지금은 일렬로 떨어뜨려
            //  "쌓이는 것"과 "두 클라가 같은 것을 보는지"만 확인한다.
            for (int i = 0; i < setup.CoinCount; i++)
            {
                entitySpawner.Spawn(new CoinCreationData
                {
                    entityId = entitySpawner.GenerateEntityId(),
                    visualId = CoinVisualId,
                    position = new Vector3(0f, 1.5f, i * 0.5f),
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                });
            }
        }
```

> `CoinVisualId` 경로에 실제 프리팹이 있어야 한다. 없으면 `LeagueOfPhysical-Art` 서브모듈에 원본
> 판치기의 `Gold_Coin` 프리팹을 옮겨 놓거나, 임시로 기존 아이템 프리팹 경로를 쓴다 —
> **이 슬라이스의 목적은 모양이 아니라 동기화 확인**이다.

- [ ] **Step 4: 스코프에 크리에이터를 등록한다 (클·서)**

두 `PanchigiLifetimeScope`의 `ConfigureGame`에 추가:

```csharp
            builder.Register<PanchigiCoinCreator>(Lifetime.Singleton);
```

- [ ] **Step 5: 컴파일 확인**

Task 12 Step 7과 같은 명령. Expected: 양쪽 `failed:false`

- [ ] **Step 6: 슬라이스 2 검증 — 두 클라가 같은 것을 본다**

두 클라(메인 에디터 + MPPM 클론)를 띄워 판치기에 입장한다
(`[[driving-both-clients-via-unity-cli]]`).

확인할 것:

1. **동전이 떨어져 판 위에 쌓인다** (서버 로그에 `Registered panchigi coin`이 개수만큼)
2. **두 클라의 화면이 같다** — 같은 자리, 같은 개수, 같은 자세
3. 서버·클라 콘솔 **에러 0** (`unity command console --params '{"tail":60}'`)
4. **동전이 판을 뚫고 내려가지 않는다.** 뚫으면 `CapsuleCollider`의 지름 클램프 문제이므로
   Task 4의 "알고 두는 한계"로 돌아가 박스/메시 콜라이더를 검토한다
5. `unity command get_performance_stats --project-path ...`로 `cpuFrameTime`을 본다.
   동전 8개로 프레임이 무너지면 콜라이더 형태·솔버 설정을 다음 계획에서 다룬다

> **이 검증에서 나온 실측이 다음 계획의 입력이다.** 특히 4·5번 결과를 기록해 둔다.

- [ ] **Step 7: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git status --short
git add Assets/Scripts/Entity/PanchigiCoinCreator.cs Assets/Scripts/Entity/PanchigiCoinCreator.cs.meta Assets/Scripts/Game/PanchigiRuleSystem.cs Assets/Scripts/Game/PanchigiLifetimeScope.cs
git commit -m "feat(panchigi): 동전을 다이나믹 몸으로 세우고 판을 차린다"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Entity/PanchigiCoinCreator.cs Assets/Scripts/Entity/PanchigiCoinCreator.cs.meta Assets/Scripts/Game/PanchigiLifetimeScope.cs
git commit -m "feat(panchigi): 동전은 보간해서 본다"
```

- [ ] **Step 8: Phase B·C를 main에 올린다**

레포 6개를 **의존 순서**로 각각 올린다:
`infrastructure` → `MasterData-Client` → `MasterData-Server` → `LOP-Shared` → `Client` → `Server`

```bash
# 레포마다 반복 — 한 줄씩 결과를 확인한다
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff <feature-branch> -m "Merge <feature-branch>: 판치기 슬라이스 1~2"
git push origin main
```

---

## 다음 계획으로 넘기는 것

| 슬라이스 | 무엇 | 왜 지금이 아닌가 |
|---|---|---|
| **3** | 타격 입력 + 힘 커널(원본 물리 재작업) | 동전이 실제로 어떻게 구르는지 보고 나서 커널을 정해야 한다 |
| **4** | 턴 상태 기계 + 면·종료 판정 + 낙·탈락 | 3이 있어야 턴이 끝나는 사건이 생긴다 |
| **5** | HUD(누구 차례·남은 시간) + 결과 화면 연동 | 4의 상태가 있어야 표시할 것이 생긴다 |

슬라이스 2 검증(Task 12 Step 6)에서 나온 실측 — 콜라이더 형태·프레임·접지 거동 — 이 다음 계획의 입력이다.
