# Server Health Slice 3 — Despawn Unify (World.DeathEvent) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 서버 디스폰을 `World.DeathEvent`로 일원화 — 신규 서버 `WorldEventBridge`(EventBus fan-out)가 드레인의 DeathEvent를 `LOPGame`에 전달해 디스폰+경험치 구슬을 구동하고, 레거시 `EntityDeath` 경로(발행·구독·struct)를 제거.

**Architecture:** 서버 전용. GameFramework/클라/wire 무변경(`World.DeathEvent`에 position 안 추가 — 드레인 시점 entityManager에서 조회, 엔티티는 EndUpdate 전까지 살아있음). 신규 `WorldEventBridge`는 클라 동형(EventBus fan-out), `WireBroadcaster`(wire)와 별개 sink. `LOPGame`이 디스폰 소유 유지(구독만 `EntityDeath`→`World.DeathEvent` 전환). 자동 테스트 없음(단일 Assembly-CSharp) — 컴파일 + 수동 플레이.

**Tech Stack:** C# (Unity), VContainer DI, EventBus, UnityMCP 컴파일 검증.

**Related spec:** `docs/superpowers/specs/2026-06-20-server-health-slice3-despawn-unify-design.md`

**Resolved Unity instance (매 UnityMCP 호출에 명시 — HTTP stateless):** Server `LeagueOfPhysical-Server@f99391fa2dbaaf3c` (서버 작업 인가).

---

## ⚠️ 서버 픽스처 — stash 댄스

`LOPGame.cs`(Task 5: 구독 전환 + HandleDeath)는 커밋 금지 로컬 픽스처(SpawnEnemies)가 dirty. `ConfigureRoomComponent.cs`도 dirty(무관). + 기존 `stash@{0}: 5-23`. 선례대로 **픽스처 2파일 stash → 깨끗한 baseline에서 변경 → 커밋 → pop**. 사망 배선은 `HandleEntityDeath`(≈L167)·Subscribe(≈L71)·Unsubscribe(≈L150)이라 픽스처(SpawnEnemies ≈L210)와 비겹침(Task 1에서 확인).

---

## File Structure (server repo: `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server`)

- **Create:** `Assets/Scripts/World/WorldEventBridge.cs` — 서버 EventBus fan-out(클라 동형). `WireBroadcaster.cs` 옆.
- **Modify:** `Assets/Scripts/Game/LOPGameEngine.cs` — `WorldEventBridge` 주입 + `ProcessEvent`에 `FanOut`.
- **Modify:** `Assets/Scripts/CombatSystem/LOPCombatSystem.cs` — 레거시 `EntityDeath` 발행 제거.
- **Modify:** `Assets/Scripts/Game/LOPGame.cs` (⚠️픽스처) — 구독 `EntityDeath`→`World.DeathEvent`, `HandleEntityDeath`→`HandleDeath`(position 조회).
- **Modify:** `Assets/Scripts/Entity/Event.Entity.cs` — 레거시 `EntityDeath` struct 삭제.
- **Modify:** `Assets/Scripts/Game/GameLifetimeScope.cs` — `WorldEventBridge` 등록.

---

## Task 1: 서버 브랜치 + 픽스처 stash

- [ ] **Step 1:**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" stash list
```
Expected: dirty = `LOPGame.cs` + `ConfigureRoomComponent.cs`, `stash@{0}: 5-23`. 다르면 중단.
- [ ] **Step 2:** `git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" checkout -b feature/server-health-slice3`
- [ ] **Step 3: 겹침 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" diff -- Assets/Scripts/Game/LOPGame.cs | grep "^@@"
```
Expected: hunk 범위가 Subscribe/Unsubscribe(≈L71/150)·HandleEntityDeath(≈L167)를 포함하지 않아야. 포함 시 중단·에스컬레이트.
- [ ] **Step 4: stash**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" stash push -m "health-slice3: hold local fixtures (LOPGame, ConfigureRoomComponent)" -- Assets/Scripts/Game/LOPGame.cs Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs
```
- [ ] **Step 5: 검증** — `git -C "..." stash list`(우리 stash@{0} + 5-23 @{1}), `git -C "..." status --short`(깨끗).

---

## Task 2: 서버 WorldEventBridge 생성

**Files:** Create `Assets/Scripts/World/WorldEventBridge.cs`

- [ ] **Step 1: 파일 작성**
```csharp
using GameFramework;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 드레인 이벤트를 서버 내부 EventBus로 fan-out한다(게임 반응용).
    /// 클라 WorldEventBridge의 서버 대응 — wire 송신(WireBroadcaster)과 별개 sink.
    /// 처리: DeathEvent → EventBus(EventTopic.Entity) → LOPGame.HandleDeath(디스폰+경험치 구슬).
    /// 향후 cascade(loot 등)는 여기 case 추가.
    /// </summary>
    public class WorldEventBridge
    {
        public void FanOut(IReadOnlyList<GameFramework.World.WorldEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case GameFramework.World.DeathEvent death:
                        EventBus.Default.Publish(EventTopic.Entity, death);
                        break;
                }
            }
        }
    }
}
```
- [ ] **Step 2: 컴파일 + .meta 확인**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")` → 0 errors.
- `ls "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server/Assets/Scripts/World/WorldEventBridge.cs.meta"` → 존재(없으면 refresh 재시도).

---

## Task 3: LOPGameEngine — WorldEventBridge 주입 + ProcessEvent FanOut

**Files:** Modify `Assets/Scripts/Game/LOPGameEngine.cs`

- [ ] **Step 1: 주입 추가**

`[Inject] private WireBroadcaster wireBroadcaster;`(24줄) 다음에 추가:
```csharp
        [Inject] private WorldEventBridge worldEventBridge;
```
- [ ] **Step 2: ProcessEvent에 FanOut 추가**

`ProcessEvent`(114–124줄)에서 `wireBroadcaster.Broadcast(snapshot);`(121줄) 다음, `worldEventBuffer.Clear();`(122줄) 앞에 추가:
```csharp
            worldEventBridge.FanOut(snapshot);
```

---

## Task 4: LOPCombatSystem — 레거시 EntityDeath 발행 제거

**Files:** Modify `Assets/Scripts/CombatSystem/LOPCombatSystem.cs`

- [ ] **Step 1:** 사망 블록(`if (isDead)`)에서 레거시 `EntityDeath` 발행 statement 제거. 현재:
```csharp
            if (isDead)
            {
                worldEventBuffer.Append(new GameFramework.World.DeathEvent(
                    victimId:   target.entityId,
                    attackerId: attacker.entityId
                ));

                EventBus.Default.Publish(EventTopic.Entity, new Event.Entity.EntityDeath(
                    target.entityId, attacker.entityId, target.position));
            }
```
→ (Publish 줄만 제거, World.DeathEvent append 유지):
```csharp
            if (isDead)
            {
                worldEventBuffer.Append(new GameFramework.World.DeathEvent(
                    victimId:   target.entityId,
                    attackerId: attacker.entityId
                ));
            }
```
(이 제거로 `using LOP.Event.Entity;`나 `EventBus`/`EventTopic` using이 미사용이 되면 컴파일 경고만 — Task 8 컴파일에서 에러 없으면 둔다. 다른 곳에서 쓰면 유지.)

---

## Task 5: LOPGame — 구독 전환 + HandleDeath (⚠️픽스처, stash됨)

**Files:** Modify `Assets/Scripts/Game/LOPGame.cs` (현재 baseline)

- [ ] **Step 1: Subscribe 전환 (71줄)**
```csharp
            EventBus.Default.Subscribe<EntityDeath>(EventTopic.Entity, HandleEntityDeath);
```
→
```csharp
            EventBus.Default.Subscribe<GameFramework.World.DeathEvent>(EventTopic.Entity, HandleDeath);
```
- [ ] **Step 2: Unsubscribe 전환 (150줄)**
```csharp
            EventBus.Default.Unsubscribe<EntityDeath>(EventTopic.Entity, HandleEntityDeath);
```
→
```csharp
            EventBus.Default.Unsubscribe<GameFramework.World.DeathEvent>(EventTopic.Entity, HandleDeath);
```
- [ ] **Step 3: 핸들러 교체 (167–172줄)**
```csharp
        private void HandleEntityDeath(EntityDeath entityDeath)
        {
            DespawnEntity(entityDeath.victimId);

            SpawnExpMarble(entityDeath.position);
        }
```
→
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
(`gameEngine`/`Debug`/`Vector3` 이미 사용 중. `DespawnEntity`/`SpawnExpMarble` 본문 무변경. 디스폰은 마크라 position 읽기는 그 전후 무관 — 명확성 위해 먼저.)

---

## Task 6: Event.Entity.cs — 레거시 EntityDeath struct 삭제

**Files:** Modify `Assets/Scripts/Entity/Event.Entity.cs`

- [ ] **Step 1:** `EntityDeath` struct(41–53줄, victimId/killerId/position + 생성자) 통째 삭제. (LOPGame 전환·LOPCombatSystem 발행 제거 후 소비자 0.)

---

## Task 7: GameLifetimeScope — WorldEventBridge 등록

**Files:** Modify `Assets/Scripts/Game/GameLifetimeScope.cs`

- [ ] **Step 1:** `builder.Register<WireBroadcaster>(Lifetime.Singleton);`(26줄) 다음에 추가:
```csharp
            builder.Register<WorldEventBridge>(Lifetime.Singleton);
```

---

## Task 8: 컴파일 + 잔존 참조 확인 (픽스처 stash된 상태)

- [ ] **Step 1: 컴파일**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")` → 0 errors.
- [ ] **Step 2: EntityDeath 잔존 0 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" grep -n "EntityDeath" -- "Assets/Scripts" || echo "NO MATCHES"
```
Expected: `NO MATCHES`(struct·발행·구독·핸들러 전부 제거됨). 매치 있으면 처리 — 중단.

---

## Task 9: 커밋 (Slice 3 파일만 — 픽스처 제외)

- [ ] **Step 1: 상태 확인**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" status --short
```
Expected: WorldEventBridge.cs(+meta) 신규 + LOPGameEngine/LOPCombatSystem/LOPGame/Event.Entity/GameLifetimeScope 수정. (픽스처 stash됨이라 안 보임.) `ConfigureRoomComponent.cs` 보이면 stash 누락 — 중단.
- [ ] **Step 2: stage + 커밋**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" add Assets/Scripts/World/WorldEventBridge.cs Assets/Scripts/World/WorldEventBridge.cs.meta Assets/Scripts/Game/LOPGameEngine.cs Assets/Scripts/CombatSystem/LOPCombatSystem.cs Assets/Scripts/Game/LOPGame.cs Assets/Scripts/Entity/Event.Entity.cs Assets/Scripts/Game/GameLifetimeScope.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" commit -m "feat(world): unify despawn onto World.DeathEvent via server WorldEventBridge, remove legacy EntityDeath

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
- [ ] **Step 3:** `git -C "..." show --stat HEAD | head -14` — 위 파일들만. `ConfigureRoomComponent.cs` 포함되면 중단.

---

## Task 10: 픽스처 복원 (pop) + 최종 검증

- [ ] **Step 1: pop**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" stash pop
```
Expected: clean pop(`LOPGame.cs`·`ConfigureRoomComponent.cs` dirty 복원). **CONFLICT 시 중단·에스컬레이트.**
- [ ] **Step 2: 검증** — `git -C "..." status --short`(픽스처 2파일 dirty), `git -C "..." stash list`(`5-23`만).
- [ ] **Step 3: 결합 재컴파일**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")` → 0 errors.

---

## Task 11: 런타임 수동 검증 (사용자)

서버 플레이:
1. 몬스터 치명타 사망 → **디스폰 1회(더블 없음) + 경험치 구슬이 사망 위치에 스폰**, 클라 수신(EntityDespawnToC/EntitySpawnToC) 정상.
2. `[World] Death entity ...`(WireBroadcaster 로그) 정상, `HandleDeath: victim not found` 경고는 정상 플레이 중 안 뜸.
3. 콘솔 에러 0. 데미지→사망→디스폰 전 경로 정상.

동작 보존(회귀 0)이 성공.

---

## Slice 3 완료 기준

- [ ] 서버 `feature/server-health-slice3`에 커밋 1개(픽스처 미포함, WorldEventBridge.cs+meta 포함).
- [ ] 서버 컴파일 0에러. `EntityDeath` 완전 제거(grep 0).
- [ ] 픽스처 2파일 working tree 보존, `5-23` stash 무손상.
- [ ] 수동 플레이: 사망→디스폰+구슬 동작 보존.

이후: 사용자 검증 후 서버 → main 머지(stash 댄스). 서버 Health 이행 완전 종료(디스폰 World 이벤트 일원화). 남은 건 Stage④급(공유 시뮬/예측/통합 fan-out).
