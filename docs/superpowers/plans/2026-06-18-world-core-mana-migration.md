# World Core Mana Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 레거시 `ManaComponent`(클·서)를 코어 `GameFramework.World.Mana`로 이행한다. MP 단일 진실원본을 World 코어로 일원화하고 양쪽 레거시 컴포넌트를 제거한다. HP와 동형(Model A).

**Architecture:** GameFramework에 `ManaSystem.ApplyAuthoritativeState`(+EditMode 테스트) 추가. 서버는 `CharacterCreator`가 `World.Mana` 생성 + `LOPGameEngine`/`CharacterCreationDataCreator`가 World.Mana에서 MP read(read-flip). 클라는 `CharacterCreator`가 World.Mana 생성 + 스냅샷 핸들러가 `ManaSystem`으로 적용하며 신규 이산 이벤트 `EntityManaChanged` 발행, `CharacterHudViewModel`이 초기 pull + 그 이벤트로 라이브 갱신. 양쪽 레거시 `ManaComponent` 삭제. MP는 게임플레이 writer가 없어(정적) writer flip 없음.

**Tech Stack:** Unity 6 (클·서 단일 Assembly-CSharp), VContainer DI, R3, GameFramework `World`(file: 공유 패키지, asmdef + EditMode 테스트), UnityMCP(인스턴스 핀).

---

## Repos / 체크아웃 / 브랜치 (3 repo)

| 영역 | repo | 편집 체크아웃 | 브랜치 |
|---|---|---|---|
| GameFramework 코드 | GameFramework | `C:/Users/re5na/workspace/LOP/GameFramework` | `feature/world-core-mana` |
| 서버 코드 | Server | `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server` | `feature/world-core-mana-migration` |
| 클라 코드 | Client | `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client` **(메인 체크아웃)** | `feature/world-core-mana-migration` |
| 클라 문서(spec/plan) | Client | `.../.claude/worktrees/mana-world-migration` (이 워크트리) | `worktree-mana-world-migration` (이미 spec/plan 커밋) |

> ⚠️ **클라 코드는 반드시 메인 체크아웃**(`.../LeagueOfPhysical-Client`)에서 편집한다 — Unity 클라 인스턴스·UnityMCP 컴파일 체크가 그 체크아웃을 본다. 문서 워크트리에서 편집하면 Unity가 못 본다.
> GameFramework는 file: 참조라 편집 즉시 클·서 양쪽이 본다. main 직접 커밋 금지.

> **⚠️ 서버 로컬 픽스처 (커밋 금지):** 서버 working-tree `Assets/Scripts/Game/LOPGame.cs`·`Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`는 미커밋 상태로 상존. 이 plan은 안 건드림. 절대 스테이징/커밋/`git restore` 금지.

## UnityMCP 인스턴스 핀

매 호출 `mcpforunity://instances`에서 resolve 후 `unity_instance` 지정:
- **클라**: `name == "LeagueOfPhysical-Client"`의 `id` (작성 시점 `LeagueOfPhysical-Client@de70658b9450cbb4`, hash 변동 가능).
- **서버**: `name == "LeagueOfPhysical-Server"`의 `id` (작성 시점 `LeagueOfPhysical-Server@f99391fa2dbaaf3c`).
- GameFramework/클라 변경 → **클라 인스턴스** 컴파일·테스트. 서버 변경 → **서버 인스턴스**. 실제 실행하고 보고.

## File Structure

| 파일 | repo | 변경 | 책임 |
|---|---|---|---|
| `Runtime/Scripts/World/Systems/ManaSystem.cs` | GameFramework | Create | `ApplyAuthoritativeState(Mana,max,current)` |
| `Tests/World/ManaSystemTests.cs` | GameFramework | Create | EditMode 테스트(TDD) |
| `Assets/Scripts/EntityCreator/CharacterCreator.cs` | Server | Modify | World.Mana 생성 + 레거시 생성 제거 |
| `Assets/Scripts/Game/LOPGameEngine.cs` | Server | Modify | 스냅샷 MP를 World.Mana에서 read |
| `Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs` | Server | Modify | 생성데이터 MP를 World.Mana에서 read |
| `Assets/Scripts/Component/ManaComponent.cs`(+meta) | Server | Delete | 레거시 제거 |
| `Assets/Scripts/EntityCreator/CharacterCreator.cs` | Client | Modify | World.Mana 생성 + 레거시 생성 제거 |
| `Assets/Scripts/Entity/Event.Entity.cs` | Client | Modify | 신규 `EntityManaChanged` 이벤트 |
| `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs` | Client | Modify | World.Mana 적용 + EntityManaChanged 발행 |
| `Assets/Scripts/UI/CharacterHud/CharacterHudViewModel.cs` | Client | Modify | 초기 pull(World.Mana) + EntityManaChanged 라이브 |
| `Assets/Scripts/Game/GameLifetimeScope.cs` | Client | Modify | `ManaSystem` Singleton 등록 |
| `Assets/Scripts/Component/ManaComponent.cs`(+meta) | Client | Delete | 레거시 제거 |

> **테스트 전략:** 코어 `ManaSystem` = GameFramework EditMode(TDD). LOP 글루 = 컴파일 + 수동 플레이(단일 Assembly-CSharp 제약). **순서:** Task 1(GameFramework, 클라가 의존) → Task 2(서버, 독립) → Task 3(클라, Task 1 의존) → Task 4(검증).

---

## Task 1: GameFramework — ManaSystem + EditMode 테스트 (TDD)

**Files:**
- Create `C:/Users/re5na/workspace/LOP/GameFramework/Tests/World/ManaSystemTests.cs`
- Create `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/World/Systems/ManaSystem.cs`

- [ ] **Step 1: 브랜치 생성 (GameFramework repo)**
```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git checkout -b feature/world-core-mana
git branch --show-current   # → feature/world-core-mana
```

- [ ] **Step 2: 실패하는 테스트 작성** — `Tests/World/ManaSystemTests.cs` 생성 (기존 `HealthSystemTests` ApplyAuthoritativeState 3종 미러):
```csharp
using NUnit.Framework;

namespace GameFramework.World.Tests
{
    public class ManaSystemTests
    {
        private readonly ManaSystem _system = new ManaSystem();

        [Test]
        public void ApplyAuthoritativeState_overwrites_Max_and_Current()
        {
            var mana = new Mana(100) { Current = 40 };

            _system.ApplyAuthoritativeState(mana, max: 200, current: 150);

            Assert.AreEqual(200, mana.Max);
            Assert.AreEqual(150, mana.Current);
        }

        [Test]
        public void ApplyAuthoritativeState_clamps_Current_above_Max()
        {
            var mana = new Mana(100);

            _system.ApplyAuthoritativeState(mana, max: 80, current: 999);

            Assert.AreEqual(80, mana.Max);
            Assert.AreEqual(80, mana.Current);
        }

        [Test]
        public void ApplyAuthoritativeState_clamps_negative_Current_to_zero()
        {
            var mana = new Mana(100);

            _system.ApplyAuthoritativeState(mana, max: 100, current: -5);

            Assert.AreEqual(0, mana.Current);
        }
    }
}
```

- [ ] **Step 3: 테스트가 실패(red)하는지 확인** — UnityMCP (CLIENT pin): `refresh_unity(unity_instance="<클라 id>")` 후 `read_console(unity_instance="<클라 id>", types=["error"])`. Expected: **컴파일 에러** `error CS0246: ... 'ManaSystem' could not be found` (타입 미존재 = red). GameFramework는 file: 공유라 클라가 즉시 봄.

- [ ] **Step 4: ManaSystem 구현** — `Runtime/Scripts/World/Systems/ManaSystem.cs` 생성 (`HealthSystem.ApplyAuthoritativeState` 미러):
```csharp
using System;

namespace GameFramework.World
{
    /// <summary><see cref="Mana"/> 데이터를 변경하는 로직. 상태 보유 없음(순수 함수적).</summary>
    public class ManaSystem
    {
        /// <summary>
        /// 권위 스냅샷 등으로 Max/Current를 통째로 덮어쓴다. 결정/계산/가드 없음 (Application 메서드).
        /// Current는 [0, Max]로 클램프해 데이터 무결성만 보장한다.
        /// </summary>
        public void ApplyAuthoritativeState(Mana mana, int max, int current)
        {
            mana.Max = max;
            mana.Current = Math.Clamp(current, 0, max);
        }
    }
}
```

- [ ] **Step 5: 테스트 green 확인** — UnityMCP (CLIENT pin):
  - `refresh_unity(unity_instance="<클라 id>")` → `read_console(unity_instance="<클라 id>", types=["error"])` = 0 에러.
  - `run_tests` (UnityMCP — ToolSearch로 로드): EditMode 모드, 필터 `GameFramework.World.Tests.ManaSystemTests` (또는 `ManaSystemTests`), `unity_instance="<클라 id>"`. 필요시 `get_test_job`으로 결과 폴링. Expected: **3 passed, 0 failed.** 실제 실행하고 보고.

- [ ] **Step 6: Commit (GameFramework repo)** — Unity 생성 `.meta` 동반:
```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git add Runtime/Scripts/World/Systems/ManaSystem.cs Runtime/Scripts/World/Systems/ManaSystem.cs.meta Tests/World/ManaSystemTests.cs Tests/World/ManaSystemTests.cs.meta
git status   # 위 4개만 staged 확인
git commit -m "feat(world): add ManaSystem.ApplyAuthoritativeState + tests

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
> `.meta`가 아직 없으면(Unity가 새 파일 import 전) Step 5의 `refresh_unity` 후 생성됨 — 생성 확인 후 add. 없으면 `ls Runtime/Scripts/World/Systems/ManaSystem.cs.meta`로 확인.

---

## Task 2: 서버 — World.Mana 생성 + MP read-flip + 레거시 제거

**Files (server repo):**
- Modify `Assets/Scripts/EntityCreator/CharacterCreator.cs`
- Modify `Assets/Scripts/Game/LOPGameEngine.cs`
- Modify `Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs`
- Delete `Assets/Scripts/Component/ManaComponent.cs` (+`.meta`)

- [ ] **Step 1: 브랜치 생성 (server repo)**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git checkout -b feature/world-core-mana-migration
git branch --show-current
```

- [ ] **Step 2: CharacterCreator — World.Mana 생성 추가.** `Assets/Scripts/EntityCreator/CharacterCreator.cs`에서 `worldEntity.Add(worldHealth);` 줄 **바로 다음**에 한 줄 추가:
```csharp
            worldEntity.Add(worldHealth);
            worldEntity.Add(new GameFramework.World.Mana(creationData.maxMP) { Current = creationData.currentMP });
```

- [ ] **Step 3: CharacterCreator — 레거시 ManaComponent 생성 제거.** 같은 파일에서 다음 3줄(+ 주변 빈 줄 정리)을 제거:
```csharp
            ManaComponent manaComponent = entity.AddEntityComponent<ManaComponent>();
            objectResolver.Inject(manaComponent);
            manaComponent.Initialize(creationData.maxMP, creationData.currentMP);
```
> `creationData.maxMP/currentMP`는 Step 2의 World.Mana 생성에서 계속 사용 — 그대로 둔다.

- [ ] **Step 4: LOPGameEngine — 스냅샷 MP를 World.Mana에서 read.** `Assets/Scripts/Game/LOPGameEngine.cs`의 `UserEntitySnapToC` 빌드부에서 다음 2줄:
```csharp
                entitySnapsToC.CurrentMP = entity.GetEntityComponent<ManaComponent>().currentMP;
                entitySnapsToC.MaxMP = entity.GetEntityComponent<ManaComponent>().maxMP;
```
을 다음으로 교체(직전에 이미 선언된 `worldEntity` 재사용):
```csharp
                GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
                if (mana == null)
                {
                    Debug.LogWarning($"[World] UserEntitySnap: Mana not found for entity {entity.entityId}");
                }
                entitySnapsToC.CurrentMP = mana?.Current ?? 0;
                entitySnapsToC.MaxMP = mana?.Max ?? 0;
```
> `worldEntity`는 같은 블록 위에서 `GameFramework.World.Entity worldEntity = entityRegistry.Get(entity.entityId);`로 이미 선언됨(Health 1b). 재선언 금지.

- [ ] **Step 5: CharacterCreationDataCreator — 생성데이터 MP를 World.Mana에서 read.** `Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs`에서 Health null-가드 블록(`if (health == null) { ... }`) **바로 다음**에 Mana 해석 추가:
```csharp
            if (health == null)
            {
                UnityEngine.Debug.LogWarning($"[World] CharacterCreationData: Health not found for entity {lopEntity.entityId}");
            }

            GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
            if (mana == null)
            {
                UnityEngine.Debug.LogWarning($"[World] CharacterCreationData: Mana not found for entity {lopEntity.entityId}");
            }
```
그리고 초기자 안의 다음 2줄:
```csharp
                MaxMP = lopEntity.GetEntityComponent<ManaComponent>().maxMP,
                CurrentMP = lopEntity.GetEntityComponent<ManaComponent>().currentMP,
```
을 다음으로 교체:
```csharp
                MaxMP = mana?.Max ?? 0,
                CurrentMP = mana?.Current ?? 0,
```
> `worldEntity`는 위에서 `GameFramework.World.Entity worldEntity = entityRegistry.Get(lopEntity.entityId);`로 이미 선언됨. Level/Exp 줄은 그대로.

- [ ] **Step 6: 참조 0 확인** — Grep tool: pattern `ManaComponent`, path `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets`, output_mode `files_with_matches`. Expected: **`Component/ManaComponent.cs` 단 하나(정의 자신)**. 다른 매치 있으면 멈추고 처리.

- [ ] **Step 7: ManaComponent 삭제**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git rm Assets/Scripts/Component/ManaComponent.cs Assets/Scripts/Component/ManaComponent.cs.meta
```

- [ ] **Step 8: 컴파일 확인** — UnityMCP (SERVER pin): `refresh_unity(unity_instance="<서버 id>")` → `read_console(unity_instance="<서버 id>", types=["error"])` = **0 에러**. 실제 실행하고 보고.

- [ ] **Step 9: Commit (server repo)** — 로컬 픽스처 제외:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/EntityCreator/CharacterCreator.cs Assets/Scripts/Game/LOPGameEngine.cs Assets/Scripts/EntityCreationDataFactory/CharacterCreationDataCreator.cs Assets/Scripts/Component/ManaComponent.cs Assets/Scripts/Component/ManaComponent.cs.meta
git status   # 위 파일들만 staged, LOPGame.cs/ConfigureRoomComponent.cs는 UNSTAGED 확인
git commit -m "refactor(world): migrate server MP to World.Mana, remove legacy ManaComponent

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: 클라 — World.Mana 생성 + EntityManaChanged + HUD 라이브 + 레거시 제거

**편집 위치: 클라 메인 체크아웃** `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client` (문서 워크트리 아님).

**Files (client repo, 메인 체크아웃):**
- Modify `Assets/Scripts/Entity/Event.Entity.cs`
- Modify `Assets/Scripts/Game/GameLifetimeScope.cs`
- Modify `Assets/Scripts/EntityCreator/CharacterCreator.cs`
- Modify `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs`
- Modify `Assets/Scripts/UI/CharacterHud/CharacterHudViewModel.cs`
- Delete `Assets/Scripts/Component/ManaComponent.cs` (+`.meta`)

- [ ] **Step 1: 브랜치 생성 (client repo, 메인 체크아웃)**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git checkout -b feature/world-core-mana-migration
git branch --show-current
```
> 이 체크아웃은 `main`에 있었음(작성 시점 tip `beb6814`). 워크트리(docs)와 별개 브랜치.

- [ ] **Step 2: EntityManaChanged 이벤트 추가.** `Assets/Scripts/Entity/Event.Entity.cs`의 `EntityDamage` struct 바로 다음(또는 파일 내 적당한 위치)에 추가:
```csharp
    public struct EntityManaChanged
    {
        public int current;
        public int max;

        public EntityManaChanged(int current, int max)
        {
            this.current = current;
            this.max = max;
        }
    }
```

- [ ] **Step 3: GameLifetimeScope — ManaSystem 등록.** `Assets/Scripts/Game/GameLifetimeScope.cs`에서 HealthSystem 등록 줄 다음에 추가:
```csharp
            builder.Register<GameFramework.World.HealthSystem>(Lifetime.Singleton);
            builder.Register<GameFramework.World.ManaSystem>(Lifetime.Singleton);
```

- [ ] **Step 4: CharacterCreator — World.Mana 생성 추가 + 레거시 제거.** `Assets/Scripts/EntityCreator/CharacterCreator.cs`에서 `worldEntity.Add(worldHealth);` 다음에 한 줄 추가:
```csharp
            worldEntity.Add(worldHealth);
            worldEntity.Add(new GameFramework.World.Mana(creationData.maxMP) { Current = creationData.currentMP });
```
그리고 레거시 생성 3줄 제거:
```csharp
            ManaComponent manaComponent = entity.AddEntityComponent<ManaComponent>();
            objectResolver.Inject(manaComponent);
            manaComponent.Initialize(creationData.maxMP, creationData.currentMP);
```

- [ ] **Step 5: 스냅샷 핸들러 — ManaSystem 주입 + World.Mana 적용 + 이벤트 발행.** `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs`:
  (a) 주입 필드 추가 — `healthSystem` 줄 다음:
```csharp
        [Inject] private GameFramework.World.HealthSystem healthSystem;
        [Inject] private GameFramework.World.ManaSystem manaSystem;
```
  (b) `OnUserEntitySnapToC`에서 레거시 Mana write 2줄:
```csharp
            playerContext.entity.GetComponent<ManaComponent>().currentMP = userEntitySnapToC.CurrentMP;
            playerContext.entity.GetComponent<ManaComponent>().maxMP = userEntitySnapToC.MaxMP;
```
  을 다음으로 교체(직전에 선언된 `worldEntity` 재사용, 변경 시에만 발행):
```csharp
            GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
            if (mana != null)
            {
                int prevCurrent = mana.Current;
                int prevMax = mana.Max;
                manaSystem.ApplyAuthoritativeState(mana, userEntitySnapToC.MaxMP, userEntitySnapToC.CurrentMP);
                if (mana.Current != prevCurrent || mana.Max != prevMax)
                {
                    EventBus.Default.Publish(
                        EventTopic.EntityId<LOPEntity>(playerContext.entity.entityId),
                        new EntityManaChanged(mana.Current, mana.Max));
                }
            }
            else
            {
                Debug.LogWarning($"[World] UserEntitySnap: Mana not found for entity {playerContext.entity.entityId}");
            }
```
> `worldEntity`는 같은 메서드 위에서 Health용으로 이미 선언됨(`GameFramework.World.Entity worldEntity = entityRegistry.Get(playerContext.entity.entityId);`). 재선언 금지. Level/User write 줄(LevelComponent/UserComponent)은 그대로 둔다.

- [ ] **Step 6: CharacterHudViewModel — 초기 pull(World.Mana) + EntityManaChanged 라이브.** `Assets/Scripts/UI/CharacterHud/CharacterHudViewModel.cs`:
  (a) 레거시 `_mana` 필드 제거:
```csharp
        private readonly ManaComponent _mana;
```
  (b) 생성자에서 `_mana = _entity.GetEntityComponent<ManaComponent>();` 줄 제거. 같은 생성자의 구독 줄들 옆에 EntityManaChanged 구독 추가:
```csharp
            EventBus.Default.Subscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnPropertyChange);
            EventBus.Default.Subscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityDamage);
            EventBus.Default.Subscribe<EntityManaChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityManaChanged);
```
  (c) `OnPropertyChange`에서 Mana 두 케이스 제거(Level 케이스는 유지):
```csharp
                case nameof(ManaComponent.currentMP): _mp.Value = _mana.currentMP; break;
                case nameof(ManaComponent.maxMP): _maxMp.Value = _mana.maxMP; break;
```
  (d) `OnEntityDamage` 메서드 다음에 핸들러 추가:
```csharp
        private void OnEntityManaChanged(EntityManaChanged e)
        {
            _mp.Value = e.current;
            _maxMp.Value = e.max;
        }
```
  (e) `PushAll`에서 레거시 Mana 블록:
```csharp
            if (_mana != null)
            {
                _mp.Value = _mana.currentMP;
                _maxMp.Value = _mana.maxMP;
            }
```
  을 World.Mana pull로 교체(직전에 선언된 `worldEntity` 재사용):
```csharp
            GameFramework.World.Mana mana = worldEntity?.Get<GameFramework.World.Mana>();
            if (mana != null)
            {
                _mp.Value = mana.Current;
                _maxMp.Value = mana.Max;
            }
```
> `worldEntity`는 `PushAll` 위에서 Health용으로 이미 선언됨. 재선언 금지.
  (f) `Dispose`에서 unsubscribe 추가:
```csharp
                EventBus.Default.Unsubscribe<EntityManaChanged>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityManaChanged);
```

- [ ] **Step 7: 참조 0 확인** — Grep tool: pattern `ManaComponent`, path `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets`, output_mode `files_with_matches`. Expected: **`Component/ManaComponent.cs` 단 하나**. 다른 매치 있으면 멈추고 처리.

- [ ] **Step 8: ManaComponent 삭제**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git rm Assets/Scripts/Component/ManaComponent.cs Assets/Scripts/Component/ManaComponent.cs.meta
```

- [ ] **Step 9: 컴파일 확인** — UnityMCP (CLIENT pin): `refresh_unity(unity_instance="<클라 id>")` → `read_console(unity_instance="<클라 id>", types=["error"])` = **0 에러**. 실제 실행하고 보고.

- [ ] **Step 10: Commit (client repo, 메인 체크아웃)**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/Entity/Event.Entity.cs Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/EntityCreator/CharacterCreator.cs "Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs" Assets/Scripts/UI/CharacterHud/CharacterHudViewModel.cs Assets/Scripts/Component/ManaComponent.cs Assets/Scripts/Component/ManaComponent.cs.meta
git status   # 위 파일들 + 삭제만 staged 확인 (UIRoot.prefab/PackageManagerSettings.asset 등 기존 미커밋은 제외)
git commit -m "refactor(world): migrate client MP to World.Mana (pull + EntityManaChanged), remove legacy ManaComponent

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
> 클라 메인 체크아웃엔 기존 미커밋 `Assets/Resources/UI/UIRoot.prefab`·`ProjectSettings/PackageManagerSettings.asset`·untracked가 상존. **위에 나열한 Mana 파일만** add. 그 외는 건드리지 말 것.

---

## Task 4: 런타임 검증 (수동)

> behavior-preserving (MP 정적) — 값/표시 동일 + 에러 0이 성공.

- [ ] **Step 1: 클·서 플레이 + 관찰** — 서버/클라 에디터 Play(룸 연결). 콘솔/화면에서:
  - [ ] 유저 HUD **MP 바 초기값 정상**(스폰 시 World.Mana pull).
  - [ ] 스폰/**늦게 접속**한 둘째 클라가 다른 유저 MP **정상 수신·표시**(생성데이터 → World.Mana 경로).
  - [ ] `[World] ... Mana not found` 경고가 정상 플레이 중 **안 뜸**(캐릭터는 항상 World.Mana 보유).
  - [ ] 콘솔 에러 0(combat/HUD DI 누락 NRE 없음 — ManaSystem 주입 정상). HP/EXP/Level 표시도 정상(Health=World, Level/Stat=legacy 유지, 무영향).
  - [ ] `ManaComponent` 클·서 완전 제거(Task 2/3 Step grep 0).

- [ ] **Step 2: 실패 시** — superpowers:systematic-debugging. 예상 위험: (a) MP 0 표시 → World.Mana 미해석(CharacterCreator World.Mana 생성/EntityRegistry 확인). (b) `Mana not found` 다발 → World.Mana 등록 타이밍(스폰 시 CharacterCreator가 creation-data보다 먼저 등록하는지). (c) 클라 컨테이너 resolve 실패 → `GameLifetimeScope`의 `ManaSystem` 등록 확인.

---

## Self-Review (작성자 체크 결과)

- **Spec coverage:** ① ManaSystem+테스트→Task1. ② 서버 CharacterCreator 생성+read-flip(LOPGameEngine/CreationDataCreator)+삭제→Task2. ③ 클라 CharacterCreator+EntityManaChanged+핸들러+HUD VM+GameLifetimeScope+삭제→Task3. 검증→Task4. live-MP(pull+이산 이벤트)·제네릭 옵저버 미도입·범위 Mana만 → 설계대로. ✓
- **Placeholder scan:** TBD/TODO 없음. 모든 코드 step에 실제 before/after. `<클라/서버 id>`는 구현 시 resolve(핀 절차 명시). ✓
- **Type consistency:** `ManaSystem.ApplyAuthoritativeState(Mana,int,int)`(Task1) ↔ 클라 핸들러 호출(Task3 Step5) 일치. `EntityManaChanged(int current,int max)`(Task3 Step2) ↔ 발행(Step5)·구독/핸들러(Step6) 필드명 `current`/`max` 일치. `worldEntity` 재사용 3곳(서버 LOPGameEngine/CreationDataCreator, 클라 핸들러/PushAll) 모두 기존 선언 존재 확인(재선언 금지 명시). `new Mana(maxMP){Current=currentMP}` ↔ `Mana(int max)` ctor 일치. ✓
- **컴파일 순서:** Task1(GameFramework, 클라 의존) → Task2(서버 독립, World.Mana 기존) → Task3(클라, ManaSystem 의존). 각 Task 최종 상태 컴파일. ✓
- **repo/체크아웃:** 클라 코드=메인 체크아웃(Unity 가시), 문서=워크트리 분리 명시. 서버 로컬 픽스처 보호. GameFramework file: 공유. ✓
