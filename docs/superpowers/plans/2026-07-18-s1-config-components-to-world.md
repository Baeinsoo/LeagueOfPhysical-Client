# S1 — 설정 컴포넌트 → World 데이터 컴포넌트 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 레거시 Unity 엔티티 컴포넌트 4종(Appearance/Character/Item/EntityType)을 제거하고 그 데이터를 LOP-Shared 순수 C# World 컴포넌트 3종으로 이관한다.

**Architecture:** strangler(점진 교체). LOP-Shared에 새 World 컴포넌트(`Appearance`/`MasterDataRef`/`EntityKind`)를 먼저 만들고(Task 1), 클라(Task 2)·서버(Task 3)를 각각 독립적으로 새 컴포넌트로 전환한 뒤 옛 클래스를 삭제한다. 옛 컴포넌트 클래스는 클·서에 **각자 복제**돼 있어 두 사이드를 독립적으로 옮길 수 있다. enum `EntityType`만 공유이므로 Task 1에서 원자적으로 이동한다.

**Tech Stack:** Unity 6 (C#), GameFramework.World(순수 C# CBD 코어), LOP-Shared 패키지, VContainer DI, UnityMCP(컴파일/콘솔/테스트), NUnit EditMode.

## Global Constraints

- **매 태스크 끝에 게임이 그대로 동작해야 함**(strangler, 빅뱅 금지). 클·서 각 editor 컴파일 에러 0.
- 새 World 컴포넌트는 `LeagueOfPhysical-Shared/Runtime/Scripts/Game/`, `namespace LOP`, `GameFramework.World.Component` 상속, **anemic**(데이터 + 생성자 무결성 검사만; 상태변경 로직 없음).
- **와이어 proto 무변경** — `MasterDataRef.Code`는 서버 릴레이 보존용, proto·메시지 안 건드림.
- **UnityMCP 호출마다 `unity_instance` 명시**(CLAUDE.md). 세션 시작 시 `mcpforunity://instances` 리소스로 해시 확인. 작성 시점: client `LeagueOfPhysical-Client@de70658b9450cbb4`, server `LeagueOfPhysical-Server@f99391fa2dbaaf3c` (해시는 바뀔 수 있음 — 매번 확인).
- 새 `.cs` 생성 후 Unity가 만든 `.meta`를 **함께 커밋**. `.meta`는 직접 만들지 않음.
- World 타입은 LOP 파일에서 **풀 네임스페이스 한정**(`GameFramework.World.Entity` 등) — `UnityEngine.Component` 충돌 회피(CLAUDE.md 컨벤션).
- 4개 저장소가 브랜치 `feature/entity-view-rearchitecture`에서 작업(client repo는 이미 이 브랜치; shared/server는 이 브랜치 생성). 커밋 메시지 끝에 `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

---

## File Structure

**LOP-Shared (신규):**
- `Runtime/Scripts/Game/EntityType.cs` — enum(클·서 로컬에서 이동, dedupe)
- `Runtime/Scripts/Game/Appearance.cs` — `Appearance : Component { string VisualId }`
- `Runtime/Scripts/Game/MasterDataRef.cs` — `MasterDataRef : Component { string Code }`
- `Runtime/Scripts/Game/EntityKind.cs` — `EntityKind : Component { EntityType Kind }`
- `Tests/EditMode/EntityDataComponentsTests.cs` — 3종 컴포넌트 단위 테스트

**Client (수정/삭제):** `Assets/Scripts/EntityCreator/{CharacterCreator,ItemCreator}.cs`(수정), `Assets/Scripts/Entity/LOPEntityView.cs`(수정), `Assets/Scripts/Game/EntityBinder.cs`(수정), `Assets/Scripts/Component/{Appearance,Character,Item,EntityType}Component.cs`(삭제), `Assets/Scripts/Entity/Entity.cs`(enum 삭제).

**Server (수정/삭제):** `Assets/Scripts/EntityCreator/{CharacterCreator,ItemCreator}.cs`(수정), `Assets/Scripts/EntityCreationDataFactory/{EntityCreationDataFactory,CharacterCreationDataCreator,ItemCreationDataCreator}.cs`(수정), `Assets/Scripts/AI/EnemyBrain.cs`(수정), `Assets/Scripts/Component/{Appearance,Character,Item,EntityType}Component.cs`(삭제), `Assets/Scripts/Entity/Entity.cs`(enum 삭제).

---

## Task 1: LOP-Shared World 컴포넌트 + enum 이동 (TDD)

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime/Scripts/Game/EntityType.cs`, `Appearance.cs`, `MasterDataRef.cs`, `EntityKind.cs`
- Create: `LeagueOfPhysical-Shared/Tests/EditMode/EntityDataComponentsTests.cs`
- Delete: `LeagueOfPhysical-Client/Assets/Scripts/Entity/Entity.cs` (+.meta), `LeagueOfPhysical-Server/Assets/Scripts/Entity/Entity.cs` (+.meta) — 둘 다 enum-only 확인됨

**Interfaces:**
- Produces: `LOP.EntityType`(enum: None=0,Character=1,Projectile=2,Item=3,Environment=4), `LOP.Appearance(string visualId){ string VisualId }`, `LOP.MasterDataRef(string code){ string Code }`, `LOP.EntityKind(EntityType kind){ EntityType Kind }` — 모두 `GameFramework.World.Component` 상속.

- [ ] **Step 1: LOP-Shared 브랜치 생성**

Run:
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared checkout -b feature/entity-view-rearchitecture
```
Expected: `Switched to a new branch`.

- [ ] **Step 2: 실패하는 테스트 작성**

Create `LeagueOfPhysical-Shared/Tests/EditMode/EntityDataComponentsTests.cs`:
```csharp
using System;
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class EntityDataComponentsTests
    {
        [Test]
        public void Appearance_StoresVisualId()
        {
            Assert.AreEqual("Assets/Art/x.prefab", new Appearance("Assets/Art/x.prefab").VisualId);
        }

        [Test]
        public void Appearance_NullVisualId_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new Appearance(null));
        }

        [Test]
        public void MasterDataRef_StoresCode()
        {
            Assert.AreEqual("knight", new MasterDataRef("knight").Code);
        }

        [Test]
        public void MasterDataRef_NullCode_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new MasterDataRef(null));
        }

        [Test]
        public void EntityKind_StoresKind()
        {
            Assert.AreEqual(EntityType.Character, new EntityKind(EntityType.Character).Kind);
        }

        [Test]
        public void Entity_AddGet_RoundTrips()
        {
            var e = new Entity("e1");
            e.Add(new Appearance("v"));
            e.Add(new MasterDataRef("c"));
            e.Add(new EntityKind(EntityType.Item));
            Assert.AreEqual("v", e.Get<Appearance>().VisualId);
            Assert.AreEqual("c", e.Get<MasterDataRef>().Code);
            Assert.AreEqual(EntityType.Item, e.Get<EntityKind>().Kind);
        }
    }
}
```

- [ ] **Step 3: 새 컴포넌트/enum 작성**

Create `LeagueOfPhysical-Shared/Runtime/Scripts/Game/EntityType.cs`:
```csharp
namespace LOP
{
    public enum EntityType
    {
        None = 0,
        Character = 1,
        Projectile = 2,
        Item = 3,
        Environment = 4,
    }
}
```

Create `Appearance.cs`:
```csharp
using System;
using GameFramework.World;

namespace LOP
{
    /// <summary>엔티티가 그릴 모델(Addressable 에셋 경로). 순수 데이터 — 로드는 뷰가 한다.</summary>
    public class Appearance : Component
    {
        public string VisualId { get; }

        public Appearance(string visualId)
        {
            VisualId = visualId ?? throw new ArgumentNullException(nameof(visualId));
        }
    }
}
```

Create `MasterDataRef.cs`:
```csharp
using System;
using GameFramework.World;

namespace LOP
{
    /// <summary>이 엔티티의 설정 테이블 키(TbCharacter/TbItem code). 스폰 데이터 재구성 시 서버가 릴레이.</summary>
    public class MasterDataRef : Component
    {
        public string Code { get; }

        public MasterDataRef(string code)
        {
            Code = code ?? throw new ArgumentNullException(nameof(code));
        }
    }
}
```

Create `EntityKind.cs`:
```csharp
using GameFramework.World;

namespace LOP
{
    /// <summary>엔티티 종류(Character/Item 등). 스폰 데이터 디스패치·"캐릭터냐" 판별용.</summary>
    public class EntityKind : Component
    {
        public EntityType Kind { get; }

        public EntityKind(EntityType kind)
        {
            Kind = kind;
        }
    }
}
```

- [ ] **Step 4: 클·서 로컬 enum 삭제 (중복 정의 회피)**

`LOP.EntityType`이 shared+로컬 양쪽에 있으면 CS0101 중복이 되므로, 로컬 두 파일을 삭제한다. **아직 refresh하지 않는다**(모든 편집을 끝낸 뒤 한 번에).

Run:
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client rm Assets/Scripts/Entity/Entity.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server rm Assets/Scripts/Entity/Entity.cs
```
Expected: 각 파일 + `.meta` 삭제 스테이징.

- [ ] **Step 5: 양쪽 editor refresh + 컴파일 확인**

UnityMCP(도구 로드: ToolSearch `select:mcp__UnityMCP__refresh_unity,mcp__UnityMCP__read_console,mcp__UnityMCP__run_tests`):
- `refresh_unity(mode=force, scope=all, compile=request, unity_instance=<client>)` 그리고 `<server>` 각각.
- `read_console(types=[error], count=30, unity_instance=<client>)` / `<server>`.
Expected: CS 컴파일 에러 0(콘솔에 남은 건 알려진 런타임 NRE뿐 — `LOPEntityView.LateUpdate`/`KcpTransport`). 새 `.meta` 파일 생성됨.

- [ ] **Step 6: EditMode 테스트 실행 (통과 확인)**

`run_tests(mode=EditMode, test_filter=EntityDataComponentsTests, unity_instance=<client>)`.
Expected: 6 테스트 PASS.

- [ ] **Step 7: 커밋 (shared / client / server 각각)**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared add Runtime/Scripts/Game/EntityType.cs Runtime/Scripts/Game/Appearance.cs Runtime/Scripts/Game/MasterDataRef.cs Runtime/Scripts/Game/EntityKind.cs Tests/EditMode/EntityDataComponentsTests.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared add -A Runtime/Scripts/Game Tests/EditMode   # .meta 포함
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared commit -m "feat(entity): World 데이터 컴포넌트 Appearance/MasterDataRef/EntityKind + EntityType 이동

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "refactor(entity): EntityType enum을 LOP-Shared로 이동(로컬 삭제)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(entity): EntityType enum을 LOP-Shared로 이동(로컬 삭제)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 클라이언트 마이그레이션

**Files:**
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs`, `Assets/Scripts/EntityCreator/ItemCreator.cs`, `Assets/Scripts/Entity/LOPEntityView.cs`, `Assets/Scripts/Game/EntityBinder.cs`
- Delete: `Assets/Scripts/Component/{AppearanceComponent,CharacterComponent,ItemComponent,EntityTypeComponent}.cs` (+.meta)

**Interfaces:**
- Consumes: Task 1의 `Appearance`/`MasterDataRef`/`EntityKind`/`EntityType`, 기존 `GameFramework.World.EntityRegistry.Get(id)`, `LOP.MasterData.LOPMasterData.Tables.TbCharacter.Get(code)`.

- [ ] **Step 1: CharacterCreator — 새 컴포넌트 시드 + 인라인 masterData**

`Assets/Scripts/EntityCreator/CharacterCreator.cs`:
① `md` 주입 추가 — `[Inject] private AbilitySystem abilitySystem;` 아래에:
```csharp
        [Inject]
        private LOP.MasterData.LOPMasterData md;
```
② `worldEntity.Add(new GameFramework.World.Velocity ...)`(line 36) **바로 다음**에 3종 추가:
```csharp
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new MasterDataRef(creationData.characterCode));
            worldEntity.Add(new Appearance(creationData.visualId));
```
③ 옛 컴포넌트 3블록 삭제 — `EntityTypeComponent`(45-47), `CharacterComponent`(49-51), `AppearanceComponent`(53-55) 세 블록 제거(각 3줄). `PhysicsComponent` 블록은 유지.
④ Stats 시드의 masterData 참조 교체 — `worldStats.BaseStats[...MoveSpeed] = characterComponent.masterData.Speed;` / `JumpPower` 두 줄을:
```csharp
            var characterMasterData = md.Tables.TbCharacter.Get(creationData.characterCode);
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.MoveSpeed] = characterMasterData.Speed;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.JumpPower] = characterMasterData.JumpPower;
```

- [ ] **Step 2: ItemCreator — 새 컴포넌트 시드**

`Assets/Scripts/EntityCreator/ItemCreator.cs`: `worldEntity.Add(...Velocity...)`(line 27) 다음에:
```csharp
            worldEntity.Add(new EntityKind(EntityType.Item));
            worldEntity.Add(new MasterDataRef(creationData.itemCode));
            worldEntity.Add(new Appearance(creationData.visualId));
```
옛 3블록 삭제 — `EntityTypeComponent`(36-38), `ItemComponent`(40-42), `AppearanceComponent`(44-46). `PhysicsComponent` 유지.

- [ ] **Step 3: LOPEntityView — visualId를 World에서 읽기 + 죽은 PropertyChange 제거**

`Assets/Scripts/Entity/LOPEntityView.cs`:
① 필드 추가(클래스 상단 `public LOPEntity entity` 아래):
```csharp
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
```
그리고 파일 상단 usings에 `using VContainer;` 추가.
② `Start()`의 visualId 로드 블록(49-52) 교체:
```csharp
            var appearance = entityRegistry.Get(entity.entityId)?.Get<Appearance>();
            if (appearance != null)
            {
                UpdateVisual(appearance.VisualId);
            }
```
③ `Start()`의 PropertyChange 구독 줄(44) 삭제:
```
GlobalMessagePipe.GetSubscriber<string, PropertyChange>().Subscribe(entity.entityId, OnPropertyChange).AddTo(bag);
```
④ `OnPropertyChange` 메서드 전체(72-80) 삭제(visualId가 유일 case였고 write-once라 불필요).

- [ ] **Step 4: EntityBinder — "캐릭터냐" 판별을 EntityKind로**

`Assets/Scripts/Game/EntityBinder.cs`:
① 필드 추가(`[Inject] private ISubscriber<EntityCreated> entityCreatedSubscriber;` 아래):
```csharp
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
```
② presence 검사(43-46) 교체:
```csharp
            // 장식 뷰는 캐릭터 엔티티에만 (아이템 등 제외).
            var kind = entityRegistry.Get(entity.entityId)?.Get<EntityKind>();
            if (kind == null || kind.Kind != EntityType.Character)
            {
                return;
            }
```

- [ ] **Step 5: 옛 클라 컴포넌트 클래스 삭제**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client rm Assets/Scripts/Component/AppearanceComponent.cs Assets/Scripts/Component/CharacterComponent.cs Assets/Scripts/Component/ItemComponent.cs Assets/Scripts/Component/EntityTypeComponent.cs
```
Expected: 4 파일 + `.meta` 삭제 스테이징.

- [ ] **Step 6: 클라 컴파일 확인**

`refresh_unity(mode=force, scope=all, compile=request, unity_instance=<client>)` → `read_console(types=[error], count=30, unity_instance=<client>)`.
Expected: CS 에러 0. `GetEntityComponent<AppearanceComponent/CharacterComponent/...>` 잔여 참조가 있으면 여기서 CS 에러로 드러남 → 잡아 수정.

- [ ] **Step 7: 인게임 검증(클라 관점) + 커밋**

플레이(서버도 함께 필요). 플레이어/적/exp마블 스폰 시 **모델 로드(visualId)**·네임플레이트·데미지 플로터 정상, 콘솔 CS/신규 예외 0 확인.
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add -A Assets/Scripts
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "refactor(entity): 클라 설정 컴포넌트 4종 → World 컴포넌트(Appearance/MasterDataRef/EntityKind)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: 서버 마이그레이션

**Files:**
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs`, `Assets/Scripts/EntityCreator/ItemCreator.cs`, `Assets/Scripts/EntityCreationDataFactory/EntityCreationDataFactory.cs`, `Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs`, `Assets/Scripts/EntityCreationDataFactory/ItemCreationDataCreator.cs`, `Assets/Scripts/AI/EnemyBrain.cs`
- Delete: `Assets/Scripts/Component/{AppearanceComponent,CharacterComponent,ItemComponent,EntityTypeComponent}.cs` (+.meta)

**Interfaces:**
- Consumes: Task 1의 World 컴포넌트, `GameFramework.World.StatsSystem.GetValue(Stats, int)`, `GameFramework.World.EntityStatType.MoveSpeed`.

- [ ] **Step 1: 서버 CharacterCreator — 새 컴포넌트 시드 + 인라인 masterData**

`Assets/Scripts/EntityCreator/CharacterCreator.cs` (서버):
① `md` 주입 추가(`[Inject] private AbilitySystem abilitySystem;` 아래):
```csharp
        [Inject]
        private LOP.MasterData.LOPMasterData md;
```
② `worldEntity.Add(...Velocity...)`(line 30) 다음에:
```csharp
            worldEntity.Add(new EntityKind(EntityType.Character));
            worldEntity.Add(new MasterDataRef(creationData.characterCode));
            worldEntity.Add(new Appearance(creationData.visualId));
```
③ 옛 3블록 삭제 — `EntityTypeComponent`(39-41), `CharacterComponent`(43-45), `AppearanceComponent`(47-49). `PhysicsComponent` 유지.
④ Stats 시드 masterData 참조(82-83) 교체:
```csharp
            var characterMasterData = md.Tables.TbCharacter.Get(creationData.characterCode);
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.MoveSpeed] = characterMasterData.Speed;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.JumpPower] = characterMasterData.JumpPower;
```

- [ ] **Step 2: 서버 ItemCreator — 새 컴포넌트 시드**

`Assets/Scripts/EntityCreator/ItemCreator.cs` (서버): `worldEntity.Add(...Velocity...)`(27) 다음에:
```csharp
            worldEntity.Add(new EntityKind(EntityType.Item));
            worldEntity.Add(new MasterDataRef(creationData.itemCode));
            worldEntity.Add(new Appearance(creationData.visualId));
```
옛 3블록 삭제 — `EntityTypeComponent`(36-38), `ItemComponent`(40-42), `AppearanceComponent`(44-46).

- [ ] **Step 3: EntityCreationDataFactory — 디스패치를 EntityKind로**

`Assets/Scripts/EntityCreationDataFactory/EntityCreationDataFactory.cs`:
① 생성자에 `EntityRegistry` 추가:
```csharp
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public EntityCreationDataFactory(IEnumerable<IEntityCreationDataCreator> creators, GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
            foreach (var creator in creators.OrEmpty())
            {
                this.creators[creator.EntityType] = creator;
            }
        }
```
② `Create(IEntity entity)` 본문(22-41) 교체:
```csharp
        public EntityCreationData Create(IEntity entity)
        {
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entity.entityId);
            EntityKind kind = worldEntity?.Get<EntityKind>();
            if (kind == null)
            {
                throw new InvalidOperationException(
                    $"Entity '{entity.entityId}' does not have an EntityKind. Ensure the entity is properly initialized.");
            }

            if (creators.TryGetValue(kind.Kind, out var creator) == false)
            {
                throw new InvalidOperationException(
                    $"No registered creation-data creator found for entity kind '{kind.Kind}'.");
            }

            return creator.Create(entity);
        }
```

- [ ] **Step 4: CharacterCreationDataCreator — code/visualId를 World에서**

`Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs`: `characterCreationData` 초기화(50-66)의 두 줄 교체(`worldEntity`는 line 25에서 이미 조회됨):
```csharp
                CharacterCode = worldEntity.Get<MasterDataRef>().Code,
                VisualId = worldEntity.Get<Appearance>().VisualId,
```

- [ ] **Step 5: ItemCreationDataCreator — code/visualId를 World에서**

`Assets/Scripts/EntityCreationDataFactory/ItemCreationDataCreator.cs`:
① `EntityRegistry` 주입 추가(클래스 상단):
```csharp
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;
```
그리고 `using VContainer;` 추가.
② `Create(LOPEntity lopEntity)`에서 `itemCreationData`(20-25) 교체:
```csharp
            GameFramework.World.Entity worldEntity = entityRegistry.Get(lopEntity.entityId);
            global::ItemCreationData itemCreationData = new global::ItemCreationData
            {
                BaseEntityCreationData = baseEntityCreationData,
                ItemCode = worldEntity.Get<MasterDataRef>().Code,
                VisualId = worldEntity.Get<Appearance>().VisualId,
            };
```

- [ ] **Step 6: EnemyBrain — Speed를 World.Stats에서**

`Assets/Scripts/AI/EnemyBrain.cs`:
① 생성자에 `StatsSystem` 추가:
```csharp
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.StatsSystem statsSystem;

        public EnemyBrain(AbilityActivator abilityActivator, GameFramework.World.EntityRegistry entityRegistry, GameFramework.World.StatsSystem statsSystem)
        {
            this.abilityActivator = abilityActivator;
            this.entityRegistry = entityRegistry;
            this.statsSystem = statsSystem;
        }
```
② Move 분기의 Speed 참조(52) 교체:
```csharp
                var stats = entityRegistry.Get(entity.entityId).Get<GameFramework.World.Stats>();
                float speed = statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.MoveSpeed);
                var velocity = direction.normalized * speed;
```

- [ ] **Step 7: 옛 서버 컴포넌트 클래스 삭제**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server rm Assets/Scripts/Component/AppearanceComponent.cs Assets/Scripts/Component/CharacterComponent.cs Assets/Scripts/Component/ItemComponent.cs Assets/Scripts/Component/EntityTypeComponent.cs
```

- [ ] **Step 8: 서버 컴파일 확인**

`refresh_unity(mode=force, scope=all, compile=request, unity_instance=<server>)` → `read_console(types=[error], count=30, unity_instance=<server>)`.
Expected: CS 에러 0(런타임 NRE만 잔존). `StatsSystem`이 서버 DI에 미등록이면 런타임 resolve 에러 → 등록 확인(다른 시스템이 이미 씀).

- [ ] **Step 9: 커밋**

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server add -A Assets/Scripts
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(entity): 서버 설정 컴포넌트 4종 → World 컴포넌트, EnemyBrain은 World.Stats 읽기

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: 통합 인게임 검증 + ROADMAP

**Files:** `docs/ROADMAP.md`(client)

- [ ] **Step 1: 클·서 동시 플레이 검증**

두 editor 플레이. 확인:
- 플레이어 스폰 + 모델(visualId) 로드 정상.
- 적(AI) 스폰 + **이동(Speed)** 정상, 근접 시 공격.
- exp 마블(아이템) 스폰 + 모델 로드 + 획득 정상.
- 넉백·데미지 플로터·네임플레이트 정상.
- 클·서 콘솔 CS 에러 0, 신규 예외 0(기존 파킹 NRE만).

- [ ] **Step 2: `LOPEntity.components`에 Physics만 남았는지 확인**

Grep: `AppearanceComponent|CharacterComponent|ItemComponent|EntityTypeComponent`가 클·서 코드에서 0건(삭제된 클래스 참조 없음). `AddEntityComponent` 잔여는 `PhysicsComponent`만.

- [ ] **Step 3: ROADMAP 갱신 + 커밋**

`docs/ROADMAP.md`의 umbrella 관련 섹션에 S1 완료 한 줄 추가(Done 원장) + S2를 다음으로 표시. 커밋:
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client add docs/ROADMAP.md
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client commit -m "docs(roadmap): 엔티티 재구조화 S1(설정 컴포넌트→World) 완료 반영

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Self-Review

**Spec coverage:** ✅ 새 컴포넌트 3종(Task1) · enum 이동(Task1) · 클 소비자 재배선[view/binder/creators](Task2) · 서버 재배선[creators/factory/creationdata/EnemyBrain](Task3) · 옛 4종 삭제(Task2/3) · EnemyBrain→World.Stats(Task3) · item masterData 소멸(클래스 삭제로) · 테스트[EditMode+play](Task1/4) · 그린 판정(Task4). 스펙의 모든 요구가 태스크에 매핑됨.

**Placeholder scan:** 코드 스텝은 전부 실제 코드. TODO/TBD 없음.

**Type consistency:** `Appearance.VisualId` / `MasterDataRef.Code` / `EntityKind.Kind`가 정의(Task1)와 소비(Task2/3)에서 일치. `EntityType.Character`/`Item` 일관. `StatsSystem.GetValue(Stats, int)` 시그니처 일관.

**주의(구현자):** ① `MasterDataRef.Code`는 클라 미독이나 대칭 위해 부착(무해). ② 서버 `StatsSystem` DI 등록 확인(미등록 시 EnemyBrain resolve 실패 — 등록 추가). ③ Task1 Step4~5는 "편집 후 한 번에 refresh"(중간 refresh 시 enum 중복 컴파일). ④ 삭제 파일의 `.meta`도 `git rm`이 함께 스테이징하는지 확인.
