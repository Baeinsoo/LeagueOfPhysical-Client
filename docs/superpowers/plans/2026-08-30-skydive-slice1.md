# Skydive 슬라이스 1 — 모드가 존재한다

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 로비에서 `Skydive`를 고르면 방에 들어가고, 참가자가 하늘에서 **중력으로 떨어진다.**

**Architecture:** 새 게임 모드는 기존 배관에 칸 하나를 채우는 일이다 — `TbGameMode` 행이 게임 씬을
가리키고, 그 씬의 `SkydiveLifetimeScope`가 자기 월드·생성기·룰을 등록한다. 시뮬(`SkydiveWorld`)은
LOP-Shared에 두어 클·서가 **같은 구체 클래스**를 컴파일한다.

**Tech Stack:** Unity 6 / VContainer / Mirror / Luban(MasterData) / NUnit(EditMode)

**Spec:** `docs/superpowers/specs/2026-08-30-skydive-game-mode-design.md`

## Global Constraints

- **게임 모드 내부명은 `Skydive`.** 파일·클래스·`TbGameMode.Code` 전부 이 접두어.
- **자세 값 이름에 `Skydive`를 쓰지 않는다** — 슬라이스 2에서 `Posture.Dive`/`Spread`/`Glide`.
- **시뮬 코드는 LOP-Shared에 구체 클래스로 둔다.** 인터페이스 seam 금지(결정론).
- **World 타입은 항상 풀 네임스페이스로 한정한다** — `GameFramework.World.Transform` 등.
  `using GameFramework.World;`를 추가하지 않는다(`UnityEngine.Component`와 충돌).
- **`git add -A` / `git commit -a` 금지.** 워킹트리에 의도적으로 커밋하지 않는 로컬 픽스처가
  상시 있다(`Assets/Art` 서브모듈 포인터, 폰트 에셋, `PackageManagerSettings.asset`).
  **바꾼 파일만 경로로 지정**하고 커밋 전에 `git status --short`로 확인한다.
- **`.cs`를 새로 만들면 Unity가 만든 `.meta`를 반드시 함께 커밋한다.** `.meta`를 손으로 만들지 않는다.
- **main에 직접 커밋 금지.** 이 계획의 모든 커밋은 피처 브랜치에서.
- 브랜치: 각 레포에서 `feature/skydive-slice1`

### 컴파일·테스트 게이트 (매 태스크 공통)

에디터가 떠 있으면 `unity` CLI가 붙는다. **클·서 에디터가 동시에 붙어 있으므로 `--project-path`를
매번 명시**한다.

```bash
CLIENT=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
SERVER=C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server

unity status                                                  # 붙어 있는 에디터 확인
unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
```

> ⚠️ **`recompile_status`의 `failed:false`만 보면 안 된다.** `status`가 `up_to_date`면 **재컴파일을
> 아예 안 한 것**이다. 결정타는 `get_console_logs`의 CS 에러를 **시각과 대조**하는 것.
>
> ⚠️ CLI 응답은 30초 상한이고 `--timeout`이 안 먹는다. `recompile`/`run_tests`가 타임아웃으로
> 죽어도 에디터에선 계속 돈다 — `*_status`로 폴링한다.

패키지 EditMode 테스트:

```bash
unity command run_tests  --mode EditMode --async_tests true --project-path "$CLIENT"
unity command test_status --project-path "$CLIENT"
```

---

## File Structure

| 파일 | 책임 |
|---|---|
| **LOP-Shared** | |
| `Runtime/Scripts/Game/SkydiveMoveSystem.cs` | 중력 → 속도 → 위치. 무상태 시스템(슬라이스 2에서 자세·항력이 여기 붙는다) |
| `Runtime/Scripts/Game/SkydiveWorld.cs` | 시뮬 코어. `Simulated` 캐릭터를 모아 매 틱 `SkydiveMoveSystem.Tick` |
| `Tests/EditMode/SkydiveMoveSystemTests.cs` | 중력·낙하 상한·바닥 정지 |
| **LOP-Client** | |
| `Assets/Scripts/Game/SkydiveLifetimeScope.cs` | 클라 덩어리 등록 — 월드·생성기·동기화 정책·카메라 |
| `Assets/Scripts/Entity/SkydivePlayerCreator.cs` | 플레이어 몸(클라) |
| `Assets/Scenes/Skydive.unity` | 게임 씬 |
| **LOP-Server** | |
| `Assets/Scripts/Game/SkydiveLifetimeScope.cs` | 서버 덩어리 등록 |
| `Assets/Scripts/Game/SkydiveRuleSystem.cs` | 스폰·종료·등수 |
| `Assets/Scripts/Entity/SkydivePlayerCreator.cs` | 플레이어 몸(서버) |
| `Assets/Scenes/Skydive.unity` | 게임 씬 |
| **LOP-Art** | |
| `Assets/Art/Scenes/SkydiveMap.unity` | 맵 — 스폰 마커 + 바닥 |
| **infrastructure** | |
| `table/Datas/#GameMode.xlsx` `#Map.xlsx` `#Queue.xlsx` | 데이터 행 |

**슬라이스 1이 의도적으로 안 하는 것:** 자세·스태미나·레이저·경계·결승선·맵 충돌. 캐릭터는 중력으로
떨어져 `GroundY`에서 멈춘다. 이 정지는 **슬라이스 3이 진짜 지형 충돌로 대체할 임시 장치**다.

---

## Task 1: 공유 시뮬 — 떨어지는 월드

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveMoveSystem.cs`
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWorld.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/SkydiveMoveSystemTests.cs`

**Interfaces:**
- Consumes: `GameFramework.World.{Entity, EntityRegistry, WorldEventBuffer, WorldBase, Transform, Velocity, Simulated}`, `LOP.{EntityKind, EntityType}`, 확장 메서드 `ToNumerics()`/`ToUnity()` (namespace `GameFramework`)
- Produces:
  - `LOP.SkydiveMoveSystem` — `void Tick(GameFramework.World.Entity entity, float deltaTime)`, `const float Gravity = 20f`, `const float MaxFallSpeed = 40f`, `const float GroundY = 0f`
  - `LOP.SkydiveWorld : GameFramework.World.WorldBase` — ctor `(EntityRegistry, WorldEventBuffer, SkydiveMoveSystem)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`LeagueOfPhysical-Shared/Tests/EditMode/SkydiveMoveSystemTests.cs`:

```csharp
using GameFramework;
using GameFramework.World;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class SkydiveMoveSystemTests
    {
        const float Tolerance = 1e-4f;

        static Entity Body(Vector3 position, Vector3 velocity)
        {
            var entity = new Entity("diver-1");
            entity.Add(new GameFramework.World.Transform { Position = position.ToNumerics() });
            entity.Add(new Velocity { Linear = velocity.ToNumerics() });
            return entity;
        }

        static Vector3 PositionOf(Entity entity) => entity.Get<GameFramework.World.Transform>().Position.ToUnity();
        static Vector3 VelocityOf(Entity entity) => entity.Get<Velocity>().Linear.ToUnity();

        [Test]
        public void 중력이_세로_속도를_깎는다()
        {
            var body = Body(new Vector3(0f, 100f, 0f), Vector3.zero);

            new SkydiveMoveSystem().Tick(body, 0.1f);

            Assert.AreEqual(-2f, VelocityOf(body).y, Tolerance);   // 20 × 0.1
        }

        [Test]
        public void 낙하_속도가_상한을_넘지_않는다()
        {
            var body = Body(new Vector3(0f, 100f, 0f), new Vector3(0f, -SkydiveMoveSystem.MaxFallSpeed, 0f));

            new SkydiveMoveSystem().Tick(body, 1f);

            Assert.AreEqual(-SkydiveMoveSystem.MaxFallSpeed, VelocityOf(body).y, Tolerance);
        }

        [Test]
        public void 속도만큼_아래로_내려간다()
        {
            var body = Body(new Vector3(0f, 100f, 0f), new Vector3(0f, -10f, 0f));

            new SkydiveMoveSystem().Tick(body, 0.1f);

            // 속도 갱신이 먼저다: -10 - 20×0.1 = -12 → 100 + (-12 × 0.1) = 98.8
            Assert.AreEqual(98.8f, PositionOf(body).y, Tolerance);
        }

        [Test]
        public void 바닥에_닿으면_멈춘다()
        {
            var body = Body(new Vector3(0f, SkydiveMoveSystem.GroundY + 0.5f, 0f), new Vector3(0f, -30f, 0f));

            new SkydiveMoveSystem().Tick(body, 0.1f);

            Assert.AreEqual(SkydiveMoveSystem.GroundY, PositionOf(body).y, Tolerance);
            Assert.AreEqual(0f, VelocityOf(body).y, Tolerance);
        }

        [Test]
        public void 수평_속도는_건드리지_않는다()
        {
            var body = Body(new Vector3(0f, 100f, 0f), new Vector3(3f, 0f, -4f));

            new SkydiveMoveSystem().Tick(body, 0.1f);

            Assert.AreEqual(3f, VelocityOf(body).x, Tolerance);
            Assert.AreEqual(-4f, VelocityOf(body).z, Tolerance);
            Assert.AreEqual(0.3f, PositionOf(body).x, Tolerance);
            Assert.AreEqual(-0.4f, PositionOf(body).z, Tolerance);
        }
    }
}
```

- [ ] **Step 2: 테스트가 실패하는지 확인한다**

```bash
unity command run_tests  --mode EditMode --async_tests true --project-path "$CLIENT"
unity command test_status --project-path "$CLIENT"
```

기대: 컴파일 에러 — `SkydiveMoveSystem`이 없다.

- [ ] **Step 3: `SkydiveMoveSystem`을 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveMoveSystem.cs`:

```csharp
namespace LOP
{
    /// <summary>
    /// Skydive의 이동. 슬라이스 1에서는 중력으로 떨어지는 것뿐이다 —
    /// 자세(항력·수평 가속)는 슬라이스 2, 지형 충돌은 슬라이스 3이 얹는다.
    /// </summary>
    public class SkydiveMoveSystem
    {
        // 슬라이스 2에서 TbSkydiveConfig로 옮긴다. 지금 필요한 것은 "떨어지는 게 보인다"뿐이라
        // 값을 데이터로 뺄 이유가 아직 없다.
        public const float Gravity = 20f;
        public const float MaxFallSpeed = 40f;

        // 진짜 지면은 슬라이스 3의 맵 충돌이 정한다. 그때까지 무한 추락을 막는 임시 바닥이다.
        public const float GroundY = 0f;

        public void Tick(GameFramework.World.Entity entity, float deltaTime)
        {
            var velocity = entity.Get<GameFramework.World.Velocity>();
            var transform = entity.Get<GameFramework.World.Transform>();
            if (velocity == null || transform == null)
            {
                return;
            }

            var linear = velocity.Linear;
            linear.Y -= Gravity * deltaTime;
            if (linear.Y < -MaxFallSpeed)
            {
                linear.Y = -MaxFallSpeed;
            }

            var position = transform.Position + linear * deltaTime;
            if (position.Y <= GroundY)
            {
                position.Y = GroundY;
                linear.Y = 0f;
            }

            velocity.Linear = linear;
            transform.Position = position;
        }
    }
}
```

- [ ] **Step 4: 테스트가 통과하는지 확인한다**

```bash
unity command run_tests  --mode EditMode --async_tests true --project-path "$CLIENT"
unity command test_status --project-path "$CLIENT"
```

기대: `SkydiveMoveSystemTests` 5개 PASS, 전체 실패 0.

- [ ] **Step 5: `SkydiveWorld`를 만든다**

`LeagueOfPhysical-Shared/Runtime/Scripts/Game/SkydiveWorld.cs`:

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// Skydive의 시뮬 코어. 클·서가 같은 구체 클래스를 돌려 결과가 갈리지 않게 한다.
    /// 슬라이스 1의 한 틱: Simulated 캐릭터를 모아 중력으로 떨어뜨린다.
    /// 레이저 판정은 Detection에 들어오지만(슬라이스 4) 지금은 비어 있다.
    /// </summary>
    public class SkydiveWorld : GameFramework.World.WorldBase
    {
        private readonly SkydiveMoveSystem _moveSystem;

        // 매 틱 도는 코드라 목록을 새로 만들지 않고 비워서 다시 쓴다.
        private readonly List<GameFramework.World.Entity> _divers = new List<GameFramework.World.Entity>();

        public SkydiveWorld(
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.WorldEventBuffer eventBuffer,
            SkydiveMoveSystem moveSystem)
            : base(entityRegistry, eventBuffer)
        {
            _moveSystem = moveSystem;
        }

        protected override void Mutation(long tick, float deltaTime)
        {
            CollectDivers();

            if (HasStarted(tick) == false)
            {
                // 출발 전. 속도를 명시적으로 0으로 둔다 — 스냅샷과 물리 팔로워가 이 값을 읽는다.
                for (int i = 0; i < _divers.Count; i++)
                {
                    _divers[i].Get<GameFramework.World.Velocity>().Linear = System.Numerics.Vector3.Zero;
                }
                return;
            }

            for (int i = 0; i < _divers.Count; i++)
            {
                _moveSystem.Tick(_divers[i], deltaTime);
            }
        }

        // id 순으로 세운다. 레지스트리 순회 순서는 정해져 있지 않은데, 처리 순서가 클·서에서
        // 같아야 두 쪽이 같은 결과에 이른다.
        private void CollectDivers()
        {
            _divers.Clear();
            foreach (var entity in EntityRegistry.All)
            {
                if (entity.Get<EntityKind>()?.Kind != EntityType.Character)
                {
                    continue;
                }
                if (entity.Has<GameFramework.World.Simulated>() == false)
                {
                    continue;   // 클라에서 남은 보간으로 그린다
                }
                _divers.Add(entity);
            }
            _divers.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        }
    }
}
```

- [ ] **Step 6: 컴파일과 테스트를 다시 확인한다**

```bash
unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
unity command run_tests  --mode EditMode --async_tests true --project-path "$CLIENT"
unity command test_status --project-path "$CLIENT"
```

기대: CS 에러 0, 테스트 실패 0.

- [ ] **Step 7: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git checkout -b feature/skydive-slice1
git status --short
git add Runtime/Scripts/Game/SkydiveMoveSystem.cs Runtime/Scripts/Game/SkydiveMoveSystem.cs.meta \
        Runtime/Scripts/Game/SkydiveWorld.cs Runtime/Scripts/Game/SkydiveWorld.cs.meta \
        Tests/EditMode/SkydiveMoveSystemTests.cs Tests/EditMode/SkydiveMoveSystemTests.cs.meta
git status --short
git commit -m "feat(skydive): 하늘에서 중력으로 떨어지는 시뮬을 더한다"
```

---

## Task 2: 맵 씬 — 스폰 자리와 바닥

**Files:**
- Create: `LeagueOfPhysical-Art/Assets/Art/Scenes/SkydiveMap.unity`

**Interfaces:**
- Consumes: `LOP.SpawnPoint` (마커 MonoBehaviour — 서버 룰이 `FindObjectsByType<SpawnPoint>`로 찾는다)
- Produces: 어드레서블 주소 `Assets/Art/Scenes/SkydiveMap.unity`

> 이 태스크는 Unity 에디터에서 손으로 한다. 아래 단계를 그대로 따른다.

- [ ] **Step 1: 맵 씬을 만든다**

클라 에디터에서 `Assets/Art/Scenes/PanchigiMap.unity`를 열고 **다른 이름으로 저장** →
`Assets/Art/Scenes/SkydiveMap.unity`. 판치기 전용 오브젝트(판·동전 배치)는 전부 지운다.

- [ ] **Step 2: 스폰 마커 8개를 놓는다**

빈 GameObject 8개를 만들어 `SpawnPoint` 컴포넌트를 붙이고, **y = 200, 반지름 12m 원 위에 균등 배치**한다
(8명이 겹치지 않게 출발하기 위한 것 — 스펙 §3.6의 "출발 지점만 가로로 벌린다").

```
i번째 마커 위치 = (12 × cos(2πi/8), 200, 12 × sin(2πi/8))
```

- [ ] **Step 3: 바닥을 놓는다**

`GameObject > 3D Object > Plane`을 y = 0에 놓고 스케일을 (20, 1, 20)으로 둔다. 레이어는 `Default`.
**떨어지는 것이 눈에 보이기 위한 기준면**이다 — 슬라이스 1의 정지는 `SkydiveMoveSystem.GroundY`가
하므로 이 콜라이더는 아직 시뮬에 쓰이지 않는다.

- [ ] **Step 4: 어드레서블로 표시한다**

씬 에셋을 선택하고 Inspector에서 **Addressable 체크** → 주소가 `Assets/Art/Scenes/SkydiveMap.unity`
인지 확인한다. 그룹은 `PanchigiMap`과 **같은 원격 그룹**에 넣는다.

> ⚠️ 로컬 그룹에 넣으면 **서버가 영영 못 받는다.** 맵은 서버도 로드한다.

- [ ] **Step 5: 씬이 열리는지 확인한다**

```bash
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
```

기대: 씬을 열고 저장하는 동안 에러 0.

- [ ] **Step 6: 커밋 (Art 레포)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Art
git checkout -b feature/skydive-slice1
git status --short
git add Assets/Art/Scenes/SkydiveMap.unity Assets/Art/Scenes/SkydiveMap.unity.meta
git status --short
git commit -m "feat(skydive): 스폰 자리와 바닥이 있는 맵을 더한다"
```

> Art는 서브모듈이다. 클·서 레포의 `Assets/Art` 포인터는 **의도적으로 커밋하지 않는 로컬 픽스처**다 —
> 이 계획에서는 건드리지 않는다.

---

## Task 3: 클라 — 스코프·생성기·씬

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Entity/SkydivePlayerCreator.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Create: `LeagueOfPhysical-Client/Assets/Scenes/Skydive.unity`
- Modify: `LeagueOfPhysical-Client/ProjectSettings/EditorBuildSettings.asset`

**Interfaces:**
- Consumes: `LOP.SkydiveWorld`, `LOP.SkydiveMoveSystem` (Task 1), `LOP.{ICharacterCreator, CharacterCreationData, IGameDataStore, IPlayerContext, IEntitySyncPolicy, CharactersPredictedSyncPolicy, IServerCorrectionHandler, NoServerCorrection, IExtrapolationAcceleration, ZeroExtrapolationAcceleration, GameLifetimeScope, CameraController}`
- Produces: `LOP.SkydivePlayerCreator`, `LOP.SkydiveLifetimeScope`

- [ ] **Step 1: 플레이어 몸 생성기를 만든다 (클라)**

`Assets/Scripts/Entity/SkydivePlayerCreator.cs`:

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Skydive의 플레이어 몸(클라). 체력·마나·레벨·어빌리티가 없다 — 이 게임에 그런 개념이 없다.
    /// 자세·스태미나 컴포넌트는 슬라이스 2가 여기에 더한다.
    /// </summary>
    public class SkydivePlayerCreator : ICharacterCreator
    {
        // 몸 크기. 슬라이스 2에서 TbSkydiveConfig로 옮긴다.
        private const float BodyRadius = 0.4f;
        private const float BodyHeight = 1.8f;

        private readonly IGameDataStore gameDataStore;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public SkydivePlayerCreator(
            IGameDataStore gameDataStore,
            IPlayerContext playerContext,
            GameFramework.World.EntityRegistry entityRegistry)
        {
            this.gameDataStore = gameDataStore;
            this.playerContext = playerContext;
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
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new MotionContributions());
            worldEntity.Add(new GameFramework.World.CapsuleShape(BodyRadius, BodyHeight));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            bool isUserEntity = gameDataStore.userEntityId == creationData.entityId;
            if (isUserEntity)
            {
                // 입력은 내 몸만 갖는다. Simulated는 EntityBinder가 동기화 정책을 보고 붙인다.
                worldEntity.Add(new InputBuffer());
            }
            entityRegistry.Add(worldEntity);

            if (isUserEntity)
            {
                playerContext.entityId = creationData.entityId;
            }

            Debug.Log($"[World] Registered skydive body {worldEntity.Id}");
        }
    }
}
```

- [ ] **Step 2: 게임 스코프를 만든다 (클라)**

`Assets/Scripts/Game/SkydiveLifetimeScope.cs`:

```csharp
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Skydive 덩어리(클라) — 떨어지는 월드, 캐릭터 전원 예측.</summary>
    public class SkydiveLifetimeScope : GameLifetimeScope
    {
        [SerializeField] private CameraController cameraController;

        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.RegisterComponent(cameraController);

            builder.Register<SkydiveMoveSystem>(Lifetime.Singleton);
            builder.Register<SkydiveWorld>(c => new SkydiveWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<SkydiveMoveSystem>()), Lifetime.Singleton)
                .As<GameFramework.World.IWorld>().AsSelf();

            builder.Register<ICharacterCreator, SkydivePlayerCreator>(Lifetime.Singleton);

            // 플레이어끼리 부딪히기로 했으므로 남도 예측한다(스펙 §4.1). 충돌 자체는 슬라이스 6이
            // 켜지만, 정책을 지금 맞춰 두면 그때 이 줄을 고칠 일이 없다.
            builder.Register<IEntitySyncPolicy, CharactersPredictedSyncPolicy>(Lifetime.Singleton);
            builder.Register<IServerCorrectionHandler, NoServerCorrection>(Lifetime.Singleton);

            // 이 게임엔 외삽 대상이 없다(정책이 Extrapolated를 절대 안 준다) — 그래도 EntityBinder의
            // 생성자 의존이라 등록은 필요하다. 값은 쓰이지 않는다.
            builder.Register<IExtrapolationAcceleration, ZeroExtrapolationAcceleration>(Lifetime.Singleton);
        }
    }
}
```

- [ ] **Step 3: 컴파일을 확인한다**

```bash
unity command recompile        --project-path "$CLIENT"
unity command recompile_status --project-path "$CLIENT"
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
```

기대: CS 에러 0. (`status`가 `up_to_date`면 재컴파일이 안 된 것이니 다시 부른다.)

- [ ] **Step 4: 게임 씬을 만든다**

클라 에디터에서 `Assets/Scenes/Panchigi.unity`를 열고 **다른 이름으로 저장** →
`Assets/Scenes/Skydive.unity`. 그다음:

1. 루트의 `PanchigiLifetimeScope` 컴포넌트를 **Remove Component**
2. 같은 GameObject에 `SkydiveLifetimeScope`를 **Add Component**
3. Inspector에서 `Runner` 슬롯에 씬 안의 `LOPRunner`를 물린다
4. `Camera Controller` 슬롯에 씬 안의 `CameraController`를 물린다
5. 판치기 전용 오브젝트(`PanchigiStrikeInput` 등)는 지운다
6. 저장

- [ ] **Step 5: 빌드 세팅에 씬을 넣는다**

```bash
unity command add_scene_to_build --path "Assets/Scenes/Skydive.unity" --project-path "$CLIENT"
```

확인:

```bash
grep -n "Skydive" "$CLIENT/ProjectSettings/EditorBuildSettings.asset"
```

기대: `path: Assets/Scenes/Skydive.unity` 한 줄.

- [ ] **Step 6: 씬을 열었을 때 에러가 없는지 확인한다**

```bash
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
```

기대: 에러 0.

- [ ] **Step 7: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git checkout -b feature/skydive-slice1
git status --short
git add Assets/Scripts/Entity/SkydivePlayerCreator.cs Assets/Scripts/Entity/SkydivePlayerCreator.cs.meta \
        Assets/Scripts/Game/SkydiveLifetimeScope.cs Assets/Scripts/Game/SkydiveLifetimeScope.cs.meta \
        Assets/Scenes/Skydive.unity Assets/Scenes/Skydive.unity.meta \
        ProjectSettings/EditorBuildSettings.asset
git status --short
git commit -m "feat(skydive): 클라에 게임 씬과 덩어리 등록을 더한다"
```

> `git status --short`에 `Assets/Art`, 폰트 에셋, `PackageManagerSettings.asset`이 `M`으로 남아
> 있어야 정상이다. **스테이지된 것이 위 목록뿐인지** 확인하고 커밋한다.

---

## Task 4: 서버 — 스코프·룰·생성기·씬

**Files:**
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Entity/SkydivePlayerCreator.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveRuleSystem.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scripts/Game/SkydiveLifetimeScope.cs`
- Create: `LeagueOfPhysical-Server/Assets/Scenes/Skydive.unity`
- Modify: `LeagueOfPhysical-Server/ProjectSettings/EditorBuildSettings.asset`

**Interfaces:**
- Consumes: `LOP.SkydiveWorld`, `LOP.SkydiveMoveSystem` (Task 1), `LOP.{IGameRuleSystem, MatchOutcome, MatchPlacement, IRoomDataStore, EntitySpawner, CharacterCreationData, ICharacterCreator, SpawnPoint, SpawnPlacement, GameLifetimeScope}`
- Produces: `LOP.SkydivePlayerCreator`, `LOP.SkydiveRuleSystem`, `LOP.SkydiveLifetimeScope`

- [ ] **Step 1: 플레이어 몸 생성기를 만든다 (서버)**

`Assets/Scripts/Entity/SkydivePlayerCreator.cs`:

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>Skydive의 플레이어 몸(서버). 체력·마나·레벨·어빌리티가 없다.</summary>
    public class SkydivePlayerCreator : ICharacterCreator
    {
        private const float BodyRadius = 0.4f;
        private const float BodyHeight = 1.8f;

        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public SkydivePlayerCreator(GameFramework.World.EntityRegistry entityRegistry)
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
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new Appearance(creationData.visualId));
            worldEntity.Add(new MotionContributions());
            worldEntity.Add(new GameFramework.World.CapsuleShape(BodyRadius, BodyHeight));
            worldEntity.Add(new GameFramework.World.PhysicsConfig(
                GameFramework.World.BodyKind.Kinematic, freezeRotation: true, isTrigger: false));

            if (string.IsNullOrEmpty(creationData.userId) == false)
            {
                worldEntity.Add(new GameFramework.World.Ownership(creationData.userId));
                worldEntity.Add(new InputBuffer());
            }
            worldEntity.Add(new GameFramework.World.Simulated());   // 서버는 모든 몸을 시뮬한다
            entityRegistry.Add(worldEntity);

            Debug.Log($"[World] Registered skydive body {worldEntity.Id}");
        }
    }
}
```

- [ ] **Step 2: 룰을 만든다 (서버)**

`Assets/Scripts/Game/SkydiveRuleSystem.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// Skydive 룰(서버). 참가자마다 몸을 하늘에 세우고, 시간 상한으로 판을 끝낸다.
    /// 결승선 판정(등수)은 슬라이스 3, 죽음 처리는 슬라이스 4가 여기에 붙는다.
    /// </summary>
    public class SkydiveRuleSystem : IGameRuleSystem
    {
        // 맵에 스폰 마커가 없을 때만 쓰는 폴백. 같은 자리에 겹쳐 세우면 누가 누군지 안 보인다.
        private const float FallbackSpawnY = 200f;
        private const float FallbackSpawnSpacingX = 3f;

        // 겉모습은 Flappy의 새를 빌려 쓴다 — 슬라이스 1에서 확인할 것은 "떨어지는가"뿐이고,
        // 전용 모델을 기다리면 그 확인이 막힌다. 자세(다이브/대자/패러세일)가 생기는 슬라이스 2에서
        // 자세별 애니메이션이 있는 몸으로 바꾼다.
        private const string BodyVisualId = "Assets/Art/Characters/FlappyBird/Bird.prefab";

        private readonly IRoomDataStore roomDataStore;
        private readonly EntitySpawner entitySpawner;

        private readonly List<string> bodyEntityIds = new List<string>();

        public SkydiveRuleSystem(IRoomDataStore roomDataStore, EntitySpawner entitySpawner)
        {
            this.roomDataStore = roomDataStore;
            this.entitySpawner = entitySpawner;
        }

        public void Initialize()
        {
            // 시작 지점은 맵이 정한다 — 룰이 좌표를 들고 있으면 맵을 새로 만들 때마다 룰을 고쳐야 한다.
            // 비활성 마커까지 찾는다: 마커는 보일 필요가 없어 꺼 둘 수도 있다.
            var slots = SpawnPlacement.Arrange(
                UnityEngine.Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            if (slots.Count == 0)
            {
                Debug.LogWarning("[Skydive] 맵에 SpawnPoint가 없다 — 하늘에 가로로 세운다");
            }

            var playerList = roomDataStore.match.playerList;
            for (int i = 0; i < playerList.Length; i++)
            {
                Vector3 position = slots.Count > 0
                    ? slots[i % slots.Count]
                    : new Vector3(i * FallbackSpawnSpacingX, FallbackSpawnY, 0f);

                string entityId = entitySpawner.GenerateEntityId();
                bodyEntityIds.Add(entityId);

                entitySpawner.Spawn(new CharacterCreationData
                {
                    userId = playerList[i],
                    entityId = entityId,
                    visualId = BodyVisualId,
                    characterCode = "",
                    position = position,
                    rotation = Vector3.zero,
                    velocity = Vector3.zero,
                });
            }
        }

        public void Deinitialize()
        {
            bodyEntityIds.Clear();
        }

        // 결승선이 아직 없다(슬라이스 3). 그때까지는 시간 상한만으로 끝난다.
        public bool IsMatchOver => false;

        // 50Hz × 60초. 200m를 40m/s 상한으로 떨어지면 10초 남짓이라 넉넉한 상한이다.
        public long MatchDurationTicks => 3000;

        // 진짜 등수(결승선 통과 순서)는 슬라이스 3에서 채운다. 그때까지는 보고 경로가 끊기지
        // 않도록 무작위로 둔다.
        public MatchOutcome ResolveOutcome()
        {
            var userIds = roomDataStore.match.playerList.ToList();

            for (int i = userIds.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (userIds[i], userIds[j]) = (userIds[j], userIds[i]);
            }

            var outcome = new MatchOutcome();
            for (int i = 0; i < userIds.Count; i++)
            {
                outcome.placements.Add(new MatchPlacement { userId = userIds[i], placement = i + 1 });
            }

            return outcome;
        }
    }
}
```

- [ ] **Step 3: 게임 스코프를 만든다 (서버)**

`Assets/Scripts/Game/SkydiveLifetimeScope.cs`:

```csharp
using VContainer;
using VContainer.Unity;

namespace LOP
{
    /// <summary>Skydive 덩어리(서버) — 떨어지는 월드, 하늘에 세우는 룰.</summary>
    public class SkydiveLifetimeScope : GameLifetimeScope
    {
        protected override void ConfigureGame(IContainerBuilder builder)
        {
            builder.Register<SkydiveMoveSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.IWorld>(c => new SkydiveWorld(
                c.Resolve<GameFramework.World.EntityRegistry>(),
                c.Resolve<GameFramework.World.WorldEventBuffer>(),
                c.Resolve<SkydiveMoveSystem>()), Lifetime.Singleton);

            builder.Register<ICharacterCreator, SkydivePlayerCreator>(Lifetime.Singleton);
            builder.Register<IGameRuleSystem, SkydiveRuleSystem>(Lifetime.Singleton);
        }
    }
}
```

- [ ] **Step 4: 컴파일을 확인한다**

```bash
unity command recompile        --project-path "$SERVER"
unity command recompile_status --project-path "$SERVER"
unity command get_console_logs --severity error --limit 40 --project-path "$SERVER"
```

기대: CS 에러 0.

- [ ] **Step 5: 게임 씬을 만든다 (서버)**

서버 에디터에서 `Assets/Scenes/Panchigi.unity`를 열고 **다른 이름으로 저장** →
`Assets/Scenes/Skydive.unity`. 그다음:

1. 루트의 `PanchigiLifetimeScope`를 **Remove Component**
2. `SkydiveLifetimeScope`를 **Add Component**
3. Inspector에서 `Runner` 슬롯에 씬 안의 `LOPRunner`를 물린다
4. 판치기 전용 오브젝트는 지운다
5. 저장

- [ ] **Step 6: 빌드 세팅에 씬을 넣는다**

```bash
unity command add_scene_to_build --path "Assets/Scenes/Skydive.unity" --project-path "$SERVER"
grep -n "Skydive" "$SERVER/ProjectSettings/EditorBuildSettings.asset"
```

기대: `path: Assets/Scenes/Skydive.unity` 한 줄.

- [ ] **Step 7: 커밋**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/skydive-slice1
git status --short
git add Assets/Scripts/Entity/SkydivePlayerCreator.cs Assets/Scripts/Entity/SkydivePlayerCreator.cs.meta \
        Assets/Scripts/Game/SkydiveRuleSystem.cs Assets/Scripts/Game/SkydiveRuleSystem.cs.meta \
        Assets/Scripts/Game/SkydiveLifetimeScope.cs Assets/Scripts/Game/SkydiveLifetimeScope.cs.meta \
        Assets/Scenes/Skydive.unity Assets/Scenes/Skydive.unity.meta \
        ProjectSettings/EditorBuildSettings.asset
git status --short
git commit -m "feat(skydive): 서버에 게임 씬과 하늘에 세우는 룰을 더한다"
```

---

## Task 5: 마스터데이터 — 고를 수 있게 만든다

**Files:**
- Modify: `infrastructure/table/Datas/#GameMode.xlsx`
- Modify: `infrastructure/table/Datas/#Map.xlsx`
- Modify: `infrastructure/table/Datas/#Queue.xlsx`
- Generated: `LeagueOfPhysical-MasterData-Client/Runtime.Generated/**`
- Generated: `LeagueOfPhysical-MasterData-Server/Runtime.Generated/**`
- Generated: `lop-backend/apps/matchmaking-server/{src/masterdata,master_data}/**`

**Interfaces:**
- Consumes: Task 3·4가 만든 씬 경로, Task 2가 만든 맵 경로
- Produces: `TbGameMode` id `8`, `TbMap` id `4`

> 로비의 게임 목록은 이미 데이터 주도다(`PlayableGameProvider`가 `TbGameMode.DataList`를 읽는다).
> **클라 코드 변경이 없다.**

- [ ] **Step 1: `#GameMode.xlsx`에 행을 더한다**

엑셀에서 열어 마지막 행(id 7, Panchigi) 아래에 한 줄:

| id | code | name | description | min_players | max_players | scene_path |
|---|---|---|---|---|---|---|
| 8 | Skydive | 스카이다이브 | 하늘에서 레이저를 피해 내려가는 레이스 | 2 | 8 | Assets/Scenes/Skydive.unity |

- [ ] **Step 2: `#Map.xlsx`에 행을 더한다**

| id | game_mode_id | code | scene_path |
|---|---|---|---|
| 4 | 8 | SkydiveMap | Assets/Art/Scenes/SkydiveMap.unity |

- [ ] **Step 3: `#Queue.xlsx`의 두 큐에 `8`을 더한다**

`Casual`(id 1)과 `Ranked`(id 2)의 `allowed_game_mode_ids`를 `1,2,3,4,5,6,7` → `1,2,3,4,5,6,7,8`로.

- [ ] **Step 4: 생성한다**

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure/table
./gen.sh
```

기대 출력: `[gen] target=client` / `target=server` / `target=matchmaking` / `[done]`.

- [ ] **Step 5: 생성 결과를 확인한다**

```bash
cd C:/Users/re5na/workspace/LOP/lop-backend/apps/matchmaking-server/master_data
grep -n "Skydive" tbgamemode.json
grep -n "SkydiveMap" tbmap.json
grep -n '8' tbqueue.json | head
```

기대: `"code": "Skydive"`, `"id": 8` / `"code": "SkydiveMap"`, `"game_mode_id": 8` /
두 큐의 `allowed_game_mode_ids`에 `8`.

`.meta`가 지워지지 않았는지 확인한다 (gen.sh의 trap이 되돌린다):

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client status --short | grep '^ D' || echo "삭제된 파일 없음"
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server status --short | grep '^ D' || echo "삭제된 파일 없음"
```

기대: "삭제된 파일 없음".

- [ ] **Step 6: 네 레포에 각각 커밋한다**

```bash
for R in infrastructure LeagueOfPhysical-MasterData-Client LeagueOfPhysical-MasterData-Server lop-backend; do
  cd "C:/Users/re5na/workspace/LOP/$R"
  git checkout -b feature/skydive-slice1
  git status --short
done
```

각 레포에서 **바뀐 경로만** 지정해 커밋한다:

```bash
cd C:/Users/re5na/workspace/LOP/infrastructure
git add "table/Datas/#GameMode.xlsx" "table/Datas/#Map.xlsx" "table/Datas/#Queue.xlsx"
git status --short
git commit -m "feat(skydive): 게임 모드·맵·큐 데이터를 더한다"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Client
git add Runtime.Generated
git status --short
git commit -m "chore(masterdata): Skydive 추가 반영 (생성물)"

cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-MasterData-Server
git add Runtime.Generated
git status --short
git commit -m "chore(masterdata): Skydive 추가 반영 (생성물)"

cd C:/Users/re5na/workspace/LOP/lop-backend
git add apps/matchmaking-server/src/masterdata apps/matchmaking-server/master_data
git status --short
git commit -m "chore(masterdata): Skydive 추가 반영 (생성물)"
```

- [ ] **Step 7: 로컬에서 방까지 들어가지는지 본다**

클라 에디터에서 Play → 로비에서 **스카이다이브**가 목록에 보이는지, 고르고 매칭이 되는지 확인한다.
혼자서는 `min_players = 2`라 매칭이 안 잡히므로, **여기서는 "목록에 보이고 티켓이 나간다"까지만**
확인한다. 실제 플레이는 Task 6.

```bash
unity command get_console_logs --severity error --limit 40 --project-path "$CLIENT"
```

기대: `입장 가능한 게임이 없다` 로그가 **없고**, 목록에 스카이다이브가 보인다.

---

## Task 6: 배포와 두 클라 실플레이

> 새 게임 모드가 실패하는 방식은 늘 배포 쪽이었다(스펙 §8.2). 이 태스크가 그 경로를 한 번 끝까지
> 뚫는다.

**Files:** (코드 변경 없음 — 머지·배포·검증)

- [ ] **Step 1: 여섯 레포를 각각 main에 머지한다**

레포마다 `CLAUDE.md`의 **푸시 규약**을 그대로 따른다. **한 줄씩 결과를 확인하고 넘어간다.**

```bash
# 각 레포에서 (Shared → Art → Client → Server → MasterData×2 → infrastructure → lop-backend)
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/skydive-slice1
git push origin main
```

> ⚠️ `--force` / `--force-with-lease` 금지. 푸시가 거절되면 다시 `fetch` → 리베이스 → 재시도.
> ⚠️ Unity 레포는 리베이스 전에 로컬 픽스처를 `git stash push -u -m skydive-fixtures`로 빼두고
> 끝나면 `pop` 한다.

- [ ] **Step 2: 게임서버 이미지를 굽는다**

`gameserver-deploy` 워크플로를 돌린다. **이걸 빠뜨리면 방에 들어가고 몇 초 뒤 튕긴다** —
서버가 모드 8을 모르기 때문이다.

확인:

```bash
kubectl get pods -A | grep -i room
```

기대: 새 태그의 파드가 `Running`.

- [ ] **Step 3: 어드레서블을 올린다**

클라 레포의 `content-deploy`를 **all**로 돌린다. 맵 씬은 서버도 로드하므로 안 올리면 로드에
실패한다.

- [ ] **Step 4: 백엔드를 배포한다**

`backend-deploy`를 **app=all**로 돌린다(매칭 서버가 새 `tbgamemode.json`을 들고 있어야 한다).

```bash
kubectl get pods -A | grep -i matchmaking
```

기대: 새 태그의 파드가 `Running`.

- [ ] **Step 5: 두 클라로 실제로 들어가 본다**

메인 에디터 + MPPM 클론 두 개를 띄우고, 둘 다 **스카이다이브**를 골라 매칭한다.

확인할 것:

1. 두 클라가 같은 방에 들어간다
2. **캐릭터가 하늘에서 떨어진다** (스폰 y = 200 → 바닥 y = 0)
3. 상대 캐릭터도 같이 떨어지는 것이 보인다
4. 60초 뒤 매치가 끝나고 결과 화면이 뜬다
5. **4초 만에 튕기지 않는다** (튕기면 게임서버 이미지가 낡은 것 — Step 2로 돌아간다)

```bash
unity command get_console_logs --severity error --limit 60 --project-path "$CLIENT"
```

기대: 에러 0.

- [ ] **Step 6: 로드맵에 기록한다**

`docs/ROADMAP.md`의 "이번 세션에 닫힌 것"에 한 줄 더하고, 클라 레포 피처 브랜치에서 커밋 후
같은 푸시 규약으로 머지한다.

```
| ✅ | **Skydive 슬라이스 1 — 모드가 존재한다** — 로비 선택 → 방 입장 → 하늘에서 낙하.
      6레포 머지 + 게임서버·백엔드·어드레서블 배포 + 두 클라 실플레이 |
```

---

## 다음 슬라이스

슬라이스 2(자세와 스태미나)부터는 **이 슬라이스가 실제로 만든 것을 보고** 계획을 쓴다.
특히 wire proto 변경이 들어가므로 `MessageIds.cs` ID diff 확인 단계가 필수다(스펙 §6.5).
