# 로직/시뮬을 LOPActor에서 World.Entity로 분리 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 순수 로직/시뮬 사이트가 `LOPActor`/`LOPEntityManager`(뷰) 대신 `GameFramework.World.Entity` + `EntityRegistry`(데이터)로 동작하게 바꾼다. `LOPActor`는 뷰 레이어만 보유. 값 동치(id 동일 → 동작 무변화).

**Architecture:** 로직 사이트가 들고 있던 `LOPActor`를 걷어내고, id/데이터가 필요하면 `worldEntity.Id`/`entityRegistry`로 직접 얻는다. `IBrain` 제네릭을 없애 `Think(World.Entity)`로, 서버 `IEntityCreationDataCreator`를 `Create(World.Entity)`로, `PlayerContext.actor`(LOPActor)를 `entityId`(string)로 강등. 뷰/UI 컴포넌트는 범위 밖.

**Tech Stack:** Unity(C#), VContainer(DI), 순수 C# World Core(`GameFramework.World`).

## Global Constraints

- **2레포:** Client(`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client`), Server(`C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server`). **GameFramework·LOP-Shared 무변경.**
- **피처 브랜치:** 각 레포 `feature/logic-decouple-from-actor`. main 직접 커밋 금지. Client엔 이미 존재(spec/plan 커밋됨). Server는 Task 2에서 생성.
- **컴파일 검증(UnityMCP, 컨트롤러):** 수정 후 `refresh_unity`(compile:request) → `read_console`(types:["error"]). `unity_instance` 명시(client=`LeagueOfPhysical-Client@<hash>`, server=`LeagueOfPhysical-Server@<hash>`; hash는 `mcpforunity://instances`). **EditMode 없음**(전부 Assembly-CSharp, `[[client-test-infra-constraint]]`) — 검증=컴파일 + 인게임 스모크(사용자).
- **구현 서브에이전트 = 코드+git만.** 컨트롤러가 컴파일 검증.
- **값 동치가 기대치:** `actor.entityId`와 `worldEntity.Id`는 같은 문자열 → 라우팅/판정 동일. 로직을 *데이터로* 옮기기만 한다(동작 보존).
- **서버 do-not-commit 픽스처**(DefaultVolumeProfile/ConfigureRoomComponent/GameRuleSystem/GraphicsSettings) 미스테이징 — `git add <파일>` 명시(‑A 금지). ⚠️ GameRuleSystem은 이 리팩터의 *대상*이지만 픽스처 수정도 얹혀 있으니 **실코드 hunk만 부분 스테이징**(fixture SpawnEnemies 값·playerList 등은 커밋 금지).
- **뷰/UI·GF 무변경:** LOPEntityView·interpolator 본체(단 RemoteEntityInterpolator의 죽은 `actor` 필드 삭제는 대상)·floater·nameplate·UI VM 본체·GameFramework 추상은 건드리지 않는다.

## 전환 규칙 (모든 로직 사이트 공통)

| 현재 | → |
|---|---|
| `entityManager.GetEntities<LOPActor>()` (로직 순회) | `entityRegistry.All` (`GameFramework.World.Entity` 순회) |
| `actor.entityId` (로직) | `worldEntity.Id` |
| `entityManager.GetEntity<LOPActor>(id)` (로직) | `entityRegistry.Get(id)` |
| `playerContext.actor` (클라 로직) | `playerContext.entityId` |
| `playerContext.actor.entityId` | `playerContext.entityId` |

로직 클래스가 `entityRegistry`를 아직 주입 안 받으면 `[Inject] private GameFramework.World.EntityRegistry entityRegistry;` 추가(대부분 이미 보유). `Runner.current.entityManager.GetEntities<LOPActor>()` 패턴도 `entityRegistry.All`로.

---

## 파일 구조

**Client (Task 1 = B1):**
- Modify: `Game/IPlayerContext.cs`, `Game/PlayerContext.cs` — `actor`(LOPActor) → `entityId`(string), `entityView` 유지.
- Modify: `Entity/RemoteEntityInterpolator.cs` — 죽은 `actor` 필드 삭제. `Game/EntityBinder.cs` — 그 필드 할당 제거.
- Modify: `Entity/Reconciler.cs`, `Game/LOPRunner.cs`, `Game/MessageHandler/Game.Entity.MessageHandler.cs` (+ 다른 클 핸들러가 playerContext.actor/`<LOPActor>`를 쓰면 함께), `UI/CharacterHud/CharacterHudViewModel.cs`, `UI/Stats/StatsViewModel.cs`.

**Server (Task 2 = B2):**
- Modify: `AI/IBrain.cs`, `AI/EnemyBrain.cs`, `Entity/LOPAIController.cs`.
- Modify: `EntityCreationDataFactory/IEntityCreationDataCreator.cs`, `EntityCreationDataFactory.cs`, `CharacterCreationDataCreator.cs`, `ItemCreationDataCreator.cs`.
- Modify: `Game/LOPRunner.cs`, `Game/GameRuleSystem.cs`(실코드 hunk만), `World/DeathCascadeSystem.cs`, server `Game/MessageHandler/*` (actor→registry).

---

### Task 1: B1 — 클라 로직 + 즉시 2건

**Files:** (위 Client 목록)

**Interfaces:**
- Produces: `IPlayerContext.entityId : string` (was `actor : LOPActor`); `entityView` 유지.
- Consumes: `entityRegistry.Get(id)`, `worldEntity.Id`.

- [ ] **Step 1: `IPlayerContext.cs` + `PlayerContext.cs` — actor→entityId**

`Game/IPlayerContext.cs`:
```csharp
using GameFramework;

namespace LOP
{
    public interface IPlayerContext
    {
        ISession session { get; set; }
        string entityId { get; set; }
        LOPEntityView entityView { get; set; }
    }
}
```
`Game/PlayerContext.cs`:
```csharp
using GameFramework;

namespace LOP
{
    public class PlayerContext : IPlayerContext
    {
        public ISession session { get; set; }
        public string entityId { get; set; }
        public LOPEntityView entityView { get; set; }
    }
}
```
(`using UnityEngine;`는 다른 멤버가 안 쓰면 제거.)

- [ ] **Step 2: playerContext.entity 세팅부 — Creator가 `entityId` 세팅**

`Assets/Scripts/EntityCreator/CharacterCreator.cs`에서 `playerContext.actor = actor;`(현 앵커 생성 직후, isUser 분기) → `playerContext.entityId = creationData.entityId;`. (`.entityView`는 EntityBinder가 세팅 — 무변.)

- [ ] **Step 3: playerContext.actor 소비처 전환 (grep + read + 적용)**

`grep -rn "playerContext.actor" Assets/Scripts`로 전 사이트 열거. 각 사이트:
- `playerContext.actor.entityId` → `playerContext.entityId`
- `playerContext.actor`를 LOPActor로 쓰던 곳(없어야 정상 — audit상 전부 id) → `entityRegistry.Get(playerContext.entityId)`(World.Entity 필요 시).
알려진 사이트: `Entity/Reconciler.cs`(`playerContext.actor` → `playerContext.entityId`, 이미 `entityRegistry.Get(...)`로 worldEntity 얻음), `Game/LOPRunner.cs`, `Game/MessageHandler/Game.Entity.MessageHandler.cs`, `UI/CharacterHud/CharacterHudViewModel.cs`, `UI/Stats/StatsViewModel.cs`. 각 파일 읽고 위 규칙 적용.

- [ ] **Step 4: 클 로직의 `GetEntity(ies)<LOPActor>` / `actor.entityId` → registry/데이터**

`grep -rn "GetEntities<LOPActor>\|GetEntity<LOPActor>\|TryGetEntity<LOPActor>" Assets/Scripts`로 클 로직 사이트 열거. 각 사이트가 **id/데이터만** 쓰면(대부분) 전환 규칙 적용(`entityRegistry.All`/`.Get(id)`, `worldEntity.Id`). **단 `.gameObject`/`GetComponent<...>`를 쓰는 사이트는 뷰-레이어라 그대로 둔다**(예: `Game.Entity.MessageHandler`가 `actor.GetComponent<RemoteEntityInterpolator>()` 하면 그건 유지 — LEGIT). 존재-가드용 `TryGetEntity<LOPActor>(out _)`는 `entityRegistry.Contains(id)`로 대체 가능하면 대체, 애매하면 유지.

- [ ] **Step 5: 죽은 `RemoteEntityInterpolator.actor` 삭제**

`Entity/RemoteEntityInterpolator.cs`에서 `public LOPActor actor { get; set; }` 필드 삭제(할당만 되고 읽는 곳 없음 — audit 확인). `Game/EntityBinder.cs`에서 `interpolator.actor = actor;`(RemoteEntityInterpolator 배선의 그 줄) 삭제. `LocalEntityInterpolator`도 `actor`를 안 읽으면(audit: line 57서 entityId 1회) — 읽으면 유지, 안 읽으면 동일 삭제. **읽는지 파일 확인 후 결정**(값 동치: 안 읽는 필드만 삭제).

- [ ] **Step 6: 클 컴파일 확인 (컨트롤러)**

`refresh_unity`+`read_console`(Client). Expected: 에러 0.

- [ ] **Step 7: 커밋 (Client)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/IPlayerContext.cs Assets/Scripts/Game/PlayerContext.cs Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/Entity/Reconciler.cs Assets/Scripts/Entity/RemoteEntityInterpolator.cs Assets/Scripts/Game/EntityBinder.cs Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs Assets/Scripts/UI/CharacterHud/CharacterHudViewModel.cs Assets/Scripts/UI/Stats/StatsViewModel.cs
# (실제 변경 파일에 맞춰 조정)
git commit -m "refactor(entity): 클라 로직을 LOPActor→World.Entity/id로 분리 (B1)"
```

- [ ] **Step 8: 인게임 스모크(사용자)** — 내 캐릭 스폰·이동·롤백·HUD/Stats·카메라 추적 무변화.

---

### Task 2: B2 — 서버 로직

**Files:** (위 Server 목록)

**Interfaces:**
- Produces: `IBrain.Think(GameFramework.World.Entity, double)` (제네릭 드롭); `IEntityCreationDataCreator.Create(GameFramework.World.Entity)`.

- [ ] **Step 1: `AI/IBrain.cs` — 제네릭 드롭**

```csharp
namespace LOP
{
    public interface IBrain
    {
        void Think(GameFramework.World.Entity worldEntity, double deltaTime);
    }
}
```
(제네릭 `IBrain<T>` 제거. `using UnityEngine;` 불필요하면 제거.)

- [ ] **Step 2: `AI/EnemyBrain.cs` — Think(World.Entity)로**

`EnemyBrain : IBrain`. `public void Think(GameFramework.World.Entity worldEntity, double deltaTime)`. 본문:
- `entity.entityId` → `worldEntity.Id`
- `Runner.current.entityManager.GetEntities<LOPActor>()` → `entityRegistry.All` (그리고 후보 `e`는 `World.Entity` → `e.Id`)
- 위치/스탯은 이미 `entityRegistry.Get(...)`/`statsSystem`로 읽음 — 대상 worldEntity를 직접 쓰도록 정리(불필요한 재조회 제거).
- `abilityActivator.TryActivate(worldEntity.Id, ...)`.
현 `Think(IEntity)`/`Think(LOPActor)` 오버로드가 있으면 단일 `Think(World.Entity)`로 통합. 파일 읽고 적용.

- [ ] **Step 3: `Entity/LOPAIController.cs` — worldEntity 해석 후 brain.Think**

컨트롤러는 뷰-브리지라 `actor`(LOPActor) 보유 유지. `[Inject] private GameFramework.World.EntityRegistry entityRegistry;` 추가(없으면). `brain` 필드 타입 `IBrain<LOPActor>` → `IBrain`. `SetBrain(IBrain)`. Think 호출부:
```csharp
var worldEntity = entityRegistry.Get(actor.entityId);
if (worldEntity != null) brain.Think(worldEntity, deltaTime);
```
(`SetBrain`이 `objectResolver.Resolve<EnemyBrain>()`로 주입되는데 `EnemyBrain : IBrain`이라 그대로 됨. `EntityViewSpawner`의 `SetBrain` 호출부 타입만 확인.)

- [ ] **Step 4: `IEntityCreationDataCreator.cs` — Create(World.Entity)로 (제네릭 드롭)**

```csharp
namespace LOP
{
    public interface IEntityCreationDataCreator
    {
        EntityType EntityType { get; }
        EntityCreationData Create(GameFramework.World.Entity worldEntity);
    }
}
```
(제네릭 `IEntityCreationDataCreator<TEntity>` 제거 — LOPActor 담으려던 것.)

- [ ] **Step 5: `CharacterCreationDataCreator.cs` / `ItemCreationDataCreator.cs` — Create(World.Entity)**

`: IEntityCreationDataCreator`(제네릭 제거). `public EntityCreationData Create(GameFramework.World.Entity worldEntity)`. 본문의 `GameFramework.World.Entity worldEntity = entityRegistry.Get(lopEntity.entityId);` **삭제**(파라미터로 받음), `lopEntity.entityId` → `worldEntity.Id`. 나머지(Health/Mana/… 읽기)는 그대로. `[Inject] entityRegistry`가 더 안 쓰이면 제거.

- [ ] **Step 6: `EntityCreationDataFactory.cs` — Create(World.Entity) 디스패치**

`Create(LOPActor actor)` → `Create(GameFramework.World.Entity worldEntity)`. EntityKind 조회를 `worldEntity.Get<EntityKind>()`로(현재 `entityRegistry.Get(actor.entityId).Get<EntityKind>()`였으면 worldEntity 직접), 디스패치 `creator.Create(worldEntity)`. 파일 읽고 적용.

- [ ] **Step 7: 팩토리 호출부 — worldEntity 넘기기**

`Game/GameRuleSystem.cs`(**실코드 hunk만 스테이징**)·`World/DeathCascadeSystem.cs`에서 `entityCreationDataFactory.Create(actor)` → `Create(entityRegistry.Get(id))` 또는 이미 손에 있는 worldEntity. `GetEntity<LOPActor>` → `entityRegistry.Get(id)` 전환도 함께(로직).

- [ ] **Step 8: 서버 `LOPRunner.cs` 루프 + 메시지핸들러 — registry/데이터로**

`Game/LOPRunner.cs` 3사이트(`GetEntities<LOPActor>()` 순회 + `actor.entityId`; `GetEntityByUserId<LOPActor>`) → `entityRegistry.All`/`worldEntity.Id`. `GetEntityByUserId`는 매니저의 userId→entityId 매핑이 필요하니 **매니저에 `string GetEntityIdByUserId(userId)` 얇게 두거나** 기존 유지(뷰-매니저에 남김) — 로직은 그 id로 `entityRegistry.Get`. 서버 `Game/MessageHandler/*`의 `actor.entityId`(로직 라우팅)도 규칙 적용. 각 파일 읽고 적용. **`.gameObject`/`GetComponent` 사이트는 유지**(뷰-브리지).

- [ ] **Step 9: 서버 컴파일 확인 (컨트롤러)**

브랜치 생성 후. `refresh_unity`+`read_console`(Server). Expected: 에러 0.

- [ ] **Step 10: 커밋 (Server)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git checkout -b feature/logic-decouple-from-actor
# 실제 변경 파일만 명시 add (GameRuleSystem은 실코드 hunk만 — git add -p 사용, fixture hunk 제외)
git add Assets/Scripts/AI/IBrain.cs Assets/Scripts/AI/EnemyBrain.cs Assets/Scripts/Entity/LOPAIController.cs Assets/Scripts/EntityCreationDataFactory/ Assets/Scripts/Game/LOPRunner.cs Assets/Scripts/World/DeathCascadeSystem.cs Assets/Scripts/Game/MessageHandler/
git add -p Assets/Scripts/Game/GameRuleSystem.cs   # 실코드 hunk만 y, fixture hunk는 n
git commit -m "refactor(entity): 서버 로직을 LOPActor→World.Entity/registry로 분리 (B2)"
```

- [ ] **Step 11: 인게임 스모크(사용자)** — AI 이동·공격·스폰·디스폰/exp·아이템·입력 무변화.

---

## Self-Review (스펙 대비)

- **spec 전환 규칙(GetEntities<LOPActor>→All, actor.entityId→Id, playerContext.actor→entityId, IBrain 드롭, Create(World.Entity))** → Task 1/2 각 Step. ✓
- **spec 범위(서버 로직 목록 / 클 로직 목록 / 즉시 2건)** → Task 2(서) / Task 1(클) / Task1 Step1·5. ✓
- **spec 범위 밖(뷰/UI 본체·GF 무변)** → 전환 규칙에 "`.gameObject`/`GetComponent` 사이트 유지" 명시, GF 미터치. ✓
- **spec IBrain 배선(AIController가 worldEntity 해석)** → Task2 Step3. ✓
- **spec 미정 세부(userEntityMap 매핑 접근)** → Task2 Step8에서 "매니저에 얇게 or 기존 유지" 확정 지시. ✓
- **위험: 로직이 실은 GameObject 필요** → 전환 시 `.gameObject`/`GetComponent` 발견하면 유지(재분류) 지시. ✓
- **위험: GameRuleSystem 픽스처** → Step7·10에서 실코드 hunk만 부분 스테이징(git add -p). ✓
- **테스트=컴파일+인게임** → 각 Task 컨트롤러 컴파일 + 스모크. ✓

## 상태

플랜 작성 완료(2026-07-19). 실행 = subagent-driven(권장). 상태 원장 `docs/ROADMAP.md`.
spec `docs/superpowers/specs/2026-07-19-logic-decouple-from-actor-design.md`.
