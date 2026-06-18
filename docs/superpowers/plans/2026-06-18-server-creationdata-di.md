# Server EntityCreationDataFactory DI Refactor (Slice 1a) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버 `EntityCreationDataFactory`를 정적+`Activator`(reflection 등록)에서 DI 인스턴스 팩토리로 전환한다 — 이미 DI인 `EntityFactory`와 동형으로. 동작 100% 동일.

**Architecture:** `IEntityCreationDataCreator`에 `EntityType` 프로퍼티를 더해 각 creator가 자기 타입을 선언하고, `EntityCreationDataFactory`는 `IEnumerable<IEntityCreationDataCreator>`를 주입받아 `EntityType` 키 dict로 디스패치한다. attribute+reflection+Activator 제거. 호출 3곳은 주입된 `IEntityCreationDataFactory` 인스턴스 사용.

**Tech Stack:** Unity (서버, 단일 Assembly-CSharp), VContainer DI, GameFramework(공유, 불변).

---

## Repo & Branch

전 작업이 **서버 repo 하나**(GameFramework 불변, 클라 불변).

| 항목 | 값 |
|---|---|
| 코드 repo | `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server` |
| 브랜치 | `feature/server-creationdata-di` (Task 1에서 생성) |
| spec/plan | 클라 repo (이미 커밋, 변경 없음) |

main 직접 커밋 금지. 모든 commit은 서버 repo 피처 브랜치에.

## UnityMCP 인스턴스 핀 — **서버**

이번은 사용자 지시 서버 작업. UnityMCP 호출마다 `unity_instance`를 **서버**로 명시:
1. `mcpforunity://instances`에서 `name == "LeagueOfPhysical-Server"`의 전체 `id`(`Name@hash`)를 resolve. (작성 시점: `LeagueOfPhysical-Server@f99391fa2dbaaf3c` — hash 변동 가능.)
2. `refresh_unity`/`read_console`에 `unity_instance="<서버 id>"`. **클라로 라우팅 금지.**

## 배경 사실 (조사)

- `EntityType`은 LOP enum(`Assets/Scripts/Entity/Entity.cs`, `namespace LOP`) — 값 `Character`/`Item`. `EntityTypeComponent.entityType`의 타입이 `EntityType`. 따라서 dict 키를 강타입 `EntityType`로 둔다.
- `EntityFactory`(GameFramework)가 이미 DI 패턴(`IEnumerable<IEntityCreator>` 생성자 주입, dict 디스패치) — 이 plan은 그 동형.
- 호출처 3곳: `LOPEntityManager.cs:193`, `LOPGame.cs:252`, `LOPGame.cs:280`. `LOPEntityManager`는 `[DIMonoBehaviour]`(필드 주입), `LOPGame`은 `IGame`(필드 주입) — 둘 다 `[Inject]` 가능.
- `OrEmpty()`/`TryGetEntityComponent`는 GameFramework 확장(`using GameFramework;`로 가용 — 현 팩토리/매니저가 사용 중).

## File Structure (서버)

| 파일 | 변경 | 책임 |
|---|---|---|
| `Assets/Scripts/EntityCreationDataFactory/IEntityCreationDataCreator.cs` | Modify | 비제네릭 인터페이스에 `EntityType EntityType { get; }` |
| `Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs` | Modify | `EntityType` 프로퍼티 구현 + (Task 2) attribute 제거 |
| `Assets/Scripts/EntityCreationDataFactory/ItemCreationDataCreator.cs` | Modify | 동일 |
| `Assets/Scripts/EntityCreationDataFactory/IEntityCreationDataFactory.cs` | Create | DI 팩토리 인터페이스 |
| `Assets/Scripts/EntityCreationDataFactory/EntityCreationDataFactory.cs` | Modify | static → DI 인스턴스 |
| `Assets/Scripts/EntityCreationDataFactory/EntityCreationDataCreatorRegistrationAttribute.cs` | Delete | 미사용화 |
| `Assets/Scripts/Game/GameLifetimeScope.cs` | Modify | 팩토리 + 두 creator 등록 |
| `Assets/Scripts/Entity/LOPEntityManager.cs` | Modify | 주입 + 인스턴스 호출 |
| `Assets/Scripts/Game/LOPGame.cs` | Modify | 주입 + 인스턴스 호출(2곳) |

**Task 순서:** Task 1은 *additive*(인터페이스 멤버 + creator 구현, 팩토리는 아직 static/attribute) → 컴파일·동작 불변. Task 2는 static→instance *원자적 전환*(호출처가 깨지므로 팩토리+DI+호출처+attribute제거+파일삭제를 한 커밋에).

> **테스트 전략:** 신규 자동화 테스트 없음(단일 Assembly-CSharp). 순수 구조 리팩터라 검증 = 서버 컴파일 + 수동 플레이(출력 동일=회귀 0). TDD 부적용.

---

## Task 1: IEntityCreationDataCreator에 EntityType 프로퍼티 (additive)

**Files:**
- Modify `Assets/Scripts/EntityCreationDataFactory/IEntityCreationDataCreator.cs`
- Modify `Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs`
- Modify `Assets/Scripts/EntityCreationDataFactory/ItemCreationDataCreator.cs`

- [ ] **Step 1: 피처 브랜치 생성 (server repo)**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git checkout -b feature/server-creationdata-di
git branch --show-current   # → feature/server-creationdata-di
```
(이미 있으면 `git checkout feature/server-creationdata-di`.)

- [ ] **Step 2: 인터페이스에 EntityType 추가** — `IEntityCreationDataCreator.cs` 전체를 다음으로:
```csharp
using GameFramework;

namespace LOP
{
    public interface IEntityCreationDataCreator
    {
        EntityType EntityType { get; }
        EntityCreationData Create(IEntity entity);
    }

    public interface IEntityCreationDataCreator<in TEntity> : IEntityCreationDataCreator
        where TEntity : IEntity
    {
        EntityCreationData Create(TEntity entity);
    }
}
```

- [ ] **Step 3: CharacterCreationDataCreator에 프로퍼티 구현** — 클래스 본문 맨 위(첫 메서드 `Create(LOPEntity)` 앞)에 한 줄 추가. attribute는 *그대로 둔다*(Task 2에서 제거):
```csharp
    [EntityCreationDataCreatorRegistration(EntityType.Character)]
    public class CharacterCreationDataCreator : IEntityCreationDataCreator<LOPEntity>
    {
        public EntityType EntityType => EntityType.Character;

        public EntityCreationData Create(LOPEntity lopEntity)
        {
```
> `public EntityType EntityType => EntityType.Character;` — 프로퍼티명이 타입명과 같지만 C#의 "Color Color" 규칙으로 정상 컴파일(우변 `EntityType.Character`는 타입의 정적 멤버 접근으로 해소). 의도된 형태이니 바꾸지 말 것.

- [ ] **Step 4: ItemCreationDataCreator에 프로퍼티 구현** — 동일하게 클래스 본문 맨 위에:
```csharp
    [EntityCreationDataCreatorRegistration(EntityType.Item)]
    public class ItemCreationDataCreator : IEntityCreationDataCreator<LOPEntity>
    {
        public EntityType EntityType => EntityType.Item;

        public EntityCreationData Create(LOPEntity lopEntity)
        {
```

- [ ] **Step 5: 컴파일 확인** — UnityMCP(서버 핀): `refresh_unity` → `read_console(types=["error"])`. Expected: 0 errors. (프로퍼티는 additive, 팩토리는 아직 attribute 사용 — 동작 불변.) **실제로 실행하고 결과 보고.**

- [ ] **Step 6: Commit**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/EntityCreationDataFactory/IEntityCreationDataCreator.cs Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs Assets/Scripts/EntityCreationDataFactory/ItemCreationDataCreator.cs
git commit -m "refactor(server): add EntityType property to IEntityCreationDataCreator

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: EntityCreationDataFactory static → DI 인스턴스 (원자적 전환)

**Files:**
- Create `Assets/Scripts/EntityCreationDataFactory/IEntityCreationDataFactory.cs`
- Modify `Assets/Scripts/EntityCreationDataFactory/EntityCreationDataFactory.cs`
- Modify `Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs` (attribute 제거)
- Modify `Assets/Scripts/EntityCreationDataFactory/ItemCreationDataCreator.cs` (attribute 제거)
- Delete `Assets/Scripts/EntityCreationDataFactory/EntityCreationDataCreatorRegistrationAttribute.cs` (+ `.meta`)
- Modify `Assets/Scripts/Game/GameLifetimeScope.cs`
- Modify `Assets/Scripts/Entity/LOPEntityManager.cs`
- Modify `Assets/Scripts/Game/LOPGame.cs`

> 이 Task는 한 커밋. 중간엔 컴파일 안 됨(static 호출이 깨짐) — 모든 step 후 컴파일 확인.

- [ ] **Step 1: IEntityCreationDataFactory 인터페이스 생성** — `Assets/Scripts/EntityCreationDataFactory/IEntityCreationDataFactory.cs`:
```csharp
using GameFramework;

namespace LOP
{
    public interface IEntityCreationDataFactory
    {
        EntityCreationData Create(IEntity entity);
    }
}
```

- [ ] **Step 2: EntityCreationDataFactory를 DI 인스턴스로 재작성** — `EntityCreationDataFactory.cs` 전체를 다음으로(정적 생성자/RegisterCreators/Activator/AppDomain 스캔 제거):
```csharp
using GameFramework;
using System;
using System.Collections.Generic;

namespace LOP
{
    public class EntityCreationDataFactory : IEntityCreationDataFactory
    {
        private readonly Dictionary<EntityType, IEntityCreationDataCreator> creators
            = new Dictionary<EntityType, IEntityCreationDataCreator>();

        // creator는 DI 컨테이너가 생성·주입해 IEnumerable로 전달한다. (정적 캐시/Activator 없음 →
        // 스코프와 함께 생성·해제되어 룸 재입장 시 stale 참조가 생기지 않는다.)
        public EntityCreationDataFactory(IEnumerable<IEntityCreationDataCreator> creators)
        {
            foreach (var creator in creators.OrEmpty())
            {
                this.creators[creator.EntityType] = creator;
            }
        }

        public EntityCreationData Create(IEntity entity)
        {
            if (entity.TryGetEntityComponent<EntityTypeComponent>(out var entityTypeComponent) == false)
            {
                throw new InvalidOperationException(
                    $"Entity '{entity.entityId}' does not have an EntityTypeComponent. " +
                    "Ensure the entity is properly initialized with its components."
                );
            }

            if (creators.TryGetValue(entityTypeComponent.entityType, out var creator) == false)
            {
                throw new InvalidOperationException(
                    $"No registered creation-data creator found for entity type '{entityTypeComponent.entityType}'. " +
                    "Ensure the appropriate IEntityCreationDataCreator is registered in the DI container."
                );
            }

            return creator.Create(entity);
        }
    }
}
```

- [ ] **Step 3: 두 creator에서 attribute 제거** — `CharacterCreationDataCreator.cs`에서 클래스 위 `[EntityCreationDataCreatorRegistration(EntityType.Character)]` 줄 삭제. `ItemCreationDataCreator.cs`에서 `[EntityCreationDataCreatorRegistration(EntityType.Item)]` 줄 삭제. (Task 1에서 추가한 `EntityType` 프로퍼티는 유지.) 두 파일의 `using`은 그대로(`ArgumentException`에 `using System;` 필요 — 유지).

- [ ] **Step 4: 어트리뷰트 파일 삭제**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git rm Assets/Scripts/EntityCreationDataFactory/EntityCreationDataCreatorRegistrationAttribute.cs Assets/Scripts/EntityCreationDataFactory/EntityCreationDataCreatorRegistrationAttribute.cs.meta
```

- [ ] **Step 5: GameLifetimeScope 등록** — `Assets/Scripts/Game/GameLifetimeScope.cs`의 기존 entity 생성 등록 블록:
```csharp
            builder.Register<IEntityCreator, CharacterCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreator, ItemCreator>(Lifetime.Singleton);
            builder.Register<IEntityFactory, EntityFactory>(Lifetime.Singleton);
```
뒤에 3줄 추가(동형):
```csharp
            builder.Register<IEntityCreator, CharacterCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreator, ItemCreator>(Lifetime.Singleton);
            builder.Register<IEntityFactory, EntityFactory>(Lifetime.Singleton);
            builder.Register<IEntityCreationDataCreator, CharacterCreationDataCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreationDataCreator, ItemCreationDataCreator>(Lifetime.Singleton);
            builder.Register<IEntityCreationDataFactory, EntityCreationDataFactory>(Lifetime.Singleton);
```

- [ ] **Step 6: LOPEntityManager 주입 + 호출 교체** — `Assets/Scripts/Entity/LOPEntityManager.cs`. 주입 필드 블록(`[Inject] private IEntityFactory entityFactory;` 옆)에 추가:
```csharp
        [Inject]
        private IEntityCreationDataFactory entityCreationDataFactory;
```
그리고 `GetAllEntityCreationDatas`의 정적 호출(193줄 부근):
```csharp
                EntityCreationData entityCreationData = EntityCreationDataFactory.Create(entity);
```
을:
```csharp
                EntityCreationData entityCreationData = entityCreationDataFactory.Create(entity);
```

- [ ] **Step 7: LOPGame 주입 + 호출 교체(2곳)** — `Assets/Scripts/Game/LOPGame.cs`. 주입 필드 블록(`[Inject] private ISessionManager sessionManager;` 옆)에 추가:
```csharp
        [Inject]
        private IEntityCreationDataFactory entityCreationDataFactory;
```
그리고 `SpawnEnemy`(252줄 부근)와 `SpawnExpMarble`(280줄 부근)의 두 정적 호출:
```csharp
                EntityCreationData = EntityCreationDataFactory.Create(entity),
```
을 각각:
```csharp
                EntityCreationData = entityCreationDataFactory.Create(entity),
```

- [ ] **Step 8: 컴파일 확인** — UnityMCP(서버 핀): `refresh_unity` → `read_console(types=["error"])`. Expected: 0 errors (CS2001 포함 0 — 파일 삭제 후 stale 시 `refresh_unity(scope="all", mode="force")` 재실행). **실제 실행하고 보고.**

- [ ] **Step 9: Commit**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/EntityCreationDataFactory/ Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Entity/LOPEntityManager.cs Assets/Scripts/Game/LOPGame.cs
git commit -m "refactor(server): EntityCreationDataFactory static -> DI instance (mirror EntityFactory)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
> `LOPGame.cs`는 미커밋 작업 중 파일이라 working-tree에 무관 변경이 있을 수 있음. **이번 변경(주입 1줄 + 호출 2줄)만** 스테이지되도록 `git add -p` 또는 해당 hunk만 add. 무관 변경은 커밋에 포함하지 말 것.

---

## Task 3: 런타임 검증 (수동)

> 순수 구조 리팩터 — 출력 동일이 성공.

- [ ] **Step 1: 서버 플레이 + 관찰** — 서버 에디터 Play(룸 연결). 콘솔에서:
  - [ ] 적 스폰(10초마다 `SpawnEnemy`) 시 클라가 캐릭터 정상 수신(외형/HP).
  - [ ] 플레이어 스폰 정상.
  - [ ] 캐릭터 사망 시 경험치구슬(Item) 스폰 + 클라 수신 정상.
  - [ ] **늦은 접속** 클라가 기존 전체 엔티티 정상 수신(`GetAllEntityCreationDatas` 경로).
  - [ ] 콘솔 에러 0 (DI 누락 NRE 없음 — 팩토리/creator 주입 정상). 기존 `Registered Creator: ...` 정적 로그는 사라짐(정상 — DI 등록으로 대체).

- [ ] **Step 2: 검증 실패 시** — superpowers:systematic-debugging. 예상 위험: (a) creator 미등록 → `No registered creation-data creator` 예외 → GameLifetimeScope 등록 확인. (b) NRE → 호출처 주입 누락 확인.

---

## Self-Review (작성자 체크 결과)

- **Spec coverage:** spec의 8개 변경(인터페이스 프로퍼티/팩토리 인터페이스/팩토리 재작성/두 creator/attribute 삭제/GameLifetimeScope/3 호출처) → Task 1(인터페이스+creator 프로퍼티) + Task 2(나머지 전부) 매핑. ✓
- **Placeholder scan:** TBD/TODO 없음. 모든 코드 step에 실제 코드. 서버 `<서버 id>`는 구현 시 resolve. ✓
- **Type consistency:** `EntityType EntityType { get; }`(Task1 인터페이스) ↔ 구현(Task1 creators) ↔ dict 키(Task2 팩토리 `Dictionary<EntityType, ...>`, `creator.EntityType`). `IEntityCreationDataFactory.Create(IEntity)`(Task2 인터페이스) ↔ 팩토리 구현 ↔ 등록(GameLifetimeScope) ↔ 호출(LOPEntityManager/LOPGame `entityCreationDataFactory.Create`). ✓
- **컴파일 순서:** Task1 additive(컴파일), Task2 원자적 전환(모든 step 후 컴파일). ✓
- **단일 repo:** 전부 서버 repo. GameFramework/클라 불변. ✓
