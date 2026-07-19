# S5b — Creator 데이터/뷰 분리 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 엔티티 생성에서 **데이터 조립(Creator)**과 **Unity 뷰 생성(반응형 스포너)**을 분리한다 — Creator는 World.Entity 데이터 + 앵커(GameObject+LOPActor) + 등록만, 모든 뷰 컴포넌트(+PhysicsBody)는 `EntityCreated`에 반응하는 뷰 스포너가.

**Architecture:** 클라 `EntityBinder`(지금 장식 뷰만)를 **모든 뷰**를 붙이는 스포너로 확장, 서버는 뷰 스포너를 신설. Creator는 뷰 코드를 잃고 데이터+앵커+등록만 남는다. `EntityCreated`는 매니저가 동기 발행(MessagePipe)하므로 스포너가 `CreateEntity` 반환 전에 뷰+PhysicsBody를 다 붙인다 → 반환 계약·물리 틱 공백 없음. GF 계약·물리 레이어·매니저·`entityMap`은 무변경.

**Tech Stack:** Unity(C#), VContainer(DI), MessagePipe(pub/sub, 동기), 순수 C# World Core(`GameFramework.World`).

## Global Constraints

- **2레포:** Client(`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client`), Server(`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server`). **GameFramework·LOP-Shared 무변경.**
- **피처 브랜치:** 각 레포 `feature/entity-s5b-view-spawner`. main 직접 커밋 금지. Client엔 이미 이 브랜치 존재(spec/plan 커밋됨). Server는 Task 2에서 생성.
- **컴파일 검증(UnityMCP, 컨트롤러):** 파일 수정 후 `refresh_unity`(compile:request) → `read_console`(types:["error"]). **모든 UnityMCP 호출에 `unity_instance` 명시**: client=`LeagueOfPhysical-Client@<hash>`, server=`LeagueOfPhysical-Server@<hash>`(hash는 `mcpforunity://instances`로 확인). **EditMode 테스트 없음**(대상 전부 Assembly-CSharp, `[[client-test-infra-constraint]]`) — 검증=컴파일 + 인게임 스모크(사용자).
- **구현 서브에이전트 = 코드+git만**(UnityMCP 미사용). 컨트롤러가 컴파일 검증.
- **동작 무변화가 기대치** — 뷰를 크리에이터에서 스포너로 *옮기기만* 한다(값·순서 보존). 스폰 전체가 `CreateEntity` 한 호출 안에서 동기 완결됨은 그대로.
- **서버 do-not-commit 픽스처**(DefaultVolumeProfile/ConfigureRoomComponent/GameRuleSystem/GraphicsSettings) 미스테이징 — `git add <파일>` 명시(‑A 금지).
- **.meta:** 새 파일(서버 스포너) 생성 시 Unity가 만든 `.meta`도 커밋(컨트롤러 refresh 후 처리).
- **어휘 rename 금지(S5b-3 전까지):** Task 1/2는 타입/구조만 옮기고 식별자 이름(`entity` 등)은 그대로 둔다.

---

## 파일 구조

**Client (Task 1):**
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs` — 뷰 코드 제거, 데이터+앵커+등록+playerContext.entity만.
- Modify: `Assets/Scripts/EntityCreator/ItemCreator.cs` — 동일(뷰 제거).
- Modify: `Assets/Scripts/Game/EntityBinder.cs` — 주요 뷰(PhysicsFollower/LOPEntityView/interpolator)+PhysicsBody+장식 뷰+playerContext.entityView를 Kind·isUser 분기로 부착.

**Server (Task 2):**
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs` / `ItemCreator.cs` — 뷰 제거.
- Create: `Assets/Scripts/Game/EntityViewSpawner.cs` — 서버 뷰 스포너(EntityCreated 구독).
- Modify: `Assets/Scripts/Game/GameLifetimeScope.cs` — 스포너 entry point 등록.

**Both (Task 3):** 어휘 rename `entity`→`actor`.

---

### Task 1: 클라 뷰 → EntityBinder 스포너

클라 크리에이터에서 뷰 컴포넌트를 빼 확장된 `EntityBinder`로 옮긴다. 한 커밋(원자) — 안 그러면 뷰가 이중 생성되거나 사라짐.

**Files:**
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs`
- Modify: `Assets/Scripts/EntityCreator/ItemCreator.cs`
- Modify: `Assets/Scripts/Game/EntityBinder.cs`

**Interfaces:**
- Consumes: `EntityCreated.entity : LOPActor`, `EntityRegistry.Get(id) : Entity`, `EntityKind.Kind`, `PhysicsFollower.Initialize(Entity, bool kinematic, bool trigger)` + `.entityRigidbody`/`.entityColliders`, `PhysicsBody(Rigidbody, CapsuleCollider)`, `RemoteEntityInterpolator.{entity, worldEntity, entityView}`, `LocalEntityInterpolator.{entity, entityView}`, `LOPEntityView.SetEntity(LOPActor)`, `DamageFloaterEmitter.SetEntity`/`CharacterNameplate.SetEntity`, `IPlayerContext.{entity, entityView}`, `IGameDataStore.userEntityId`.
- Produces: EntityBinder가 모든 클라 뷰의 단일 소유자.

- [ ] **Step 1: `CharacterCreator.cs` 축소 (뷰 제거)**

전체를 아래로 교체(데이터+앵커+등록+playerContext.entity, 뷰·PhysicsBody·interpolator 없음):

```csharp
using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class CharacterCreator : IEntityCreator<LOPActor, CharacterCreationData>
    {
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IPlayerContext playerContext;
        [Inject] private IObjectResolver objectResolver;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private AbilitySystem abilitySystem;
        [Inject] private LOP.MasterData.LOPMasterData md;

        public LOPActor Create(CharacterCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new MasterDataRef(creationData.characterCode));
            worldEntity.Add(new Appearance(creationData.visualId));

            var worldHealth = new GameFramework.World.Health(creationData.maxHP) { Current = creationData.currentHP };
            worldEntity.Add(worldHealth);
            worldEntity.Add(new GameFramework.World.Mana(creationData.maxMP) { Current = creationData.currentMP });
            worldEntity.Add(new GameFramework.World.Level { Value = creationData.level, Exp = creationData.currentExp, ExpToNext = 100 });
            var worldStats = new GameFramework.World.Stats();
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Strength] = creationData.strength;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Dexterity] = creationData.dexterity;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Intelligence] = creationData.intelligence;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Vitality] = creationData.vitality;
            var characterMasterData = md.Tables.TbCharacter.Get(creationData.characterCode);
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.MoveSpeed] = characterMasterData.Speed;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.JumpPower] = characterMasterData.JumpPower;
            worldEntity.Add(worldStats);
            worldEntity.Add(new Abilities());
            worldEntity.Add(new StatusEffects());
            worldEntity.Add(new MotionContributions());

            bool isUserEntity = gameDataStore.userEntityId == creationData.entityId;
            if (isUserEntity)
            {
                // 입력으로 조종되는 엔티티(내 캐릭)만. 클라 시뮬 대상=예측하는 내 캐릭만(Simulated).
                worldEntity.Add(new InputBuffer());
                worldEntity.Add(new GameFramework.World.Simulated());
            }
            entityRegistry.Add(worldEntity);

            abilitySystem.Grant(worldEntity, 1);
            abilitySystem.Grant(worldEntity, 2);   // dash
            abilitySystem.Grant(worldEntity, 3);   // attack
            if (isUserEntity)
            {
                abilitySystem.Grant(worldEntity, 4);   // 내 캐릭 전용 테스트 툴(G키)
            }

            // 앵커: 뷰 컴포넌트(물리/모델/보간/장식)는 EntityBinder가 EntityCreated 반응으로 붙인다.
            GameObject root = new GameObject($"Actor_{creationData.entityId}");
            LOPActor entity = root.AddComponent<LOPActor>();
            objectResolver.Inject(entity);
            entity.Initialize(creationData);
            if (isUserEntity)
            {
                playerContext.entity = entity;   // .entityView는 스포너가 뷰 생성 후 세팅
            }

            Debug.Log($"[World] Registered entity {worldEntity.Id} Health={worldHealth.Current}/{worldHealth.Max}");
            return entity;
        }
    }
}
```

- [ ] **Step 2: `ItemCreator.cs` 축소 (뷰 제거)**

전체를 아래로 교체:

```csharp
using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class ItemCreator : IEntityCreator<LOPActor, ItemCreationData>
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        public LOPActor Create(ItemCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Item));
            worldEntity.Add(new MasterDataRef(creationData.itemCode));
            worldEntity.Add(new Appearance(creationData.visualId));
            entityRegistry.Add(worldEntity);

            // 앵커: 뷰(물리/모델/보간)는 EntityBinder가 붙인다.
            GameObject root = new GameObject($"Actor_{creationData.entityId}");
            LOPActor entity = root.AddComponent<LOPActor>();
            objectResolver.Inject(entity);
            entity.Initialize(creationData);
            return entity;
        }
    }
}
```

- [ ] **Step 3: `EntityBinder.cs` 확장 (모든 뷰 부착)**

전체를 아래로 교체 — Kind(Character/Item) 분기, isUser 분기, 주요 뷰+PhysicsBody+장식:

```csharp
using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using System;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 엔티티 수명 신호(<see cref="EntityCreated"/>)에 반응해 엔티티의 모든 Unity 뷰를 생성·연결한다
    /// (분리형 뷰 스포너 — ECS/Entitas 뷰 리졸버). Creator는 데이터+앵커만, 뷰는 여기가 전담.
    /// 부착: 물리 팔로워 + PhysicsBody + 뷰 + 보간기(+ 캐릭터 장식 뷰). 파괴는 root GameObject 파괴 +
    /// ICleanup 경로가 처리(생성물이 같은 root 자식이라 함께 정리).
    /// </summary>
    public class EntityBinder : IGameMessageHandler
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private ISubscriber<EntityCreated> entityCreatedSubscriber;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IPlayerContext playerContext;

        private IDisposable subscription;

        public void Initialize()
        {
            subscription = entityCreatedSubscriber.Subscribe(OnEntityCreated);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            LOPActor entity = entityCreated.entity;
            if (entity == null)
            {
                return;
            }
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entity.entityId);
            if (worldEntity == null)
            {
                return;
            }
            EntityKind kind = worldEntity.Get<EntityKind>();
            if (kind == null)
            {
                return;
            }

            GameObject root = entity.gameObject;
            bool isItem = kind.Kind == EntityType.Item;

            // 물리 팔로워 + PhysicsBody (모든 엔티티 공통). 아이템=trigger, 캐릭터=non-trigger.
            PhysicsFollower physicsFollower = root.AddComponent<PhysicsFollower>();
            objectResolver.Inject(physicsFollower);
            physicsFollower.Initialize(worldEntity, true, isItem);
            worldEntity.Add(new PhysicsBody(physicsFollower.entityRigidbody, (CapsuleCollider)physicsFollower.entityColliders[0]));

            LOPEntityView view = root.AddComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(entity);

            if (kind.Kind == EntityType.Character)
            {
                bool isUserEntity = gameDataStore.userEntityId == entity.entityId;
                if (isUserEntity)
                {
                    playerContext.entityView = view;

                    LocalEntityInterpolator interpolator = root.AddComponent<LocalEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.entity = entity;
                    interpolator.entityView = view;
                }
                else
                {
                    RemoteEntityInterpolator interpolator = root.AddComponent<RemoteEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.entity = entity;
                    interpolator.worldEntity = worldEntity;
                    interpolator.entityView = view;
                }

                // 장식 뷰(캐릭터만).
                DamageFloaterEmitter damageFloaterEmitter = root.AddComponent<DamageFloaterEmitter>();
                objectResolver.Inject(damageFloaterEmitter);
                damageFloaterEmitter.SetEntity(entity);

                CharacterNameplate nameplate = root.AddComponent<CharacterNameplate>();
                objectResolver.Inject(nameplate);
                nameplate.SetEntity(entity);
            }
            else
            {
                // 아이템: 원격 보간만(내 예측 대상 아님).
                RemoteEntityInterpolator interpolator = root.AddComponent<RemoteEntityInterpolator>();
                objectResolver.Inject(interpolator);
                interpolator.entity = entity;
                interpolator.worldEntity = worldEntity;
                interpolator.entityView = view;
            }
        }
    }
}
```

> 주: `EntityBinder`는 이미 `GameLifetimeScope.cs:72`에 `RegisterEntryPoint<EntityBinder>()`로 등록됨 — 등록 변경 불필요. 새 injects(`gameDataStore`/`playerContext`)는 게임 스코프에 이미 등록돼 있음(크리에이터가 쓰던 것).

- [ ] **Step 4: 클라 컴파일 확인 (컨트롤러)**

UnityMCP `refresh_unity`+`read_console`(unity_instance=Client). Expected: 에러 0. (뷰가 크리에이터→스포너로 이동, 이중/누락 없음.)

- [ ] **Step 5: 커밋 (Client)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/EntityCreator/ItemCreator.cs Assets/Scripts/Game/EntityBinder.cs
git commit -m "refactor(entity): 클라 뷰 생성을 Creator→EntityBinder 스포너로 (S5b Task1)"
```

- [ ] **Step 6: 인게임 스모크(사용자)** — 스폰·모델 로드·이동·충돌·아이템·넉백/데미지(플로터)·HP바(nameplate)·롤백·원격 보간·playerContext(카메라 추적/HUD 열림) 무변화.

---

### Task 2: 서버 뷰 → 뷰 스포너 신설

서버 크리에이터에서 뷰를 빼 신설 `EntityViewSpawner`로. 한 커밋(원자).

**Files:**
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs` / `ItemCreator.cs`
- Create: `Assets/Scripts/Game/EntityViewSpawner.cs`
- Modify: `Assets/Scripts/Game/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: (Task 1과 동일 서버측) `EntityCreated.entity : LOPActor`, `EntityRegistry.Get`, `EntityKind.Kind`, `Ownership`(플레이어 마커), `PhysicsFollower`, `PhysicsBody`, `LOPEntityView.SetEntity`, `LOPAIController.{SetEntity, SetBrain}`, `EnemyBrain`(DI resolve).
- Produces: 서버 뷰 스포너(EntityCreated 구독 entry point).

- [ ] **Step 1: 서버 entry-point 인터페이스 확인**

`Assets/Scripts/Game/` 또는 MessageHandler 폴더에서 서버의 기존 entry-point 핸들러(`GameInfoMessageHandler` 등)가 구현하는 라이프사이클 인터페이스를 확인한다(`IGameMessageHandler`인지, VContainer `IInitializable`+`IDisposable`인지). 신설 스포너는 **그와 동일한 인터페이스**를 구현하고 `Initialize`에서 구독한다. (아래 코드는 `IGameMessageHandler` 가정 — 다르면 그에 맞춰 조정.)

- [ ] **Step 2: 서버 `CharacterCreator.cs` 축소 (뷰 제거)**

전체를 아래로 교체(데이터+앵커+등록, 뷰·AIController·PhysicsBody 없음):

```csharp
using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class CharacterCreator : IEntityCreator<LOPActor, CharacterCreationData>
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private AbilitySystem abilitySystem;
        [Inject] private LOP.MasterData.LOPMasterData md;

        public LOPActor Create(CharacterCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new MasterDataRef(creationData.characterCode));
            worldEntity.Add(new Appearance(creationData.visualId));

            var worldHealth = new GameFramework.World.Health(creationData.maxHP) { Current = creationData.currentHP };
            worldEntity.Add(worldHealth);
            worldEntity.Add(new GameFramework.World.Mana(creationData.maxMP) { Current = creationData.currentMP });
            worldEntity.Add(new GameFramework.World.Level { Value = creationData.level, Exp = creationData.currentExp, ExpToNext = 100 });
            var worldStats = new GameFramework.World.Stats();
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Strength] = creationData.strength;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Dexterity] = creationData.dexterity;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Intelligence] = creationData.intelligence;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Vitality] = creationData.vitality;
            var characterMasterData = md.Tables.TbCharacter.Get(creationData.characterCode);
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.MoveSpeed] = characterMasterData.Speed;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.JumpPower] = characterMasterData.JumpPower;
            worldEntity.Add(worldStats);

            bool isPlayer = !string.IsNullOrEmpty(creationData.userId);
            if (isPlayer)
            {
                worldEntity.Add(new GameFramework.World.Ownership(creationData.userId));
                worldEntity.Add(new InputBuffer());
            }
            worldEntity.Add(new Abilities());
            worldEntity.Add(new StatusEffects());
            worldEntity.Add(new MotionContributions());
            worldEntity.Add(new GameFramework.World.Simulated());   // 서버는 모든 캐릭터를 시뮬
            entityRegistry.Add(worldEntity);

            abilitySystem.Grant(worldEntity, 1);
            abilitySystem.Grant(worldEntity, 2);   // dash
            abilitySystem.Grant(worldEntity, 3);   // attack
            if (isPlayer)
            {
                abilitySystem.Grant(worldEntity, 4);
            }

            // 앵커: 뷰(물리/테스트렌더/AI)는 EntityViewSpawner가 EntityCreated 반응으로 붙인다.
            GameObject root = new GameObject($"Actor_{creationData.entityId}");
            LOPActor entity = root.AddComponent<LOPActor>();
            objectResolver.Inject(entity);
            entity.Initialize(creationData);

            Debug.Log($"[World] Registered entity {worldEntity.Id} Health={worldHealth.Current}/{worldHealth.Max}");
            return entity;
        }
    }
}
```

- [ ] **Step 3: 서버 `ItemCreator.cs` 축소 (뷰 제거)**

전체를 아래로 교체:

```csharp
using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class ItemCreator : IEntityCreator<LOPActor, ItemCreationData>
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        public LOPActor Create(ItemCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Item));
            worldEntity.Add(new MasterDataRef(creationData.itemCode));
            worldEntity.Add(new Appearance(creationData.visualId));
            entityRegistry.Add(worldEntity);

            GameObject root = new GameObject($"Actor_{creationData.entityId}");
            LOPActor entity = root.AddComponent<LOPActor>();
            objectResolver.Inject(entity);
            entity.Initialize(creationData);
            return entity;
        }
    }
}
```

- [ ] **Step 4: 서버 `EntityViewSpawner.cs` 신설**

`Assets/Scripts/Game/EntityViewSpawner.cs` 생성(Step 1에서 확인한 entry-point 인터페이스로):

```csharp
using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using System;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 서버 뷰 스포너 — 엔티티 수명 신호(<see cref="EntityCreated"/>)에 반응해 서버측 Unity 뷰를 부착한다:
    /// 물리 팔로워 + PhysicsBody + 테스트렌더 뷰(+ 비-플레이어에 AIController). Creator는 데이터+앵커만.
    /// (서버는 장식 뷰·보간 없음 — 권위 시뮬.)
    /// </summary>
    public class EntityViewSpawner : IGameMessageHandler
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private ISubscriber<EntityCreated> entityCreatedSubscriber;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        private IDisposable subscription;

        public void Initialize()
        {
            subscription = entityCreatedSubscriber.Subscribe(OnEntityCreated);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            LOPActor entity = entityCreated.entity;
            if (entity == null)
            {
                return;
            }
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entity.entityId);
            if (worldEntity == null)
            {
                return;
            }
            EntityKind kind = worldEntity.Get<EntityKind>();
            if (kind == null)
            {
                return;
            }

            GameObject root = entity.gameObject;
            bool isItem = kind.Kind == EntityType.Item;

            PhysicsFollower physicsFollower = root.AddComponent<PhysicsFollower>();
            objectResolver.Inject(physicsFollower);
            physicsFollower.Initialize(worldEntity, true, isItem);
            worldEntity.Add(new PhysicsBody(physicsFollower.entityRigidbody, (CapsuleCollider)physicsFollower.entityColliders[0]));

            LOPEntityView view = root.AddComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(entity);

            if (kind.Kind == EntityType.Character)
            {
                bool isPlayer = worldEntity.Has<GameFramework.World.Ownership>();
                if (isPlayer == false)
                {
                    LOPAIController aiController = root.AddComponent<LOPAIController>();
                    objectResolver.Inject(aiController);
                    aiController.SetEntity(entity);
                    aiController.SetBrain(objectResolver.Resolve<EnemyBrain>());
                }
            }
        }
    }
}
```

> **동작 보존 확인**: 구 서버 `CharacterCreator`는 non-player에 `PhysicsFollower.Initialize(worldEntity, true, false)`(non-trigger). 스포너도 캐릭터는 `isItem=false`→non-trigger. 아이템은 구 `ItemCreator`가 `Initialize(worldEntity, true, true)`(trigger)였으니 스포너도 `isItem=true`→trigger. 일치.

- [ ] **Step 5: `GameLifetimeScope.cs`에 스포너 등록**

서버 `Assets/Scripts/Game/GameLifetimeScope.cs`의 entry point 등록부(`RegisterEntryPoint<GameInfoMessageHandler>()` 인근, 현 68-70행)에 추가:

```csharp
            builder.RegisterEntryPoint<GameInfoMessageHandler>();
            builder.RegisterEntryPoint<GameEntityMessageHandler>();
            builder.RegisterEntryPoint<GameInputMessageHandler>();
            builder.RegisterEntryPoint<EntityViewSpawner>();   // 서버 뷰 스포너(EntityCreated 반응)
```

> `ISubscriber<EntityCreated>` 해결성: 서버 매니저가 `IPublisher<EntityCreated>`를 이미 주입받아 발행하므로 MessagePipe 브로커가 등록돼 있음 → `ISubscriber<EntityCreated>`도 resolve됨. 컴파일/런타임에서 확인(미등록이면 브로커 등록 추가). `EnemyBrain`은 서버 스코프에 이미 등록(구 CharacterCreator가 `objectResolver.Resolve<EnemyBrain>()` 사용).

- [ ] **Step 6: 서버 컴파일 확인 (컨트롤러)**

브랜치 생성 후. UnityMCP `refresh_unity`+`read_console`(unity_instance=Server). Expected: 에러 0. 신설 `.cs`의 `.meta`는 컨트롤러가 refresh 후 커밋에 포함.

- [ ] **Step 7: 커밋 (Server)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/entity-s5b-view-spawner   # 없으면 생성
git add Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/EntityCreator/ItemCreator.cs Assets/Scripts/Game/EntityViewSpawner.cs Assets/Scripts/Game/EntityViewSpawner.cs.meta Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "refactor(entity): 서버 뷰 생성을 Creator→EntityViewSpawner로 (S5b Task2)"
```

- [ ] **Step 8: 인게임 스모크(사용자)** — 서버 스폰·AI 이동·테스트렌더·물리·아이템 무변화.

---

### Task 3: 어휘 rename (`entity` → `actor`)

`LOPActor` 타입인데 식별자가 `entity`/`entities`/`LOPEntities`인 것을 `actor`/`actors`로. 순수 기계적, 별도 커밋(리뷰 가독). **주의: `GameFramework.World.Entity` 타입 변수(`worldEntity`, `entityRegistry.All`의 `entity` 등)는 rename 금지** — 오직 `LOPActor` 타입 식별자만.

**Files:** 클·서 전반 — `LOPActor` 타입 지역변수/필드/파라미터/프로퍼티가 `entity` 어휘인 곳(예: 크리에이터의 `LOPActor entity`, 스포너의 `entity`, `RemoteEntityInterpolator.entity`, `LocalEntityInterpolator.entity`, `IPlayerContext.entity`/`.entityView`, `LOPEntityView.entity`, `EntityCreated.entity`, 서버 `LOPRunner.LOPEntities` 지역변수, `LOPAIController`/`DamageFloaterEmitter`/`CharacterNameplate`의 `entity`).

- [ ] **Step 1: 대상 grep + 판별**

각 레포에서 `\bentity\b`/`\bentities\b`/`\bLOPEntities\b` 식별자를 훑어 **타입이 `LOPActor`인 것만** 선별. `World.Entity`(=`worldEntity`, `entityRegistry` 순회의 `entity`)·`entityId`·`entityRegistry`·`EntityKind`·`EntityCreated`(타입명) 등은 제외. 애매하면 선언 타입으로 판별.

- [ ] **Step 2: rename 적용 (클)**

`LOPActor` 타입 식별자를 `actor`로. 공개 프로퍼티(`IPlayerContext.entity`→`.actor`, `RemoteEntityInterpolator.entity`→`.actor`, `EntityCreated.entity`→`.actor` 등)는 인터페이스/필드 선언 + 모든 소비처를 함께. 각 프로퍼티 rename은 소비처 전수 확인.

- [ ] **Step 3: 클 컴파일 확인 (컨트롤러)** — `read_console`(Client) 에러 0.

- [ ] **Step 4: rename 적용 (서)** — 클과 동일. `LOPRunner.cs`의 `LOPEntities` 지역변수도 `actors`로.

- [ ] **Step 5: 서 컴파일 확인 (컨트롤러)** — `read_console`(Server) 에러 0.

- [ ] **Step 6: 커밋 (클·서 각각)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client && git add -A && git commit -m "refactor(entity): entity→actor 어휘 rename (S5b Task3)"
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server && git add Assets/Scripts && git commit -m "refactor(entity): entity→actor 어휘 rename (S5b Task3)"
```

> Task 3는 순수 rename이라 인게임 스모크 불필요(컴파일 그린이면 동작 동일). 서버는 `git add Assets/Scripts`로 픽스처 회피.

---

## Self-Review (스펙 대비)

- **spec 두 역할(Creator=데이터+앵커 / 스포너=뷰)** → Task 1(클)·Task 2(서). ✓
- **spec 흐름(동기 발행, playerContext.entity=Creator / .entityView=스포너)** → Task 1 Step 1·3. ✓
- **spec 클·서 비대칭(클=EntityBinder 확장 / 서=신설)** → Task 1 / Task 2. ✓
- **spec PhysicsBody=스포너가 rb 뒤 구성(X)** → Task 1 Step 3·Task 2 Step 4. ✓
- **spec isUser/isPlayer 재판정(클=gameDataStore / 서=Ownership)** → Task 1 Step 3(gameDataStore)·Task 2 Step 4(Has<Ownership>). ✓
- **spec Kind 분기(Character/Item)** → 스포너 `kind.Kind`. ✓
- **spec 보류(entityMap/매니저/포트정화/GF 무변)** → 플랜은 매니저·GF·물리 무변, PhysicsBody 콘크리트 유지. ✓
- **spec 어휘 rename 별도 패스** → Task 3. ✓
- **spec 테스트=컴파일+인게임(EditMode 불가)** → 각 Task 컨트롤러 컴파일 + 스모크. ✓
- **위험: 동기 발행 의존** → Task 1/2가 스폰 스모크로 검출(뷰·PhysicsBody가 반환 전에 붙는지). ✓
- **위험: 서버 ISubscriber<EntityCreated> 등록** → Task 2 Step 5에서 확인 지시. ✓
- **위험: 구독자 순서(playerContext)** → PlayerHudCoordinator가 playerContext 미참조 확인됨(스펙 Open Decision 해소) — 비이슈. ✓
- **위험: entry-point 인터페이스** → Task 2 Step 1에서 서버 기존 핸들러 인터페이스 확인 지시. ✓

## 상태

플랜 작성 완료(2026-07-18). 실행 = subagent-driven(권장). 상태 원장 `docs/ROADMAP.md`.
spec `docs/superpowers/specs/2026-07-18-s5b-creator-view-spawner-design.md`.
