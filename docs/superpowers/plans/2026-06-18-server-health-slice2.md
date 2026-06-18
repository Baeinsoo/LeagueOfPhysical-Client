# Server Health Writer Flip + Death Relocation + Legacy Removal (Slice 2) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버 데미지 writer를 레거시 `HealthComponent.TakeDamage` → 코어 `World.Health`+`HealthSystem.TakeDamage`로 뒤집고, `EntityDeath` 발행을 `LOPCombatSystem`으로 재배치한 뒤, 레거시 `HealthComponent`를 삭제한다. 동작 보존(HP·디스폰·경험치구슬 결과 동일).

**Architecture:** `LOPCombatSystem`이 `entityRegistry.Get(target).Get<Health>()`를 해석해 `healthSystem.TakeDamage(health, dealtAmount)`로 직접 깎고, `remaining=health.Current`/`isDead=health.IsDead`를 산출. 사망 시 기존 `World.DeathEvent` append은 유지하고, 디스폰을 구동하는 레거시 `EntityDeath(victimId, attackerId, target.position)`를 같은 지점에서 `EventBus`로 발행(발행 위치만 `HealthComponent` → combat으로 이동). 디스폰 경로(`LOPGame.HandleEntityDeath`)·`EntityDeath` 구조체·`World.DeathEvent`·GameFramework 무변경. 디스폰을 `World.DeathEvent`로 통일하는 것(옵션 B)은 후속 Slice 3.

**Tech Stack:** Unity (서버, 단일 Assembly-CSharp), VContainer DI(생성자 주입), GameFramework `World`(공유, 불변).

---

## Repo & Branch

전 작업이 **서버 repo 하나**(GameFramework·클라 불변).

| 항목 | 값 |
|---|---|
| 코드 repo | `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server` |
| 브랜치 | `feature/server-health-slice2` (Task 1에서 생성) |
| spec/plan | 클라 repo (이미 커밋, 변경 없음) |

main 직접 커밋 금지. 모든 commit은 서버 repo 피처 브랜치에.

> **⚠️ 서버 로컬 픽스처 (건드리지 말 것):** 서버 working-tree에 미커밋 변경 `Assets/Scripts/Game/LOPGame.cs`(SpawnEnemies 수)·`Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`(playerList UUID)가 상존. **이 plan은 이 두 파일을 안 건드린다**(옵션 A는 `LOPGame.cs` 무변경). 절대 스테이징/커밋/`git restore` 하지 말 것.

## UnityMCP 인스턴스 핀 — **서버**

사용자 지시 서버 작업. UnityMCP 호출마다 `unity_instance`를 **서버**로:
1. `mcpforunity://instances`에서 `name == "LeagueOfPhysical-Server"`의 전체 `id` resolve. (작성 시점 참고: `LeagueOfPhysical-Server@f99391fa2dbaaf3c` — hash 변동 가능, 매번 resolve.)
2. `refresh_unity`/`read_console`에 `unity_instance="<서버 id>"`. **클라로 라우팅 금지. 실제 실행하고 보고.**

## 배경 사실 (조사)

- 1a/1b 후 서버 `HealthComponent` 참조는 **정확히 4곳**(정의 1 + 사용 3): `HealthComponent.cs`(정의), `CharacterCreator.cs:41-43`(생성), `LOPCombatSystem.cs:25,30,52,59,67`(존재 가드·이미사망 가드·`TakeDamage`·`remaining`/`isDead`). 1b가 뒤집은 두 reader는 더 이상 `HealthComponent` 미참조.
- `HealthComponent.TakeDamage`(`HealthComponent.cs:25`)는 `currentHP <= 0`일 때 `EventBus.Default.Publish(EventTopic.Entity, new Event.Entity.EntityDeath(entity.entityId, attackerId, entity.position))` — **`EntityDeath`의 유일 발행처**. 구독자 = `LOPGame.HandleEntityDeath`(유일) → `DespawnEntity(victimId)` + `SpawnExpMarble(position)`.
- `LOPCombatSystem`은 `ICombatSystem` Singleton(`GameLifetimeScope.cs:38`), **생성자 주입**(현재 `WorldEventBuffer`만). `EntityRegistry`/`HealthSystem`도 같은 스코프 Singleton(`GameLifetimeScope.cs:20,22`) → 생성자 인자 추가 시 자동 resolve, **DI 등록 추가 불필요**.
- `EventBus`/`EventTopic`/`Event.Entity.EntityDeath`는 `HealthComponent.cs`(usings: `using UnityEngine;`만, namespace `LOP`)에서 정상 컴파일됨 → 네임스페이스 `LOP`에서 추가 using 없이 resolve. `LOPCombatSystem`(usings: `using GameFramework;`+`using UnityEngine;`, namespace `LOP`)은 상위집합이라 **새 using 불필요**.
- `HealthSystem.TakeDamage(Health, int)`(GameFramework): `Current = Math.Max(0, Current - amount)`. 순수(attackerId·death·max클램프 없음). `Health.IsDead => Current <= 0`, `Health.Current`/`Max`는 int.
- `entityRegistry.Get(id)` → `GameFramework.World.Entity`(없으면 null), `.Get<Health>()` → Health(없으면 null). `LOPEntity.position`은 `UnityEngine.Vector3`(combat에 `using UnityEngine;` 보유).
- `WorldEventApplicator.ApplyDamageDealt`(`LOPGameEngine.ProcessEvent`)가 매 틱 `Current = e.remaining` 재적용 → combat이 이미 `TakeDamage`로 깎은 값과 동일(멱등).
- `creationData.maxHP`/`currentHP`는 `CharacterCreator`의 World.Health 생성(`new Health(creationData.maxHP){ Current = creationData.currentHP }`, 84-85줄)에서 계속 사용 → 레거시 생성 3줄 제거해도 orphan 없음.
- `HealthComponent`는 코드로 `AddEntityComponent`되는 컴포넌트 — 씬/prefab의 SerializedField가 GUID로 참조 안 함. `Assets/` 내 단일 Assembly-CSharp(패키지 아님) → 삭제 시 일반 recompile, cross-repo CS2001 없음.

## File Structure (서버)

| 파일 | 변경 | 책임 |
|---|---|---|
| `Assets/Scripts/CombatSystem/LOPCombatSystem.cs` | Modify | writer를 World.Health로 + 사망 시 EntityDeath 발행 |
| `Assets/Scripts/EntityCreator/CharacterCreator.cs` | Modify | 레거시 HealthComponent 생성 제거 |
| `Assets/Scripts/Component/HealthComponent.cs` (+`.meta`) | Delete | 참조 0이 된 레거시 store 제거 |

> **테스트 전략:** 신규 자동화 테스트 없음(단일 Assembly-CSharp). 검증 = 서버 컴파일 + 수동 플레이(HP·디스폰·경험치구슬 동일=회귀 0). `HealthSystem.TakeDamage`는 GameFramework EditMode에 기존 커버. TDD 부적용.

**컴파일 순서:** Task 1(combat — combat의 HealthComponent 참조 제거) → Task 2(creator 제거 + HealthComponent 삭제) → Task 3(검증). 각 Task 끝은 컴파일 통과 상태.

---

## Task 1: Writer flip + 사망 발행 재배치 (LOPCombatSystem)

**Files:**
- Modify `Assets/Scripts/CombatSystem/LOPCombatSystem.cs`

- [ ] **Step 1: 피처 브랜치 생성 (server repo)**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git checkout -b feature/server-health-slice2
git branch --show-current   # → feature/server-health-slice2
```
(이미 있으면 `git checkout feature/server-health-slice2`.)

- [ ] **Step 2: 생성자에 EntityRegistry + HealthSystem 주입 추가** — 현재 (`LOPCombatSystem.cs:8-13`):
```csharp
        private readonly GameFramework.World.WorldEventBuffer worldEventBuffer;

        public LOPCombatSystem(GameFramework.World.WorldEventBuffer worldEventBuffer)
        {
            this.worldEventBuffer = worldEventBuffer;
        }
```
를 다음으로:
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

- [ ] **Step 3: Attack 본문(가드·writer·이벤트) 교체** — 현재 (`LOPCombatSystem.cs:25-78`):
```csharp
            if (target.TryGetEntityComponent<HealthComponent>(out var healthComponent) == false)
            {
                return;
            }

            if (healthComponent.currentHP <= 0)
            {
                Debug.LogWarning($"Target {target.entityId} is already dead.");
                return;
            }

            int damage = 10;

            StatsComponent attackerStats = attacker.GetEntityComponent<StatsComponent>();
            StatsComponent targetStats = target.GetEntityComponent<StatsComponent>();

            damage += attackerStats.strength;

            bool isDodged = IsDodge(attackerStats.dexterity, targetStats.dexterity);
            bool isCritical = IsCritical(attackerStats.strength, targetStats.strength);
            if (isCritical)
            {
                damage = Mathf.RoundToInt(damage * Random.Range(1.25f, 1.75f));
            }

            if (!isDodged)
            {
                healthComponent.TakeDamage(attacker.entityId, damage);
            }

            // --- World Core — 슬라이스 3: Generation → 버퍼 Append ---
            // 송신은 WireBroadcaster, Application은 WorldEventApplicator가 ProcessEvent에서 처리.
            // 레거시 HealthComponent.TakeDamage는 walking-skeleton 병렬 경로로 그대로 유지.
            int dealtAmount = isDodged ? 0 : damage;
            bool isDead = healthComponent.currentHP <= 0;

            worldEventBuffer.Append(new GameFramework.World.DamageDealtEvent(
                targetId:   target.entityId,
                attackerId: attacker.entityId,
                amount:     dealtAmount,
                isCritical: isCritical,
                isDodged:   isDodged,
                remaining:  healthComponent.currentHP,
                isDead:     isDead
            ));

            if (isDead)
            {
                worldEventBuffer.Append(new GameFramework.World.DeathEvent(
                    victimId:   target.entityId,
                    attackerId: attacker.entityId
                ));
            }
            // --- end World Core slice 3 ---
```
를 다음으로:
```csharp
            GameFramework.World.Entity worldEntity = entityRegistry.Get(target.entityId);
            GameFramework.World.Health health = worldEntity?.Get<GameFramework.World.Health>();
            if (health == null)
            {
                Debug.LogWarning($"[World] Attack: Health not found for entity {target.entityId}");
                return;
            }

            if (health.IsDead)
            {
                Debug.LogWarning($"Target {target.entityId} is already dead.");
                return;
            }

            int damage = 10;

            StatsComponent attackerStats = attacker.GetEntityComponent<StatsComponent>();
            StatsComponent targetStats = target.GetEntityComponent<StatsComponent>();

            damage += attackerStats.strength;

            bool isDodged = IsDodge(attackerStats.dexterity, targetStats.dexterity);
            bool isCritical = IsCritical(attackerStats.strength, targetStats.strength);
            if (isCritical)
            {
                damage = Mathf.RoundToInt(damage * Random.Range(1.25f, 1.75f));
            }

            int dealtAmount = isDodged ? 0 : damage;

            // --- World Core — Slice 2: writer flip + 사망 발행 재배치 ---
            // World.Health가 HP 진실원본. Generation(여기)이 mutate, WorldEventApplicator(ProcessEvent)가
            // remaining으로 재적용(멱등). 디스폰 구동 신호 EntityDeath도 사망 시 여기서 발행
            // (예전엔 HealthComponent.TakeDamage 안에 있었음). 디스폰 경로/구독자는 무변경.
            if (!isDodged)
            {
                healthSystem.TakeDamage(health, dealtAmount);
            }

            bool isDead = health.IsDead;

            worldEventBuffer.Append(new GameFramework.World.DamageDealtEvent(
                targetId:   target.entityId,
                attackerId: attacker.entityId,
                amount:     dealtAmount,
                isCritical: isCritical,
                isDodged:   isDodged,
                remaining:  health.Current,
                isDead:     isDead
            ));

            if (isDead)
            {
                worldEventBuffer.Append(new GameFramework.World.DeathEvent(
                    victimId:   target.entityId,
                    attackerId: attacker.entityId
                ));

                EventBus.Default.Publish(EventTopic.Entity, new Event.Entity.EntityDeath(
                    target.entityId, attacker.entityId, target.position));
            }
            // --- end World Core slice 2 ---
```
> dodge면 `TakeDamage` 미호출 → `health.Current` 불변 → `isDead=false` → 사망 발행 없음(기존 동작). `EntityDeath` 발행은 `if (isDead)` 안 = 치명타 시점 동기 발행으로 레거시와 동일 타이밍. `EventBus`/`EventTopic`/`Event.Entity.EntityDeath`는 새 using 없이 resolve(배경 사실).

- [ ] **Step 4: 컴파일 확인** — UnityMCP (SERVER pin):
  - `refresh_unity(unity_instance="<서버 id>")`
  - `read_console(unity_instance="<서버 id>", types=["error"])`
  Expected: 0 errors (HealthComponent는 아직 CharacterCreator가 참조 → 존재). **실제 실행하고 보고.**

- [ ] **Step 5: Commit** — combat 파일만 스테이징(LOPGame.cs/ConfigureRoomComponent.cs 절대 포함 금지):
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/CombatSystem/LOPCombatSystem.cs
git status   # LOPCombatSystem.cs만 staged 확인 (로컬 픽스처는 unstaged로 남아야 함)
git commit -m "refactor(world): flip damage writer to World.Health + relocate EntityDeath emit (Slice 2)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: 레거시 생성 제거 + HealthComponent 삭제

**Files:**
- Modify `Assets/Scripts/EntityCreator/CharacterCreator.cs`
- Delete `Assets/Scripts/Component/HealthComponent.cs` (+`.meta`)

- [ ] **Step 1: CharacterCreator의 HealthComponent 생성 제거** — 현재 (`CharacterCreator.cs:39-45`):
```csharp
            physicsComponent.Initialize(false, false);

            HealthComponent healthComponent = entity.AddEntityComponent<HealthComponent>();
            objectResolver.Inject(healthComponent);
            healthComponent.Initialize(creationData.maxHP, creationData.currentHP);

            ManaComponent manaComponent = entity.AddEntityComponent<ManaComponent>();
```
를 다음으로 (HealthComponent 3줄 제거):
```csharp
            physicsComponent.Initialize(false, false);

            ManaComponent manaComponent = entity.AddEntityComponent<ManaComponent>();
```
> `creationData.maxHP`/`currentHP`는 World.Health 생성(84-85줄)에서 계속 사용 — 그대로 둔다.

- [ ] **Step 2: 참조 0 확인 (삭제 전 안전장치)** — Grep `HealthComponent` (server `Assets/`):
  - Run: Grep tool — pattern `HealthComponent`, path `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets`, output_mode `files_with_matches`.
  - Expected: **`HealthComponent.cs` 단 한 파일만** 매치(정의 자신). 다른 매치가 있으면 멈추고 그 참조부터 처리.

- [ ] **Step 3: HealthComponent.cs + .meta 삭제**
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git rm Assets/Scripts/Component/HealthComponent.cs Assets/Scripts/Component/HealthComponent.cs.meta
```

- [ ] **Step 4: 컴파일 확인** — UnityMCP (SERVER pin):
  - `refresh_unity(unity_instance="<서버 id>")` (삭제 반영 위해 재스캔)
  - `read_console(unity_instance="<서버 id>", types=["error"])`
  Expected: 0 errors. (HealthComponent 참조 0 → CS2001/CS0246 없음.) **실제 실행하고 보고.**

- [ ] **Step 5: Commit** — creator + 삭제만 스테이징:
```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/EntityCreator/CharacterCreator.cs
git status   # CharacterCreator.cs(M) + HealthComponent.cs/.meta(D)만 staged 확인
git commit -m "refactor(world): remove legacy HealthComponent (server HP fully on World.Health, Slice 2)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: 런타임 검증 (수동)

> writer flip + 발행 재배치 — HP·디스폰·경험치구슬이 이전과 동일이 성공.

- [ ] **Step 1: 서버 플레이 + 관찰** — 서버 에디터 Play(룸 연결, 적/플레이어 피격·처치 발생). 콘솔/클라에서:
  - [ ] 피격 시 **HP 정상 감소** + 클라 HUD/네임플레이트 HP바 갱신(World.Health writer + 스냅샷/EntityDamage 경로).
  - [ ] **치명타 처치**: 대상 엔티티 **디스폰**(1회, 더블-데스 없음) + **경험치구슬이 사망 위치에 스폰**(`LOPGame.HandleEntityDeath` → DespawnEntity + SpawnExpMarble).
  - [ ] 이미 죽은 대상에 추가 데미지 → 두 번째 `EntityDeath`/디스폰 없음(`health.IsDead` 가드).
  - [ ] `[World] Death entity ...` broadcaster 로그 정상(slice 3 회귀). `Entity {id} has died.` 로그는 **사라짐**(HealthComponent 제거됨) — 정상.
  - [ ] `[World] Attack: Health not found` 경고가 정상 플레이 중 **안 뜸**(공격 대상=캐릭터, 항상 World.Health 보유).
  - [ ] 콘솔 에러 0(combat 생성자 DI 누락 NRE 없음 — EntityRegistry/HealthSystem 주입 정상).

- [ ] **Step 2: 검증 실패 시** — superpowers:systematic-debugging. 예상 위험:
  - (a) 디스폰 안 됨/경험치구슬 안 뜸 → combat의 `EntityDeath` 발행 누락 또는 position 인자 확인(`target.position`).
  - (b) `Health not found` 다발 → 비캐릭터 대상이 공격에 들어오는지(상단 player 가드)·World.Health 등록 타이밍 확인.
  - (c) combat 생성자 resolve 실패(NRE/컨테이너 에러) → `EntityRegistry`/`HealthSystem` 등록(`GameLifetimeScope.cs:20,22`) 확인.

---

## Self-Review (작성자 체크 결과)

- **Spec coverage:** spec 4개 항목 — writer flip(combat) → Task 1 Step 2-3, 사망 재배치(EntityDeath in combat) → Task 1 Step 3, CharacterCreator 제거 → Task 2 Step 1, HealthComponent 삭제 → Task 2 Step 3. DI 확인(추가 불필요) → 배경 사실 + Task 1 Step 2. 검증 → Task 3. ✓
- **Placeholder scan:** TBD/TODO 없음. 모든 코드 step에 실제 before/after. `<서버 id>`는 구현 시 resolve(인스턴스 핀 절차 명시). ✓
- **Type consistency:** `entityRegistry.Get(id)` → `GameFramework.World.Entity`, `?.Get<GameFramework.World.Health>()` → `Health`, `healthSystem.TakeDamage(health, dealtAmount)`(int), `health.Current`(int)/`health.IsDead`(bool), `EntityDeath(string, string, Vector3)` — Task 1 일관. 생성자 인자 3개 ↔ `GameLifetimeScope` 등록 Singleton 3개 매칭. ✓
- **컴파일 순서:** Task 1(combat 참조 제거) → Task 2(creator 제거 후 삭제) → 각 단계 0 refs 유지. ✓
- **로컬 픽스처 보호:** LOPGame.cs/ConfigureRoomComponent.cs 미접촉(옵션 A) + 커밋 제외 명시. ✓
- **단일 repo:** 전부 서버. GameFramework/클라 불변. ✓
- **범위:** HP만(MP/Level 등 legacy 유지). 디스폰의 World.DeathEvent 통일은 Slice 3 — 이 plan 밖. ✓
