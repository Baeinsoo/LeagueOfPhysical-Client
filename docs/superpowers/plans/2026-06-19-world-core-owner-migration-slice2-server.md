# World Core Owner Migration — Slice 2 (Server) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버에서 레거시 `PlayerComponent`를 제거하고 — 플레이어 엔티티에 `World.Ownership`(마커+OwnerId) 생성, 마커 5곳을 `Has<Ownership>()`로 repoint, 레벨업 보상/할당/스냅샷의 statPoints를 `World.Stats.UnspentPoints`로 이행.

**Architecture:** 서버 single Assembly-CSharp LOP 글루 + GameFramework 코어(Slice 1 머지: `Ownership`, `Stats.UnspentPoints`, `StatsSystem.AddUnspent/Allocate/SetUnspent`). 마커 = `entityRegistry.Get(id)?.Has<GameFramework.World.Ownership>() == true`(5곳 전부 EntityRegistry 도달 가능 — 조사 확인). statPoints는 상주 값(레벨업 grant + 할당 spend), wire `UserEntitySnapToC.StatPoints`(필드 7) 무변경. 자동 테스트 없음 — 컴파일 + 수동 플레이.

**Tech Stack:** C# (Unity), VContainer DI, UnityMCP 컴파일 검증.

**Related spec:** `docs/superpowers/specs/2026-06-19-world-core-owner-migration-design.md` (Slice 2). 컴포넌트명 `Ownership`(← `Owner`에서 리네임, Component.Owner 충돌 회피), ctor `Ownership(string ownerId)`, 마커=`Has<Ownership>()`.

**Resolved Unity instance (매 UnityMCP 호출에 명시 — HTTP stateless):** Server `LeagueOfPhysical-Server@f99391fa2dbaaf3c` (서버 작업 인가).

---

## ⚠️ 서버 픽스처 — stash 댄스 필요

`LOPGame.cs`(레벨업 grant 줄, Task 3 수정)는 **커밋 금지 로컬 픽스처**(SpawnEnemies 수 줄임)가 dirty로 떠 있다. `ConfigureRoomComponent.cs`도 dirty(무관). + 기존 `stash@{0}: 5-23`. Level Slice 2 선례대로 **픽스처 2파일 전용 stash → 깨끗한 baseline에서 Owner 변경 → 커밋 → pop 복원**. 레벨업 grant는 `HandleItemTouch`(≈L171-191), 픽스처는 `SpawnEnemies`(≈L210)라 비겹침 → pop clean 예상(Task 1에서 확인).

---

## File Structure (server repo: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`)

- **Modify:** `Assets/Scripts/EntityCreator/CharacterCreator.cs` — Ownership 생성(플레이어 gate) + PlayerComponent 생성 제거.
- **Modify:** `Assets/Scripts/Game/LOPGame.cs` (⚠️픽스처) — `[Inject] StatsSystem` + 레벨업 grant → AddUnspent.
- **Modify:** `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs` — `OnStatAllocationToS` → Allocate.
- **Modify:** `Assets/Scripts/Game/LOPGameEngine.cs` — 스냅샷 statPoints read → UnspentPoints.
- **Modify (마커 repoint):** `Assets/Scripts/CombatSystem/LOPCombatSystem.cs`, `Assets/Scripts/Game/LOPActionManager.cs`, `Assets/Scripts/AI/EnemyBrain.cs`, `Assets/Scripts/Component/PhysicsComponent.cs`.
- **Delete:** `Assets/Scripts/Component/PlayerComponent.cs` (+`.meta`).
- 무변경(커밋 제외): `LOPGame.cs`/`ConfigureRoomComponent.cs` 픽스처분. `GameLifetimeScope.cs`(StatsSystem 이미 등록, Ownership 시스템 없음 — 확인만).

---

## Task 1: 서버 브랜치 + 픽스처 stash

- [ ] **Step 1: 상태 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" stash list
```
Expected: dirty = `LOPGame.cs` + `ConfigureRoomComponent.cs`, `stash@{0}: 5-23`. 다르면 중단.

- [ ] **Step 2: 브랜치 생성**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" checkout -b feature/world-core-owner-migration
```

- [ ] **Step 3: 픽스처가 레벨업 grant 영역과 겹치는지 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" diff -- Assets/Scripts/Game/LOPGame.cs | grep "^@@"
```
Expected: hunk 범위가 `HandleItemTouch`(≈L171-191) 및 `[Inject]` 헤더(≈L22-41)를 **포함하지 않아야** 함. 포함하면 중단·에스컬레이트.

- [ ] **Step 4: 픽스처 stash**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" stash push -m "owner-slice2: hold local fixtures (LOPGame, ConfigureRoomComponent)" -- Assets/Scripts/Game/LOPGame.cs Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs
```
- [ ] **Step 5: 검증**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" stash list
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
```
Expected: `stash@{0}` = owner-slice2 hold, `stash@{1}` = 5-23. `status --short` 깨끗.

---

## Task 2: CharacterCreator — Ownership 생성 + 레거시 제거

**Files:** Modify `Assets/Scripts/EntityCreator/CharacterCreator.cs`

- [ ] **Step 1: PlayerComponent 생성 제거 (isPlayer 블록에서)**

`if (isPlayer)` 블록(L50-58)에서 PlayerComponent 3줄(L52-54)만 삭제, `EntityInputComponent`(L56-57)는 유지:
```csharp
        bool isPlayer = !string.IsNullOrEmpty(creationData.userId);
        if (isPlayer)
        {
            PlayerComponent playerComponent = entity.AddEntityComponent<PlayerComponent>();
            objectResolver.Inject(playerComponent);
            playerComponent.Initialize(creationData.userId);

            EntityInputComponent entityInputComponent = entity.AddEntityComponent<EntityInputComponent>();
            objectResolver.Inject(entityInputComponent);
        }
```
→
```csharp
        bool isPlayer = !string.IsNullOrEmpty(creationData.userId);
        if (isPlayer)
        {
            EntityInputComponent entityInputComponent = entity.AddEntityComponent<EntityInputComponent>();
            objectResolver.Inject(entityInputComponent);
        }
```
(`isPlayer` bool은 Step 2의 Ownership gate에 재사용 — 유지.)

- [ ] **Step 2: World.Ownership 추가 (플레이어 gate)**

World Core 블록에서 `worldEntity.Add(worldStats);`(L84) 다음, `entityRegistry.Add(worldEntity);`(L85) 앞에 추가:
```csharp
            if (isPlayer)
            {
                worldEntity.Add(new GameFramework.World.Ownership(creationData.userId));
            }
```
(존재 = 플레이어 마커. NPC/아이템은 Ownership 없음 → `Has<Ownership>()` false.)

---

## Task 3: LOPGame — 레벨업 grant → AddUnspent (⚠️픽스처, stash됨)

**Files:** Modify `Assets/Scripts/Game/LOPGame.cs` (현재 baseline — 픽스처 stash됨)

- [ ] **Step 1: StatsSystem 주입 추가**

`[Inject]` 블록(L22-41)의 `GameFramework.World.LevelSystem levelSystem;`(L40-41) 다음에 추가:
```csharp

        [Inject]
        private GameFramework.World.StatsSystem statsSystem;
```

- [ ] **Step 2: 레벨업 grant repoint**

`HandleItemTouch`의 188줄을 교체:
```csharp
            int gained = levelSystem.AddExperience(level, 10);
            if (gained > 0)
            {
                toucher.GetEntityComponent<PlayerComponent>().statPoints += gained;
            }
```
→
```csharp
            int gained = levelSystem.AddExperience(level, 10);
            if (gained > 0)
            {
                GameFramework.World.Stats stats = entityRegistry.Get(toucher.entityId)?.Get<GameFramework.World.Stats>();
                if (stats != null)
                {
                    statsSystem.AddUnspent(stats, gained);
                }
            }
```
(`entityRegistry` 이미 주입. `level`/`gained`는 위 블록 그대로.)

---

## Task 4: OnStatAllocationToS — Allocate

**Files:** Modify `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs`

- [ ] **Step 1: AddBase-switch + statPoints-- → Allocate 한 호출**

`OnStatAllocationToS`에서 `int statValue = 0;` switch 블록(L47-63) + `entity.GetEntityComponent<PlayerComponent>().statPoints--;`(L65)을 교체. 현재:
```csharp
            int statValue = 0;
            // wire stat 문자열은 소문자 필드명("strength" 등) — 클라가 보내는 기존 계약 유지.
            switch (statAllocationToS.Stat)
            {
                case "strength":
                    statValue = (int)statsSystem.AddBase(stats, (int)GameFramework.World.EntityStatType.Strength, 1);
                    break;
                case "dexterity":
                    statValue = (int)statsSystem.AddBase(stats, (int)GameFramework.World.EntityStatType.Dexterity, 1);
                    break;
                case "intelligence":
                    statValue = (int)statsSystem.AddBase(stats, (int)GameFramework.World.EntityStatType.Intelligence, 1);
                    break;
                case "vitality":
                    statValue = (int)statsSystem.AddBase(stats, (int)GameFramework.World.EntityStatType.Vitality, 1);
                    break;
            }

            entity.GetEntityComponent<PlayerComponent>().statPoints--;
```
→
```csharp
            int statType;
            // wire stat 문자열은 소문자 필드명("strength" 등) — 클라가 보내는 기존 계약 유지.
            switch (statAllocationToS.Stat)
            {
                case "strength": statType = (int)GameFramework.World.EntityStatType.Strength; break;
                case "dexterity": statType = (int)GameFramework.World.EntityStatType.Dexterity; break;
                case "intelligence": statType = (int)GameFramework.World.EntityStatType.Intelligence; break;
                case "vitality": statType = (int)GameFramework.World.EntityStatType.Vitality; break;
                default: return;
            }

            int statValue = statsSystem.Allocate(stats, statType);
```
(`Allocate` = UnspentPoints-- + AddBase + 새 stat 값 반환. UnspentPoints>0 가드 내장 — 기존 무가드 statPoints--보다 안전. `stats` 해석·null 가드[L40-45]는 그대로 위에 있음. `StatAllocationToC` 회신[L67-73]은 `StatValue = statValue` 그대로.)

---

## Task 5: LOPGameEngine — 스냅샷 statPoints read → UnspentPoints

**Files:** Modify `Assets/Scripts/Game/LOPGameEngine.cs`

- [ ] **Step 1: 주석 + read flip**

145줄 주석을 갱신하고 170줄을 교체. `worldEntity`는 L146에서 이미 해석됨 — 재사용.

145줄:
```csharp
                // HP/MP/Level/Exp는 World 코어에서 읽는다. StatPoints는 이행 전까지 legacy(PlayerComponent) 유지.
```
→
```csharp
                // HP/MP/Level/Exp/StatPoints 모두 World 코어에서 읽는다.
```
170줄:
```csharp
                entitySnapsToC.StatPoints = entity.GetEntityComponent<PlayerComponent>().statPoints;
```
→
```csharp
                GameFramework.World.Stats stats = worldEntity?.Get<GameFramework.World.Stats>();
                entitySnapsToC.StatPoints = stats?.UnspentPoints ?? 0;
```

---

## Task 6: 마커 5곳 → Has<Ownership>() repoint

> 마커 = `entityRegistry.Get(id)?.Has<GameFramework.World.Ownership>() == true`. NPC/아이템엔 Ownership 없음 → false(레거시 `HasEntityComponent<PlayerComponent>()`와 동일 의미).

**Files:** Modify `LOPCombatSystem.cs`, `LOPActionManager.cs`, `EnemyBrain.cs`, `PhysicsComponent.cs`

- [ ] **Step 1: LOPCombatSystem.cs (entityRegistry 이미 보유)**

27-28줄 교체:
```csharp
            bool attackerIsPlayer = attacker.HasEntityComponent<PlayerComponent>();
            bool targetIsPlayer = target.HasEntityComponent<PlayerComponent>();
```
→
```csharp
            bool attackerIsPlayer = entityRegistry.Get(attacker.entityId)?.Has<GameFramework.World.Ownership>() == true;
            bool targetIsPlayer = entityRegistry.Get(target.entityId)?.Has<GameFramework.World.Ownership>() == true;
```

- [ ] **Step 2: LOPActionManager.cs — [Inject] EntityRegistry 추가 + repoint**

`[Inject]` 필드 블록(L10-17)에 추가:
```csharp
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;
```
38줄 교체:
```csharp
                .Where(e => !e.HasEntityComponent<PlayerComponent>())
```
→
```csharp
                .Where(e => entityRegistry.Get(e.entityId)?.Has<GameFramework.World.Ownership>() != true)
```

- [ ] **Step 3: EnemyBrain.cs — ctor 파라미터 추가 + repoint**

생성자(L12-15)에 `EntityRegistry` 파라미터 + 필드 추가:
```csharp
        private readonly IActionManager<LOPEntity> actionManager;

        public EnemyBrain(IActionManager<LOPEntity> actionManager)
        {
            this.actionManager = actionManager;
        }
```
→
```csharp
        private readonly IActionManager<LOPEntity> actionManager;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public EnemyBrain(IActionManager<LOPEntity> actionManager, GameFramework.World.EntityRegistry entityRegistry)
        {
            this.actionManager = actionManager;
            this.entityRegistry = entityRegistry;
        }
```
> 실제 ctor 시그니처를 파일에서 확인해 맞춤(IActionManager 제네릭 인자 형태). DI(Transient, `objectResolver.Resolve<EnemyBrain>()`)가 EntityRegistry Singleton 공급.

22줄 교체:
```csharp
                .Where(e => e.HasEntityComponent<PlayerComponent>())
```
→
```csharp
                .Where(e => entityRegistry.Get(e.entityId)?.Has<GameFramework.World.Ownership>() == true)
```

- [ ] **Step 4: PhysicsComponent.cs — [Inject] EntityRegistry 추가 + repoint**

클래스 필드에 추가(`[Inject]` — PhysicsComponent는 생성 시 `objectResolver.Inject` 받음):
```csharp
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;
```
(필요 시 `using VContainer;` 추가 — `[Inject]` 어트리뷰트.)
88줄 교체:
```csharp
            if (otherEntity.HasEntityComponent<PlayerComponent>())
```
→
```csharp
            if (entityRegistry.Get(otherEntity.entityId)?.Has<GameFramework.World.Ownership>() == true)
```

- [ ] **Step 5: PhysicsComponent 주입 populate 검증 (리스크 확인)**

PhysicsComponent의 `[Inject] entityRegistry`가 **모든 생성 경로에서 채워지는지** 확인(안 채워지면 L88에서 NRE). PhysicsComponent를 생성하는 곳을 전수 확인:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" grep -n "AddEntityComponent<PhysicsComponent>" -- Assets/Scripts
```
각 사이트가 직후 `objectResolver.Inject(physicsComponent)`를 호출하는지 확인. **마커 L88은 아이템의 PhysicsComponent가 toucher(플레이어)를 감지하는 경로** — 아이템 생성(ItemCreator)도 PhysicsComponent를 inject하는지 필수 확인. inject 안 하는 경로가 있으면 그 creator에 `objectResolver.Inject(physicsComponent)` 추가(또는 그 사이트만 `LOPEntityManager` 기반 대체). 확인 결과를 보고.

---

## Task 7: 레거시 PlayerComponent 삭제

**Files:** Delete `Assets/Scripts/Component/PlayerComponent.cs` (+`.meta`)

- [ ] **Step 1:** 디스크에서 `Assets/Scripts/Component/PlayerComponent.cs` + `.meta` 삭제.
- [ ] **Step 2: 잔존 참조 0 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" grep -n "PlayerComponent" -- "Assets/Scripts" || echo "NO MATCHES"
```
Expected: `NO MATCHES`. 매치 있으면 그 사이트도 처리 — 중단·확인.

---

## Task 8: 컴파일 검증 (Owner 격리, 픽스처 stash됨)

- [ ] **Step 1:**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
Expected: 에러 0건. 에러 시 해당 태스크로 돌아가 수정.

---

## Task 9: 커밋 (Owner 파일만 — 픽스처 제외)

- [ ] **Step 1: 상태 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
```
Expected: Owner 7개 수정 + PlayerComponent.cs/.meta 삭제. (픽스처는 stash됨이라 안 보임.) 만약 `LOPGame.cs`/`ConfigureRoomComponent.cs`가 보이면 stash 누락 — 중단.

- [ ] **Step 2: stage + 커밋**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" add Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/Game/LOPGame.cs Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs Assets/Scripts/Game/LOPGameEngine.cs Assets/Scripts/CombatSystem/LOPCombatSystem.cs Assets/Scripts/Game/LOPActionManager.cs Assets/Scripts/AI/EnemyBrain.cs Assets/Scripts/Component/PhysicsComponent.cs Assets/Scripts/Component/PlayerComponent.cs Assets/Scripts/Component/PlayerComponent.cs.meta
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" commit -m "feat(world): migrate server PlayerComponent to World.Ownership + Stats.UnspentPoints (marker, level-up grant, allocation, snapshot)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
- [ ] **Step 3: 커밋 확인** — `git -C "..." show --stat HEAD | head -16`. 위 파일들만(8 수정 + PlayerComponent 삭제). **`ConfigureRoomComponent.cs` 포함되면 중단.**

---

## Task 10: 픽스처 복원 (stash pop) + 최종 검증

- [ ] **Step 1: pop**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" stash pop
```
Expected: clean pop(`LOPGame.cs`·`ConfigureRoomComponent.cs` dirty 복원). **CONFLICT 시 중단·에스컬레이트.**
- [ ] **Step 2: 검증**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" stash list
```
Expected: `status` = `M LOPGame.cs` + `M ConfigureRoomComponent.cs`(픽스처 복원). `stash list` = `stash@{0}: 5-23`(기존)만.
- [ ] **Step 3: 결합 재컴파일**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
Expected: 에러 0건.

---

## Task 11: 런타임 수동 검증 (사용자)

서버 플레이로:
1. **마커 동작**: AI가 플레이어를 타겟팅(EnemyBrain), 액션이 NPC만 필터(LOPActionManager), 아이템 획득이 플레이어 접촉에만 발동(PhysicsComponent→ItemTouch), combat 플레이어/NPC 판정(LOPCombatSystem) — 전부 정상.
2. **레벨업**: 경험치구슬 획득 → statPoints(UnspentPoints) 증가.
3. **할당**: +버튼 → 스탯 증가·statPoints 감소.
4. `[World] ... Stats not found`/NRE(특히 PhysicsComponent 마커) 경고·에러 0. 데미지→사망 회귀 정상.

동작 보존(회귀 0)이 성공.

---

## Slice 2 완료 기준

- [ ] 서버 `feature/world-core-owner-migration`에 Owner 커밋 1개(픽스처 미포함).
- [ ] 서버 컴파일 0에러. `PlayerComponent` 완전 제거(grep 0).
- [ ] 픽스처 2파일 working tree 보존, `5-23` stash 무손상.
- [ ] 수동 플레이: 마커 4종/레벨업/할당 동작 확인(특히 PhysicsComponent NRE 없음).

다음: **Slice 3 (클라)** — UserComponent 제거 + statPoints를 World.Stats.UnspentPoints로(스냅샷 apply + EntityStatPointsChanged + HUD) + StatsViewModel PropertyChange 제거. (별도 plan)
