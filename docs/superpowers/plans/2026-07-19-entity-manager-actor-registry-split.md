# 엔티티 매니저 → ActorRegistry + EntitySpawner 분리 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 뚱뚱한 `LOPEntityManager`를 데이터 축(`EntitySpawner` — 출생/사망 조율)과 뷰 축(`ActorRegistry` 인덱스 + 통합 `EntityBinder` 리액티브 뷰 스포너)으로 갈라, "데이터가 대장 / 뷰는 반응 팔로워" 구조로 수렴한다. 클·서 양쪽 + GameFramework.

**Architecture:** 데이터 creator는 `World.Entity`만 조립·등록(actor 안 만듦). `EntitySpawner`가 `Spawn`(creator 호출 + `EntityCreated(id)` 방송) / `Despawn`(마킹) / `FlushDespawns`(registry.Remove + `EntityDestroyed(id)` 방송)만 하고 `LOPActor`/`ActorRegistry`를 **한 번도 참조하지 않는다**. `EntityBinder`가 `EntityCreated`/`EntityDestroyed`를 구독해 actor GameObject·뷰를 생성/파괴하고 `ActorRegistry`를 채우고 비운다. actor를 만지는 코드는 전부 `EntityBinder` 한 곳. GameFramework의 제네릭 3종(`IEntityManager`/`IEntityFactory`/`IEntityCreator`)과 `EntityFactory`는 삭제되고 `IRunner`/`RunnerBase`에서 `entityManager`가 제거된다.

**Tech Stack:** Unity (Assembly-CSharp, 클·서 각자) + `com.baegames.gameframework`(file: 패키지) + VContainer(DI) + MessagePipe(동기 pub/sub) + `GameFramework.World.EntityRegistry`(순수 C# 데이터 진실원본).

## Global Constraints

- **범위**: `LOPEntityManager`(클·서) → `ActorRegistry` + `EntitySpawner` 분해, 클 `EntityBinder`/서 `EntityViewSpawner`를 `EntityBinder`로 통일 + actor 생성/파괴 흡수, 데이터 creator를 순수 데이터로 축소, `EntityCreated` 페이로드 `LOPActor`→`entityId` flip, GameFramework 3 인터페이스 + `EntityFactory` 삭제, DI 재배선, 씬 컴포넌트 제거. **비범위**: `LOPActor` 공유 베이스 추출(spec §10), `PhysicsBody` 순수 포트화, `EntityRegistry` 자체 변경, 뷰 컴포넌트 내부 로직 변경.
- **동작 무변화(값 동치) 리팩터** — 게임플레이 변화 없음. 스폰/디스폰/보간/데미지/넉백/롤백/아이템 줍기가 현 동작과 **동일**해야 한다.
- **핵심 불변식**: `EntitySpawner`는 `LOPActor`/`ActorRegistry`를 참조하지 않는다. actor는 오직 `EntityBinder`에서만 생성/파괴/등록/해제.
- **World 타입 풀 한정(프로젝트 컨벤션)**: LOP 측 파일은 `using GameFramework.World;`를 추가하지 않고 World 타입을 항상 풀 네임스페이스로 쓴다 — `GameFramework.World.Entity`, `GameFramework.World.EntityRegistry`, `GameFramework.World.Ownership` 등. (`Component` 모호성 회피.)
- **`.meta` 규칙**: 새 `.cs`는 Unity가 생성한 `.meta`를 함께 커밋한다(Write 후 `refresh_unity`로 임포트). 삭제하는 `.cs`는 `.cs.meta`도 함께 `git rm`. `.meta`를 직접 만들거나 수정하지 않는다.
- **GameFramework는 file: 패키지** — 그 안의 `.cs`를 지우면 클·서에 stale CS2001이 남는다. GF 삭제 뒤 반드시 `refresh_unity`(scope=all, force)로 양쪽 재스캔([[deleting-package-files-cs2001]]).
- **UnityMCP 타깃팅**: 모든 UnityMCP 호출에 `unity_instance`를 명시한다. 클라 인스턴스 id는 `mcpforunity://instances`에서 `name == "LeagueOfPhysical-Client"`인 항목의 `id`(`Name@hash`, 해시 변동 가능). 서버 인스턴스는 사용자가 명시적으로 요청할 때만 조작한다.
- **Git**: main 직접 커밋 금지. 각 레포에서 피처 브랜치 작업. 커밋 메시지 말미:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`
- **테스트 인프라 제약(중요)**: `ActorRegistry`/`EntitySpawner`는 Assembly-CSharp에 있어(그리고 `LOPActor` MonoBehaviour / MessagePipe 퍼블리셔에 의존) **EditMode asmdef가 참조할 수 없다** — 프로젝트에 standalone .NET 테스트 도구도 아직 없다([[client-test-infra-constraint]], [[tdd-first-always]]). 따라서 이 리팩터의 안전망은 spec §13대로 **컴파일 클린 + 인게임 스모크 + 최종 whole-branch 리뷰**다. 러닝 불가한 EditMode 스텝은 넣지 않는다.
- **컴파일 그린 시점(레드 윈도우 인지)**: Task 1(GF)이 끝나면 GF는 그린이지만 클·서 use-side는 새 코드가 들어오기 전까지 **레드**다. 각 use-side 레포는 자기 Task 끝에서 그린이 된다(클=Task 2 끝, 서=Task 3 끝). 브랜치 전체 그린은 Task 3 끝. 이는 `IEntityManager`가 `RunnerBase`에 박혀 있어 불가피한 조율 창이다.

---

## File Structure

### GameFramework (`C:\Users\re5na\workspace\LOP\GameFramework`)
- **Delete:** `Runtime/Scripts/Entity/IEntityManager.cs` (+ `.meta`)
- **Delete:** `Runtime/Scripts/Entity/IEntityFactory.cs` (+ `.meta`)
- **Delete:** `Runtime/Scripts/Entity/IEntityCreator.cs` (+ `.meta`)
- **Delete:** `Runtime/Scripts/Entity/EntityFactory.cs` (+ `.meta`)
- **Modify:** `Runtime/Scripts/Game/IRunner.cs` — `IEntityManager entityManager { get; }` 멤버 제거
- **Modify:** `Runtime/Scripts/Game/RunnerBase.cs` — `entityManager` 프로퍼티 + `GetComponent<IEntityManager>()` + `entityManager = null` 제거

### Client (`C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client\Assets\Scripts`)
- **Create:** `Entity/ActorRegistry.cs` — id→LOPActor 인덱스(순수)
- **Create:** `Entity/EntitySpawner.cs` — 데이터 수명(Spawn/Despawn/FlushDespawns), actor 미참조
- **Modify:** `Entity/Event.Entity.cs` — `EntityCreated` 페이로드 `LOPActor actor`→`string entityId`
- **Modify:** `Entity/LOPActor.cs` — 제네릭 `Initialize` 제거, `SetEntityId(string)` 추가
- **Modify:** `EntityCreator/CharacterCreator.cs` — `IEntityCreator<,>` 제거, 데이터 전용 `void Create(CharacterCreationData)`, actor 앵커 생성 제거
- **Modify:** `EntityCreator/ItemCreator.cs` — 동형
- **Modify:** `Game/EntityBinder.cs` — actor GameObject 생성 흡수 + `ActorRegistry.Add` + `EntityDestroyed` 구독(파괴)
- **Modify:** `Game/PlayerHudCoordinator.cs` — `entityCreated.actor`→`entityCreated.entityId`
- **Modify:** `Game/LOPRunner.cs` — `entityManager` 프로퍼티 제거, `UpdateEntities()` 호출 제거, `DestroyMarkedEntities()`→`entitySpawner.FlushDespawns()`
- **Modify:** `Game/GameLifetimeScope.cs` — DI 재배선(creator concrete + EntitySpawner + ActorRegistry, IEntity* 제거), EntityBinder를 PlayerHudCoordinator 앞에 등록
- **Modify:** `Game/MessageHandler/Game.Entity.MessageHandler.cs` — `runner.entityManager` 사용을 `entitySpawner`/`actorRegistry`로
- **Modify:** `Game/MessageHandler/Game.Info.MessageHandler.cs` — `runner.entityManager.CreateEntity`→`entitySpawner.Spawn`
- **Delete:** `Entity/LOPEntityManager.cs` (+ `.meta`)
- **Scene:** `Assets/Scenes/LOPGame.unity` — 런너 GameObject의 `LOPEntityManager` 컴포넌트 제거(Editor)

### Server (`C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server\Assets\Scripts`)
- **Create:** `Entity/ActorRegistry.cs`
- **Create:** `Entity/EntitySpawner.cs` — + 서버 부가(`GenerateEntityId`, `userEntityMap`/`GetEntityIdByUserId`, despawn 와이어)
- **Modify:** `Entity/Event.Entity.cs` — `EntityCreated` 페이로드 flip (클라와 동일)
- **Modify:** `Entity/LOPActor.cs` — `SetEntityId`
- **Modify:** `EntityCreator/CharacterCreator.cs` / `EntityCreator/ItemCreator.cs` — 데이터 전용
- **Rename+Modify:** `Game/EntityViewSpawner.cs` → `Game/EntityBinder.cs` — actor 생성/파괴 흡수 + ActorRegistry + EntityDestroyed
- **Modify:** `Game/LOPRunner.cs` — `entityManager` 제거, `GetUserIdByEntityId`→Ownership 파생, `GetAllEntitySnaps`를 private로 이전, `GetEntityIdByUserId`/`FlushDespawns`→entitySpawner, `UpdateEntities` 제거
- **Modify:** `Game/GameRuleSystem.cs` — `IEntityManager`→`EntitySpawner` + registry 카운트/조회
- **Modify:** `World/DeathCascadeSystem.cs` — `IEntityManager`→`EntitySpawner`
- **Modify:** `Game/MessageHandler/Game.Info.MessageHandler.cs` — `GetAllEntityCreationDatas`를 private로, `GetEntityIdByUserId`→entitySpawner
- **Modify:** `Game/MessageHandler/Game.Entity.MessageHandler.cs` / `Game.Input.MessageHandler.cs` — `GetEntityIdByUserId`→entitySpawner
- **Modify:** `Game/GameLifetimeScope.cs` — DI 재배선 + EntityViewSpawner→EntityBinder + `[SerializeField] entityManager` 제거
- **Modify:** `RootLifetimeScope.cs` — `RegisterMessageBroker<EntityDestroyed>` 추가
- **Delete:** `Entity/LOPEntityManager.cs` (+ `.meta`)
- **Scene:** `Assets/Scenes/LOPGame.unity` — 런너/스코프의 `LOPEntityManager` 컴포넌트 제거(Editor) + 스코프 직렬화 필드 정리

---

## Task 1: GameFramework — 인터페이스 삭제 + Runner 디커플

**Files:**
- Delete: `Runtime/Scripts/Entity/IEntityManager.cs`, `IEntityFactory.cs`, `IEntityCreator.cs`, `EntityFactory.cs` (각 `.meta` 포함)
- Modify: `Runtime/Scripts/Game/IRunner.cs`
- Modify: `Runtime/Scripts/Game/RunnerBase.cs`

**Interfaces:**
- Consumes: (없음 — 삭제/축소만)
- Produces: `IRunner`/`RunnerBase`에서 `entityManager` 멤버가 사라진다. use-side(클·서 LOPRunner)는 더 이상 `base.entityManager`를 갖지 못한다 — Task 2/3가 `EntitySpawner` 주입으로 대체.

- [ ] **Step 1: GameFramework 피처 브랜치 생성**

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git checkout main && git pull
git checkout -b feature/entity-manager-actor-registry-split
```

- [ ] **Step 2: `IRunner`에서 entityManager 멤버 제거**

`Runtime/Scripts/Game/IRunner.cs`의 다음 줄을 삭제:

```csharp
        IEntityManager entityManager { get; }
```

(그 위 `IGameState gameState { get; }`와 아래 `ITickUpdater tickUpdater { get; }`는 유지.)

- [ ] **Step 3: `RunnerBase`에서 entityManager 프로퍼티·조회·해제 제거**

`Runtime/Scripts/Game/RunnerBase.cs`에서 3곳 수정.

3-1) 프로퍼티 선언 삭제:
```csharp
        public IEntityManager entityManager { get; private set; }
```
(위 `public IGameState gameState`, 아래 `public ITickUpdater tickUpdater { get; private set; }`는 유지.)

3-2) `InitializeAsync`에서 조회 줄 삭제:
```csharp
            entityManager = GetComponent<IEntityManager>() ?? throw new ArgumentNullException(nameof(IEntityManager));
```
(위 `tickUpdater = GetComponent<ITickUpdater>() ...`, 아래 `networkTime = CreateNetworkTime();`는 유지.)

3-3) `DeinitializeAsync`에서 해제 줄 삭제:
```csharp
            entityManager = null;
```
(`tickUpdater = null;`, `networkTime = null;`는 유지.)

- [ ] **Step 4: 4개 파일 삭제(.cs + .meta)**

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git rm Runtime/Scripts/Entity/IEntityManager.cs Runtime/Scripts/Entity/IEntityManager.cs.meta
git rm Runtime/Scripts/Entity/IEntityFactory.cs Runtime/Scripts/Entity/IEntityFactory.cs.meta
git rm Runtime/Scripts/Entity/IEntityCreator.cs Runtime/Scripts/Entity/IEntityCreator.cs.meta
git rm Runtime/Scripts/Entity/EntityFactory.cs Runtime/Scripts/Entity/EntityFactory.cs.meta
```

- [ ] **Step 5: GF에 잔여 참조가 없는지 확인**

Grep으로 `Runtime/Scripts` 안에 위 심볼이 남아있지 않은지 확인한다.
Run: `grep -rn "IEntityManager\|IEntityFactory\|IEntityCreator" /c/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts`
Expected: **출력 없음** (전부 use-side로 이동/삭제). 하나라도 남으면 그 파일을 먼저 정리.

- [ ] **Step 6: GameFramework 컴파일 확인**

클라 에디터가 GF를 file: 참조하므로, GF만 단독 컴파일 검증은 어렵다. 대신 여기선 텍스트 검증(Step 5)로 갈음하고, 실제 컴파일은 Task 2 Step에서 클라 에디터 `read_console`로 확인한다(GF 오류가 있으면 거기서 드러난다). GF 자체는 LOP를 참조하지 않으므로 Step 2~5로 충분.

- [ ] **Step 7: GameFramework 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/GameFramework
git add -A
git commit -m "refactor(entity): drop IEntityManager/IEntityFactory/IEntityCreator + EntityFactory; decouple Runner

RunnerBase/IRunner no longer expose entityManager. Use-sides move to
EntitySpawner (data lifecycle) + ActorRegistry (view index).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Client 마이그레이션

> 이 Task는 여러 파일을 한 원자 단위로 바꾼다. **중간 스텝은 컴파일 실패가 정상이다.** 첫 그린 체크포인트는 Step 16(컴파일 확인). Step 순서는 "새 파일 → 데이터 축 → 뷰 축 → 호출부 → DI → 씬/삭제 → 컴파일 → 스모크".

**Files:** (File Structure의 Client 항목 전체)

**Interfaces:**
- Consumes: Task 1 이후 `RunnerBase`에 `entityManager` 없음.
- Produces (Task 3가 참조하지 않음 — 클·서 각자 콘크리트이나 **모양은 동일**):
  - `ActorRegistry`: `void Add(LOPActor)`, `bool Remove(string)`, `LOPActor Get(string)`, `bool TryGet(string, out LOPActor)`, `bool Contains(string)`, `int Count`, `IEnumerable<LOPActor> All`.
  - `EntitySpawner`(클): `void Spawn(CharacterCreationData)`, `void Spawn(ItemCreationData)`, `void Despawn(string)`, `void FlushDespawns()`.
  - `EntityCreated { string entityId }`, `EntityDestroyed { string entityId }`.
  - `LOPActor.SetEntityId(string)`.

- [ ] **Step 1: 클라 피처 브랜치 생성 + 클라 인스턴스 id 확인**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git checkout -b feature/entity-manager-actor-registry-split
```
그리고 `mcpforunity://instances`를 읽어 `LeagueOfPhysical-Client@<hash>`를 확보(이후 모든 UnityMCP 호출에 `unity_instance`로 사용).

- [ ] **Step 2: `EntityCreated` 페이로드 flip**

`Assets/Scripts/Entity/Event.Entity.cs`의 `EntityCreated` 구조체를 교체:

```csharp
    public struct EntityCreated
    {
        public string entityId;
        public EntityCreated(string entityId)
        {
            this.entityId = entityId;
        }
    }
```

(`EntityDestroyed`는 이미 `string entityId`라 변경 없음.)

- [ ] **Step 3: `LOPActor`에 `SetEntityId` 추가, 제네릭 `Initialize` 제거**

`Assets/Scripts/Entity/LOPActor.cs`에서 `Initialize<TEntityCreationData>(...)` 메서드를 삭제하고 `SetEntityId`로 교체. 결과 파일:

```csharp
using UnityEngine;

namespace LOP
{
    public class LOPActor : MonoBehaviour
    {
        public string entityId { get; private set; }

        private LOPEntityView view;

        // 스포너(EntityBinder)가 actor를 만든 직후 id를 세팅한다.
        public void SetEntityId(string entityId)
        {
            this.entityId = entityId;
        }

        // 스포너가 뷰를 만든 뒤 등록한다(Actor 생성 시점엔 뷰가 아직 없음).
        public void SetView(LOPEntityView view)
        {
            this.view = view;
        }

        // 렌더되는 모델 GameObject. 뷰가 async 로드 전이거나 파괴됐으면 null.
        public GameObject visualGameObject => view != null ? view.visualGameObject : null;
    }
}
```

(더 이상 `using GameFramework;`가 필요 없으면 제거 — `IEntityCreationData`를 안 쓰므로.)

- [ ] **Step 4: `ActorRegistry` 생성**

`Assets/Scripts/Entity/ActorRegistry.cs` 생성:

```csharp
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// id→뷰 앵커(<see cref="LOPActor"/>) 인덱스. World <c>EntityRegistry</c>(id→데이터 진실원본)와 별개 축.
    /// 로직·조율 없는 dumb 인덱스 — <see cref="EntityBinder"/>가 채우고 비운다. 소비자는 서버 스냅 수신 등에서
    /// id→actor가 필요할 때 여기를 조회한다.
    /// </summary>
    public class ActorRegistry
    {
        private readonly Dictionary<string, LOPActor> actors = new Dictionary<string, LOPActor>();

        public void Add(LOPActor actor)
        {
            actors[actor.entityId] = actor;
        }

        public bool Remove(string entityId)
        {
            return actors.Remove(entityId);
        }

        public LOPActor Get(string entityId)
        {
            return actors.TryGetValue(entityId, out var actor) ? actor : null;
        }

        public bool TryGet(string entityId, out LOPActor actor)
        {
            return actors.TryGetValue(entityId, out actor);
        }

        public bool Contains(string entityId)
        {
            return actors.ContainsKey(entityId);
        }

        public int Count => actors.Count;

        public IEnumerable<LOPActor> All => actors.Values;
    }
}
```

- [ ] **Step 5: 클라 `EntitySpawner` 생성 (데이터 전용)**

`Assets/Scripts/Entity/EntitySpawner.cs` 생성:

```csharp
using LOP.Event.Entity;
using MessagePipe;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 데이터 수명(출생·사망) 조율 — 데이터만 만진다. <see cref="LOPActor"/>/<see cref="ActorRegistry"/>를
    /// 참조하지 않는다(핵심 불변식). Spawn은 데이터 creator를 호출해 World.Entity를 등록하고 "태어났다(id)"를,
    /// Despawn/FlushDespawns는 등록 해제 후 "죽었다(id)"를 방송한다 — actor 생성/파괴는 <see cref="EntityBinder"/> 반응.
    /// </summary>
    public class EntitySpawner
    {
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private CharacterCreator characterCreator;
        [Inject] private ItemCreator itemCreator;
        [Inject] private IPublisher<EntityCreated> entityCreatedPublisher;
        [Inject] private IPublisher<EntityDestroyed> entityDestroyedPublisher;

        private readonly HashSet<string> entitiesToDestroy = new HashSet<string>();

        public void Spawn(CharacterCreationData creationData)
        {
            characterCreator.Create(creationData);
            entityCreatedPublisher.Publish(new EntityCreated(creationData.entityId));
        }

        public void Spawn(ItemCreationData creationData)
        {
            itemCreator.Create(creationData);
            entityCreatedPublisher.Publish(new EntityCreated(creationData.entityId));
        }

        public void Despawn(string entityId)
        {
            entitiesToDestroy.Add(entityId);
        }

        // LOPRunner가 틱 끝에 호출. registry에서 제거하고 "죽었다"를 방송 → EntityBinder가 actor cleanup+파괴.
        public void FlushDespawns()
        {
            foreach (var entityId in entitiesToDestroy)
            {
                if (entityRegistry.Remove(entityId))
                {
                    Debug.Log($"[World] Unregistered entity {entityId}");
                }

                entityDestroyedPublisher.Publish(new EntityDestroyed(entityId));
            }

            entitiesToDestroy.Clear();
        }
    }
}
```

- [ ] **Step 6: 클라 `CharacterCreator`를 데이터 전용으로 축소**

`Assets/Scripts/EntityCreator/CharacterCreator.cs`를 교체. `IEntityCreator<,>` 제거, `void Create`, actor 앵커 생성(GameObject/AddComponent/Inject/Initialize) 삭제, `objectResolver` inject 삭제. `playerContext.entityId` 세팅(유저 엔티티)은 **유지**(데이터 시점 배선).

```csharp
using UnityEngine;
using VContainer;

namespace LOP
{
    // 데이터 전용 creator — World.Entity 조립 + registry.Add + 어빌리티 Grant. actor(뷰 앵커)는 EntityBinder가 만든다.
    public class CharacterCreator
    {
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IPlayerContext playerContext;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private AbilitySystem abilitySystem;
        [Inject] private LOP.MasterData.LOPMasterData md;

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
                playerContext.entityId = creationData.entityId;   // .actor는 EntityBinder가 뷰 생성 후 세팅
            }

            Debug.Log($"[World] Registered entity {worldEntity.Id} Health={worldHealth.Current}/{worldHealth.Max}");
        }
    }
}
```

> 주: 원본은 `playerContext.entityId`를 `isUserEntity` 별도 블록에서 세팅했다. 위처럼 어빌리티 Grant의 `isUserEntity` 블록으로 합쳐도 값 동치(둘 다 유저일 때 1회). `using GameFramework;`는 더 이상 필요 없으면 제거.

- [ ] **Step 7: 클라 `ItemCreator`를 데이터 전용으로 축소**

`Assets/Scripts/EntityCreator/ItemCreator.cs` 교체:

```csharp
using UnityEngine;
using VContainer;

namespace LOP
{
    public class ItemCreator
    {
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        public void Create(ItemCreationData creationData)
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
        }
    }
}
```

- [ ] **Step 8: 클라 `EntityBinder`가 actor 생성/파괴를 흡수**

`Assets/Scripts/Game/EntityBinder.cs` 교체. `EntityCreated`에서 actor를 **직접 생성**(구 creator 앵커 로직 이관) + `ActorRegistry.Add`, `EntityDestroyed`에서 cleanup+파괴 + `ActorRegistry.Remove`.

```csharp
using LOP.Event.Entity;
using MessagePipe;
using System;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 엔티티 수명 신호(<see cref="EntityCreated"/>/<see cref="EntityDestroyed"/>)에 반응해 actor GameObject와
    /// 모든 Unity 뷰를 생성·연결·파괴한다(분리형 뷰 스포너 — ECS/Entitas 뷰 리졸버). Creator는 데이터만 만든다.
    /// </summary>
    public class EntityBinder : IGameMessageHandler
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private ISubscriber<EntityCreated> entityCreatedSubscriber;
        [Inject] private ISubscriber<EntityDestroyed> entityDestroyedSubscriber;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private ActorRegistry actorRegistry;
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IPlayerContext playerContext;

        private IDisposable subscriptions;

        public void Initialize()
        {
            var bag = DisposableBag.CreateBuilder();
            entityCreatedSubscriber.Subscribe(OnEntityCreated).AddTo(bag);
            entityDestroyedSubscriber.Subscribe(OnEntityDestroyed).AddTo(bag);
            subscriptions = bag.Build();
        }

        public void Dispose()
        {
            subscriptions?.Dispose();
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entityCreated.entityId);
            if (worldEntity == null)
            {
                return;
            }
            EntityKind kind = worldEntity.Get<EntityKind>();
            if (kind == null)
            {
                return;
            }

            // 앵커 GameObject + LOPActor 생성(구 creator 말미 로직 이관).
            GameObject root = new GameObject($"Actor_{entityCreated.entityId}");
            LOPActor actor = root.AddComponent<LOPActor>();
            objectResolver.Inject(actor);
            actor.SetEntityId(entityCreated.entityId);
            actorRegistry.Add(actor);

            bool isItem = kind.Kind == EntityType.Item;

            // 물리 팔로워 + PhysicsBody (모든 엔티티 공통). 아이템=trigger, 캐릭터=non-trigger.
            PhysicsFollower physicsFollower = root.AddComponent<PhysicsFollower>();
            objectResolver.Inject(physicsFollower);
            physicsFollower.Initialize(worldEntity, true, isItem);
            worldEntity.Add(new PhysicsBody(physicsFollower.entityRigidbody, (CapsuleCollider)physicsFollower.entityColliders[0]));

            LOPEntityView view = root.AddComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntityId(entityCreated.entityId);
            actor.SetView(view);

            if (kind.Kind == EntityType.Character)
            {
                bool isUserEntity = gameDataStore.userEntityId == entityCreated.entityId;
                if (isUserEntity)
                {
                    playerContext.actor = actor;

                    LocalEntityInterpolator interpolator = root.AddComponent<LocalEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.actor = actor;
                }
                else
                {
                    RemoteEntityInterpolator interpolator = root.AddComponent<RemoteEntityInterpolator>();
                    objectResolver.Inject(interpolator);
                    interpolator.worldEntity = worldEntity;
                    interpolator.actor = actor;
                }

                // 장식 뷰(캐릭터만).
                DamageFloaterEmitter damageFloaterEmitter = root.AddComponent<DamageFloaterEmitter>();
                objectResolver.Inject(damageFloaterEmitter);
                damageFloaterEmitter.SetEntity(actor);

                CharacterNameplate nameplate = root.AddComponent<CharacterNameplate>();
                objectResolver.Inject(nameplate);
                nameplate.SetEntity(actor);
            }
            else
            {
                // 아이템: 원격 보간만(내 예측 대상 아님).
                RemoteEntityInterpolator interpolator = root.AddComponent<RemoteEntityInterpolator>();
                objectResolver.Inject(interpolator);
                interpolator.worldEntity = worldEntity;
                interpolator.actor = actor;
            }
        }

        private void OnEntityDestroyed(EntityDestroyed entityDestroyed)
        {
            if (actorRegistry.TryGet(entityDestroyed.entityId, out var actor) == false)
            {
                return;
            }

            foreach (var cleanup in actor.transform.GetComponentsInChildren<ICleanup>(true))
            {
                cleanup.Cleanup();
            }

            actorRegistry.Remove(entityDestroyed.entityId);
            UnityEngine.Object.Destroy(actor.gameObject);
        }
    }
}
```

> 주: `ICleanup`가 `GameFramework` 네임스페이스라면 `using GameFramework;`를 상단에 추가(원본 EntityBinder에 있었음). 컴파일 오류 시 추가.

- [ ] **Step 9: 클라 `PlayerHudCoordinator` 페이로드 반영**

`Assets/Scripts/Game/PlayerHudCoordinator.cs`의 `OnEntityCreated`를 교체(`entityCreated.actor` → `entityCreated.entityId`):

```csharp
        private void OnEntityCreated(EntityCreated entityCreated)
        {
            if (_opened || entityCreated.entityId != gameDataStore.userEntityId)
            {
                return;
            }

            // GamePad를 먼저 열어 Window 밴드 최하단에 깐다(전체화면 카메라 드래그 배경이 위 UI 위젯 입력을 막지 않도록).
            windowManager.Open<GamePadView>();
            windowManager.Open<StatsView>();
            windowManager.Open<CharacterHudView>();
            windowManager.Open<DebugHudView>();
            _opened = true;
        }
```

- [ ] **Step 10: 클라 `Game.Entity.MessageHandler` 호출부 교체**

`Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs`:

10-1) inject 필드 교체 — `[Inject] private IRunner runner;`를 제거하고 다음 둘을 추가:
```csharp
        [Inject] private EntitySpawner entitySpawner;
        [Inject] private ActorRegistry actorRegistry;
```
(단, `Runner.current`는 정적 접근이라 `runner` 필드 없이도 동작 — 그대로 `Runner.current`.)

10-2) `OnEntitySnapsToC`의 조회 교체(line ~70):
```csharp
                if (actorRegistry.TryGet(serverEntitySnap.EntityId, out var actor) == false)
```

10-3) `OnEntitySpawnToC`의 두 `runner.entityManager.CreateEntity<LOPActor, …>(…)`를 `entitySpawner.Spawn(…)`로:
- `runner.entityManager.CreateEntity<LOPActor, CharacterCreationData>(new CharacterCreationData { … });` → `entitySpawner.Spawn(new CharacterCreationData { … });`
- `runner.entityManager.CreateEntity<LOPActor, ItemCreationData>(new ItemCreationData { … });` → `entitySpawner.Spawn(new ItemCreationData { … });`
(중괄호 안 필드는 그대로.)

10-4) `OnEntityDespawnToC`(line ~172):
```csharp
                entitySpawner.Despawn(entityDespawnToC.EntityId);
```

- [ ] **Step 11: 클라 `Game.Info.MessageHandler` 호출부 교체**

`Assets/Scripts/Game/MessageHandler/Game.Info.MessageHandler.cs`:

11-1) `[Inject] private IRunner runner;`를 제거하고 추가:
```csharp
        [Inject] private EntitySpawner entitySpawner;
```

11-2) 두 `runner.entityManager.CreateEntity<LOPActor, …>(…)`를 `entitySpawner.Spawn(…)`로 교체(Step 10-3과 동형).

(다른 inject — `playerInputManager`, `matchSeed`, `gameInfoSubscriber` — 및 마지막 `playerInputManager.SetSequenceNumber(...)`는 유지.)

- [ ] **Step 12: 클라 `LOPRunner` 정리**

`Assets/Scripts/Game/LOPRunner.cs`:

12-1) `EntitySpawner` inject 추가(예: 기존 `[Inject] private Reconciler reconciler;` 아래):
```csharp
        [Inject] private EntitySpawner entitySpawner;
```

12-2) `entityManager` 프로퍼티 삭제:
```csharp
        public new LOPEntityManager entityManager => base.entityManager as LOPEntityManager;
```

12-3) `UpdateEntity()`에서 `entityManager.UpdateEntities();` 줄 삭제(주변 `DispatchEvent<BeforeEntityUpdate>()`/`AfterEntityUpdate`는 유지). 결과:
```csharp
        private void UpdateEntity()
        {
            DispatchEvent<BeforeEntityUpdate>();

            DispatchEvent<AfterEntityUpdate>();
        }
```

12-4) `EndUpdate()`의 `entityManager.DestroyMarkedEntities();` → `entitySpawner.FlushDespawns();`.

- [ ] **Step 13: 클라 `GameLifetimeScope` DI 재배선**

`Assets/Scripts/Game/GameLifetimeScope.cs`의 `Configure` 안:

13-1) 기존 3줄 삭제:
```csharp
            builder.Register<IEntityCreator, CharacterCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreator, ItemCreator>(Lifetime.Singleton);
            builder.Register<IEntityFactory, EntityFactory>(Lifetime.Singleton);
```

13-2) 대체 등록 추가(같은 자리):
```csharp
            builder.Register<CharacterCreator>(Lifetime.Singleton);
            builder.Register<ItemCreator>(Lifetime.Singleton);
            builder.Register<EntitySpawner>(Lifetime.Singleton);
            builder.Register<ActorRegistry>(Lifetime.Singleton);
```

13-3) EntryPoint 등록 순서: `EntityBinder`가 `PlayerHudCoordinator`보다 **먼저** Initialize되도록 순서를 바꾼다(spec §11 — 구독 순서 안전장치). 즉 기존:
```csharp
            builder.RegisterEntryPoint<PlayerHudCoordinator>();
            builder.RegisterEntryPoint<EntityBinder>();
```
를
```csharp
            builder.RegisterEntryPoint<EntityBinder>();
            builder.RegisterEntryPoint<PlayerHudCoordinator>();
```
로.

- [ ] **Step 14: 클라 `LOPEntityManager` 삭제**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git rm Assets/Scripts/Entity/LOPEntityManager.cs Assets/Scripts/Entity/LOPEntityManager.cs.meta
```

- [ ] **Step 15: 씬에서 `LOPEntityManager` 컴포넌트 제거 (Editor)**

클라 Unity 에디터에서 `Assets/Scenes/LOPGame.unity`를 열고, 런너 GameObject(LOPRunner가 붙은 오브젝트)에서 이제 "Missing (Mono Script)"로 표시되는 (구)LOPEntityManager 컴포넌트를 제거한 뒤 씬 저장.
- UnityMCP로 수행: `manage_scene`으로 `LOPGame` 로드 → `find_gameobjects`로 런너 오브젝트 찾기 → `manage_gameobject`(remove_component / 또는 missing script 제거) → 씬 저장. 각 호출에 `unity_instance="LeagueOfPhysical-Client@<hash>"`.
- 검증: `Assets/Scenes/LOPGame.unity`에 GUID `ee3ec6a7a4895ef4089642d3b3d9a1cb` 참조가 **0**이어야 한다.
  Run: `grep -c "ee3ec6a7a4895ef4089642d3b3d9a1cb" Assets/Scenes/LOPGame.unity`
  Expected: `0`

- [ ] **Step 16: 클라 컴파일 확인 (첫 그린 체크포인트)**

새 파일 임포트 + 재컴파일: `refresh_unity`(scope=all, force, `unity_instance` 클라) → `editor_state.isCompiling`이 false 될 때까지 대기 → `read_console`(errors 필터, `unity_instance` 클라).
Expected: **컴파일 에러 0.** (GF 오류가 남아 있으면 Task 1을 재확인.)
자주 나오는 잔여 에러와 대응:
- `IEntityManager/IEntityFactory/IEntityCreator` 미해결 → 놓친 호출부. `grep -rn "IEntityManager\|IEntityFactory\|IEntityCreator\|entityManager\|CreateEntity<\|DeleteEntityById" Assets/Scripts`로 남은 곳 확인.
- `ICleanup` 미해결 → EntityBinder 상단에 `using GameFramework;` 추가.

- [ ] **Step 17: 클라 스모크 테스트 (사용자와 함께)**

클라+서버를 띄워 다음이 **현 동작과 동일**한지 육안 확인(값 동치 리팩터):
1. 스폰: 내 캐릭·적(AI)·아이템이 올바른 위치/외형으로 생성.
2. 원격 보간: 다른 엔티티가 부드럽게 따라옴.
3. 디스폰: 죽거나 아이템 주우면 GameObject가 사라짐(missing/leak 없음).
4. 아이템 줍기 → 경험치, 넉백/데미지 연출, HP바, 롤백(공중 점프) 정상.
5. HUD: 로컬 유저 스폰 시 GamePad/Stats/CharacterHud/DebugHud가 1회 열림.
Run(콘솔): `read_console`(errors/warnings, `unity_instance` 클라) — 런타임 예외 0.

- [ ] **Step 18: 클라 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add -A
git commit -m "refactor(entity): split LOPEntityManager into EntitySpawner + ActorRegistry (client)

Data lifecycle (EntitySpawner: Spawn/Despawn/FlushDespawns, actor-free) vs
view lifecycle (EntityBinder creates/destroys actor + views, ActorRegistry index).
Creators reduced to data-only. EntityCreated payload flipped actor->entityId.
GameFramework IEntity* interfaces retired.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Server 마이그레이션

> 클라(Task 2)와 대칭이되 **서버 부가**(id 생성·userId 매핑·despawn 와이어·스냅/생성데이터 브로드캐스트)를 담는다. 중간 스텝은 컴파일 실패 정상, 첫 그린은 Step 20.

**Files:** (File Structure의 Server 항목 전체)

**Interfaces:**
- Consumes: Task 1(GF `entityManager` 없음).
- Produces (서버 콘크리트):
  - `ActorRegistry`(클라와 동일 모양).
  - `EntitySpawner`(서버): `void Spawn(CharacterCreationData)`, `void Spawn(ItemCreationData)`, `void Despawn(string)`, `void FlushDespawns()`, `string GenerateEntityId()`, `string GetEntityIdByUserId(string userId)`.

- [ ] **Step 1: 서버 피처 브랜치 생성 + 서버 인스턴스 id 확인**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/entity-manager-actor-registry-split
```
(서버 UnityMCP 조작은 사용자 확인 하에. `mcpforunity://instances`에서 서버 인스턴스 id 확보.)

- [ ] **Step 2: 서버 `EntityCreated` 페이로드 flip**

`Assets/Scripts/Entity/Event.Entity.cs`의 `EntityCreated`를 Task 2 Step 2와 **동일**하게 교체:
```csharp
    public struct EntityCreated
    {
        public string entityId;
        public EntityCreated(string entityId)
        {
            this.entityId = entityId;
        }
    }
```

- [ ] **Step 3: 서버 `LOPActor`에 `SetEntityId` 추가, `Initialize` 제거**

`Assets/Scripts/Entity/LOPActor.cs` 교체:
```csharp
namespace LOP
{
    public class LOPActor : MonoBehaviour
    {
        public string entityId { get; private set; }

        public void SetEntityId(string entityId)
        {
            this.entityId = entityId;
        }
    }
}
```
(`using` 정리 — `GameFramework`/`UnityEngine`이 필요한지 확인. `MonoBehaviour` 때문에 `using UnityEngine;`는 유지.)

- [ ] **Step 4: 서버 `EntityDestroyed` 브로커 등록**

`Assets/Scripts/RootLifetimeScope.cs`에서 `RegisterMessageBroker<Event.Entity.EntityCreated>(options);` 아래에 추가:
```csharp
            builder.RegisterMessageBroker<Event.Entity.EntityDestroyed>(options);
```
(서버가 `EntityDestroyed`를 처음으로 방송·구독하므로 브로커 필수.)

- [ ] **Step 5: 서버 `ActorRegistry` 생성**

`Assets/Scripts/Entity/ActorRegistry.cs` 생성 — Task 2 Step 4와 **동일 내용**(클·서 각자 콘크리트, 모양 동일).

- [ ] **Step 6: 서버 `EntitySpawner` 생성 (데이터 전용 + 서버 부가)**

`Assets/Scripts/Entity/EntitySpawner.cs` 생성:

```csharp
using LOP.Event.Entity;
using MessagePipe;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 서버 데이터 수명(출생·사망) 조율 — 데이터만 만진다. <see cref="LOPActor"/>/<see cref="ActorRegistry"/> 미참조.
    /// 서버 부가: entityId 발급, userId↔entityId 매핑(스폰 수명 결합), despawn 와이어 송신.
    /// </summary>
    public class EntitySpawner
    {
        [Inject] private ISessionManager sessionManager;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private CharacterCreator characterCreator;
        [Inject] private ItemCreator itemCreator;
        [Inject] private IPublisher<EntityCreated> entityCreatedPublisher;
        [Inject] private IPublisher<EntityDestroyed> entityDestroyedPublisher;

        private readonly Dictionary<string, string> userEntityMap = new Dictionary<string, string>();
        private readonly HashSet<string> entitiesToDestroy = new HashSet<string>();
        private int entityIdCounter = 1;

        public string GenerateEntityId()
        {
            return (entityIdCounter++).ToString();
        }

        // userId→entityId. 순수 로직 사이트가 LOPActor를 거치지 않고 id만 얻는다.
        public string GetEntityIdByUserId(string userId)
        {
            return userEntityMap[userId];
        }

        public void Spawn(CharacterCreationData creationData)
        {
            characterCreator.Create(creationData);
            entityCreatedPublisher.Publish(new EntityCreated(creationData.entityId));

            if (string.IsNullOrEmpty(creationData.userId) == false)
            {
                userEntityMap[creationData.userId] = creationData.entityId;
            }
        }

        public void Spawn(ItemCreationData creationData)
        {
            itemCreator.Create(creationData);
            entityCreatedPublisher.Publish(new EntityCreated(creationData.entityId));
        }

        public void Despawn(string entityId)
        {
            entitiesToDestroy.Add(entityId);
        }

        // LOPRunner가 틱 끝에 호출. registry 제거 + "죽었다" 방송(EntityBinder가 actor 파괴) + userMap 정리 + despawn 와이어.
        public void FlushDespawns()
        {
            foreach (var entityId in entitiesToDestroy)
            {
                // Ownership은 registry.Remove 전에 읽는다(제거 후엔 사라짐).
                string ownerId = entityRegistry.Get(entityId)?.Get<GameFramework.World.Ownership>()?.OwnerId;

                if (entityRegistry.Remove(entityId))
                {
                    Debug.Log($"[World] Unregistered entity {entityId}");
                }

                entityDestroyedPublisher.Publish(new EntityDestroyed(entityId));

                if (ownerId != null)
                {
                    userEntityMap.Remove(ownerId);
                }

                foreach (var session in sessionManager.GetAllSessions().DefaultIfEmpty())
                {
                    session.Send(new EntityDespawnToC
                    {
                        EntityId = entityId,
                    });
                }
            }

            entitiesToDestroy.Clear();
        }
    }
}
```

> `.DefaultIfEmpty()`/`.OrEmpty()` 사용을 위해 `System.Linq` + 프로젝트 확장(`OrEmpty`는 GameFramework 확장)이 필요. 원본 `DestroyMarkedEntities`가 `GetAllSessions().DefaultIfEmpty()`를 썼으니 동일 유지. 컴파일 오류 시 `using GameFramework;` 추가.

- [ ] **Step 7: 서버 `CharacterCreator` 데이터 전용 축소**

`Assets/Scripts/EntityCreator/CharacterCreator.cs` 교체 — `IEntityCreator<,>` 제거, `void Create`, actor 앵커 생성(GameObject/AddComponent/Inject/Initialize) 삭제, `objectResolver` inject 삭제. 나머지(Ownership/InputBuffer/Simulated/어빌리티 Grant)는 유지:

```csharp
using UnityEngine;
using VContainer;

namespace LOP
{
    public class CharacterCreator
    {
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private AbilitySystem abilitySystem;
        [Inject] private LOP.MasterData.LOPMasterData md;

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

            Debug.Log($"[World] Registered entity {worldEntity.Id} Health={worldHealth.Current}/{worldHealth.Max}");
        }
    }
}
```

- [ ] **Step 8: 서버 `ItemCreator` 데이터 전용 축소**

`Assets/Scripts/EntityCreator/ItemCreator.cs` 교체:
```csharp
using UnityEngine;
using VContainer;

namespace LOP
{
    public class ItemCreator
    {
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        public void Create(ItemCreationData creationData)
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
        }
    }
}
```

- [ ] **Step 9: 서버 `EntityViewSpawner` → `EntityBinder` 리네임 + actor 생성/파괴 흡수**

9-1) 파일 리네임(git이 히스토리 추적하도록):
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git mv Assets/Scripts/Game/EntityViewSpawner.cs Assets/Scripts/Game/EntityBinder.cs
git mv Assets/Scripts/Game/EntityViewSpawner.cs.meta Assets/Scripts/Game/EntityBinder.cs.meta
```

9-2) `Assets/Scripts/Game/EntityBinder.cs` 내용 교체(클래스명도 `EntityBinder`):

```csharp
using LOP.Event.Entity;
using MessagePipe;
using System;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 서버 뷰 스포너 — 엔티티 수명 신호(<see cref="EntityCreated"/>/<see cref="EntityDestroyed"/>)에 반응해
    /// actor GameObject + 서버측 Unity 뷰(물리 팔로워 + PhysicsBody + 테스트렌더 뷰 + 비-플레이어 AIController)를
    /// 생성·파괴한다. Creator는 데이터만. (서버는 장식 뷰·보간 없음 — 권위 시뮬.)
    /// </summary>
    public class EntityBinder : IGameMessageHandler
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private ISubscriber<EntityCreated> entityCreatedSubscriber;
        [Inject] private ISubscriber<EntityDestroyed> entityDestroyedSubscriber;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private ActorRegistry actorRegistry;

        private IDisposable subscriptions;

        public void Initialize()
        {
            var bag = DisposableBag.CreateBuilder();
            entityCreatedSubscriber.Subscribe(OnEntityCreated).AddTo(bag);
            entityDestroyedSubscriber.Subscribe(OnEntityDestroyed).AddTo(bag);
            subscriptions = bag.Build();
        }

        public void Dispose()
        {
            subscriptions?.Dispose();
        }

        private void OnEntityCreated(EntityCreated entityCreated)
        {
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entityCreated.entityId);
            if (worldEntity == null)
            {
                return;
            }
            EntityKind kind = worldEntity.Get<EntityKind>();
            if (kind == null)
            {
                return;
            }

            GameObject root = new GameObject($"Actor_{entityCreated.entityId}");
            LOPActor actor = root.AddComponent<LOPActor>();
            objectResolver.Inject(actor);
            actor.SetEntityId(entityCreated.entityId);
            actorRegistry.Add(actor);

            bool isItem = kind.Kind == EntityType.Item;

            PhysicsFollower physicsFollower = root.AddComponent<PhysicsFollower>();
            objectResolver.Inject(physicsFollower);
            physicsFollower.Initialize(worldEntity, true, isItem);
            worldEntity.Add(new PhysicsBody(physicsFollower.entityRigidbody, (CapsuleCollider)physicsFollower.entityColliders[0]));

            LOPEntityView view = root.AddComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(actor);

            if (kind.Kind == EntityType.Character)
            {
                bool isPlayer = worldEntity.Has<GameFramework.World.Ownership>();
                if (isPlayer == false)
                {
                    LOPAIController aiController = root.AddComponent<LOPAIController>();
                    objectResolver.Inject(aiController);
                    aiController.SetEntity(actor);
                    aiController.SetBrain(objectResolver.Resolve<EnemyBrain>());
                }
            }
        }

        private void OnEntityDestroyed(EntityDestroyed entityDestroyed)
        {
            if (actorRegistry.TryGet(entityDestroyed.entityId, out var actor) == false)
            {
                return;
            }

            foreach (var cleanup in actor.transform.GetComponentsInChildren<ICleanup>(true))
            {
                cleanup.Cleanup();
            }

            actorRegistry.Remove(entityDestroyed.entityId);
            UnityEngine.Object.Destroy(actor.gameObject);
        }
    }
}
```

> `ICleanup`가 `GameFramework`면 `using GameFramework;` 추가.

- [ ] **Step 10: 서버 `GameRuleSystem` — IEntityManager→EntitySpawner + registry 조회**

`Assets/Scripts/Game/GameRuleSystem.cs`:

10-1) 필드 타입 교체:
```csharp
        private readonly EntitySpawner entitySpawner;
```
(구 `private readonly IEntityManager entityManager;` 대체.)

10-2) 생성자 파라미터/대입 교체: ctor의 `IEntityManager entityManager` → `EntitySpawner entitySpawner`, `this.entityManager = entityManager;` → `this.entitySpawner = entitySpawner;`.

10-3) `OnTick`의 엔티티 수 카운트(구 `entityManager.GetEntities().Count() < 100`):
```csharp
                if (entityRegistry.All.Count() < 100)
```
(`entityRegistry`는 이미 필드로 주입됨. `System.Linq` 이미 using.)

10-4) `CreateInitialPlayers`/`SpawnEnemy`의 `entityManager.GenerateEntityId()` → `entitySpawner.GenerateEntityId()`(2곳).

10-5) `entityManager.CreateEntity<LOPActor, CharacterCreationData>(data)` → `entitySpawner.Spawn(data)`(2곳). 반환값을 쓰던 `SpawnEnemy`(line ~203)에서 이어지는 `entityRegistry.Get(actor.entityId)`를 `entityRegistry.Get(data.entityId)`로 교체(actor 반환 없음). `CreateInitialPlayers`의 `LOPActor actor = ...;`는 미사용 로컬이었으니 그냥 `entitySpawner.Spawn(data);`로.

10-6) `HandleItemTouch`의 `entityManager.GetEntity(itemTouch.itemId) != null`:
```csharp
            if (entityRegistry.Get(itemTouch.itemId) != null)
```

10-7) `DespawnEntity`의 `entityManager.DeleteEntityById(entityId)` → `entitySpawner.Despawn(entityId)`.

- [ ] **Step 11: 서버 `DeathCascadeSystem` — IEntityManager→EntitySpawner**

`Assets/Scripts/World/DeathCascadeSystem.cs`:

11-1) 필드 `private readonly IEntityManager _entityManager;` → `private readonly EntitySpawner _entitySpawner;`. ctor 파라미터/대입 동일 교체.

11-2) `ResolveDeath`의 `_entityManager.DeleteEntityById(death.victimId)` → `_entitySpawner.Despawn(death.victimId)`.

11-3) `SpawnExpMarble`의 `_entityManager.GenerateEntityId()` → `_entitySpawner.GenerateEntityId()`; `LOPActor actor = _entityManager.CreateEntity<LOPActor, ItemCreationData>(data)` → `_entitySpawner.Spawn(data)`; 이어지는 `_entityRegistry.Get(actor.entityId)` → `_entityRegistry.Get(data.entityId)`.

- [ ] **Step 12: 서버 `Game.Info.MessageHandler` — GetAllEntityCreationDatas 내재화 + GetEntityIdByUserId→entitySpawner**

`Assets/Scripts/Game/MessageHandler/Game.Info.MessageHandler.cs`:

12-1) inject 추가:
```csharp
        [Inject] private IEntityCreationDataFactory entityCreationDataFactory;
        [Inject] private EntitySpawner entitySpawner;
```
(`runner`는 `AddListener`/`RemoveListener`에 쓰이므로 **유지**.)

12-2) `OnEnd`에서 구 코드
```csharp
            LOPEntityManager lopEntityManager = (runner as LOPRunner).entityManager;
            EntityCreationData[] allEntityCreationDatas = lopEntityManager.GetAllEntityCreationDatas();
```
를
```csharp
            EntityCreationData[] allEntityCreationDatas = BuildAllEntityCreationDatas();
```
로. 그리고 루프 안 `string entityId = lopEntityManager.GetEntityIdByUserId(gameInfoToS.UserId);` → `string entityId = entitySpawner.GetEntityIdByUserId(gameInfoToS.UserId);`.

12-3) 클래스에 private 헬퍼 추가(구 `LOPEntityManager.GetAllEntityCreationDatas`를 `entityRegistry.All` 순회로 이관):
```csharp
        private EntityCreationData[] BuildAllEntityCreationDatas()
        {
            var list = new List<EntityCreationData>();
            foreach (var worldEntity in entityRegistry.All)
            {
                list.Add(entityCreationDataFactory.Create(worldEntity));
            }
            return list.ToArray();
        }
```
(`using System.Collections.Generic;`가 이미 있음. `entityRegistry`는 이미 주입됨.)

- [ ] **Step 13: 서버 `Game.Entity.MessageHandler` — GetEntityIdByUserId→entitySpawner**

`Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs`:

13-1) inject 추가 `[Inject] private EntitySpawner entitySpawner;`. (이 핸들러의 `runner`는 오직 `(runner as LOPRunner).entityManager`에만 쓰였으므로, 아래 교체 후 `runner` 필드가 미사용이면 그 `[Inject] private IRunner runner;`도 제거.)

13-2) `OnStatAllocationToS`의
```csharp
            string entityId = (runner as LOPRunner).entityManager.GetEntityIdByUserId(session.userId);
```
→
```csharp
            string entityId = entitySpawner.GetEntityIdByUserId(session.userId);
```

- [ ] **Step 14: 서버 `Game.Input.MessageHandler` — GetEntityIdByUserId→entitySpawner**

`Assets/Scripts/Game/MessageHandler/Game.Input.MessageHandler.cs`:

14-1) inject 추가 `[Inject] private EntitySpawner entitySpawner;`. (`runner`는 이 핸들러에서 line 39에만 쓰였으니 교체 후 미사용이면 `[Inject] private IRunner runner;` 제거. `Runner.Time`은 정적이라 무관.)

14-2) `OnInputCommandToS`의
```csharp
            string entityId = (runner as LOPRunner).entityManager.GetEntityIdByUserId(session.userId);
```
→
```csharp
            string entityId = entitySpawner.GetEntityIdByUserId(session.userId);
```

- [ ] **Step 15: 서버 `LOPRunner` 정리 + GetAllEntitySnaps 내재화**

`Assets/Scripts/Game/LOPRunner.cs`:

15-1) `EntitySpawner` inject 추가(예: `[Inject] private GameRuleSystem gameRuleSystem;` 아래):
```csharp
        [Inject] private EntitySpawner entitySpawner;
```

15-2) `entityManager` 프로퍼티 삭제:
```csharp
        public new LOPEntityManager entityManager => base.entityManager as LOPEntityManager;
```

15-3) `ProcessInput`·`SendInputTimingFeedback`의 두 곳
```csharp
                string userId = entityManager.GetUserIdByEntityId(worldEntity.Id);
```
→ (Ownership 파생 — worldEntity가 이미 손에 있음)
```csharp
                string userId = worldEntity.Get<GameFramework.World.Ownership>()?.OwnerId;
```

15-4) `UpdateEntity`의 `entityManager.UpdateEntities();` 줄 삭제(주변 dispatch 유지).

15-5) `EndUpdate`의 `EntitySnap[] allEntitySnaps = entityManager.GetAllEntitySnaps();` → `EntitySnap[] allEntitySnaps = BuildAllEntitySnaps();`.

15-6) `EndUpdate`의 `string entityId = entityManager.GetEntityIdByUserId(session.userId);` → `string entityId = entitySpawner.GetEntityIdByUserId(session.userId);`.

15-7) `EndUpdate` 말미 `entityManager.DestroyMarkedEntities();` → `entitySpawner.FlushDespawns();`.

15-8) 클래스에 private 헬퍼 추가(구 `LOPEntityManager.GetAllEntitySnaps`를 `entityRegistry.All` 순회로 이관 — actor 미사용):
```csharp
        private EntitySnap[] BuildAllEntitySnaps()
        {
            var entitySnapList = new List<EntitySnap>();

            foreach (var worldEntity in entityRegistry.All)
            {
                GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
                var snap = new EntitySnap
                {
                    EntityId = worldEntity.Id,
                    Position = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity)),
                    Rotation = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity)),
                    Velocity = MapperConfig.mapper.Map<ProtoVector3>(GameFramework.World.EntityMotionExtensions.GetVelocity(worldEntity)),
                    MaxHP = health?.Max ?? 0,
                    CurrentHP = health?.Current ?? 0,
                };

                var contributions = worldEntity?.Get<MotionContributions>();
                if (contributions != null)
                {
                    foreach (var c in contributions.Items)
                    {
                        snap.MotionContributions.Add(new ProtoMotionContribution
                        {
                            Horizontal = new ProtoVector3 { X = c.Horizontal.X, Y = c.Horizontal.Y, Z = c.Horizontal.Z },
                            Mode = (int)c.Mode,
                            Priority = c.Priority,
                            StartTick = c.StartTick,
                            EndTick = c.EndTick,
                            DecayPerTick = c.DecayPerTick,
                        });
                    }
                }

                entitySnapList.Add(snap);
            }

            return entitySnapList.ToArray();
        }
```
(`using System.Collections.Generic;`가 상단에 있는지 확인 — 없으면 추가.)

- [ ] **Step 16: 서버 `GameLifetimeScope` DI 재배선**

`Assets/Scripts/Game/GameLifetimeScope.cs`:

16-1) `[SerializeField] private LOPEntityManager entityManager;` 필드 삭제(line ~16).

16-2) `builder.RegisterComponent(entityManager).As<IEntityManager>();` 삭제(line ~63). 위 주석("entity manager는 아래 RegisterComponent…") 도 삭제/정정.

16-3) creator/factory 등록 교체 — 삭제:
```csharp
            builder.Register<IEntityCreator, CharacterCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreator, ItemCreator>(Lifetime.Singleton);
            builder.Register<IEntityFactory, EntityFactory>(Lifetime.Singleton);
```
추가:
```csharp
            builder.Register<CharacterCreator>(Lifetime.Singleton);
            builder.Register<ItemCreator>(Lifetime.Singleton);
            builder.Register<EntitySpawner>(Lifetime.Singleton);
            builder.Register<ActorRegistry>(Lifetime.Singleton);
```
(단, `IEntityCreationDataCreator`/`IEntityCreationDataFactory` 등록 3줄은 **유지** — 별개 서버 로컬 인터페이스로 삭제 대상 아님.)

16-4) EntryPoint 등록 리네임: `builder.RegisterEntryPoint<EntityViewSpawner>();` → `builder.RegisterEntryPoint<EntityBinder>();`.

- [ ] **Step 17: 서버 `LOPEntityManager` 삭제**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git rm Assets/Scripts/Entity/LOPEntityManager.cs Assets/Scripts/Entity/LOPEntityManager.cs.meta
```

- [ ] **Step 18: 서버 씬에서 `LOPEntityManager` 컴포넌트 제거 (Editor)**

서버 Unity 에디터에서 `Assets/Scenes/LOPGame.unity`를 열고, `LOPEntityManager` 컴포넌트를 붙인 GameObject에서 (이제 missing script인) 컴포넌트를 제거 + `GameLifetimeScope`의 인스펙터에 비게 된 필드 확인 후 씬 저장. (사용자 확인 하에 서버 인스턴스 조작.)
- 검증: `grep -c "21112dd39defdcb44a6182ab2ae1786a" Assets/Scenes/LOPGame.unity` → Expected `0`.

- [ ] **Step 19: GameFramework 삭제 반영 — 양쪽 재스캔(stale CS2001 방지)**

Task 1에서 GF 파일을 지웠으므로, 서버 에디터도 stale CS2001이 날 수 있다. 서버 인스턴스에서 `refresh_unity`(scope=all, force) 실행([[deleting-package-files-cs2001]]).

- [ ] **Step 20: 서버 컴파일 확인 (첫 그린 체크포인트)**

서버 인스턴스에서 `refresh_unity`(force) → `editor_state.isCompiling` false 대기 → `read_console`(errors 필터).
Expected: **컴파일 에러 0.**
잔여 확인: `grep -rn "IEntityManager\|IEntityFactory\|IEntityCreator\|\.entityManager\|CreateEntity<\|DeleteEntityById\|GenerateEntityId\|GetAllEntitySnaps\|GetAllEntityCreationDatas\|GetEntityIdByUserId\|GetUserIdByEntityId\|EntityViewSpawner" Assets/Scripts` → 남은 참조가 없어야 함(전부 이관/삭제됨).

- [ ] **Step 21: 서버 커밋**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add -A
git commit -m "refactor(entity): split LOPEntityManager into EntitySpawner + ActorRegistry (server)

Server EntitySpawner adds id issuance, userId<->entityId map, despawn wire.
EntityViewSpawner renamed to EntityBinder + absorbs actor create/destroy.
GetAllEntitySnaps/GetAllEntityCreationDatas relocated to their callers over
entityRegistry.All. GameFramework IEntity* interfaces retired.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: 통합 스모크 + whole-branch 리뷰 + 문서

**Files:**
- Modify: `docs/ROADMAP.md` (클라 repo) — 슬라이스 완료 반영
- (선택) Memory 업데이트

- [ ] **Step 1: 클·서 동시 인게임 스모크 (사용자와 함께)**

서버 + 클라를 함께 구동해 값 동치 재확인(Task 2 Step 17 항목 전체) + 서버 전용 경로:
- 초기 플레이어 생성(GameRuleSystem) + 적 주기 스폰(10초마다, 상한 100).
- 아이템 줍기 → despawn 와이어 → 클라에서 사라짐 + 경험치/스탯포인트.
- 죽음 → DeathCascade despawn + 경험치 구슬 스폰.
- 스탯 할당(StatAllocation) 왕복.
양쪽 `read_console`(errors/warnings)에 런타임 예외 0.

- [ ] **Step 2: whole-branch 값 동치 리뷰**

3개 브랜치(GF/클/서)를 훑어 다음을 확인(spec §13):
- `EntitySpawner`가 `LOPActor`/`ActorRegistry`를 **한 줄도** 참조하지 않는다(핵심 불변식).
  Run: `grep -n "LOPActor\|ActorRegistry" LeagueOfPhysical-Client/Assets/Scripts/Entity/EntitySpawner.cs LeagueOfPhysical-Server/Assets/Scripts/Entity/EntitySpawner.cs`
  Expected: **출력 없음.**
- actor 생성·파괴·cleanup가 전부 `EntityBinder`에만 있다.
- `EntityCreated` 페이로드가 양쪽 `string entityId`로 일치.
- 삭제된 GF 심볼이 어디에도 안 남음(3 레포 grep).

- [ ] **Step 3: `superpowers:requesting-code-review` 스킬로 코드 리뷰 요청**

리뷰 피드백은 `superpowers:receiving-code-review`로 처리.

- [ ] **Step 4: `docs/ROADMAP.md` 업데이트 (클라 repo)**

엔티티 매니저 분리 슬라이스를 "한 일"로 이동(상태 단일 원천 = ROADMAP, [[roadmap-status-tracking]]). 왜/gotcha는 메모리, 구조는 아키텍처 문서 경계 유지.

- [ ] **Step 5: ROADMAP 커밋 (클라 피처 브랜치)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add docs/ROADMAP.md
git commit -m "docs(roadmap): 엔티티 매니저 분리(ActorRegistry+EntitySpawner) 완료 반영

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 6: 브랜치 마무리 결정**

`superpowers:finishing-a-development-branch` 스킬로 3 레포(GameFramework/Client/Server)의 `feature/entity-manager-actor-registry-split`을 각각 main에 `--no-ff` 머지할지 등을 사용자와 결정. (CLAUDE.md: 완료 후 main에 `--no-ff` 머지. 클·서·GF는 file: 패키지라 함께 그린일 때 머지.)

---

## Self-Review 노트 (플랜 작성자 확인 완료)

- **Spec 커버리지**: §2 목표(데이터/뷰 축 분리)=Task 2/3 전체. §4.1 ActorRegistry=T2S4/T3S5. §4.2 EntitySpawner=T2S5/T3S6. §4.3 EntityBinder 통일=T2S8/T3S9. §6 페이로드 flip=T2S2/T3S2 + 소비자(PlayerHudCoordinator T2S9, Binder, EntityViewSpawner→Binder). §7 GF 3종 삭제=Task 1. §8 멤버 매핑 전부 스텝화(UpdateEntities 삭제=T2S12-3/T3S15-4; GenerateEntityId/userEntityMap=T3S6; GetUserIdByEntityId→Ownership=T3S15-3; GetAll*=T3S12/T3S15-8). §11 리스크(구독 순서=T2S13-3; 스폰 배치=PhysicsFollower 유지; PhysicsBody 동기=Spawn 동기 발행; despawn 와이어=T3S6 FlushDespawns). §14 open questions 전부 확정(overload 디스패치 / GetAll* 이관처 / userEntityMap 위치 / EntityFactory 은퇴).
- **§14 확정 근거**: (1) creator 디스패치=`EntitySpawner.Spawn` 타입별 오버로드(리플렉션 factory 폐기, 타입 2개뿐 → YAGNI). (2) `GetAllEntitySnaps`→`LOPRunner.BuildAllEntitySnaps`(유일 호출자), `GetAllEntityCreationDatas`→`GameInfoMessageHandler.BuildAllEntityCreationDatas`(유일 호출자), 둘 다 `entityRegistry.All` 순회(actor 불요). (3) `userEntityMap`/`GetEntityIdByUserId`=서버 `EntitySpawner`(스폰 수명 결합). (4) `EntityFactory` 콘크리트 존재 확인 → Task 1에서 삭제.
- **타입 일관성**: `Spawn(CharacterCreationData)`/`Spawn(ItemCreationData)`, `FlushDespawns()`, `Despawn(string)`, `SetEntityId(string)`, `ActorRegistry.TryGet(string, out LOPActor)` — 클·서 호출부에서 동일 시그니처 사용 확인.
- **Placeholder 스캔**: 코드 스텝은 전부 완전한 본문/정확한 old→new. "적절히 처리" 류 없음.
- **테스트 편차 사유**: Global Constraints의 테스트 인프라 제약 참고 — Assembly-CSharp 도달성 부재로 EditMode 불가, spec §13대로 컴파일+스모크+리뷰가 안전망.
