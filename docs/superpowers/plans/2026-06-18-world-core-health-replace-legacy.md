# Remove Legacy HealthComponent — Unify HP on World.Health — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 레거시 `HealthComponent`(MonoBehaviour)를 제거하고 HP의 단일 진실원본을 순수 C# 코어 `World.Health`로 일원화한다. 화면 동작은 이전과 동일(회귀 0).

**Architecture:** 클라의 남은 `HealthComponent` 사용처 3곳(쓰기 2 + 읽기 1)을 `World.Health`로 repoint한 뒤 컴포넌트를 삭제한다. 권위 스냅샷 쓰기는 새 `HealthSystem.ApplyAuthoritativeState`(Application 메서드, 코어) 경유. inside-out 이행(Model A)에서 "reader를 코어로 옮기고 레거시 제거"의 한 칸.

**Tech Stack:** Unity (Assembly-CSharp 단일), VContainer DI, R3, GameFramework `World`(순수 C# 코어, file: 패키지), NUnit EditMode (GameFramework `World.Tests`).

---

## Repos & Branches

이 작업은 **두 git 저장소**에 걸친다:

| 저장소 | 경로 | 브랜치 | 다루는 Task |
|---|---|---|---|
| GameFramework | `C:/Users/re5na/workspace/LOP/GameFramework` | `feature/world-core-health-replace-legacy` (이 repo에서 생성) | Task 1 |
| LOP-Client | `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client` | `feature/world-core-health-replace-legacy` (이미 생성됨) | Task 2~5 |

각 Task의 commit은 **해당 저장소의 피처 브랜치**에 한다. main 직접 커밋 금지.

## UnityMCP 인스턴스 핀 (모든 컴파일/테스트 검증에 필수)

CLAUDE.md 규칙 — UnityMCP 호출마다 `unity_instance`를 **클라**로 명시한다. 라우팅이 서버로 샐 수 있음.

1. `mcpforunity://instances` 리소스를 읽어 `name == "LeagueOfPhysical-Client"`인 인스턴스의 전체 `id`(`Name@hash`)를 얻는다. (작성 시점 예시: `LeagueOfPhysical-Client@de70658b9450cbb4` — hash는 바뀔 수 있으니 매번 resolve.)
2. 모든 `refresh_unity` / `read_console` / `run_tests` 호출에 `unity_instance="<그 id>"`를 전달한다.

> GameFramework는 file: 패키지라 **클라 에디터가 함께 컴파일**한다. 따라서 코어(Task 1) 변경도 클라 인스턴스에서 컴파일·테스트 확인이 된다.

## File Structure

| 파일 | 변경 | 책임 |
|---|---|---|
| `GameFramework/Runtime/Scripts/World/Systems/HealthSystem.cs` | Modify | `ApplyAuthoritativeState(Health, max, current)` 추가 |
| `GameFramework/Tests/World/HealthSystemTests.cs` | Modify | 새 메서드 테스트 3개 추가 |
| `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs` | Modify | 스냅샷 HP 쓰기를 World.Health로 (EntityRegistry + HealthSystem 주입) |
| `LeagueOfPhysical-Client/Assets/Scripts/UI/CharacterHud/CharacterHudViewModel.cs` | Modify | HP 초기 read를 World.Health로 (EntityRegistry 주입, HealthComponent 제거) |
| `LeagueOfPhysical-Client/Assets/Scripts/EntityCreator/CharacterCreator.cs` | Modify | `HealthComponent` 생성 3줄 제거 |
| `LeagueOfPhysical-Client/Assets/Scripts/Component/HealthComponent.cs` (+ `.meta`) | Delete | 레거시 컴포넌트 제거 |

**Task 순서 근거 (각 중간 상태가 컴파일 가능):** 코어 메서드 먼저(Task 1) → 레거시 참조를 하나씩 제거(Task 2,3,4) → 참조가 0이 된 뒤 파일 삭제(Task 5). 삭제를 마지막에 두지 않으면 컴파일이 깨진다.

---

## Task 1: Core — `HealthSystem.ApplyAuthoritativeState` (TDD)

**Repo:** GameFramework
**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/World/Systems/HealthSystem.cs`
- Test: `C:/Users/re5na/workspace/LOP/GameFramework/Tests/World/HealthSystemTests.cs`

- [ ] **Step 1: 피처 브랜치 생성 (GameFramework repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git checkout -b feature/world-core-health-replace-legacy
git branch --show-current   # → feature/world-core-health-replace-legacy
```

- [ ] **Step 2: 실패 테스트 작성**

`HealthSystemTests.cs` 의 마지막 테스트(`ApplyDamageDealt_does_not_branch_on_isDodged_or_isCritical`, 102줄 `}`) 뒤, 클래스 닫는 `}` 앞에 추가:

```csharp
        [Test]
        public void ApplyAuthoritativeState_overwrites_Max_and_Current()
        {
            var health = new Health(100) { Current = 40 };

            _system.ApplyAuthoritativeState(health, max: 200, current: 150);

            Assert.AreEqual(200, health.Max);
            Assert.AreEqual(150, health.Current);
        }

        [Test]
        public void ApplyAuthoritativeState_clamps_Current_above_Max()
        {
            var health = new Health(100);

            _system.ApplyAuthoritativeState(health, max: 80, current: 999);

            Assert.AreEqual(80, health.Max);
            Assert.AreEqual(80, health.Current);
        }

        [Test]
        public void ApplyAuthoritativeState_clamps_negative_Current_to_zero()
        {
            var health = new Health(100);

            _system.ApplyAuthoritativeState(health, max: 100, current: -5);

            Assert.AreEqual(0, health.Current);
        }
```

- [ ] **Step 3: 컴파일 실패 확인 (TDD red)**

UnityMCP (클라 인스턴스 핀):
- `refresh_unity(unity_instance="<client id>")`
- `read_console(unity_instance="<client id>", types=["error"])`

Expected: 컴파일 에러 — `'HealthSystem' does not contain a definition for 'ApplyAuthoritativeState'`.

- [ ] **Step 4: 최소 구현**

`HealthSystem.cs` — 파일 상단에 `using System;`가 이미 있음(`Math.Max/Min` 사용 중). `ApplyDamageDealt` 메서드(33줄, `health.Current = e.remaining;`) 뒤, 클래스 닫는 `}` 앞에 추가:

```csharp

        /// <summary>
        /// 권위 스냅샷 등으로 Max/Current를 통째로 덮어쓴다. 결정/계산/가드 없음 (Application 메서드).
        /// Current는 [0, Max]로 클램프해 데이터 무결성만 보장한다.
        /// </summary>
        public void ApplyAuthoritativeState(Health health, int max, int current)
        {
            health.Max = max;
            health.Current = Math.Clamp(current, 0, max);
        }
```

- [ ] **Step 5: 테스트 통과 확인**

UnityMCP (클라 인스턴스 핀):
- `refresh_unity(unity_instance="<client id>")`
- `read_console(unity_instance="<client id>", types=["error"])` → 에러 0
- `run_tests(unity_instance="<client id>", mode="EditMode", testFilter="GameFramework.World.Tests.HealthSystemTests")`

Expected: `HealthSystemTests`의 모든 테스트 PASS (기존 9개 + 신규 3개 = 12개).

- [ ] **Step 6: Commit (GameFramework repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git add Runtime/Scripts/World/Systems/HealthSystem.cs Tests/World/HealthSystemTests.cs
git commit -m "feat(world): HealthSystem.ApplyAuthoritativeState for authoritative HP overwrite

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Client — 스냅샷 HP 쓰기를 World.Health로

**Repo:** LOP-Client
**Files:**
- Modify: `Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs`

> 동작 보존: 유저 본인 권위 스냅샷이 HP를 `World.Health`에 쓴다. Mana/Level/User 줄은 그대로 유지.

- [ ] **Step 1: 주입 필드 추가**

`Game.Entity.MessageHandler.cs` 의 `GameEntityMessageHandler` 클래스 주입 필드 블록(10-13줄):

```csharp
        [Inject] private IPlayerContext playerContext;
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IGameEngine gameEngine;
        [Inject] private IActionManager actionManager;
```

뒤에 두 줄 추가 (World 타입은 풀 네임스페이스 한정 — 프로젝트 컨벤션):

```csharp
        [Inject] private IPlayerContext playerContext;
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IGameEngine gameEngine;
        [Inject] private IActionManager actionManager;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private GameFramework.World.HealthSystem healthSystem;
```

- [ ] **Step 2: `OnUserEntitySnapToC`의 HP 두 줄 교체**

현재 (170-171줄):

```csharp
            playerContext.entity.GetComponent<HealthComponent>().currentHP = userEntitySnapToC.CurrentHP;
            playerContext.entity.GetComponent<HealthComponent>().maxHP = userEntitySnapToC.MaxHP;
```

를 다음으로 교체:

```csharp
            GameFramework.World.Entity worldEntity = entityRegistry.Get(playerContext.entity.entityId);
            GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
            if (health != null)
            {
                healthSystem.ApplyAuthoritativeState(health, userEntitySnapToC.MaxHP, userEntitySnapToC.CurrentHP);
            }
```

> 그 아래 Mana/Level/User 줄(172-176)은 **변경하지 않는다.**

- [ ] **Step 3: 컴파일 확인**

UnityMCP (클라 인스턴스 핀): `refresh_unity` → `read_console(types=["error"])`
Expected: 에러 0. (이 시점엔 `HealthComponent.cs`가 아직 존재하고 다른 파일이 참조 중 — 정상 컴파일.)

- [ ] **Step 4: Commit (LOP-Client repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/Game/MessageHandler/Game.Entity.MessageHandler.cs
git commit -m "refactor(world): write authoritative HP snapshot to World.Health via HealthSystem

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Client — HUD 초기 HP read를 World.Health로

**Repo:** LOP-Client
**Files:**
- Modify: `Assets/Scripts/UI/CharacterHud/CharacterHudViewModel.cs`

> 동작 보존: 생성 시 초기 HP를 `World.Health`에서 읽고, 라이브 HP는 기존 `EntityDamage` 구독 그대로.

- [ ] **Step 1: 클래스 요약 주석 갱신 + `_health` 필드 제거 + EntityRegistry 필드 추가**

클래스 XML 요약(9-14줄)에서 레거시 언급 줄을 갱신. 현재:

```csharp
    /// MP/EXP/Level은 컴포넌트 PropertyChange(반응형, M2a 패턴)로, HP는 EntityDamage 이벤트로 갱신한다
    /// (레거시 HealthComponent.currentHP는 새 데미지 흐름이 갱신하지 않으므로 — World Core가 HP 진실).
```

를:

```csharp
    /// MP/EXP/Level은 컴포넌트 PropertyChange(반응형, M2a 패턴)로, HP는 EntityDamage 이벤트로 갱신한다.
    /// HP 초기값은 World.Entity의 World.Health(순수 C# 코어 — HP 진실원본)에서 읽는다(Model A — pull).
```

필드 선언(17-20줄) 현재:

```csharp
        private readonly LOPEntity _entity;
        private readonly HealthComponent _health;
        private readonly ManaComponent _mana;
        private readonly LevelComponent _level;
```

를 (`_health` 제거, `_entityRegistry` 추가):

```csharp
        private readonly LOPEntity _entity;
        private readonly GameFramework.World.EntityRegistry _entityRegistry;
        private readonly ManaComponent _mana;
        private readonly LevelComponent _level;
```

- [ ] **Step 2: 생성자에 EntityRegistry 주입 + `_health` 초기화 제거**

현재 생성자(38-55줄):

```csharp
        public CharacterHudViewModel(IPlayerContext playerContext)
        {
            _entity = playerContext.entity;
            if (_entity == null)
            {
                Debug.LogWarning("[CharacterHudViewModel] playerContext.entity가 null입니다. 유저 엔티티 생성 후 열어야 합니다.");
                return;
            }

            _health = _entity.GetEntityComponent<HealthComponent>();
            _mana = _entity.GetEntityComponent<ManaComponent>();
            _level = _entity.GetEntityComponent<LevelComponent>();

            PushAll();

            EventBus.Default.Subscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnPropertyChange);
            EventBus.Default.Subscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityDamage);
        }
```

를:

```csharp
        public CharacterHudViewModel(IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry)
        {
            _entityRegistry = entityRegistry;
            _entity = playerContext.entity;
            if (_entity == null)
            {
                Debug.LogWarning("[CharacterHudViewModel] playerContext.entity가 null입니다. 유저 엔티티 생성 후 열어야 합니다.");
                return;
            }

            _mana = _entity.GetEntityComponent<ManaComponent>();
            _level = _entity.GetEntityComponent<LevelComponent>();

            PushAll();

            EventBus.Default.Subscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnPropertyChange);
            EventBus.Default.Subscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(_entity.entityId), OnEntityDamage);
        }
```

- [ ] **Step 3: `PushAll`의 Health 분기를 World.Health read로 교체**

현재 `PushAll`(74-92줄):

```csharp
        private void PushAll()
        {
            if (_health != null)
            {
                _hp.Value = _health.currentHP;
                _maxHp.Value = _health.maxHP;
            }
            if (_mana != null)
            {
                _mp.Value = _mana.currentMP;
                _maxMp.Value = _mana.maxMP;
            }
            if (_level != null)
            {
                _exp.Value = _level.currentExp;
                _expToNext.Value = _level.expToNextLevel;
                _levelValue.Value = _level.level;
            }
        }
```

를 (Health 분기만 교체, Mana/Level 분기는 그대로):

```csharp
        private void PushAll()
        {
            GameFramework.World.Entity worldEntity = _entityRegistry.Get(_entity.entityId);
            GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
            if (health != null)
            {
                _hp.Value = health.Current;
                _maxHp.Value = health.Max;
            }
            if (_mana != null)
            {
                _mp.Value = _mana.currentMP;
                _maxMp.Value = _mana.maxMP;
            }
            if (_level != null)
            {
                _exp.Value = _level.currentExp;
                _expToNext.Value = _level.expToNextLevel;
                _levelValue.Value = _level.level;
            }
        }
```

> `PushAll`은 `_entity` null 체크를 통과한 뒤에만 호출되므로 `_entity`/`_entityRegistry`는 비-null이다.

- [ ] **Step 4: 컴파일 확인**

UnityMCP (클라 인스턴스 핀): `refresh_unity` → `read_console(types=["error"])`
Expected: 에러 0. (`HealthComponent.cs` 아직 존재 — CharacterCreator가 참조 중.)

- [ ] **Step 5: Commit (LOP-Client repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/UI/CharacterHud/CharacterHudViewModel.cs
git commit -m "refactor(world): read initial HUD HP from World.Health instead of HealthComponent

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Client — CharacterCreator에서 HealthComponent 생성 제거

**Repo:** LOP-Client
**Files:**
- Modify: `Assets/Scripts/EntityCreator/CharacterCreator.cs`

> `World.Health` 등록(94-104줄 블록)은 그대로 진실원본 역할. 레거시 생성만 제거.

- [ ] **Step 1: HealthComponent 생성 3줄 제거**

현재 (47-49줄):

```csharp
            HealthComponent healthComponent = entity.AddEntityComponent<HealthComponent>();
            objectResolver.Inject(healthComponent);
            healthComponent.Initialize(creationData.maxHP, creationData.currentHP);

            ManaComponent manaComponent = entity.AddEntityComponent<ManaComponent>();
```

에서 위 3줄을 삭제 → 다음과 같이 됨 (ManaComponent 블록이 PhysicsComponent 블록 바로 뒤로):

```csharp
            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            objectResolver.Inject(physicsComponent);
            physicsComponent.Initialize(false, false);

            ManaComponent manaComponent = entity.AddEntityComponent<ManaComponent>();
```

- [ ] **Step 2: 컴파일 확인**

UnityMCP (클라 인스턴스 핀): `refresh_unity` → `read_console(types=["error"])`
Expected: 에러 0. (이제 `HealthComponent` 참조처가 0이지만 파일은 아직 존재 — 정상.)

- [ ] **Step 3: Commit (LOP-Client repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/EntityCreator/CharacterCreator.cs
git commit -m "refactor(world): stop creating legacy HealthComponent in CharacterCreator

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: Client — HealthComponent.cs 삭제

**Repo:** LOP-Client
**Files:**
- Delete: `Assets/Scripts/Component/HealthComponent.cs` (+ `.meta`)

- [ ] **Step 1: 참조 0 재확인**

Grep `HealthComponent` (코드 전체). Expected: `HealthComponent.cs` 자기 자신 외 매치 0 (Task 2~4로 모두 제거됨).

- [ ] **Step 2: 파일 + meta 삭제**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git rm Assets/Scripts/Component/HealthComponent.cs Assets/Scripts/Component/HealthComponent.cs.meta
```

- [ ] **Step 3: 전체 재스캔 + 컴파일 확인**

UnityMCP (클라 인스턴스 핀) — 파일 삭제는 stale CS2001을 남길 수 있어 **force 전체 재스캔** 필요:
- `refresh_unity(unity_instance="<client id>", scope="all", force=true)`
- `read_console(unity_instance="<client id>", types=["error"])`

Expected: 에러 0 (CS2001 포함 0).

- [ ] **Step 4: Commit (LOP-Client repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git commit -m "refactor(world): delete legacy HealthComponent — World.Health is sole HP source

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: 런타임 회귀 검증 (수동)

> 자동화 테스트는 코어(Task 1)로 커버됨. 클라는 단일 Assembly-CSharp라 EditMode 불가 — 플레이로 검증.

- [ ] **Step 1: 플레이 진입** — 클라 에디터 Play, 로컬 테스트 룸 연결(필요 시 Local test auth fixture: 현재 게스트 uuid를 서버 playerList에 추가 후 서버 재시작).

- [ ] **Step 2: 다음을 관찰 (모두 이전과 동일해야 — 회귀 0이 성공):**
  - [ ] 유저 HUD의 HP 바/숫자가 **초기값** 정상 표시 (World.Health에서 read).
  - [ ] 피격 시 HUD HP 숫자·바 감소 (EntityDamage 라이브).
  - [ ] 캐릭터 머리 위 **네임플레이트 HP바** 감소 (Slice A 경로, 회귀 없음 확인).
  - [ ] 데미지 숫자 플로터 출현.
  - [ ] 사망까지 진행 → 콘솔 `[World] Death entity {id} (killer={id})` 로그 (slice 3 회귀).
  - [ ] 스폰 시 `[World] Registered entity {id} Health=.../...` 로그 정상.
  - [ ] 콘솔 에러 0건 (DI 누락 NRE 없음 — HudViewModel/MessageHandler 새 주입 정상).

- [ ] **Step 3: 검증 실패 시** — superpowers:systematic-debugging로 진단. (예상 위험: `EntityRegistry.Get`이 user entityId에 null 반환 → 타이밍 확인. spec 조사상 user의 World.Entity는 `CharacterCreator`에서 등록되고 HUD/스냅샷은 그 후이므로 안전.)

---

## Self-Review (작성자 체크 결과)

- **Spec coverage:** spec의 변경 3곳(CharacterCreator/MessageHandler/HudViewModel) + 코어 메서드 + 삭제 + 테스트 + 검증 — 모두 Task 1~6에 매핑됨. ✓
- **Placeholder scan:** TBD/TODO/"적절히 처리" 없음. 모든 코드 step에 실제 코드 블록 존재. ✓
- **Type consistency:** `ApplyAuthoritativeState(Health, int max, int current)` 시그니처가 Task 1 정의 ↔ Task 2 호출 일치. `entityRegistry.Get(...)` → `worldEntity?.Get<GameFramework.World.Health>()` 패턴이 Task 2/3 동일. ✓
- **컴파일 순서:** 각 Task 종료 상태가 컴파일 가능(레거시 삭제는 참조 0 이후 Task 5). ✓
- **두 repo 분리:** Task 1 = GameFramework, Task 2~5 = LOP-Client. 각 commit 경로 명시. ✓
