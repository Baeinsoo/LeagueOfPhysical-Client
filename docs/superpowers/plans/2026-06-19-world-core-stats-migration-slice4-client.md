# World Core Stats Migration — Slice 4 (Client) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클라에서 stat을 레거시 `StatsComponent` → `GameFramework.World.Stats`로 옮긴다 — 스폰/재접 시 wire `CharacterCreationData`의 4 stat을 World.Stats에 시드, 할당 적용(`SetBase` + 신규 `EntityStatChanged`), HUD가 World.Stats pull + 이벤트 구독, 레거시 제거. **재접 시 할당 스탯이 0으로 리셋되던 버그를 닫는다**(서버 Slice 3이 wire를 채웠고, 이번에 클라가 받아 시드·표시).

**Architecture:** 클라 single Assembly-CSharp LOP 글루 + GameFramework(merged: `EntityStatType`/`StatsSystem.GetValue/SetBase`) + Shared wire(merged: `CharacterCreationData` +4 stat). stat은 상주 값(할당 시에만 변함). HUD 라이브 = Mana/Level의 `EntityXChanged`와 동형인 `EntityStatChanged`. **statPoints는 무변경**(`UserComponent` PropertyChange 유지) — Level과 달리 PropertyChange 구독은 잔존. 자동 테스트 없음 — 컴파일 + 수동 플레이.

**Tech Stack:** C# (Unity), R3, VContainer, EventBus. UnityMCP 컴파일 검증.

**Related spec:** `docs/superpowers/specs/2026-06-19-world-core-stats-migration-design.md` (Slice 4)

**Resolved Unity instance (매 UnityMCP 호출에 명시 — HTTP stateless):** Client `LeagueOfPhysical-Client@de70658b9450cbb4`

> **브랜치:** 클라 repo는 이미 `feature/world-core-stats-migration`(spec + Slice 1–4 plan 보유). Slice 4 코드도 이 브랜치. **무관 dirty 파일**(`Assets/Resources/UI/UIRoot.prefab`, `ProjectSettings/PackageManagerSettings.asset`, 미추적 `docs/.../2026-06-14-entity-view-mvc-decouple-slice1.md`)은 **커밋 제외**(Stats 파일만 stage).

> **stat 식별 문자열:** wire `StatAllocationToS/ToC.Stat`는 소문자 리터럴 `"strength"/"dexterity"/"intelligence"/"vitality"`(서버 Slice 3과 동일 계약). 레거시 `nameof(StatsComponent.strength)`가 우연히 이 값을 생성했음 — `StatsComponent` 제거 후 동일 소문자 리터럴로 치환(서버와 일치, 비파괴).

---

## File Structure (client repo: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client`)

- **Modify:** `Assets/Scripts/Entity/CharacterCreationData.cs` — 도메인 struct +4 camelCase int 필드.
- **Modify:** `Assets/Scripts/Entity/Event.Entity.cs` — `EntityStatChanged` struct.
- **Modify:** `Assets/Scripts/EntityCreator/CharacterCreator.cs` — World.Stats 시드 + 레거시 제거.
- **Modify:** `Assets/Scripts/Game/MessageHandler/Game.Info.MessageHandler.cs` — `OnGameInfoToC` wire→domain +4 stat(재접 경로).
- **Modify:** `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs` — `OnEntitySpawnToC` +4 stat; `OnStatAllocationToC` SetBase+publish; `[Inject] StatsSystem`.
- **Modify:** `Assets/Scripts/UI/Stats/StatsViewModel.cs` — World.Stats pull + `EntityStatChanged` 구독, stat PropertyChange 케이스 제거(statPoints 유지).
- **Modify:** `Assets/Scripts/UI/Stats/StatsView.cs` — 할당 버튼 소문자 리터럴.
- **Modify:** `Assets/Scripts/Game/GameLifetimeScope.cs` — `StatsSystem` 등록.
- **Delete:** `Assets/Scripts/Component/StatsComponent.cs` (+`.meta`).

---

## Task 1: 도메인 struct +4 stat 필드

**Files:** Modify `Assets/Scripts/Entity/CharacterCreationData.cs`

- [ ] **Step 1:** `public long currentExp { get; set; }` 다음, 닫는 `}` 앞에 추가:
```csharp

        public int strength { get; set; }
        public int dexterity { get; set; }
        public int intelligence { get; set; }
        public int vitality { get; set; }
```

---

## Task 2: EntityStatChanged 이벤트 추가

**Files:** Modify `Assets/Scripts/Entity/Event.Entity.cs`

- [ ] **Step 1:** `EntityLevelChanged` struct 다음(빈 줄 두고)에 추가:
```csharp

    public struct EntityStatChanged
    {
        public int statType;
        public int value;

        public EntityStatChanged(int statType, int value)
        {
            this.statType = statType;
            this.value = value;
        }
    }
```
(`statType` = `(int)GameFramework.World.EntityStatType`.)

---

## Task 3: CharacterCreator — World.Stats 시드 + 레거시 제거

**Files:** Modify `Assets/Scripts/EntityCreator/CharacterCreator.cs`

- [ ] **Step 1: World.Stats 시드 추가**

World Core 블록에서 `worldEntity.Add(new GameFramework.World.Level { ... });`(87줄) 다음, `worldEntity.Add(new GameFramework.World.Transform`(88줄) 앞에 추가:
```csharp
            var worldStats = new GameFramework.World.Stats();
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Strength] = creationData.strength;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Dexterity] = creationData.dexterity;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Intelligence] = creationData.intelligence;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Vitality] = creationData.vitality;
            worldEntity.Add(worldStats);
```

- [ ] **Step 2: 레거시 StatsComponent 생성 제거**

47–49줄 삭제:
```csharp
            StatsComponent statsComponent = entity.AddEntityComponent<StatsComponent>();
            objectResolver.Inject(statsComponent);
            statsComponent.Initialize(creationData.characterCode);
```

---

## Task 4: OnGameInfoToC — wire→domain +4 stat (재접 경로)

**Files:** Modify `Assets/Scripts/Game/MessageHandler/Game.Info.MessageHandler.cs`

- [ ] **Step 1:** `OnGameInfoToC`의 CharacterCreationData 초기화자에서 `currentExp = entityCreationData.CharacterCreationData.CurrentExp,`(46줄) 다음에 추가:
```csharp
                            strength = entityCreationData.CharacterCreationData.Strength,
                            dexterity = entityCreationData.CharacterCreationData.Dexterity,
                            intelligence = entityCreationData.CharacterCreationData.Intelligence,
                            vitality = entityCreationData.CharacterCreationData.Vitality,
```
(이것이 재접/늦은접속 시 서버가 보낸 할당 스탯을 도메인 struct로 옮긴다 → CharacterCreator가 World.Stats에 시드 → 버그 해소.)

---

## Task 5: OnEntitySpawnToC — wire→domain +4 stat (스폰 경로)

**Files:** Modify `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs`

- [ ] **Step 1:** `OnEntitySpawnToC`의 CharacterCreationData 초기화자에서 `currentExp = entitySpawnToC.EntityCreationData.CharacterCreationData.CurrentExp,`(98줄) 다음에 추가:
```csharp
                        strength = entitySpawnToC.EntityCreationData.CharacterCreationData.Strength,
                        dexterity = entitySpawnToC.EntityCreationData.CharacterCreationData.Dexterity,
                        intelligence = entitySpawnToC.EntityCreationData.CharacterCreationData.Intelligence,
                        vitality = entitySpawnToC.EntityCreationData.CharacterCreationData.Vitality,
```

---

## Task 6: OnStatAllocationToC — World.Stats 적용 + EntityStatChanged

**Files:** Modify `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs`

- [ ] **Step 1: StatsSystem 주입 추가**

`[Inject]` 필드 블록(10–17줄)에 `levelSystem` 다음 추가:
```csharp
        [Inject] private GameFramework.World.StatsSystem statsSystem;
```

- [ ] **Step 2: 핸들러 본문 교체**

`OnStatAllocationToC`(221–242줄) 전체를 교체:
```csharp
        private void OnStatAllocationToC(StatAllocationToC statAllocationToC)
        {
            StatsComponent statsComponent = playerContext.entity.GetComponent<StatsComponent>();
            switch (statAllocationToC.Stat)
            {
                case nameof(StatsComponent.strength):
                    statsComponent.strength = statAllocationToC.StatValue;
                    break;
                case nameof(StatsComponent.dexterity):
                    statsComponent.dexterity = statAllocationToC.StatValue;
                    break;
                case nameof(StatsComponent.intelligence):
                    statsComponent.intelligence = statAllocationToC.StatValue;
                    break;
                case nameof(StatsComponent.vitality):
                    statsComponent.vitality = statAllocationToC.StatValue;
                    break;
            }
        }
```
→
```csharp
        private void OnStatAllocationToC(StatAllocationToC statAllocationToC)
        {
            GameFramework.World.Stats stats = entityRegistry.Get(playerContext.entity.entityId)?.Get<GameFramework.World.Stats>();
            if (stats == null)
            {
                Debug.LogWarning($"[World] StatAllocation: Stats not found for entity {playerContext.entity.entityId}");
                return;
            }

            int statType;
            // wire stat 문자열은 소문자 필드명("strength" 등) — 서버 Slice 3 계약과 일치.
            switch (statAllocationToC.Stat)
            {
                case "strength": statType = (int)GameFramework.World.EntityStatType.Strength; break;
                case "dexterity": statType = (int)GameFramework.World.EntityStatType.Dexterity; break;
                case "intelligence": statType = (int)GameFramework.World.EntityStatType.Intelligence; break;
                case "vitality": statType = (int)GameFramework.World.EntityStatType.Vitality; break;
                default: return;
            }

            statsSystem.SetBase(stats, statType, statAllocationToC.StatValue);
            EventBus.Default.Publish(
                EventTopic.EntityId<LOPEntity>(playerContext.entity.entityId),
                new EntityStatChanged(statType, statAllocationToC.StatValue));
        }
```
(`entityRegistry` 이미 주입됨[Level/Health]. `Debug`/`EventBus`/`EventTopic`/`LOPEntity` 이미 사용 중.)

---

## Task 7: StatsViewModel — World.Stats pull + EntityStatChanged 구독

**Files:** Modify `Assets/Scripts/UI/Stats/StatsViewModel.cs`

- [ ] **Step 1: 필드 교체 (`_stats` 제거, 주입 추가)**

17–18줄:
```csharp
        private readonly StatsComponent _stats;
        private readonly UserComponent _user;
```
→
```csharp
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly GameFramework.World.StatsSystem _statsSystem;
        private readonly UserComponent _user;
```

- [ ] **Step 2: 생성자 시그니처 + 본문**

34줄 `public StatsViewModel(IPlayerContext playerContext)` →
```csharp
        public StatsViewModel(IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry, GameFramework.World.StatsSystem statsSystem)
```
그리고 36줄 `_playerContext = playerContext;` 다음에 추가:
```csharp
            _entityRegistry = entityRegistry;
            _statsSystem = statsSystem;
```
46줄 `_stats = _entity.GetEntityComponent<StatsComponent>();` 삭제(47줄 `_user = ...` 유지).
50줄 `Subscribe<PropertyChange>(...)` 다음에 추가(EntityStatChanged 구독):
```csharp
            EventBus.Default.Subscribe<EntityStatChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityStatChanged);
```
(PropertyChange 구독은 **유지** — statPoints[UserComponent]가 계속 사용.)

- [ ] **Step 3: OnPropertyChange에서 stat 케이스 제거**

67–77줄 switch에서 4 stat 케이스(71–74) 삭제, statPoints 케이스(75) 유지:
```csharp
        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(UserComponent.statPoints): _statPoints.Value = _user.statPoints; break;
            }
        }
```

- [ ] **Step 4: OnEntityStatChanged 핸들러 추가**

`OnPropertyChange` 다음에 추가:
```csharp
        private void OnEntityStatChanged(EntityStatChanged e)
        {
            switch (e.statType)
            {
                case (int)GameFramework.World.EntityStatType.Strength: _strength.Value = e.value; break;
                case (int)GameFramework.World.EntityStatType.Dexterity: _dexterity.Value = e.value; break;
                case (int)GameFramework.World.EntityStatType.Intelligence: _intelligence.Value = e.value; break;
                case (int)GameFramework.World.EntityStatType.Vitality: _vitality.Value = e.value; break;
            }
        }
```

- [ ] **Step 5: PushAll을 World.Stats pull로**

79–86줄:
```csharp
        private void PushAll()
        {
            _strength.Value = _stats.strength;
            _dexterity.Value = _stats.dexterity;
            _intelligence.Value = _stats.intelligence;
            _vitality.Value = _stats.vitality;
            _statPoints.Value = _user.statPoints;
        }
```
→
```csharp
        private void PushAll()
        {
            GameFramework.World.Stats stats = _entityRegistry.Get(_entity.entityId)?.Get<GameFramework.World.Stats>();
            if (stats != null)
            {
                _strength.Value = Mathf.RoundToInt(_statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.Strength));
                _dexterity.Value = Mathf.RoundToInt(_statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.Dexterity));
                _intelligence.Value = Mathf.RoundToInt(_statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.Intelligence));
                _vitality.Value = Mathf.RoundToInt(_statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.Vitality));
            }
            _statPoints.Value = _user.statPoints;
        }
```

- [ ] **Step 6: Dispose에 EntityStatChanged unsubscribe 추가**

92줄 `Unsubscribe<PropertyChange>(...)` 다음에 추가:
```csharp
                EventBus.Default.Unsubscribe<EntityStatChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityStatChanged);
```

---

## Task 8: StatsView — 할당 버튼 소문자 리터럴

**Files:** Modify `Assets/Scripts/UI/Stats/StatsView.cs`

- [ ] **Step 1:** 71–74줄 교체:
```csharp
        private void OnStrengthClicked() => _viewModel.Allocate(nameof(StatsComponent.strength));
        private void OnDexterityClicked() => _viewModel.Allocate(nameof(StatsComponent.dexterity));
        private void OnIntelligenceClicked() => _viewModel.Allocate(nameof(StatsComponent.intelligence));
        private void OnVitalityClicked() => _viewModel.Allocate(nameof(StatsComponent.vitality));
```
→
```csharp
        // wire stat 식별 문자열(소문자) — 서버 Slice 3 switch 키와 일치.
        private void OnStrengthClicked() => _viewModel.Allocate("strength");
        private void OnDexterityClicked() => _viewModel.Allocate("dexterity");
        private void OnIntelligenceClicked() => _viewModel.Allocate("intelligence");
        private void OnVitalityClicked() => _viewModel.Allocate("vitality");
```
(`StatsView`의 다른 `StatsComponent` 참조 없음 — uxml 요소 id는 별개. 만약 다른 `StatsComponent` 참조가 있으면 함께 정리 — Task 10 grep에서 확인.)

---

## Task 9: GameLifetimeScope — StatsSystem 등록

**Files:** Modify `Assets/Scripts/Game/GameLifetimeScope.cs`

- [ ] **Step 1:** `builder.Register<GameFramework.World.LevelSystem>(Lifetime.Singleton);`(32줄) 다음에 추가:
```csharp
            builder.Register<GameFramework.World.StatsSystem>(Lifetime.Singleton);
```

---

## Task 10: 레거시 StatsComponent 삭제 + 잔존 참조 확인

**Files:** Delete `Assets/Scripts/Component/StatsComponent.cs` (+`.meta`)

- [ ] **Step 1:** `Assets/Scripts/Component/StatsComponent.cs` + `.meta` 디스크에서 삭제.

- [ ] **Step 2:** 잔존 참조 0 확인:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" grep -n "StatsComponent" -- "Assets/Scripts" || echo "NO MATCHES"
```
Expected: `NO MATCHES`. 매치 있으면 그 파일도 repoint — 중단·확인.

---

## Task 11: 컴파일 검증

- [ ] **Step 1:**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`

Expected: 에러 0건.

---

## Task 12: 커밋 (Stats 파일만 — 무관 dirty 제외)

- [ ] **Step 1: 상태 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" status --short
```
Expected: Stats 8개 수정 + StatsComponent.cs/.meta 삭제 + (무관) `UIRoot.prefab`/`PackageManagerSettings.asset`/미추적 plan. **무관 파일 stage 안 함.**

- [ ] **Step 2: Stats 파일만 stage + 커밋**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" add Assets/Scripts/Entity/CharacterCreationData.cs Assets/Scripts/Entity/Event.Entity.cs Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/Game/MessageHandler/Game.Info.MessageHandler.cs Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs Assets/Scripts/UI/Stats/StatsViewModel.cs Assets/Scripts/UI/Stats/StatsView.cs Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Component/StatsComponent.cs Assets/Scripts/Component/StatsComponent.cs.meta
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" commit -m "feat(world): migrate client Stats to World.Stats (CreationData seed + EntityStatChanged, HUD pull, allocation apply, remove legacy StatsComponent)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 3:** `git show --stat HEAD | head -16` — 위 파일들만. `UIRoot.prefab`/`PackageManagerSettings.asset` 포함되면 중단.

---

## Task 13: 런타임 수동 검증 (사용자)

1. HUD 스탯 패널 초기값 정상(스폰 시 World.Stats pull).
2. +버튼 할당 → 값 즉시 증가(`EntityStatChanged`), statPoints 감소.
3. **재접/늦은접속**: 할당한 스탯이 **유지**(0 리셋 안 됨) ← **버그 해소 최종 확인**.
4. combat 체감 정상(서버 권위). `[World] ... Stats not found` 경고 안 뜸. 콘솔 에러 0. 데미지→사망 회귀 정상.

동작 보존 + 재접 버그 해소가 성공.

---

## Slice 4 완료 기준

- [ ] 클라 `feature/world-core-stats-migration`에 Stats 커밋(무관 dirty 미포함).
- [ ] 클라 컴파일 0에러. `StatsComponent` 완전 제거(grep 0).
- [ ] 수동 플레이: 할당/HUD/**재접 유지** 확인 → Stats 이행 4슬라이스 전체 완료.

이후: 사용자 검증 후 클라 `feature/world-core-stats-migration` → 클라 main 머지(spec+plans+Slice 4 코드). Stats 이행 종료. 후속 레거시 = User/Player(별도).
