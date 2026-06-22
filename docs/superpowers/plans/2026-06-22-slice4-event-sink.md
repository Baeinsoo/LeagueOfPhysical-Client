# 이벤트 송출 포트 `IEventSink` Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `LOPGameEngine.ProcessEvent`의 이벤트 송출을 주입된 `IEventSink`(GameFramework.World) 포트 뒤로 격리하고, 구현체를 `WorldEventSink`로(클·서), 서버 death cascade를 `WorldEventReactor`로 rename·분리한다.

**Architecture:** GameFramework.World에 `IEventSink` 포트. 클 `WorldEventBridge`·서 `WireBroadcaster` → 둘 다 `WorldEventSink : IEventSink`(같은 이름, 사이드별 본문, `FanOut`/`Broadcast`→`Emit`). 서버 `WorldEventBridge`(death→HandleDeath) → `WorldEventReactor`(`FanOut`→`React`, sink 아님). `ProcessEvent`가 `eventSink.Emit` + (서버)`reactor.React` 호출. 동작 보존.

**Tech Stack:** Unity, C#, VContainer DI, UnityMCP(컴파일 검증). 3 git repo(GameFramework / LeagueOfPhysical-Client / LeagueOfPhysical-Server).

---

## 작업 규약 (모든 Task 공통)

- **테스트 없음(의도적):** 동작 보존 리팩터. 구현체가 EventBus/Mirror/세션 의존 + Assembly-CSharp라 asmdef 단위 테스트 불가. 검증 = **컴파일 클린 + 실행 동작 동일**. (가짜 테스트 금지.)
- **UnityMCP 인스턴스 타게팅(필수):** 모든 UnityMCP 호출에 `unity_instance` 명시. Client `LeagueOfPhysical-Client@de70658b9450cbb4` / Server `LeagueOfPhysical-Server@f99391fa2dbaaf3c`. (불일치 시 `mcpforunity://instances` 재확인.)
- **파일 rename = `git mv` (.cs+.meta 동반):** Unity가 만든 `.meta` GUID를 보존하려면 `git mv X.cs Y.cs` + `git mv X.cs.meta Y.cs.meta`. rename 후 `.cs` 내용을 Edit으로 갱신하고, 그 다음 `refresh_unity`. (순서: git mv → 내용 Edit → refresh.)
- **Git(LOP 패턴):** 각 repo 피처 브랜치 `feature/event-sink` → main `--no-ff`. main 직접 커밋 금지. 워크트리 안 씀. **변경/rename 파일만 선택적 `git add`**(절대 `git add -A`/`.`/`commit -am`) — 클 픽스처(`Assets/Scenes/Room.unity`·`Assets/Resources/UI/UIRoot.prefab`·`ProjectSettings/PackageManagerSettings.asset`)·서 픽스처(`Assets/Scenes/Room.unity`·`Assets/Scripts/Entrance/EntranceComponent/ConfigureRoomComponent.cs`·`Assets/Art/`) 보존. push 안 함.
- **순서 의존:** Task 1(GameFramework `IEventSink`) 먼저 — 클·서가 참조. Task 1에서 두 에디터 컴파일 클린 확인 후 Task 2·3.

---

## Task 1: GameFramework — `IEventSink` 포트

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/World/Events/IEventSink.cs`
- (Repo: `C:/Users/re5na/workspace/LOP/GameFramework`, asmdef `baegames.GameFramework.World`, noEngineReferences=true, `WorldEventBuffer.cs` 옆)

- [ ] **Step 1: 인터페이스 파일 생성**

`Runtime/Scripts/World/Events/IEventSink.cs`:
```csharp
using System.Collections.Generic;

namespace GameFramework.World
{
    /// <summary>
    /// 확정된 WorldEvent를 바깥(프레젠테이션·네트워크)으로 송출하는 egress 포트.
    /// 순수 송출 — 상태를 바꾸거나 새 이벤트를 만들지 않는다(그건 Generation 소관).
    /// 사이드별 구현(WorldEventSink): 클라=프레젠테이션 EventBus, 서버=wire broadcast.
    /// </summary>
    public interface IEventSink
    {
        void Emit(IReadOnlyList<WorldEvent> events);
    }
}
```

- [ ] **Step 2: 두 에디터 refresh + 컴파일 검증**

- `refresh_unity(scope="scripts", compile="request", mode="force", wait_for_ready=true, unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `refresh_unity(... unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
- 각 `read_console(action="get", types=["error"], unity_instance=<해당>)` → 에러 0.

Expected: 0 errors. `GameFramework.World.IEventSink` 양쪽 해석.

- [ ] **Step 3: 커밋 (GameFramework repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git checkout -b feature/event-sink
git add Runtime/Scripts/World/Events/IEventSink.cs Runtime/Scripts/World/Events/IEventSink.cs.meta
git status --short
git commit -m "feat(world): IEventSink egress port

Port for emitting settled WorldEvents to presentation/network. Pure
egress — no state change, no new events (that is Generation's job).
Side-specific impls (WorldEventSink) live in the projects.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git checkout main
git merge --no-ff feature/event-sink -m "Merge feature/event-sink: IEventSink port"
git branch -d feature/event-sink
git log --oneline -2
```

커밋 전 `git status --short`로 2개 파일(.cs+.meta)만 스테이징 확인.

---

## Task 2: Client — `WorldEventBridge` → `WorldEventSink` + 재배선

**Files:**
- Rename: `Assets/Scripts/World/WorldEventBridge.cs` → `Assets/Scripts/World/WorldEventSink.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Game/LOPGameEngine.cs` (inject 15, 호출 97)
- Modify: `Assets/Scripts/Game/GameLifetimeScope.cs` (등록 35)
- (Repo: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client`)

- [ ] **Step 1: 파일 rename (git mv, .cs+.meta)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git checkout -b feature/event-sink
git mv Assets/Scripts/World/WorldEventBridge.cs Assets/Scripts/World/WorldEventSink.cs
git mv Assets/Scripts/World/WorldEventBridge.cs.meta Assets/Scripts/World/WorldEventSink.cs.meta
```

- [ ] **Step 2: `WorldEventSink.cs` 내용 갱신 (클래스·인터페이스·메서드)**

`Assets/Scripts/World/WorldEventSink.cs` 전체를 아래로(클래스 `WorldEventBridge`→`WorldEventSink`, `: GameFramework.World.IEventSink` 추가, `FanOut`→`Emit`; 본문·EventBus 호출 동일):
```csharp
using GameFramework;
using LOP.Event.Entity;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 스냅샷을 프레젠테이션 EventBus로 송출하는 egress sink(클라). 코어 상태·새 이벤트 안 만듦.
    ///   DamageDealtEvent → EventBus.Publish(EntityId&lt;LOPEntity&gt;(targetId), EntityDamage) → DamageFloaterEmitter/CharacterNameplate/LOPEntityView 소비
    ///   DeathEvent       → Debug.Log only (구독자 없음, future-proof 자리)
    /// </summary>
    public class WorldEventSink : GameFramework.World.IEventSink
    {
        public void Emit(IReadOnlyList<GameFramework.World.WorldEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case GameFramework.World.DamageDealtEvent dde:
                        EventBus.Default.Publish(
                            EventTopic.EntityId<LOPEntity>(dde.targetId),
                            new EntityDamage(dde.isDodged, dde.isCritical, dde.amount, dde.remaining, dde.isDead)
                        );
                        break;
                    case GameFramework.World.DeathEvent de:
                        Debug.Log($"[World] Death entity {de.victimId} (killer={de.attackerId})");
                        break;
                }
            }
        }
    }
}
```

- [ ] **Step 3: `LOPGameEngine.cs` — inject 필드 교체**

변경 전 (현재 15행):
```csharp
        [Inject] private WorldEventBridge worldEventBridge;
```
변경 후:
```csharp
        [Inject] private GameFramework.World.IEventSink eventSink;
```

- [ ] **Step 4: `LOPGameEngine.cs` — `ProcessEvent` 호출 교체**

`ProcessEvent`(현재 89-99행)의 fan-out 줄. 변경 전 (현재 97행):
```csharp
            worldEventBridge.FanOut(snapshot);
```
변경 후:
```csharp
            eventSink.Emit(snapshot);
```

- [ ] **Step 5: `GameLifetimeScope.cs` — DI 등록 교체**

변경 전 (현재 35행):
```csharp
            builder.Register<WorldEventBridge>(Lifetime.Singleton);
```
변경 후:
```csharp
            builder.Register<GameFramework.World.IEventSink, WorldEventSink>(Lifetime.Singleton);
```

- [ ] **Step 6: refresh + 컴파일 검증 (클라)**

- `refresh_unity(scope="scripts", compile="request", mode="force", wait_for_ready=true, unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Client@de70658b9450cbb4")` → 0 errors. (특히 "WorldEventBridge not found"/"No registration for IEventSink" 없음.)

- [ ] **Step 7: 커밋 (클라 repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/World/WorldEventSink.cs Assets/Scripts/World/WorldEventSink.cs.meta Assets/Scripts/Game/LOPGameEngine.cs Assets/Scripts/Game/GameLifetimeScope.cs
git status --short
git commit -m "refactor(world): client WorldEventBridge -> WorldEventSink : IEventSink

Rename the client presentation egress to WorldEventSink implementing the
IEventSink port (FanOut -> Emit). LOPGameEngine injects IEventSink;
GameLifetimeScope registers it. Behavior-preserving.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git checkout main
git merge --no-ff feature/event-sink -m "Merge feature/event-sink: client WorldEventSink"
git branch -d feature/event-sink
git log --oneline -2
```

커밋 전 `git status --short`: rename된 `WorldEventSink.cs`(+meta, `R` 표시) + `LOPGameEngine.cs` + `GameLifetimeScope.cs`만 스테이징, 클 픽스처 3개(Room.unity/UIRoot.prefab/PackageManagerSettings) unstaged 유지 확인. 그 외 스테이징되면 STOP → BLOCKED.

---

## Task 3: Server — `WireBroadcaster`→`WorldEventSink` + `WorldEventBridge`→`WorldEventReactor` + 재배선

**Files:**
- Rename: `Assets/Scripts/World/WireBroadcaster.cs` → `Assets/Scripts/World/WorldEventSink.cs` (+ `.meta`)
- Rename: `Assets/Scripts/World/WorldEventBridge.cs` → `Assets/Scripts/World/WorldEventReactor.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Game/LOPGameEngine.cs` (inject 24-25, 호출 171-172)
- Modify: `Assets/Scripts/Game/GameLifetimeScope.cs` (등록 26-27)
- Modify: `Assets/Scripts/CombatSystem/LOPCombatSystem.cs` (주석 73, 76)
- (Repo: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server`)

- [ ] **Step 1: 파일 rename 2건 (git mv, .cs+.meta)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git checkout -b feature/event-sink
git mv Assets/Scripts/World/WireBroadcaster.cs Assets/Scripts/World/WorldEventSink.cs
git mv Assets/Scripts/World/WireBroadcaster.cs.meta Assets/Scripts/World/WorldEventSink.cs.meta
git mv Assets/Scripts/World/WorldEventBridge.cs Assets/Scripts/World/WorldEventReactor.cs
git mv Assets/Scripts/World/WorldEventBridge.cs.meta Assets/Scripts/World/WorldEventReactor.cs.meta
```

- [ ] **Step 2: `WorldEventSink.cs` 내용 갱신 (구 WireBroadcaster)**

`Assets/Scripts/World/WorldEventSink.cs` 전체를 아래로(클래스 `WireBroadcaster`→`WorldEventSink`, `: GameFramework.World.IEventSink`, `Broadcast`→`Emit`; ctor·본문 동일):
```csharp
using GameFramework;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 스냅샷을 와이어 메시지로 변환해 모든 세션에 송출하는 egress sink(서버). 출구=네트워크.
    /// 코어 상태·새 이벤트 안 만듦.
    ///   DamageDealtEvent → DamageEventToC → session.Send (모든 세션)
    ///   DeathEvent       → Debug.Log only (별도 wire 메시지 없음)
    /// </summary>
    public class WorldEventSink : GameFramework.World.IEventSink
    {
        private readonly ISessionManager _sessionManager;

        public WorldEventSink(ISessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public void Emit(IReadOnlyList<GameFramework.World.WorldEvent> events)
        {
            foreach (var e in events)
            {
                switch (e)
                {
                    case GameFramework.World.DamageDealtEvent dde:
                    {
                        var msg = new DamageEventToC
                        {
                            Tick        = GameEngine.Time.tick,
                            AttackerId  = dde.attackerId,
                            TargetId    = dde.targetId,
                            ActionCode  = "attack",
                            DamageType  = "physical",
                            Damage      = dde.amount,
                            IsCritical  = dde.isCritical,
                            IsDodged    = dde.isDodged,
                            IsBlocked   = false,
                            RemainingHP = dde.remaining,
                            IsDead      = dde.isDead,
                        };
                        foreach (var session in _sessionManager.GetAllSessions())
                        {
                            session.Send(msg);
                        }
                        break;
                    }
                    case GameFramework.World.DeathEvent de:
                        Debug.Log($"[World] Death entity {de.victimId} (killer={de.attackerId})");
                        break;
                }
            }
        }
    }
}
```

- [ ] **Step 3: `WorldEventReactor.cs` 내용 갱신 (구 WorldEventBridge)**

`Assets/Scripts/World/WorldEventReactor.cs` 전체를 아래로(클래스 `WorldEventBridge`→`WorldEventReactor`, `FanOut`→`React`; 본문 동일, `IEventSink` 구현 안 함):
```csharp
using GameFramework;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 드레인 이벤트에 반응해 서버 게임플레이 cascade를 일으키는 reactor(egress 아님).
    ///   DeathEvent → EventBus(EventTopic.Entity) → LOPGame.HandleDeath(디스폰+경험치 구슬).
    /// 향후 cascade(loot 등)는 여기 case 추가. (death→despawn을 Generation cascade로 옮기는 건 backlog #2-full.)
    /// </summary>
    public class WorldEventReactor
    {
        public void React(IReadOnlyList<GameFramework.World.WorldEvent> events)
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

- [ ] **Step 4: `LOPGameEngine.cs` — inject 필드 교체**

변경 전 (현재 24-25행):
```csharp
        [Inject] private WireBroadcaster wireBroadcaster;
        [Inject] private WorldEventBridge worldEventBridge;
```
변경 후:
```csharp
        [Inject] private GameFramework.World.IEventSink eventSink;
        [Inject] private WorldEventReactor reactor;
```

- [ ] **Step 5: `LOPGameEngine.cs` — `ProcessEvent` 호출 교체**

`ProcessEvent`(현재 163-174행)의 두 줄. 변경 전 (현재 171-172행):
```csharp
            wireBroadcaster.Broadcast(snapshot);
            worldEventBridge.FanOut(snapshot);
```
변경 후 (egress 먼저, reactor 다음 — 순서 보존):
```csharp
            eventSink.Emit(snapshot);
            reactor.React(snapshot);
```

- [ ] **Step 6: `GameLifetimeScope.cs` — DI 등록 교체**

변경 전 (현재 26-27행):
```csharp
            builder.Register<WireBroadcaster>(Lifetime.Singleton);
            builder.Register<WorldEventBridge>(Lifetime.Singleton);
```
변경 후:
```csharp
            builder.Register<GameFramework.World.IEventSink, WorldEventSink>(Lifetime.Singleton);
            builder.Register<WorldEventReactor>(Lifetime.Singleton);
```

- [ ] **Step 7: `LOPCombatSystem.cs` — 주석 정합 (2곳)**

변경 전 (현재 73행): `            // --- World Core — Slice 3: DeathEvent → WorldEventBridge → LOPGame.HandleDeath ---`
변경 후: `            // --- World Core — Slice 3: DeathEvent → WorldEventReactor → LOPGame.HandleDeath ---`

변경 전 (현재 76행): `            // WorldEventBridge.FanOut이 EventBus로 fan-out → LOPGame.HandleDeath(디스폰+경험치 구슬).`
변경 후: `            // WorldEventReactor.React가 EventBus로 fan-out → LOPGame.HandleDeath(디스폰+경험치 구슬).`

- [ ] **Step 8: refresh + 컴파일 검증 (서버)**

- `refresh_unity(scope="scripts", compile="request", mode="force", wait_for_ready=true, unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")`
- `read_console(action="get", types=["error"], unity_instance="LeagueOfPhysical-Server@f99391fa2dbaaf3c")` → 0 errors. (특히 "WireBroadcaster/WorldEventBridge not found", DI 미등록 없음.)

- [ ] **Step 9: 커밋 (서버 repo)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/World/WorldEventSink.cs Assets/Scripts/World/WorldEventSink.cs.meta Assets/Scripts/World/WorldEventReactor.cs Assets/Scripts/World/WorldEventReactor.cs.meta Assets/Scripts/Game/LOPGameEngine.cs Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/CombatSystem/LOPCombatSystem.cs
git status --short
git commit -m "refactor(world): server WireBroadcaster -> WorldEventSink : IEventSink; WorldEventBridge -> WorldEventReactor

Server wire egress becomes WorldEventSink implementing IEventSink
(Broadcast -> Emit). The death cascade (was WorldEventBridge) becomes
WorldEventReactor (FanOut -> React), separated out of the sink — egress
no longer carries the gameplay cascade. ProcessEvent calls eventSink.Emit
then reactor.React. Behavior-preserving.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git checkout main
git merge --no-ff feature/event-sink -m "Merge feature/event-sink: server WorldEventSink + WorldEventReactor"
git branch -d feature/event-sink
git log --oneline -2
```

커밋 전 `git status --short`: rename 2건(`WorldEventSink.cs`/`WorldEventReactor.cs` +meta) + `LOPGameEngine.cs` + `GameLifetimeScope.cs` + `LOPCombatSystem.cs`만 스테이징, 서 픽스처(`Room.unity`/`ConfigureRoomComponent.cs`/`Art/`) unstaged·untracked 유지 확인. 그 외 스테이징되면 STOP → BLOCKED.

---

## Task 4: 통합 검증 (동작 보존)

**Files:** (없음 — 실행 검증)

- [ ] **Step 1: 서버 → 클라 플레이**

서버 Play → 클라 Play 접속.

- [ ] **Step 2: egress·reactor 경로 동작 확인**

- **클라 egress:** 공격 시 데미지 **플로터/체력바 정상**(`WorldEventSink.Emit` → EventBus).
- **서버 egress:** 클라가 데미지 **수신 정상**(`WorldEventSink.Emit` → wire `DamageEventToC`).
- **서버 reactor:** 적 처치 시 **디스폰 + 경험치 구슬 정상**(`WorldEventReactor.React` → HandleDeath).
- 클·서 콘솔 신규 에러 없음.

Expected: 동작 차이 0. 차이 시 — DI가 `WorldEventSink`를 `IEventSink`로 주입했는지, 서버 `reactor.React` 호출이 살아있는지(death 디스폰), Emit 본문이 기존 FanOut/Broadcast와 동일한지 확인.

- [ ] **Step 3: 완료**

3 repo main 머지(push 안 함). 픽스처 보존. `IEventSink` egress 포트 슬라이스 완료. (backlog #1/#2-full/#3은 별도.)

---

## Self-Review (작성자 확인 완료)

- **Spec 커버리지:** IEventSink(Task1) / 클 WorldEventSink rename+구현+재배선(Task2) / 서 WorldEventSink+WorldEventReactor rename+재배선+주석(Task3) / 검증(Task4) — 전부 대응. ✅
- **Placeholder:** 모든 코드·경로·git mv·커밋 실제값. `<해당>` 인스턴스만 규약 해소. ✅
- **타입 일관:** 포트 `IEventSink.Emit(IReadOnlyList<WorldEvent>)` / 구현 `WorldEventSink.Emit`(클·서) / `WorldEventReactor.React` / inject `eventSink`·`reactor` / DI `Register<IEventSink, WorldEventSink>` — 전 Task 일관. ✅
