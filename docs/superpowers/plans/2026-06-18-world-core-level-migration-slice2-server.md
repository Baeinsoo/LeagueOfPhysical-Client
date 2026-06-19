# World Core Level Migration — Slice 2 (Server) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버에서 level/exp의 진실원본을 레거시 `LevelComponent` → `GameFramework.World.Level`로 뒤집는다 — 생성(`World.Level` 등록), writer flip(경험치 획득 → `LevelSystem.AddExperience` + statPoints 증분), 읽기 flip(스냅샷·생성데이터), DI 등록, 레거시 컴포넌트 제거. 동작 보존(동일 임계 100·동일 statPoints++).

**Architecture:** 서버 single Assembly-CSharp LOP 글루 변경 + GameFramework 코어(Slice 1 머지 완료) 호출. 자동 단위 테스트 없음(클라/서버 단일 어셈블리 제약) — 검증은 **컴파일 + 수동 플레이**(기존 슬라이스 기조). `World.Level`은 Slice 1에서 추가된 `LevelSystem.AddExperience`(int 반환)/`ApplyAuthoritativeState`를 사용. statPoints는 별도 상주 값이라 `gained` 반환값으로 서버 호출지에서 증분(이벤트 파이프라인 없음).

**Tech Stack:** C# (Unity), VContainer DI, UnityMCP(`refresh_unity`/`read_console`)로 컴파일 검증. GameFramework는 `file:` UPM 공유(Slice 1이 GameFramework main에 머지됨 — `7311df4`).

**Related spec:** `docs/superpowers/specs/2026-06-18-world-core-level-migration-design.md` (Slice 2)

**Resolved Unity instance (사용자 환경, 매 UnityMCP 호출에 명시):**
- Server: `LeagueOfPhysical-Server@f99391fa2dbaaf3c`
- (UnityMCP HTTP는 stateless — `unity_instance=`를 매 호출에 반드시 전달. 이 슬라이스는 **서버 작업**이라 서버 인스턴스 조작 인가됨.)

---

## ⚠️ 서버 working-tree 픽스처 주의 (이 슬라이스의 핵심 리스크)

서버 working tree에 **커밋 금지 로컬 픽스처**가 떠 있다(`git status` 확인됨):
- `Assets/Scripts/Game/LOPGame.cs` ← **Slice 2가 수정해야 하는 파일** (writer flip)
- `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`

또 기존 stash `stash@{0}: On main: 5-23`가 있다(우리 것 아님 — 건드리지 않는다).

Level writer-flip은 `LOPGame.HandleItemTouch`(L165–174)에 들어가는데, 픽스처는 auth/spawn 영역이라 **이 메서드와 겹치지 않을 가능성이 높다**. 전략: **픽스처 2파일을 전용 stash로 보존 → 깨끗한 baseline에서 Level 변경 → 커밋 → stash pop으로 픽스처 복원**. 이렇게 해야 Level 변경만 커밋되고 픽스처는 working tree에 보존된다. Task 1·8에서 안전 검증과 함께 수행한다.

> 픽스처 stash의 diff가 `HandleItemTouch`(L165–174)나 `LOPGame` `[Inject]` 헤더(L22–35)와 겹치면 pop 시 충돌한다. Task 1에서 겹침 여부를 확인하고, 겹치면 **즉시 중단·에스컬레이트**(controller가 수동 처리). 겹치지 않으면 그대로 진행.

---

## File Structure

모든 경로는 서버 repo 기준: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`.

- **Modify:** `Assets/Scripts/EntityCreator/CharacterCreator.cs` — `World.Level` 등록 추가 + 레거시 `LevelComponent` 생성 제거.
- **Modify:** `Assets/Scripts/Game/LOPGame.cs` (⚠️픽스처) — writer flip + `[Inject]` 추가.
- **Modify:** `Assets/Scripts/Game/LOPGameEngine.cs` — 스냅샷 read flip(Level/Exp).
- **Modify:** `Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs` — 생성데이터 read flip(Level/Exp).
- **Modify:** `Assets/Scripts/Game/GameLifetimeScope.cs` — `LevelSystem` DI 등록.
- **Delete:** `Assets/Scripts/Component/LevelComponent.cs` (+`.meta`).

---

## Task 1: 서버 피처 브랜치 + 픽스처 stash 보존

**Files:** (git 작업만)

- [ ] **Step 1: 현재 상태 스냅샷**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" stash list
```
Expected: branch `main`, dirty 파일 = `Assets/Scripts/Game/LOPGame.cs` + `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`, stash list에 `stash@{0}: On main: 5-23`. 다른 파일이 dirty거나 stash 개수가 다르면 **중단·에스컬레이트**(상태가 예상과 다름).

- [ ] **Step 2: 피처 브랜치 생성 (dirty 변경은 따라옴)**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" checkout -b feature/world-core-level-migration
```
Expected: `Switched to a new branch 'feature/world-core-level-migration'`. (dirty 픽스처 2파일은 그대로 working tree에 남음.)

- [ ] **Step 3: 픽스처가 writer-flip 영역과 겹치는지 확인**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" diff -- Assets/Scripts/Game/LOPGame.cs
```
Expected: diff hunk들의 `@@` 라인 범위가 `HandleItemTouch`(대략 L165–174)와 `[Inject]` 헤더(대략 L22–35)를 **포함하지 않아야** 한다. 포함하면 stash pop이 충돌하므로 **중단·에스컬레이트**(controller가 수동 병합 결정). 포함 안 하면 다음 단계.

- [ ] **Step 4: 픽스처 2파일만 전용 stash로 보존**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" stash push -m "level-slice2: hold local fixtures (LOPGame, ConfigureRoomComponent)" -- Assets/Scripts/Game/LOPGame.cs Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs
```
Expected: `Saved working directory and index state On feature/world-core-level-migration: level-slice2: hold local fixtures ...`

- [ ] **Step 5: stash·working tree 검증**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" stash list
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
```
Expected: `stash@{0}`가 우리 "level-slice2: hold local fixtures ...", `stash@{1}`가 기존 "On main: 5-23". `status --short`는 **깨끗**(no changes). LOPGame.cs는 이제 커밋된 baseline 상태.

---

## Task 2: CharacterCreator — World.Level 등록 + 레거시 생성 제거

**Files:**
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs`

- [ ] **Step 1: World.Level 등록 추가**

`CharacterCreator.cs` 75–88줄의 World Core 블록에서, `worldEntity.Add(new GameFramework.World.Mana(...))`(현재 79줄) 바로 다음 줄에 추가:
```csharp
            worldEntity.Add(new GameFramework.World.Level { Value = creationData.level, Exp = creationData.currentExp, ExpToNext = 100 });
```
(ExpToNext=100은 레거시 `LevelComponent.Initialize`의 `expToNextLevel=100`과 동일 — 레벨업 루프 임계 유지.)

- [ ] **Step 2: 레거시 LevelComponent 생성 제거**

같은 파일 45–47줄 삭제:
```csharp
            LevelComponent levelComponent = entity.AddEntityComponent<LevelComponent>();
            objectResolver.Inject(levelComponent);
            levelComponent.Initialize(creationData.level, creationData.currentExp);
```

- [ ] **Step 3: (컴파일은 Task 8에서 일괄)**

이 시점엔 `LevelComponent` 타입이 아직 존재하고 다른 파일이 참조 중이라 단독 컴파일 안 함. 다음 태스크로.

---

## Task 3: LOPGame — writer flip (경험치 → World.Level + statPoints)

**Files:**
- Modify: `Assets/Scripts/Game/LOPGame.cs` (⚠️픽스처 stash됨 — 현재 baseline 상태)

- [ ] **Step 1: `[Inject]` 필드 추가**

`LOPGame.cs` 클래스 헤더의 `[Inject]` 블록(L22–35) 끝, `IEntityCreationDataFactory entityCreationDataFactory;`(L35) 다음에 추가:
```csharp

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private GameFramework.World.LevelSystem levelSystem;
```

- [ ] **Step 2: 경험치 획득 지점 writer flip**

`HandleItemTouch`의 172줄을 교체:
```csharp
                LOPEntity toucher = gameEngine.entityManager.GetEntity<LOPEntity>(itemTouch.toucherId);
                toucher.GetEntityComponent<LevelComponent>().AddExperience(10);
```
→
```csharp
                LOPEntity toucher = gameEngine.entityManager.GetEntity<LOPEntity>(itemTouch.toucherId);
                GameFramework.World.Level level = entityRegistry.Get(toucher.entityId)?.Get<GameFramework.World.Level>();
                if (level != null)
                {
                    int gained = levelSystem.AddExperience(level, 10);
                    if (gained > 0)
                    {
                        toucher.GetEntityComponent<PlayerComponent>().statPoints += gained;
                    }
                }
```
(레거시 `AddExperience`의 `Debug.Log("Level Up!...")`는 디버그 로그라 미보존 — 게임플레이 동작[level/exp/statPoints]은 동일. statPoints는 레벨업 1회당 1 증가 = `+= gained`로 등가.)

- [ ] **Step 3: (컴파일은 Task 8에서 일괄)**

---

## Task 4: LOPGameEngine — 스냅샷 read flip (Level/Exp)

**Files:**
- Modify: `Assets/Scripts/Game/LOPGameEngine.cs`

- [ ] **Step 1: 주석 갱신 + Level read flip**

`LOPGameEngine.cs`의 `EndUpdate` 스냅샷 블록(L143–166). 145줄 주석을 갱신하고, 163–164줄(legacy Level/Exp read)을 World.Level read로 교체한다. `worldEntity`는 L146에서 이미 해석됨 — 재사용.

145줄 주석 교체:
```csharp
                // HP/MP는 World.Health/World.Mana(코어)에서 읽는다. Exp/Level/StatPoints는 각자 이행 전까지 legacy 컴포넌트 유지.
```
→
```csharp
                // HP/MP/Level/Exp는 World 코어에서 읽는다. StatPoints는 이행 전까지 legacy(PlayerComponent) 유지.
```

163–164줄 교체:
```csharp
                entitySnapsToC.CurrentExp = entity.GetEntityComponent<LevelComponent>().currentExp;
                entitySnapsToC.Level = entity.GetEntityComponent<LevelComponent>().level;
```
→
```csharp
                GameFramework.World.Level level = worldEntity?.Get<GameFramework.World.Level>();
                if (level == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Level not found for entity {entity.entityId}");
                }
                entitySnapsToC.CurrentExp = level?.Exp ?? 0;
                entitySnapsToC.Level = level?.Value ?? 0;
```
165줄(`entitySnapsToC.StatPoints = entity.GetEntityComponent<PlayerComponent>().statPoints;`)은 **그대로 유지**.

---

## Task 5: CharacterCreationDataCreator — 생성데이터 read flip (Level/Exp)

**Files:**
- Modify: `Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs`

- [ ] **Step 1: 주석 갱신 + Level 해석 추가**

`CharacterCreationDataCreator.cs` 24줄 주석 교체:
```csharp
            // HP/MP는 World.Health/World.Mana(코어)에서 읽는다. Level/Exp는 각자 이행 전까지 legacy 컴포넌트 유지.
```
→
```csharp
            // HP/MP/Level/Exp는 World 코어에서 읽는다.
```

Mana null-guard 블록(L32–36) 다음, `global::CharacterCreationData ... = new ...` (L38) 직전에 Level 해석 추가:
```csharp

            GameFramework.World.Level level = worldEntity?.Get<GameFramework.World.Level>();
            if (level == null)
            {
                UnityEngine.Debug.LogWarning($"[World] CharacterCreationData: Level not found for entity {lopEntity.entityId}");
            }
```

- [ ] **Step 2: initializer의 Level/CurrentExp read flip**

object initializer의 48–49줄 교체:
```csharp
                Level = lopEntity.GetEntityComponent<LevelComponent>().level,
                CurrentExp = lopEntity.GetEntityComponent<LevelComponent>().currentExp,
```
→
```csharp
                Level = level?.Value ?? 0,
                CurrentExp = level?.Exp ?? 0,
```

---

## Task 6: GameLifetimeScope — LevelSystem DI 등록

**Files:**
- Modify: `Assets/Scripts/Game/GameLifetimeScope.cs`

- [ ] **Step 1: LevelSystem Singleton 등록**

`GameLifetimeScope.cs` 22줄(`builder.Register<GameFramework.World.HealthSystem>(Lifetime.Singleton);`) 다음 줄에 추가:
```csharp
            builder.Register<GameFramework.World.LevelSystem>(Lifetime.Singleton);
```

---

## Task 7: 레거시 LevelComponent 삭제

**Files:**
- Delete: `Assets/Scripts/Component/LevelComponent.cs` (+`.meta`)

- [ ] **Step 1: git rm으로 .cs + .meta 삭제**

이 시점엔 LevelComponent 참조가 모두 제거됨(Task 2 생성·Task 3 writer·Task 4 스냅샷·Task 5 생성데이터). 안전하게 삭제:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" rm Assets/Scripts/Component/LevelComponent.cs Assets/Scripts/Component/LevelComponent.cs.meta
```
Expected: 두 파일 `rm`. (`LevelComponent`는 코드로 `AddEntityComponent`되던 컴포넌트 — 씬/prefab GUID 참조 없음.)

- [ ] **Step 2: 잔존 참조 0 확인**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" grep -n "LevelComponent" -- "Assets/Scripts" || echo "NO MATCHES"
```
Expected: `NO MATCHES` (LevelComponent 참조 0). 매치가 있으면 그 파일도 repoint 필요 — 중단·확인.

---

## Task 8: 컴파일 검증 (Level 격리, 픽스처 stash된 상태)

**Files:** (검증만)

- [ ] **Step 1: 서버 인스턴스 컴파일**

Run (UnityMCP):
- `refresh_unity(unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c", types=["error"])`

Expected: 에러 0건. (이 컴파일은 Level 변경만 반영된 상태 — 픽스처는 아직 stash됨.) 에러가 있으면 해당 태스크로 돌아가 수정 후 재컴파일.

---

## Task 9: Level 변경 커밋

**Files:** (git 작업)

- [ ] **Step 1: 변경 파일 스테이징 + 커밋**

LOPGame.cs는 현재 baseline + Level writer-flip만 반영된 상태(픽스처 stash됨)라 안전하게 커밋 가능.
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" add Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/Game/LOPGame.cs Assets/Scripts/Game/LOPGameEngine.cs Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Component/LevelComponent.cs Assets/Scripts/Component/LevelComponent.cs.meta
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" commit -m "feat(world): migrate server Level/Exp to World.Level (writer flip + statPoints, read flip, remove legacy LevelComponent)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
Expected: 커밋 성공, 6개 수정 + 1개 삭제(.cs) + .meta 삭제 반영.

- [ ] **Step 2: 커밋 내용 확인**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" show --stat HEAD | head -20
```
Expected: 위 파일들만 포함. **`ConfigureRoomComponent.cs`가 커밋에 포함되면 안 됨**(픽스처 — 여전히 stash). 포함됐으면 즉시 중단·에스컬레이트.

---

## Task 10: 픽스처 복원 (stash pop) + 최종 검증

**Files:** (git + 검증)

- [ ] **Step 1: 픽스처 stash pop**

우리 픽스처 stash(`stash@{0}`)를 복원:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" stash pop
```
Expected: `Auto-merging`/clean pop — `LOPGame.cs`·`ConfigureRoomComponent.cs`가 working tree에 dirty로 복원. **충돌(CONFLICT) 발생 시 중단·에스컬레이트**(Task 1 Step 3에서 비겹침 확인했으므로 정상 시 충돌 없어야 함). pop 후 `stash@{0}`는 기존 "5-23"만 남아야 함.

- [ ] **Step 2: 최종 상태 검증**

Run:
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" stash list
```
Expected: `status --short`에 `M Assets/Scripts/Game/LOPGame.cs` + `M Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs` (픽스처 복원됨, 커밋 안 됨). `stash list`에 `stash@{0}: On main: 5-23`(기존)만 — 우리 stash는 pop됨.

- [ ] **Step 3: 픽스처+Level 결합 컴파일 재검증**

Run (UnityMCP):
- `refresh_unity(unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c", types=["error"])`

Expected: 에러 0건 (실제 런타임 working tree 상태 = Level 커밋 + 픽스처 dirty).

---

## Task 11: 런타임 수동 검증 (사용자)

**Files:** (없음 — 플레이 검증)

- [ ] **Step 1: 서버 플레이로 동작 확인**

서버에서 한 라운드 플레이하며 관찰:
1. 경험치구슬 획득 → level/exp 정상 증가(World.Level), 레벨업 시 statPoints 증가(클라 HUD/디버그로 확인).
2. 클라 HUD 레벨/exp 바 갱신(스냅샷 경로).
3. 스폰/**늦은 접속** 다른 유저의 level/exp 정상 표시(생성데이터 경로).
4. `[World] ... Level not found` 경고가 정상 플레이 중 **안 뜸**(캐릭터는 항상 World.Level 보유).
5. 콘솔 에러 0. World Core 회귀(데미지→사망 `[World] Death entity ...`) 정상.

동작 변화 없음(회귀 0)이 곧 성공.

---

## Slice 2 완료 기준

- [ ] 서버 `feature/world-core-level-migration` 브랜치에 Level 커밋 1개 (픽스처 미포함).
- [ ] 픽스처 2파일이 working tree에 dirty로 보존, 기존 `5-23` stash 무손상.
- [ ] 서버 인스턴스 컴파일 에러 0 (Level 격리 시 + 픽스처 결합 시 모두).
- [ ] `LevelComponent` 서버에서 완전 제거(grep 0).
- [ ] 수동 플레이로 level/exp/statPoints·HUD·늦은접속 동작 보존 확인.

다음: **Slice 3 (클라)** — 생성 + `EntityLevelChanged` + 스냅샷 apply + HUD pull/구독 + DI + 레거시 제거. (별도 plan)
