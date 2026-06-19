# World Core Level Migration — Slice 3 (Client) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클라에서 level/exp 표시를 레거시 `LevelComponent` → `GameFramework.World.Level`로 옮긴다 — 스폰 시 `World.Level` 생성, 스냅샷 적용(`LevelSystem.ApplyAuthoritativeState`) + 신규 presentation 이벤트 `EntityLevelChanged` 발행, HUD가 World.Level pull + 이벤트 구독, 레거시 `LevelComponent`(+PropertyChange 경로) 제거. Mana 클라 이행과 동형. 동작 보존.

**Architecture:** 클라 single Assembly-CSharp LOP 글루 + GameFramework 코어(Slice 1 머지됨). level/exp는 상주 값 권위 싱크(HP/MP와 동급) — 도메인 이벤트 파이프라인 없음. HUD 라이브 갱신은 Mana의 `EntityManaChanged`와 동형인 얇은 `EntityLevelChanged` 신호. `CharacterHudViewModel`의 `OnPropertyChange` switch가 **Level 케이스뿐**이라(조사 확인), Level 이행 시 `PropertyChange` 구독 전체를 제거한다.

**Tech Stack:** C# (Unity), R3(ReactiveProperty), VContainer DI, EventBus. UnityMCP로 컴파일 검증. GameFramework `file:` 공유. 자동 단위 테스트 없음(단일 어셈블리) — 검증은 컴파일 + 수동 플레이.

**Related spec:** `docs/superpowers/specs/2026-06-18-world-core-level-migration-design.md` (Slice 3)

**Resolved Unity instance (매 UnityMCP 호출에 명시):** Client `LeagueOfPhysical-Client@de70658b9450cbb4`

> **브랜치:** 클라 repo는 이미 `feature/world-core-level-migration` 브랜치(spec·plan 커밋 보유). Slice 3 코드도 이 브랜치에 커밋. **주의:** working tree에 무관한 dirty 파일(`Assets/Resources/UI/UIRoot.prefab`, `ProjectSettings/PackageManagerSettings.asset`)이 있다 — **커밋에 포함하지 말 것**(Level 파일만 stage).

---

## File Structure (client repo: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client`)

- **Modify:** `Assets/Scripts/EntityCreator/CharacterCreator.cs` — `World.Level` 등록 + 레거시 생성 제거.
- **Modify:** `Assets/Scripts/Entity/Event.Entity.cs` — `EntityLevelChanged` struct 추가.
- **Modify:** `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs` — `[Inject] LevelSystem` + 스냅샷 apply/publish.
- **Modify:** `Assets/Scripts/UI/CharacterHud/CharacterHudViewModel.cs` — World.Level pull + `EntityLevelChanged` 구독, `PropertyChange`/`_level` 제거.
- **Modify:** `Assets/Scripts/Game/GameLifetimeScope.cs` — `LevelSystem` DI 등록.
- **Delete:** `Assets/Scripts/Component/LevelComponent.cs` (+`.meta`).

---

## Task 1: CharacterCreator — World.Level 등록 + 레거시 제거

**Files:** Modify `Assets/Scripts/EntityCreator/CharacterCreator.cs`

- [ ] **Step 1: World.Level 등록 추가**

World Core 블록(86–99)에서, `worldEntity.Add(new GameFramework.World.Mana(...))`(90줄) 다음 줄에 추가:
```csharp
            worldEntity.Add(new GameFramework.World.Level { Value = creationData.level, Exp = creationData.currentExp, ExpToNext = 100 });
```

- [ ] **Step 2: 레거시 LevelComponent 생성 제거**

51–53줄 삭제:
```csharp
            LevelComponent levelComponent = entity.AddEntityComponent<LevelComponent>();
            objectResolver.Inject(levelComponent);
            levelComponent.Initialize(creationData.level, creationData.currentExp);
```

(컴파일은 Task 6에서 일괄 — 아직 다른 파일이 LevelComponent 참조.)

---

## Task 2: Event.Entity.cs — EntityLevelChanged 이벤트 추가

**Files:** Modify `Assets/Scripts/Entity/Event.Entity.cs`

- [ ] **Step 1: EntityLevelChanged struct 추가**

`EntityManaChanged` struct(65–75줄) 다음, 빈 줄을 두고 추가(동형):
```csharp

    public struct EntityLevelChanged
    {
        public int level;
        public long currentExp;
        public long expToNext;

        public EntityLevelChanged(int level, long currentExp, long expToNext)
        {
            this.level = level;
            this.currentExp = currentExp;
            this.expToNext = expToNext;
        }
    }
```

---

## Task 3: Game.Entity.MessageHandler — LevelSystem 주입 + 스냅샷 apply/publish

**Files:** Modify `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs`

- [ ] **Step 1: LevelSystem 주입 추가**

`[Inject]` 필드 블록(10–16) 끝, `ManaSystem manaSystem;`(16줄) 다음에 추가:
```csharp
        [Inject] private GameFramework.World.LevelSystem levelSystem;
```

- [ ] **Step 2: 레거시 Level write를 World.Level apply + 이벤트 발행으로 교체**

`OnUserEntitySnapToC`의 200–201줄을 교체(Mana 블록 183–199 동형, `worldEntity`는 173줄에서 이미 해석됨 — 재사용):
```csharp
            playerContext.entity.GetComponent<LevelComponent>().currentExp = userEntitySnapToC.CurrentExp;
            playerContext.entity.GetComponent<LevelComponent>().level = userEntitySnapToC.Level;
```
→
```csharp
            GameFramework.World.Level level = worldEntity?.Get<GameFramework.World.Level>();
            if (level != null)
            {
                int prevValue = level.Value;
                long prevExp = level.Exp;
                levelSystem.ApplyAuthoritativeState(level, userEntitySnapToC.Level, userEntitySnapToC.CurrentExp);
                if (level.Value != prevValue || level.Exp != prevExp)
                {
                    EventBus.Default.Publish(
                        EventTopic.EntityId<LOPEntity>(playerContext.entity.entityId),
                        new EntityLevelChanged(level.Value, level.Exp, level.ExpToNext));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Level not found for entity {playerContext.entity.entityId}");
            }
```
**202줄(`...UserComponent>().statPoints = userEntitySnapToC.StatPoints;`)은 그대로 유지** (statPoints는 별도, 이번 비대상).

---

## Task 4: CharacterHudViewModel — World.Level pull + EntityLevelChanged 구독, PropertyChange/_level 제거

**Files:** Modify `Assets/Scripts/UI/CharacterHud/CharacterHudViewModel.cs`

- [ ] **Step 1: 클래스 doc 주석 갱신 (14줄)**

```csharp
    /// EXP/Level: 컴포넌트 PropertyChange(반응형, M2a 패턴)로 갱신.
```
→
```csharp
    /// EXP/Level: 초기값 World.Level pull, 라이브 EntityLevelChanged 이벤트로 갱신.
```

- [ ] **Step 2: `_level` 필드 제거 (20줄)**

삭제:
```csharp
        private readonly LevelComponent _level;
```

- [ ] **Step 3: 생성자의 `_level` 해석 제거 (48줄)**

삭제:
```csharp
            _level = _entity.GetEntityComponent<LevelComponent>();
```
(48줄 위의 빈 줄/아래 `PushAll();` 호출은 유지.)

- [ ] **Step 4: 구독 교체 — PropertyChange 제거, EntityLevelChanged 추가 (52–54줄)**

```csharp
            EventBus.Default.Subscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnPropertyChange);
            EventBus.Default.Subscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityDamage);
            EventBus.Default.Subscribe<EntityManaChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityManaChanged);
```
→
```csharp
            EventBus.Default.Subscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityDamage);
            EventBus.Default.Subscribe<EntityManaChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityManaChanged);
            EventBus.Default.Subscribe<EntityLevelChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityLevelChanged);
```

- [ ] **Step 5: OnPropertyChange 핸들러를 OnEntityLevelChanged로 교체 (57–65줄)**

```csharp
        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(LevelComponent.level): _levelValue.Value = _level.level; break;
                case nameof(LevelComponent.currentExp): _exp.Value = _level.currentExp; break;
                case nameof(LevelComponent.expToNextLevel): _expToNext.Value = _level.expToNextLevel; break;
            }
        }
```
→
```csharp
        private void OnEntityLevelChanged(EntityLevelChanged e)
        {
            _levelValue.Value = e.level;
            _exp.Value = e.currentExp;
            _expToNext.Value = e.expToNext;
        }
```

- [ ] **Step 6: PushAll의 레거시 Level pull을 World.Level pull로 교체 (93–98줄)**

```csharp
            if (_level != null)
            {
                _exp.Value = _level.currentExp;
                _expToNext.Value = _level.expToNextLevel;
                _levelValue.Value = _level.level;
            }
```
→
```csharp
            GameFramework.World.Level level = worldEntity?.Get<GameFramework.World.Level>();
            if (level != null)
            {
                _exp.Value = level.Exp;
                _expToNext.Value = level.ExpToNext;
                _levelValue.Value = level.Value;
            }
```
(`worldEntity`는 PushAll 80줄에서 이미 해석됨 — 재사용.)

- [ ] **Step 7: Dispose의 PropertyChange unsubscribe를 EntityLevelChanged로 교체 (105–107줄)**

```csharp
                EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnPropertyChange);
                EventBus.Default.Unsubscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityDamage);
                EventBus.Default.Unsubscribe<EntityManaChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityManaChanged);
```
→
```csharp
                EventBus.Default.Unsubscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityDamage);
                EventBus.Default.Unsubscribe<EntityManaChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityManaChanged);
                EventBus.Default.Unsubscribe<EntityLevelChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityLevelChanged);
```

---

## Task 5: GameLifetimeScope — LevelSystem DI 등록

**Files:** Modify `Assets/Scripts/Game/GameLifetimeScope.cs`

- [ ] **Step 1: LevelSystem Singleton 등록**

`builder.Register<GameFramework.World.ManaSystem>(Lifetime.Singleton);`(31줄) 다음 줄에 추가:
```csharp
            builder.Register<GameFramework.World.LevelSystem>(Lifetime.Singleton);
```

---

## Task 6: 레거시 LevelComponent 삭제

**Files:** Delete `Assets/Scripts/Component/LevelComponent.cs` (+`.meta`)

- [ ] **Step 1: 파일 삭제 (디스크에서)**

`Assets/Scripts/Component/LevelComponent.cs`와 `Assets/Scripts/Component/LevelComponent.cs.meta`를 삭제한다. (git rm은 controller가 커밋 시 처리 — 또는 직접 디스크 삭제 후 controller가 stage.)

- [ ] **Step 2: 잔존 참조 0 확인**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" grep -n "LevelComponent" -- "Assets/Scripts" || echo "NO MATCHES"
```
Expected: `NO MATCHES`. 매치가 있으면 그 파일도 repoint 필요 — 중단·확인.

---

## Task 7: 컴파일 검증

**Files:** (검증만)

- [ ] **Step 1: 클라 인스턴스 컴파일**

Run (UnityMCP):
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`

Expected: 에러 0건. 에러 시 해당 태스크로 돌아가 수정 후 재컴파일.

---

## Task 8: Level 변경 커밋 (Level 파일만)

**Files:** (git)

- [ ] **Step 1: working tree 확인 — 무관 dirty 파일 제외**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" status --short
```
Expected: Level 5개 수정 + LevelComponent.cs/.meta 삭제 + (무관) `UIRoot.prefab`/`PackageManagerSettings.asset`. **무관 2파일은 커밋하지 않는다.**

- [ ] **Step 2: Level 파일만 stage + 커밋**

```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" add Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/Entity/Event.Entity.cs Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs Assets/Scripts/UI/CharacterHud/CharacterHudViewModel.cs Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Component/LevelComponent.cs Assets/Scripts/Component/LevelComponent.cs.meta
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" commit -m "feat(world): migrate client Level/Exp to World.Level (snapshot apply + EntityLevelChanged, HUD pull/subscribe, remove legacy LevelComponent)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 3: 커밋 내용 확인**

```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" show --stat HEAD | head -16
```
Expected: 위 7개 파일만. `UIRoot.prefab`/`PackageManagerSettings.asset`가 포함되면 안 됨.

---

## Task 9: 런타임 수동 검증 (사용자)

**Files:** (없음 — 플레이)

- [ ] **Step 1: 클라 플레이로 확인**

1. 유저 HUD 레벨/exp 바 초기값 정상(스폰 시 World.Level pull).
2. 경험치 획득 → 레벨/exp 갱신(서버 스냅샷 → `EntityLevelChanged` → HUD).
3. 스폰/**늦은 접속** 다른 유저 표시 정상.
4. `[World] ... Level not found` 경고 정상 플레이 중 안 뜸. 콘솔 에러 0. World Core 회귀(데미지→사망) 정상.

동작 변화 없음(회귀 0)이 성공.

---

## Slice 3 완료 기준

- [ ] 클라 `feature/world-core-level-migration`에 Level 커밋 1개 (무관 dirty 파일 미포함).
- [ ] 클라 인스턴스 컴파일 0에러.
- [ ] `LevelComponent` 클라에서 완전 제거(grep 0). `PropertyChange` 구독/`OnPropertyChange` 제거됨.
- [ ] 수동 플레이로 HUD 레벨/exp·늦은접속 동작 보존 확인.

이후: Level 이행 3슬라이스 완료 → 클라 Slice 3 main 머지(사용자 검증 후). 후속 레거시 = Stats/User(각자 별도). Stats 이행에서 재접 스탯 리셋 버그(메모리 기록) 동반 해소 검토.
