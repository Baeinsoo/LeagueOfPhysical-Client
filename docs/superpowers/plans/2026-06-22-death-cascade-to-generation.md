# 죽음 cascade를 resolve 단계로 이전 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 죽음의 결과 처리(despawn + 경험치 구슬)를 egress(`ProcessEvent`) 경로에서 빼서 resolve 단계(`ProcessDeaths`, 송출 전)로 옮긴다. `WorldEventReactor` → `EventBus` → `LOPGame.HandleDeath` 왕복을 전용 `DeathCascadeSystem`이 resolve 단계에서 직접 처리하는 것으로 대체한다. (cleanup backlog #2 — 위치 교정)

**Architecture:** 서버 전용 리팩터. 동작 보존(죽음→despawn+구슬, 사망 연출, 아이템 줍기 전부 동일). `WorldEventReactor`를 `DeathCascadeSystem`으로 repurpose하고, `LOPGameEngine.UpdateEngine`에 `SimulatePhysics` 뒤·`ProcessEvent` 앞 `ProcessDeaths()` 스텝을 넣어 버퍼의 `DeathEvent`를 읽어 처리.

**Tech Stack:** Unity / C# / VContainer DI / UnityMCP(서버 인스턴스 컴파일 검증).

**Spec:** `docs/superpowers/specs/2026-06-22-death-cascade-to-generation-design.md`

---

## 레포 / 도구 참조

- **변경 레포: Server만** — `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server` (클라 코드 변경 0; 클라 repo엔 spec/plan 문서만).
- **UnityMCP 서버 인스턴스:** `mcpforunity://instances`에서 name=`LeagueOfPhysical-Server`의 `id` (현재 `LeagueOfPhysical-Server@f99391fa2dbaaf3c`, 해시 변동 가능). 모든 UnityMCP 호출에 `unity_instance` 명시.

**원자성(중요):** rename(`WorldEventReactor`→`DeathCascadeSystem`)이 3개 파일(클래스·엔진·스코프)에 걸쳐 있고, `LOPGame`의 기존 cascade를 *동시에* 제거해야 한다. 일부만 적용하면 ① 컴파일 깨짐 또는 ② 이중 cascade(새 `ProcessDeaths` + 구 `HandleDeath` 둘 다 실행)가 된다. 따라서 **Task 1을 하나의 코디네이티드 변경으로 전부 적용한 뒤 컴파일 검증**한다.

**픽스처 보존:** `git add -A`/`.`/`commit -am` 금지. Task에 명시된 파일만 `git add`. (서버 픽스처: `Assets/Scenes/Room.unity`, `Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs` — 커밋 금지.)

**.meta:** 파일 rename 시 `.cs`와 `.cs.meta`를 함께 `git mv`.

**컨벤션:** LOP 측 파일은 `using GameFramework.World;`를 추가하지 않고 World 타입은 풀 네임스페이스(`GameFramework.World.DeathEvent`)로 한정한다.

---

## Task 0: 서버 피처 브랜치 셋업

- [ ] **Step 1: 서버 working tree가 깨끗한지 확인 (픽스처 외)**

Run: `git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server status --short`
Expected: `Assets/Scenes/Room.unity`, `ConfigureRoomComponent.cs`만 ` M`. `LOPGame.cs` 등 이번에 바꿀 파일은 깨끗(HEAD-clean)해야 함. 다른 변경이 있으면 멈추고 보고.

- [ ] **Step 2: 피처 브랜치 생성**

Run:
```bash
git -C C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server checkout -b feature/death-cascade-to-generation
```
Expected: `Switched to a new branch ...`

---

## Task 1: 죽음 cascade를 resolve 단계로 이전 (서버, 코디네이티드)

**Files:**
- Rename: `Assets/Scripts/World/WorldEventReactor.cs` → `Assets/Scripts/World/DeathCascadeSystem.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Game/LOPGameEngine.cs`
- Modify: `Assets/Scripts/Game/GameLifetimeScope.cs`
- Modify: `Assets/Scripts/Game/LOPGame.cs`
- Modify: `Assets/Scripts/CombatSystem/LOPCombatSystem.cs`
- Modify: `Assets/Scripts/World/WorldEventSink.cs`

(모든 경로는 Server repo 루트 기준.)

- [ ] **Step 1: 파일 rename (git mv, .meta 포함)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git mv Assets/Scripts/World/WorldEventReactor.cs Assets/Scripts/World/DeathCascadeSystem.cs
git mv Assets/Scripts/World/WorldEventReactor.cs.meta Assets/Scripts/World/DeathCascadeSystem.cs.meta
```

- [ ] **Step 2: `DeathCascadeSystem.cs` 전체 내용 교체**

`Assets/Scripts/World/DeathCascadeSystem.cs` 를 아래로 덮어쓴다:
```csharp
using GameFramework;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// resolve 단계(egress 전)에서 죽음의 결과를 직접 처리하는 서버 cascade 시스템.
    /// WorldEventBuffer의 DeathEvent를 읽어 victim despawn + 경험치 구슬 스폰.
    /// (구 WorldEventReactor의 EventBus → LOPGame.HandleDeath 왕복을 대체.)
    /// </summary>
    public class DeathCascadeSystem
    {
        private readonly LOPEntityManager _entityManager;
        private readonly ISessionManager _sessionManager;
        private readonly IEntityCreationDataFactory _entityCreationDataFactory;

        public DeathCascadeSystem(
            LOPEntityManager entityManager,
            ISessionManager sessionManager,
            IEntityCreationDataFactory entityCreationDataFactory)
        {
            _entityManager = entityManager;
            _sessionManager = sessionManager;
            _entityCreationDataFactory = entityCreationDataFactory;
        }

        public void Resolve(IReadOnlyList<GameFramework.World.WorldEvent> events)
        {
            foreach (var e in events)
            {
                if (e is GameFramework.World.DeathEvent death)
                {
                    ResolveDeath(death);
                }
            }
        }

        private void ResolveDeath(GameFramework.World.DeathEvent death)
        {
            LOPEntity victim = _entityManager.GetEntity<LOPEntity>(death.victimId);
            if (victim == null)
            {
                Debug.LogWarning($"[World] DeathCascade: victim {death.victimId} not found");
                return;
            }
            Vector3 position = victim.position;

            _entityManager.DeleteEntityById(death.victimId);
            SpawnExpMarble(position);
        }

        private void SpawnExpMarble(Vector3 position)
        {
            string visualId = "Assets/Art/Items/ExpMarble/ExpMarble.prefab";
            string itemCode = "exp_marble";

            ItemCreationData data = new ItemCreationData
            {
                entityId = _entityManager.GenerateEntityId(),
                visualId = visualId,
                itemCode = itemCode,
                position = position,
                rotation = Vector3.zero,
                velocity = Vector3.zero,
            };

            LOPEntity entity = _entityManager.CreateEntity<LOPEntity, ItemCreationData>(data);

            EntitySpawnToC entitySpawnToC = new EntitySpawnToC
            {
                EntityCreationData = _entityCreationDataFactory.Create(entity),
            };

            foreach (var session in _sessionManager.GetAllSessions().OrEmpty())
            {
                session.Send(entitySpawnToC);
            }
        }
    }
}
```
> `OrEmpty()`/`ISessionManager`/`IEntityCreationDataFactory`/`ItemCreationData`/`EntitySpawnToC`는 기존 `LOPGame`이 같은 usings(`using GameFramework;` + `namespace LOP`)에서 쓰던 것 그대로다. 컴파일 에러 시 `LOPGame.cs`의 usings를 참고해 누락 using만 보강.

- [ ] **Step 3: `LOPGameEngine.cs` — 주입 교체 + `ProcessDeaths` 추가 + `React` 제거**

(a) 주입 필드 교체:
```csharp
        [Inject] private WorldEventReactor reactor;
```
→
```csharp
        [Inject] private DeathCascadeSystem deathCascade;
```

(b) `UpdateEngine()`에서 `SimulatePhysics();` 와 `ProcessEvent();` 사이에 `ProcessDeaths();` 삽입:
```csharp
            SimulatePhysics();

            ProcessEvent();
```
→
```csharp
            SimulatePhysics();

            ProcessDeaths();

            ProcessEvent();
```

(c) `ProcessEvent()`에서 `reactor.React(snapshot);` 삭제:
```csharp
            eventSink.Emit(snapshot);
            reactor.React(snapshot);
            worldEventBuffer.Clear();
```
→
```csharp
            eventSink.Emit(snapshot);
            worldEventBuffer.Clear();
```

(d) `ProcessEvent()` 메서드 바로 위(또는 아래)에 `ProcessDeaths()` 신규 메서드 추가:
```csharp
        private void ProcessDeaths()
        {
            var snapshot = worldEventBuffer.Snapshot;
            if (snapshot.Count == 0) return;
            deathCascade.Resolve(snapshot);
        }
```

- [ ] **Step 4: `GameLifetimeScope.cs` — 등록 교체 + entityManager AsSelf**

(a) 등록 교체:
```csharp
            builder.Register<WorldEventReactor>(Lifetime.Singleton);
```
→
```csharp
            builder.Register<DeathCascadeSystem>(Lifetime.Singleton);
```

(b) concrete `LOPEntityManager` 주입 가능하게 `AsSelf()` 추가:
```csharp
            builder.RegisterComponent(entityManager).As<IEntityManager>();
```
→
```csharp
            builder.RegisterComponent(entityManager).As<IEntityManager>().AsSelf();
```

- [ ] **Step 5: `LOPGame.cs` — 구독·`HandleDeath`·`SpawnExpMarble` 제거**

(a) `InitializeAsync`의 DeathEvent 구독 삭제:
```csharp
            EventBus.Default.Subscribe<GameFramework.World.DeathEvent>(EventTopic.Entity, HandleDeath);
```
(이 한 줄 삭제. 바로 아래 `Subscribe<ItemTouch>(...)`는 유지.)

(b) `DeinitializeAsync`의 대응 Unsubscribe 삭제:
```csharp
            EventBus.Default.Unsubscribe<GameFramework.World.DeathEvent>(EventTopic.Entity, HandleDeath);
```
(이 한 줄 삭제. `Unsubscribe<ItemTouch>(...)`는 유지.)

(c) `HandleDeath` 메서드 전체 삭제:
```csharp
        private void HandleDeath(GameFramework.World.DeathEvent deathEvent)
        {
            LOPEntity victim = gameEngine.entityManager.GetEntity<LOPEntity>(deathEvent.victimId);
            if (victim == null)
            {
                Debug.LogWarning($"[World] HandleDeath: victim {deathEvent.victimId} not found");
                return;
            }
            Vector3 position = victim.position;

            DespawnEntity(deathEvent.victimId);
            SpawnExpMarble(position);
        }
```

(d) `SpawnExpMarble` 메서드 전체 삭제 (DeathCascadeSystem으로 이전됨):
```csharp
        public void SpawnExpMarble(Vector3 position)
        {
            string visualId = "Assets/Art/Items/ExpMarble/ExpMarble.prefab";
            string itemCode = "exp_marble";

            ItemCreationData data = new ItemCreationData
            {
                entityId = gameEngine.entityManager.GenerateEntityId(),
                visualId = visualId,
                itemCode = itemCode,
                position = position,
                rotation = Vector3.zero,
                velocity = Vector3.zero,
            };

            LOPEntity entity = gameEngine.entityManager.CreateEntity<LOPEntity, ItemCreationData>(data);

            EntitySpawnToC entitySpawnToC = new EntitySpawnToC
            {
                EntityCreationData = entityCreationDataFactory.Create(entity),
            };

            foreach (var session in sessionManager.GetAllSessions().OrEmpty())
            {
                session.Send(entitySpawnToC);
            }
        }
```
> `HandleItemTouch`, `DespawnEntity`, `GetRandomSpawnPosition`는 **유지**한다.

- [ ] **Step 6: `LOPCombatSystem.cs` — stale 주석 갱신**

```csharp
            // --- World Core: DeathEvent → WorldEventReactor → LOPGame.HandleDeath ---
            // World.Health가 HP 진실원본 — Generation(여기)이 직접 mutate하고, 스냅샷(UserEntitySnap)이
            // 클라로의 유일 권위 경로다. DamageDealtEvent는 연출(숫자/크리)용으로만 송출.
            // DeathEvent를 WorldEventBuffer에 append → WorldEventReactor.React가 EventBus fan-out →
            // LOPGame.HandleDeath(디스폰+경험치 구슬).
```
→
```csharp
            // --- World Core: DeathEvent → (resolve) ProcessDeaths → DeathCascadeSystem ---
            // World.Health가 HP 진실원본 — Generation(여기)이 직접 mutate하고, 스냅샷(UserEntitySnap)이
            // 클라로의 유일 권위 경로다. DamageDealtEvent는 연출(숫자/크리)용으로만 송출.
            // DeathEvent를 WorldEventBuffer에 append → resolve 단계 ProcessDeaths가 읽어
            // DeathCascadeSystem이 디스폰+경험치 구슬 처리(egress 전).
```

- [ ] **Step 7: `WorldEventSink.cs` (서버) — DeathEvent case 제거**

`Emit`의 switch에서 DeathEvent case 삭제(잉여 — 죽음은 이제 ProcessDeaths가 소비):
```csharp
                    case GameFramework.World.DeathEvent de:
                        Debug.Log($"[World] Death entity {de.victimId} (killer={de.attackerId})");
                        break;
```
(삭제. `DamageDealtEvent` case는 유지. XML 주석의 `DeathEvent → Debug.Log` 줄도 함께 정리.)

- [ ] **Step 8: 컴파일 검증 (서버 인스턴스)**

UnityMCP `refresh_unity`(`scope=all`, `mode=force`) 후 `read_console`(`unity_instance`=서버 id)로 **에러 0** 확인. (rename + 모든 참조가 일관돼야 통과.) `execute_code` 사용 금지.

- [ ] **Step 9: 커밋 (Server repo)**

```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/World/DeathCascadeSystem.cs Assets/Scripts/World/DeathCascadeSystem.cs.meta Assets/Scripts/World/WorldEventSink.cs Assets/Scripts/Game/LOPGameEngine.cs Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Game/LOPGame.cs Assets/Scripts/CombatSystem/LOPCombatSystem.cs
git commit -m "refactor(world): move death cascade out of egress into resolve-phase ProcessDeaths

DeathCascadeSystem (repurposed WorldEventReactor) resolves death
consequences (despawn + exp marble) in a new ProcessDeaths step before
ProcessEvent, dropping the WorldEventReactor->EventBus->LOPGame.HandleDeath
round-trip. Behavior preserved; full event-ification deferred to Stage 4.
(cleanup backlog #2 position fix)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
> `git mv`(Step 1)로 stage된 rename + 수정본을 함께 커밋. 픽스처(Room.unity, ConfigureRoomComponent.cs)는 add 안 함.

---

## Task 2: 종합 검증 (플레이 — 사용자)

**Files:** (없음 — 검증만)

- [ ] **Step 1: 컴파일 최종 확인** — 서버 인스턴스 `read_console` 에러 0.

- [ ] **Step 2: 플레이 검증 (사용자)** — 서버·클라 플레이, **이전과 동일** 확인:
  1. 엔티티가 죽으면 **사라지고** 그 자리에 **경험치 구슬** 생성
  2. 죽을 때 **사망 연출** 남(`DamageDealtEvent.IsDead` — 무변경)
  3. 경험치 구슬 **주우면 경험치** 오름(`HandleItemTouch` — 무변경)
  4. 데미지 숫자·HP바 정상

성공 기준 = 동작 동일(순서/구조만 변경).

- [ ] **Step 3: 사용자 OK 시 — push + --no-ff merge (Server + Client)**

사용자 확인 후, Server·Client 각 피처 브랜치를 main에 `--no-ff` 머지 + push. (Client 브랜치 `feature/death-cascade-to-generation`엔 arch-doc 정리 + spec + plan 커밋이 있음.) 머지·push는 **사용자 요청 시에만**.

---

## Self-Review (작성자 체크)

- **Spec 커버리지:** DeathCascadeSystem 신규=Step1-2 / ProcessDeaths 스텝+React 제거=Step3 / DI(등록+AsSelf)=Step4 / LOPGame cascade 제거=Step5 / 주석=Step6 / Sink 정리=Step7 / 검증=Task2. ✅
- **플레이스홀더:** 없음 — 모든 편집에 정확한 old/new 코드. ✅
- **원자성:** rename 3파일 + LOPGame 제거를 Task 1 한 묶음으로 → 중간 이중 cascade/컴파일 깨짐 방지. ✅
- **타입 일관:** `DeathCascadeSystem.Resolve(IReadOnlyList<WorldEvent>)` ↔ `ProcessDeaths`가 `worldEventBuffer.Snapshot` 전달. deps(LOPEntityManager via AsSelf, ISessionManager, IEntityCreationDataFactory) ↔ DI 등록 정합. ✅
- **클라 무변경:** 죽음은 클라에서 스냅샷 파생 — 코드 변경 0. ✅
