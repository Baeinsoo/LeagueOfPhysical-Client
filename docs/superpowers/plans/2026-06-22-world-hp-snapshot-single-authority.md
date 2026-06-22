# HP 스냅샷 단일권위 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** HP의 진실원본을 스냅샷 한 경로로 단일화한다 — 서버·클라의 중복 event-apply를 제거하고, 호출처가 사라지는 Application 코드(`WorldEventApplicator`, `HealthSystem.ApplyDamageDealt`)를 삭제한다. (cleanup backlog #1 + #3)

**Architecture:** 삭제 위주. Generation(서버 `LOPCombatSystem.TakeDamage`)이 권위 HP를 mutate하고, 스냅샷(`UserEntitySnap` → `HealthSystem.ApplyAuthoritativeState`)이 클라로의 유일 HP 권위 경로가 된다. `DamageDealtEvent`/`DamageEventToC`/`EntityDamage`는 연출(숫자·크리·HP바·HUD)용으로만 남는다. 죽음/despawn은 무변경(이미 스냅샷 파생).

**Tech Stack:** Unity / C# / VContainer DI / GameFramework(file: 패키지, 클·서 공유) / UnityMCP(컴파일·테스트 검증).

**Spec:** `docs/superpowers/specs/2026-06-22-world-hp-snapshot-single-authority-design.md`

---

## 레포 / 도구 참조

| 레포 | 루트 |
|---|---|
| Client | `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client` |
| Server | `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server` |
| GameFramework | `C:\Users\re5na\workspace\LOP\GameFramework` |

**UnityMCP 인스턴스** (이 프로젝트는 클라 — 모든 호출에 `unity_instance` 명시):
- 클라: `mcpforunity://instances`에서 name=`LeagueOfPhysical-Client` 의 `id` 확인 (현재 `LeagueOfPhysical-Client@de70658b9450cbb4`, 해시 변동 가능)
- 서버: name=`LeagueOfPhysical-Server` 의 `id` (현재 `LeagueOfPhysical-Server@f99391fa2dbaaf3c`)

**컴파일 안전 순서:** GameFramework는 클·서가 공유하는 패키지다. 먼저 **클라·서버의 사용처(주입·호출·등록)** 를 제거(Task 1·2)한 뒤 **GameFramework 클래스/메서드를 삭제**(Task 3)한다. 반대로 하면 양쪽이 깨진다.

**픽스처 보존:** `git add -A`/`.`/`commit -am` 금지. 각 Task에 명시된 파일만 `git add`. (클라: Room.unity·UIRoot.prefab·PackageManagerSettings.asset / 서버: Room.unity·ConfigureRoomComponent.cs·Art/ 는 커밋 금지.)

**.meta:** 파일 삭제 시 `.meta`도 함께 `git rm`.

---

## Task 0: 피처 브랜치 셋업 (서버·GameFramework)

**Files:** (git만)

- [ ] **Step 1: 클라는 이미 브랜치 위 — 확인만**

Run (Client 루트): `git rev-parse --abbrev-ref HEAD`
Expected: `feature/world-hp-snapshot-single-authority`

- [ ] **Step 2: 서버 피처 브랜치 생성**

Run (Server 루트):
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server checkout -b feature/world-hp-snapshot-single-authority
```
Expected: `Switched to a new branch ...`

- [ ] **Step 3: GameFramework 피처 브랜치 생성**

Run (GameFramework 루트):
```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework checkout -b feature/world-hp-snapshot-single-authority
```
Expected: `Switched to a new branch ...`

---

## Task 1: 클라 — event-apply 제거

**Files:**
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/LOPGameEngine.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs`

- [ ] **Step 1: 주입 필드 제거** (`LOPGameEngine.cs`)

Remove this line:
```csharp
        [Inject] private GameFramework.World.WorldEventApplicator worldEventApplicator;
```

- [ ] **Step 2: ProcessEvent의 Apply 호출 제거** (`LOPGameEngine.cs`)

Replace:
```csharp
            worldEventApplicator.Apply(snapshot);
            eventSink.Emit(snapshot);
            worldEventBuffer.Clear();
```
With:
```csharp
            eventSink.Emit(snapshot);
            worldEventBuffer.Clear();
```

- [ ] **Step 3: DI 등록 제거** (`GameLifetimeScope.cs`)

Remove this line:
```csharp
            builder.Register<GameFramework.World.WorldEventApplicator>(Lifetime.Singleton);
```

- [ ] **Step 4: 컴파일 확인** (클라 인스턴스)

UnityMCP `read_console`(`unity_instance`=클라 id)로 에러 0 확인. (GameFramework 클래스는 아직 존재하므로 컴파일 통과해야 함.)

- [ ] **Step 5: 커밋** (Client 루트)

```bash
git add Assets/Scripts/Game/LOPGameEngine.cs Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "refactor(world): client stops applying HP from damage event (snapshot is sole authority)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 서버 — event-apply 제거 + 주석 정리

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/LOPGameEngine.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Game/GameLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/CombatSystem/LOPCombatSystem.cs`

- [ ] **Step 1: 주입 필드 제거** (`LOPGameEngine.cs`)

Remove this line:
```csharp
        [Inject] private GameFramework.World.WorldEventApplicator worldEventApplicator;
```

- [ ] **Step 2: ProcessEvent의 Apply 호출 제거** (`LOPGameEngine.cs`)

Replace:
```csharp
            worldEventApplicator.Apply(snapshot);
            eventSink.Emit(snapshot);
            reactor.React(snapshot);
            worldEventBuffer.Clear();
```
With:
```csharp
            eventSink.Emit(snapshot);
            reactor.React(snapshot);
            worldEventBuffer.Clear();
```

- [ ] **Step 3: DI 등록 제거** (`GameLifetimeScope.cs`)

Remove this line:
```csharp
            builder.Register<GameFramework.World.WorldEventApplicator>(Lifetime.Singleton);
```

- [ ] **Step 4: stale 주석 정리** (`LOPCombatSystem.cs`)

Replace:
```csharp
            // --- World Core — Slice 3: DeathEvent → WorldEventReactor → LOPGame.HandleDeath ---
            // World.Health가 HP 진실원본. Generation(여기)이 mutate, WorldEventApplicator(ProcessEvent)가
            // remaining으로 재적용(멱등). DeathEvent를 WorldEventBuffer에 append →
            // WorldEventReactor.React가 EventBus로 fan-out → LOPGame.HandleDeath(디스폰+경험치 구슬).
```
With:
```csharp
            // --- World Core: DeathEvent → WorldEventReactor → LOPGame.HandleDeath ---
            // World.Health가 HP 진실원본 — Generation(여기)이 직접 mutate하고, 스냅샷(UserEntitySnap)이
            // 클라로의 유일 권위 경로다. DamageDealtEvent는 연출(숫자/크리)용으로만 송출.
            // DeathEvent를 WorldEventBuffer에 append → WorldEventReactor.React가 EventBus fan-out →
            // LOPGame.HandleDeath(디스폰+경험치 구슬).
```

- [ ] **Step 5: 컴파일 확인** (서버 인스턴스)

UnityMCP `read_console`(`unity_instance`=서버 id)로 에러 0 확인.

- [ ] **Step 6: 커밋** (Server 루트)

```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server add Assets/Scripts/Game/LOPGameEngine.cs Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/CombatSystem/LOPCombatSystem.cs
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server commit -m "refactor(world): server drops redundant HP re-apply (generation already mutated)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: GameFramework — 고아 Application 코드 삭제

**Files:**
- Delete: `GameFramework/Runtime/Scripts/World/Systems/WorldEventApplicator.cs` (+ `.meta`)
- Delete: `GameFramework/Tests/World/WorldEventApplicatorTests.cs` (+ `.meta`)
- Modify: `GameFramework/Runtime/Scripts/World/Systems/HealthSystem.cs`
- Modify: `GameFramework/Tests/World/HealthSystemTests.cs`

- [ ] **Step 1: WorldEventApplicator 클래스 삭제 (git rm)**

Run:
```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework rm Runtime/Scripts/World/Systems/WorldEventApplicator.cs Runtime/Scripts/World/Systems/WorldEventApplicator.cs.meta
```

- [ ] **Step 2: WorldEventApplicatorTests 삭제 (git rm)**

Run:
```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework rm Tests/World/WorldEventApplicatorTests.cs Tests/World/WorldEventApplicatorTests.cs.meta
```

- [ ] **Step 3: HealthSystem.ApplyDamageDealt 메서드 삭제** (`HealthSystem.cs`)

Remove this method (그 위 `ApplyAuthoritativeState`는 유지):
```csharp
        /// <summary>
        /// 데미지 결과 이벤트를 그대로 Health에 반영한다. 결정/계산/가드 없음 (Application 메서드).
        /// </summary>
        public void ApplyDamageDealt(Health health, DamageDealtEvent e)
        {
            health.Current = e.remaining;
        }

```

- [ ] **Step 4: HealthSystemTests의 ApplyDamageDealt 테스트 3개 삭제** (`HealthSystemTests.cs`)

Remove these three tests:
```csharp
        [Test]
        public void ApplyDamageDealt_writes_remaining_to_Current()
        {
            var health = new Health(100);
            var e = new DamageDealtEvent("1", "2", amount: 30, isCritical: false, isDodged: false, remaining: 70, isDead: false);

            _system.ApplyDamageDealt(health, e);

            Assert.AreEqual(70, health.Current);
        }

        [Test]
        public void ApplyDamageDealt_writes_zero_when_event_says_dead()
        {
            var health = new Health(100) { Current = 50 };
            var e = new DamageDealtEvent("1", "2", amount: 60, isCritical: true, isDodged: false, remaining: 0, isDead: true);

            _system.ApplyDamageDealt(health, e);

            Assert.AreEqual(0, health.Current);
        }

        [Test]
        public void ApplyDamageDealt_does_not_branch_on_isDodged_or_isCritical()
        {
            // Application 메서드는 결정 없음 — remaining을 그대로 씀. isDodged/isCritical은 무시.
            var health = new Health(100);
            var e = new DamageDealtEvent("1", "2", amount: 999, isCritical: true, isDodged: true, remaining: 42, isDead: false);

            _system.ApplyDamageDealt(health, e);

            Assert.AreEqual(42, health.Current);
        }

```

> 주의: 삭제 후 `HealthSystemTests.cs`의 나머지 테스트(`TakeDamage_*`, `Heal_*`, `SetMax_*`, `ApplyAuthoritativeState_*`)와 `[Test]` 어트리뷰트 짝이 깨지지 않게 블록 경계를 정확히 제거할 것.

- [ ] **Step 5: 패키지 파일 삭제 반영 — 양쪽 에디터 재스캔**

GameFramework는 file: 패키지라 .cs+.meta 삭제 시 클·서에 stale CS2001이 뜰 수 있다. UnityMCP `refresh_unity`(`scope=all`, `force=true`)를 **클라·서버 각 인스턴스**에 호출 후 `read_console`로 에러 0 확인. (메모리 `deleting-package-files-cs2001` 참고.)

- [ ] **Step 6: GameFramework EditMode 테스트 (World) 통과 확인**

UnityMCP `run_tests`(EditMode, 가능하면 World 필터, `unity_instance`=클라)로 `HealthSystemTests` 등 잔여 World 테스트 통과 확인. (삭제된 3 테스트·WorldEventApplicatorTests는 더 이상 없음.)

- [ ] **Step 7: 커밋** (GameFramework 루트)

```bash
git -C C:/Users/re5na/workspace/LOP/GameFramework add Runtime/Scripts/World/Systems/HealthSystem.cs Tests/World/HealthSystemTests.cs
git -C C:/Users/re5na/workspace/LOP/GameFramework commit -m "refactor(world): delete orphaned Application code (event-apply removed)

WorldEventApplicator + HealthSystem.ApplyDamageDealt no longer have any
production caller after HP authority collapsed onto the snapshot path.
ApplyAuthoritativeState (snapshot path) retained. Returns at Stage 4.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

> `git rm`(Step 1·2)으로 이미 stage된 삭제 + Step 3·4 수정본을 함께 커밋한다.

---

## Task 4: 문서 — cleanup backlog 상태 업데이트

**Files:**
- Modify: `LeagueOfPhysical-Client/docs/world-core-connection-architecture.md`

- [ ] **Step 1: "알려진 괴리" 섹션 #1·#3 해소 표시**

Replace:
```markdown
모델은 위와 같고, 현재 코드는 slice-3 단순화라 3곳이 어긋난다. 각각 별도 슬라이스로 정리(`IEventSink` egress 포트 작업과 독립):

1. **이중 apply** — Generation이 `TakeDamage`로 직접 mutate + Application이 `remaining`으로 재적용. 표준은 "단일 권위 mutation"(Overwatch는 큐에 기록→한 번 드레인). → Generation은 *결정/기록*만, 단일 드레인이 *유일* 적용.
2. **despawn이 fan-out에** — `LOPGame.HandleDeath`(디스폰+경험치)가 egress(fan-out) 경로에서 상태를 바꿈 = "egress가 새 사실 생성" 안티패턴. → Cascade Reactor(Generation)로 이전해 `DespawnEvent` emit, egress는 *통지만*.
3. **이중 HP 경로** — 클라가 `DamageDealtEvent.remaining`(event)으로도, `UserEntitySnap.CurrentHP`(state)로도 HP를 받음. → HP 권위 = **state 단일화**, event = 연출 전용(클라가 state로 안 씀).
```
With:
```markdown
모델은 위와 같고, 현재 코드는 slice-3 단순화라 어긋난 곳이 있었다. #1·#3은 해소됨(2026-06-22, HP 스냅샷 단일권위 슬라이스), #2는 남음:

1. ~~**이중 apply**~~ ✅ **해소(2026-06-22)** — 서버 Generation이 `TakeDamage`로 mutate한 뒤 Application(`WorldEventApplicator`)이 `remaining`으로 재적용하던 중복을 제거. 서버·클라 양쪽 event-apply 삭제, 고아가 된 `WorldEventApplicator`/`HealthSystem.ApplyDamageDealt`도 삭제. **Application 코드는 Stage④(Death 컴포넌트·클라 예측-apply)에서 새 모양으로 복귀.**
2. **despawn이 fan-out에** — `LOPGame.HandleDeath`(디스폰+경험치)가 egress(fan-out) 경로에서 상태를 바꿈 = "egress가 새 사실 생성" 안티패턴. → Cascade Reactor(Generation)로 이전해 `DespawnEvent` emit, egress는 *통지만*. (별도 슬라이스, 미해소)
3. ~~**이중 HP 경로**~~ ✅ **해소(2026-06-22)** — 클라가 `DamageDealtEvent.remaining`(event)과 `UserEntitySnap.CurrentHP`(state) 둘로 HP를 받던 것을, **HP 권위 = 스냅샷 state 단일화**로 정리(클라 event-apply 삭제). 이벤트는 연출 전용(숫자·크리·HP바·HUD). 단 HP **UI**가 아직 그 연출 이벤트에서 값을 읽는 잔여(scope B: UI를 스냅샷-fed 모델로 이전 + 와이어 이벤트에서 HP 흔적 제거)는 다음 슬라이스.
```

- [ ] **Step 2: 커밋** (Client 루트)

```bash
git add docs/world-core-connection-architecture.md
git commit -m "docs(world): mark cleanup backlog #1/#3 resolved (HP snapshot single-authority)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 5: 종합 검증 (플레이 — 사용자)

**Files:** (없음 — 검증만)

- [ ] **Step 1: 양쪽 컴파일 최종 확인**

클라·서버 인스턴스 각각 `read_console`(`unity_instance` 명시) 에러 0.

- [ ] **Step 2: 플레이 검증 (사용자)**

서버·클라 에디터로 플레이, 다음이 **이전과 동일**해야 성공(구조만 변경):
1. 데미지 시 숫자 팝업이 뜬다
2. HP바(머리 위)·HUD가 줄어든다
3. 죽으면 엔티티가 사라지고 사망 연출이 난다
4. 내 캐릭터 HP가 정상 표시된다

- [ ] **Step 3: 사용자 OK 시 — push + --no-ff merge (3 레포)**

사용자 확인 후, 각 레포에서 피처 브랜치를 main에 `--no-ff` 머지 + push. (커밋·머지·push는 **사용자 요청 시에만**.)

---

## Self-Review (작성자 체크)

- **Spec 커버리지:** 서버 Apply 제거(#1)=Task2 / 클라 Apply 제거(#3)=Task1 / 고아 코드 삭제=Task3 / 죽음 무변경=명시(코드 변경 없음) / 문서=Task4 / 검증=Task5. ✅
- **플레이스홀더:** 없음. 모든 편집에 정확한 old/new 코드 블록. ✅
- **타입/시그니처 일관:** `ApplyAuthoritativeState`(유지) vs `ApplyDamageDealt`(삭제) 구분 명확. DI 등록·주입·호출이 같은 Task 안에서 짝지어 제거되어 컴파일 안전. ✅
- **순서:** 클·서 사용처(Task1·2) → GameFramework 삭제(Task3). 역순 방지. ✅
