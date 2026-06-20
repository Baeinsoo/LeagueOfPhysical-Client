# World Core Owner Migration — Slice 3 (Client) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클라에서 마지막 레거시 컴포넌트 `UserComponent`를 제거한다 — statPoints를 (이미 로컬 유저 엔티티에 있는) `World.Stats.UnspentPoints`로 옮기고, 스냅샷 적용(`SetUnspent` + 신규 `EntityStatPointsChanged`)·HUD(World.Stats pull + 구독)로 전환. `StatsViewModel`의 `PropertyChange` 경로가 완전히 사라진다.

**Architecture:** 클라 single Assembly-CSharp LOP 글루 + GameFramework(Slice 1 머지: `Stats.UnspentPoints`, `StatsSystem.SetUnspent`). statPoints는 상주 값, wire `UserEntitySnapToC.StatPoints`(필드 7) 무변경. 클라는 마커 미사용 → `Ownership` 미생성(신규 클라 컴포넌트 0). 자동 테스트 없음 — 컴파일 + 수동 플레이.

**Tech Stack:** C# (Unity), R3, VContainer, EventBus. UnityMCP 컴파일 검증.

**Related spec:** `docs/superpowers/specs/2026-06-19-world-core-owner-migration-design.md` (Slice 3).

**Resolved Unity instance (매 UnityMCP 호출에 명시 — HTTP stateless):** Client `LeagueOfPhysical-Client@de70658b9450cbb4`

> **브랜치:** 클라 repo는 이미 `feature/world-core-owner-migration`(spec + Slice 1–3 plan). Slice 3 코드도 이 브랜치. **무관 dirty**(`Assets/Resources/UI/UIRoot.prefab`, `ProjectSettings/PackageManagerSettings.asset`, 미추적 plan)는 **커밋 제외**.

---

## File Structure (client repo: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client`)

- **Modify:** `Assets/Scripts/Entity/Event.Entity.cs` — `EntityStatPointsChanged` struct.
- **Modify:** `Assets/Scripts/EntityCreator/CharacterCreator.cs` — 레거시 `UserComponent` 생성 제거.
- **Modify:** `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs` — `OnUserEntitySnapToC` statPoints write → `SetUnspent` + `EntityStatPointsChanged`.
- **Modify:** `Assets/Scripts/UI/Stats/StatsViewModel.cs` — World.Stats.UnspentPoints pull + `EntityStatPointsChanged` 구독, `_user`/`PropertyChange` 통째 제거.
- **Delete:** `Assets/Scripts/Component/UserComponent.cs` (+`.meta`).
- 무변경: `StatsView.cs`(이미 소문자 리터럴), `GameLifetimeScope.cs`(StatsSystem 이미 등록).

---

## Task 1: EntityStatPointsChanged 이벤트 추가

**Files:** Modify `Assets/Scripts/Entity/Event.Entity.cs`

- [ ] **Step 1:** `EntityStatChanged` struct(91–99줄, `}`로 끝남) 다음, 빈 줄 두고 추가(`EntityCreated` 앞):
```csharp

    public struct EntityStatPointsChanged
    {
        public int statPoints;

        public EntityStatPointsChanged(int statPoints)
        {
            this.statPoints = statPoints;
        }
    }
```

---

## Task 2: CharacterCreator — 레거시 UserComponent 생성 제거

**Files:** Modify `Assets/Scripts/EntityCreator/CharacterCreator.cs`

- [ ] **Step 1:** `if (isUserEntity)` 블록(57–69줄)에서 UserComponent 2줄(59–60)만 삭제, 나머지(playerContext 할당, SnapReconciler)는 유지:
```csharp
                UserComponent userComponent = entity.AddEntityComponent<UserComponent>();
                objectResolver.Inject(userComponent);

```
(이 2줄 + 직후 빈 줄 제거. `isUserEntity` gate와 playerContext/SnapReconciler는 그대로. 클라는 statPoints를 이미 있는 World.Stats[Stats 이행에서 생성]에서 받으므로 신규 컴포넌트 불필요. 마커 미사용 → Ownership도 안 만듦.)

---

## Task 3: OnUserEntitySnapToC — statPoints write → World.Stats.UnspentPoints + 이벤트

**Files:** Modify `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs`

- [ ] **Step 1:** `OnUserEntitySnapToC`의 마지막 줄(223) 교체. `worldEntity`(179줄)·`statsSystem`(주입됨, L18) 재사용. Mana/Level change-detect 동형:
```csharp
            playerContext.entity.GetComponent<UserComponent>().statPoints = userEntitySnapToC.StatPoints;
```
→
```csharp
            GameFramework.World.Stats stats = worldEntity?.Get<GameFramework.World.Stats>();
            if (stats != null)
            {
                int prevUnspent = stats.UnspentPoints;
                statsSystem.SetUnspent(stats, userEntitySnapToC.StatPoints);
                if (stats.UnspentPoints != prevUnspent)
                {
                    EventBus.Default.Publish(
                        EventTopic.EntityId<LOPEntity>(playerContext.entity.entityId),
                        new EntityStatPointsChanged(stats.UnspentPoints));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Stats not found for entity {playerContext.entity.entityId}");
            }
```

---

## Task 4: StatsViewModel — World.Stats.UnspentPoints pull + EntityStatPointsChanged 구독, _user/PropertyChange 제거

**Files:** Modify `Assets/Scripts/UI/Stats/StatsViewModel.cs`

- [ ] **Step 1: `_user` 필드 제거 (19줄)**
```csharp
        private readonly UserComponent _user;
```
삭제.

- [ ] **Step 2: 생성자의 `_user` 해석 제거 + 구독 교체 (49·52·53줄)**

49줄 `_user = _entity.GetEntityComponent<UserComponent>();` 삭제.
52줄 PropertyChange 구독을 EntityStatPointsChanged 구독으로 교체(53줄 EntityStatChanged 구독은 유지):
```csharp
            EventBus.Default.Subscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnPropertyChange);
            EventBus.Default.Subscribe<EntityStatChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityStatChanged);
```
→
```csharp
            EventBus.Default.Subscribe<EntityStatChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityStatChanged);
            EventBus.Default.Subscribe<EntityStatPointsChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityStatPointsChanged);
```

- [ ] **Step 3: Allocate 가드 (58줄)** — `_user` 제거에 맞춰 reactive prop 기준으로:
```csharp
            if (_user == null || _user.statPoints == 0)
            {
                return;
            }
```
→
```csharp
            if (_statPoints.CurrentValue == 0)
            {
                return;
            }
```

- [ ] **Step 4: OnPropertyChange → OnEntityStatPointsChanged (70–76줄 교체)**

`OnPropertyChange` 핸들러(statPoints 케이스만 남아있음)를 통째로 교체:
```csharp
        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(UserComponent.statPoints): _statPoints.Value = _user.statPoints; break;
            }
        }
```
→
```csharp
        private void OnEntityStatPointsChanged(EntityStatPointsChanged e)
        {
            _statPoints.Value = e.statPoints;
        }
```

- [ ] **Step 5: PushAll의 statPoints를 World.Stats.UnspentPoints로 (99줄)**

`_statPoints.Value = _user.statPoints;`(99줄, `if (stats != null)` 블록 *밖*)를 블록 *안*으로 옮겨 UnspentPoints에서:
```csharp
            GameFramework.World.Stats stats = _entityRegistry.Get(_entity.entityId)?.Get<GameFramework.World.Stats>();
            if (stats != null)
            {
                _strength.Value = Mathf.RoundToInt(_statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.Strength));
                _dexterity.Value = Mathf.RoundToInt(_statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.Dexterity));
                _intelligence.Value = Mathf.RoundToInt(_statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.Intelligence));
                _vitality.Value = Mathf.RoundToInt(_statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.Vitality));
            }
            _statPoints.Value = _user.statPoints;
```
→
```csharp
            GameFramework.World.Stats stats = _entityRegistry.Get(_entity.entityId)?.Get<GameFramework.World.Stats>();
            if (stats != null)
            {
                _strength.Value = Mathf.RoundToInt(_statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.Strength));
                _dexterity.Value = Mathf.RoundToInt(_statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.Dexterity));
                _intelligence.Value = Mathf.RoundToInt(_statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.Intelligence));
                _vitality.Value = Mathf.RoundToInt(_statsSystem.GetValue(stats, (int)GameFramework.World.EntityStatType.Vitality));
                _statPoints.Value = stats.UnspentPoints;
            }
```

- [ ] **Step 6: Dispose의 PropertyChange unsubscribe → EntityStatPointsChanged (106줄)**
```csharp
                EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnPropertyChange);
                EventBus.Default.Unsubscribe<EntityStatChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityStatChanged);
```
→
```csharp
                EventBus.Default.Unsubscribe<EntityStatChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityStatChanged);
                EventBus.Default.Unsubscribe<EntityStatPointsChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityStatPointsChanged);
```

> `_statPoints.CurrentValue`(R3 ReactiveProperty)·`using` 그대로. `UserComponent` 참조가 파일에서 0이 됨.

---

## Task 5: 레거시 UserComponent 삭제

**Files:** Delete `Assets/Scripts/Component/UserComponent.cs` (+`.meta`)

- [ ] **Step 1:** 디스크에서 `Assets/Scripts/Component/UserComponent.cs` + `.meta` 삭제.
- [ ] **Step 2: 잔존 참조 0 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" grep -n "UserComponent" -- "Assets/Scripts" || echo "NO MATCHES"
```
Expected: `NO MATCHES`. 매치 있으면 처리 — 중단·확인.

---

## Task 6: 컴파일 검증

- [ ] **Step 1:**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
Expected: 에러 0건.

---

## Task 7: 커밋 (Owner 파일만 — 무관 dirty 제외)

- [ ] **Step 1: 상태 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" status --short
```
Expected: Owner 4개 수정 + UserComponent.cs/.meta 삭제 + (무관) `UIRoot.prefab`/`PackageManagerSettings.asset`/미추적 plan. **무관 파일 stage 안 함.**

- [ ] **Step 2: Owner 파일만 stage + 커밋**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" add Assets/Scripts/Entity/Event.Entity.cs Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs Assets/Scripts/UI/Stats/StatsViewModel.cs Assets/Scripts/Component/UserComponent.cs Assets/Scripts/Component/UserComponent.cs.meta
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" commit -m "feat(world): migrate client statPoints to Stats.UnspentPoints + EntityStatPointsChanged, remove legacy UserComponent

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
- [ ] **Step 3:** `git show --stat HEAD | head -12` — 위 파일들만. `UIRoot.prefab`/`PackageManagerSettings.asset` 포함되면 중단.

---

## Task 8: 런타임 수동 검증 (사용자)

1. HUD 스탯 패널 statPoints 초기값 정상(스폰 시 World.Stats.UnspentPoints pull).
2. 레벨업 → statPoints 증가(서버 스냅샷 → `EntityStatPointsChanged` → HUD).
3. +버튼 할당 → 스탯 증가·statPoints 감소(할당 → 다음 스냅샷에서 UnspentPoints 반영 → HUD).
4. CanAllocate(statPoints>0) 정상 동작. 콘솔 에러 0. `[World] ... Stats not found` 경고 안 뜸.

동작 보존이 성공.

---

## Slice 3 완료 기준

- [ ] 클라 `feature/world-core-owner-migration`에 Owner 커밋(무관 dirty 미포함).
- [ ] 클라 컴파일 0에러. `UserComponent` 완전 제거(grep 0). `StatsViewModel`에서 `PropertyChange` 완전 제거.
- [ ] 수동 플레이: statPoints 표시/레벨업/할당 동작 확인.

이후: 사용자 검증 후 클라 `feature/world-core-owner-migration` → 클라 main 머지(spec+plans+Slice 3 코드). **User/Player 이행 완료 → 레거시 엔티티 컴포넌트(Health/Mana/Level/Stats/User/Player) 전부 World Core 이행 완료.**
