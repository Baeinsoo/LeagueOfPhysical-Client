# World Core Stats Migration — Slice 3 (Server) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버에서 stat의 진실원본을 레거시 `StatsComponent` → `GameFramework.World.Stats`로 뒤집는다 — World.Stats 생성·시드, combat read-flip(strength/dexterity), 할당 write-flip(AddBase + statPoints--), 늦은접속 wire 채움(World.Stats→CharacterCreationData), 도메인 struct +4필드, DI 등록, 레거시 제거. 동작 보존.

**Architecture:** 서버 single Assembly-CSharp LOP 글루 + GameFramework 코어(Slice 1 머지: `EntityStatType`/`StatsSystem.GetValue/SetBase/AddBase`) + Shared wire(Slice 2 머지: `CharacterCreationData` +4 stat). 자동 단위 테스트 없음(단일 어셈블리) — 검증은 컴파일 + 수동 플레이. stat은 상주 값(할당 시에만 변함). **세 가지 다른 stat 접근**: 생성 시드/늦은접속 영속 = `BaseStats` 직접(base 값), combat = `GetValue`(effective, 미래 모디파이어 호환, int 캐스트), 할당 = `AddBase`(base 증가).

**Tech Stack:** C# (Unity), VContainer DI(ctor 주입 + `[Inject]` 필드), UnityMCP 컴파일 검증.

**Related spec:** `docs/superpowers/specs/2026-06-19-world-core-stats-migration-design.md` (Slice 3)

**Resolved Unity instance (매 UnityMCP 호출에 명시 — HTTP stateless):** Server `LeagueOfPhysical-Server@f99391fa2dbaaf3c` (서버 작업 인가됨)

---

## ⚠️ 서버 픽스처 — 이번엔 stash 불필요

서버 working-tree에 커밋 금지 픽스처 `LOPGame.cs`·`ConfigureRoomComponent.cs`가 dirty로 상존(+ 기존 `stash@{0}: 5-23`). **이번 Slice 3는 그 두 파일을 건드리지 않는다**(조사 확인): combat=`LOPCombatSystem`, 할당=`Game.Entity.MessageHandler`, 생성=`CharacterCreator`, 둘 다 LOPGame 아님. 도메인 struct에 stat 필드를 추가해도 **LOPGame의 struct 리터럴은 새 필드를 안 써서 기본 0**(신규 스폰=0 스탯, 현행 보존) → LOPGame 편집 불필요. 따라서 **stash 댄스 없이** 진행하고, 커밋 시 픽스처 2파일만 제외(stage 명시). 만약 작업 중 `LOPGame.cs`/`ConfigureRoomComponent.cs`를 건드려야 하는 상황이 생기면 중단·재검토.

---

## File Structure (server repo: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`)

- **Modify:** `Assets/Scripts/Entity/CharacterCreationData.cs` — 도메인 struct +4 int 필드.
- **Modify:** `Assets/Scripts/EntityCreator/CharacterCreator.cs` — World.Stats 생성·시드 + 레거시 제거.
- **Modify:** `Assets/Scripts/CombatSystem/LOPCombatSystem.cs` — ctor +StatsSystem, combat read-flip.
- **Modify:** `Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs` — wire 4 stat 채움(World.Stats BaseStats).
- **Modify:** `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs` — `OnStatAllocationToS` write-flip + 주입.
- **Modify:** `Assets/Scripts/Game/GameLifetimeScope.cs` — StatsSystem 등록.
- **Delete:** `Assets/Scripts/Component/StatsComponent.cs` (+`.meta`).
- 무변경(커밋 제외): `Assets/Scripts/Game/LOPGame.cs`, `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs` (픽스처).

---

## Task 1: 서버 피처 브랜치

**Files:** (git)

- [ ] **Step 1: 상태 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" stash list
```
Expected: dirty = `LOPGame.cs` + `ConfigureRoomComponent.cs`(픽스처, 그대로 둠), `stash@{0}: 5-23`. 다른 dirty가 있으면 중단.

- [ ] **Step 2: 브랜치 생성** (dirty 픽스처는 따라옴 — stash 안 함)
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" checkout -b feature/world-core-stats-migration
```

---

## Task 2: 도메인 struct +4 stat 필드

**Files:** Modify `Assets/Scripts/Entity/CharacterCreationData.cs`

- [ ] **Step 1: 4 필드 추가**

`public long currentExp { get; set; }`(21줄) 다음, 닫는 `}` 앞에 추가:
```csharp

        public int strength { get; set; }
        public int dexterity { get; set; }
        public int intelligence { get; set; }
        public int vitality { get; set; }
```
(LOPGame의 struct 리터럴은 이 필드를 설정하지 않음 → 신규 스폰 시 기본 0. 의도된 동작.)

---

## Task 3: CharacterCreator — World.Stats 시드 + 레거시 제거

**Files:** Modify `Assets/Scripts/EntityCreator/CharacterCreator.cs`

- [ ] **Step 1: World.Stats 생성·시드 추가**

World Core 블록에서 `worldEntity.Add(new GameFramework.World.Velocity { Linear = entity.velocity.ToNumerics() });`(82줄) 다음, `entityRegistry.Add(worldEntity);`(83줄) 앞에 추가:
```csharp
            var worldStats = new GameFramework.World.Stats();
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Strength] = creationData.strength;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Dexterity] = creationData.dexterity;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Intelligence] = creationData.intelligence;
            worldStats.BaseStats[(int)GameFramework.World.EntityStatType.Vitality] = creationData.vitality;
            worldEntity.Add(worldStats);
```
(BaseStats 직접 초기화 — Health가 `new Health(max){Current=...}`로 직접 데이터 init하는 것과 동일 결. StatsSystem 주입 불필요. GF 테스트도 `stats.BaseStats[key]=...`를 직접 씀.)

- [ ] **Step 2: 레거시 StatsComponent 생성 제거**

41–43줄 삭제:
```csharp
            StatsComponent statsComponent = entity.AddEntityComponent<StatsComponent>();
            objectResolver.Inject(statsComponent);
            statsComponent.Initialize(creationData.characterCode);
```

(컴파일은 Task 8에서 일괄 — `StatsComponent` 타입 아직 존재.)

---

## Task 4: LOPCombatSystem — combat read-flip (World.Stats GetValue)

**Files:** Modify `Assets/Scripts/CombatSystem/LOPCombatSystem.cs`

- [ ] **Step 1: ctor에 StatsSystem 주입 추가**

필드 + 생성자(8–20줄)를 교체:
```csharp
        private readonly GameFramework.World.WorldEventBuffer worldEventBuffer;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.HealthSystem healthSystem;

        public LOPCombatSystem(
            GameFramework.World.WorldEventBuffer worldEventBuffer,
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.HealthSystem healthSystem)
        {
            this.worldEventBuffer = worldEventBuffer;
            this.entityRegistry = entityRegistry;
            this.healthSystem = healthSystem;
        }
```
→
```csharp
        private readonly GameFramework.World.WorldEventBuffer worldEventBuffer;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.HealthSystem healthSystem;
        private readonly GameFramework.World.StatsSystem statsSystem;

        public LOPCombatSystem(
            GameFramework.World.WorldEventBuffer worldEventBuffer,
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.World.HealthSystem healthSystem,
            GameFramework.World.StatsSystem statsSystem)
        {
            this.worldEventBuffer = worldEventBuffer;
            this.entityRegistry = entityRegistry;
            this.healthSystem = healthSystem;
            this.statsSystem = statsSystem;
        }
```

- [ ] **Step 2: stat 읽기를 World.Stats로 교체**

48–54줄을 교체:
```csharp
            StatsComponent attackerStats = attacker.GetEntityComponent<StatsComponent>();
            StatsComponent targetStats = target.GetEntityComponent<StatsComponent>();

            damage += attackerStats.strength;

            bool isDodged = IsDodge(attackerStats.dexterity, targetStats.dexterity);
            bool isCritical = IsCritical(attackerStats.strength, targetStats.strength);
```
→
```csharp
            GameFramework.World.Stats attackerStats = entityRegistry.Get(attacker.entityId)?.Get<GameFramework.World.Stats>();
            GameFramework.World.Stats targetStats = entityRegistry.Get(target.entityId)?.Get<GameFramework.World.Stats>();

            int attackerStrength = attackerStats != null ? Mathf.RoundToInt(statsSystem.GetValue(attackerStats, (int)GameFramework.World.EntityStatType.Strength)) : 0;
            int attackerDexterity = attackerStats != null ? Mathf.RoundToInt(statsSystem.GetValue(attackerStats, (int)GameFramework.World.EntityStatType.Dexterity)) : 0;
            int targetStrength = targetStats != null ? Mathf.RoundToInt(statsSystem.GetValue(targetStats, (int)GameFramework.World.EntityStatType.Strength)) : 0;
            int targetDexterity = targetStats != null ? Mathf.RoundToInt(statsSystem.GetValue(targetStats, (int)GameFramework.World.EntityStatType.Dexterity)) : 0;

            damage += attackerStrength;

            bool isDodged = IsDodge(attackerDexterity, targetDexterity);
            bool isCritical = IsCritical(attackerStrength, targetStrength);
```
(GetValue=effective stat. 모디파이어 없으면 base==레거시 할당값 → 동작 동일. `Mathf`/`using UnityEngine`는 이미 존재[helpers가 사용].)

---

## Task 5: CharacterCreationDataCreator — wire 4 stat 채움 (늦은접속 영속)

**Files:** Modify `Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs`

- [ ] **Step 1: Level 블록 뒤에 Stats 해석 추가**

`if (level == null) { ... }`(38–42줄) 다음, `global::CharacterCreationData ... = new`(44줄) 앞에 추가:
```csharp

            GameFramework.World.Stats stats = worldEntity?.Get<GameFramework.World.Stats>();
            if (stats == null)
            {
                UnityEngine.Debug.LogWarning($"[World] CharacterCreationData: Stats not found for entity {lopEntity.entityId}");
            }
```

- [ ] **Step 2: 초기화자에 4 stat 필드 추가**

object initializer의 `CurrentExp = level?.Exp ?? 0,`(55줄) 다음에 추가(`}` 앞):
```csharp
                Strength = BaseStatInt(stats, GameFramework.World.EntityStatType.Strength),
                Dexterity = BaseStatInt(stats, GameFramework.World.EntityStatType.Dexterity),
                Intelligence = BaseStatInt(stats, GameFramework.World.EntityStatType.Intelligence),
                Vitality = BaseStatInt(stats, GameFramework.World.EntityStatType.Vitality),
```

- [ ] **Step 3: BaseStatInt 헬퍼 추가**

클래스 내부(예: `Create` 메서드들 뒤, 클래스 닫는 `}` 앞)에 private static 헬퍼 추가:
```csharp
        private static int BaseStatInt(GameFramework.World.Stats stats, GameFramework.World.EntityStatType statType)
        {
            return stats != null && stats.BaseStats.TryGetValue((int)statType, out var v) ? (int)v : 0;
        }
```
(base 값 영속 — combat의 effective(GetValue)와 달리 *base*를 보냄. 미래 모디파이어가 base로 baking되는 것 방지.)

---

## Task 6: OnStatAllocationToS — 할당 write-flip (AddBase)

**Files:** Modify `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs`

- [ ] **Step 1: 주입 추가**

`[Inject]` 필드 블록(8–12줄, `gameEngine`+`sessionManager`) 끝에 추가:
```csharp

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private GameFramework.World.StatsSystem statsSystem;
```

- [ ] **Step 2: 핸들러 본문 교체**

`OnStatAllocationToS`의 34–57줄(StatsComponent 해석 + switch)을 교체:
```csharp
            StatsComponent statsComponent = entity.GetEntityComponent<StatsComponent>();
            int statValue = 0;
            switch (statAllocationToS.Stat)
            {
                case nameof(StatsComponent.strength):
                    statsComponent.strength++;
                    statValue = statsComponent.strength;
                    break;

                case nameof(StatsComponent.dexterity):
                    statValue = statsComponent.dexterity++;
                    statValue = statsComponent.dexterity;
                    break;

                case nameof(StatsComponent.intelligence):
                    statValue = statsComponent.intelligence++;
                    statValue = statsComponent.intelligence;
                    break;

                case nameof(StatsComponent.vitality):
                    statValue = statsComponent.vitality++;
                    statValue = statsComponent.vitality;
                    break;
            }
```
→
```csharp
            GameFramework.World.Stats stats = entityRegistry.Get(entity.entityId)?.Get<GameFramework.World.Stats>();
            int statValue = 0;
            if (stats != null)
            {
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
            }
```
59줄 `entity.GetEntityComponent<PlayerComponent>().statPoints--;` **유지**(이번 비대상). 61–67줄 `StatAllocationToC` 회신 **유지**(`Stat = statAllocationToS.Stat`, `StatValue = statValue`).

> 문자열 계약: 클라는 현재 `nameof(StatsComponent.strength)`="strength"를 보냄. Slice 4에서 클라 `StatsComponent` 제거 후에도 동일하게 "strength" 리터럴을 보내도록 한다 → 서버 switch 키와 일치(슬라이스 간 비파괴).

---

## Task 7: GameLifetimeScope — StatsSystem 등록

**Files:** Modify `Assets/Scripts/Game/GameLifetimeScope.cs`

- [ ] **Step 1: StatsSystem Singleton 등록**

`builder.Register<GameFramework.World.LevelSystem>(Lifetime.Singleton);`(23줄) 다음에 추가:
```csharp
            builder.Register<GameFramework.World.StatsSystem>(Lifetime.Singleton);
```
(VContainer가 `LOPCombatSystem` ctor + `CharacterCreationDataCreator`/`Game.Entity.MessageHandler` `[Inject]`에 자동 주입.)

---

## Task 8: 레거시 StatsComponent 삭제

**Files:** Delete `Assets/Scripts/Component/StatsComponent.cs` (+`.meta`)

- [ ] **Step 1: 디스크에서 삭제** (controller가 커밋 시 git rm 처리; 또는 직접 삭제)

`Assets/Scripts/Component/StatsComponent.cs`와 `Assets/Scripts/Component/StatsComponent.cs.meta` 삭제.

- [ ] **Step 2: 잔존 참조 0 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" grep -n "StatsComponent" -- "Assets/Scripts" || echo "NO MATCHES"
```
Expected: `NO MATCHES`. 매치 있으면 그 파일도 repoint 필요 — 중단·확인.

---

## Task 9: 컴파일 검증

**Files:** (검증만)

- [ ] **Step 1: 서버 인스턴스 컴파일**

Run (UnityMCP):
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`

Expected: 에러 0건. 에러 시 해당 태스크로 돌아가 수정.

---

## Task 10: 커밋 (Stats 파일만 — 픽스처 제외)

**Files:** (git)

- [ ] **Step 1: working tree 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
```
Expected: Stats 6개 수정 + StatsComponent.cs/.meta 삭제 + (무관 픽스처) `LOPGame.cs`/`ConfigureRoomComponent.cs`. **픽스처 2파일은 stage 안 함.**

- [ ] **Step 2: Stats 파일만 stage + 커밋**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" add Assets/Scripts/Entity/CharacterCreationData.cs Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/CombatSystem/LOPCombatSystem.cs Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Component/StatsComponent.cs Assets/Scripts/Component/StatsComponent.cs.meta
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" commit -m "feat(world): migrate server Stats to World.Stats (combat read-flip, allocation AddBase, creation seed + late-join fill, remove legacy StatsComponent)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

- [ ] **Step 3: 커밋 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" show --stat HEAD | head -16
```
Expected: 위 파일들만(7 + 삭제). **`LOPGame.cs`/`ConfigureRoomComponent.cs` 포함되면 즉시 중단·에스컬레이트.**

---

## Task 11: 런타임 수동 검증 (사용자)

**Files:** (없음 — 플레이)

- [ ] **Step 1: 서버 플레이**
1. 스탯 할당(+버튼) → statValue 증가 회신, statPoints 감소.
2. combat이 strength/dexterity 반영(데미지/회피/치명) — 할당 전후 차이 관찰(0-스탯 신규 캐릭은 현행과 동일).
3. **늦은접속**: 할당한 스탯이 클라에 **유지**(0 리셋 안 됨) — 재접 버그 해소(서버 측 wire 채움 확인; 클라 seed는 Slice 4).
4. `[World] ... Stats not found` 경고 정상 플레이 중 안 뜸. 콘솔 에러 0. World Core 회귀(데미지→사망) 정상.

동작 보존(회귀 0)이 성공.

---

## Slice 3 완료 기준

- [ ] 서버 `feature/world-core-stats-migration`에 Stats 커밋 1개(픽스처 미포함).
- [ ] 서버 컴파일 0에러. `StatsComponent` 완전 제거(grep 0).
- [ ] 픽스처 2파일 working tree 보존(미커밋), `5-23` stash 무손상.
- [ ] 수동 플레이로 할당/combat/늦은접속 동작 확인.

다음: **Slice 4 (클라)** — World.Stats 시드 + `EntityStatChanged` + 할당 적용 flip + HUD flip + DI + 레거시 제거. (별도 plan)
