# WorldEvent 단일 폴리모픽 envelope 통합 (#7) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `WorldEvent` egress 경로의 개념별 top-level 패킷(`DamageEventToC`, `AbilityActivatedToC`)을 단일 폴리모픽 Mirror 메시지 `WorldEventBatchToC`(oneof 기반) 하나로 통합한다.

**Architecture:** 서버 `WorldEventSink`가 버퍼의 `WorldEvent`들을 공유 순수 매퍼 `WorldEventWire.ToWire`로 `WorldEventToC`(oneof) 레코드로 바꿔 `WorldEventBatchToC` 하나에 담아 세션당 1회 송신. 클라 단일 핸들러가 배치를 받아 `WorldEventWire.FromWire`로 되돌려 `WorldEventBuffer`에 append. 매퍼는 LOP-Shared의 Generated 어셈블리에 두어 클·서 공유 + EditMode 테스트.

**Tech Stack:** Unity, Mirror, Google.Protobuf(proto3 oneof), VContainer, MessagePipe, NUnit(EditMode).

## Global Constraints

- **3개 저장소 손댐**: `LeagueOfPhysical-Shared`(proto·매퍼·생성물), `LeagueOfPhysical-Server`(sink), `LeagueOfPhysical-Client`(핸들러·배선). GameFramework는 **미변경**(WorldEvent 타입 그대로).
- **각 저장소에 피처 브랜치** `feature/world-event-batch-envelope`를 만들어 작업(main 직접 커밋 금지). 클라 저장소는 이미 이 브랜치에 있음.
- **`.meta` 파일은 반드시 커밋**(Unity가 생성한 것만; 직접 만들지 않음).
- **proto 재생성은 `LeagueOfPhysical-Shared/Scripts/generate_protos.sh`(부모)** 로만. 이 스크립트는 `MessageIds.cs`를 지우지 않고 기존 ID를 보존한다(생존 메시지 wire 계약 안정). 재생성 후 **생존 MessageId 불변 diff-verify** 필수.
- **전송 채널/신뢰성 현행 유지** — `session.Send(msg)` 기본 `reliable:true`. 변경 금지(#4 계열 밖).
- **컴파일 건강**: Damage/Ability proto의 `@auto_generate`는 **Task 5(마지막)까지 유지**한다. 그래야 중간 태스크에서도 두 타입이 `IMessage`로 남아 클·서가 계속 컴파일된다. 모든 top-level 참조가 사라진 뒤 Task 5에서만 제거.
- **UnityMCP 대상 지정**: 클라 콘솔/리프레시는 `unity_instance`로 client 인스턴스를 명시(CLAUDE.md 규칙). 서버 인스턴스는 별도.
- **World 타입 네임스페이스**: LOP 측 `using UnityEngine` 파일에서는 `GameFramework.World.*`를 풀 한정. 매퍼(Generated, 엔진 미사용)는 `using GameFramework.World;` 허용.

---

## 파일 구조 (무엇이 어디서 바뀌나)

**LOP-Shared:**
- 신규 proto: `Protos/WorldEventToC.proto`(oneof 래퍼), `Protos/WorldEventBatchToC.proto`(top-level 배치)
- 수정 proto(Task 5): `Protos/DamageEventToC.proto`, `Protos/AbilityActivatedToC.proto` — `@auto_generate` 제거
- 신규 매퍼: `Runtime.Generated/Scripts/WorldEventWire.cs`(hand-written, 순수, 클·서 공유)
- asmdef 수정: `Runtime.Generated/baegames.LOP.Shared.Generated.asmdef` — `baegames.GameFramework.World` 참조 추가
- 신규 테스트: `Tests/EditMode/WorldEventWireTests.cs`
- 재생성 산출물: `Runtime.Generated/Scripts/Protobuf/*`, `MessageIds.cs`, `MessageInitializer.cs`

**LOP-Server:**
- 수정: `Assets/Scripts/World/WorldEventSink.cs` — 배치 조립 + 세션당 1 Send

**LOP-Client:**
- 신규: `Assets/Scripts/Game/MessageHandler/Game.WorldEvent.MessageHandler.cs`
- 삭제: `Assets/Scripts/Game/MessageHandler/Game.Damage.MessageHandler.cs`, `Game.Ability.MessageHandler.cs`
- 수정: `Assets/Scripts/Messaging/NetworkMessageDispatcher.cs`, `Assets/Scripts/RootLifetimeScope.cs`, `Assets/Scripts/Game/GameLifetimeScope.cs`

---

## Task 1: proto 스캐폴딩 + 재생성 (LOP-Shared)

새 폴리모픽 래퍼(`WorldEventToC`)와 top-level 배치(`WorldEventBatchToC`) proto를 추가하고 재생성한다. Damage/Ability의 `@auto_generate`는 **유지**(이 태스크에서 안 건드림). 매퍼가 볼 수 있도록 Generated asmdef에 `GameFramework.World` 참조를 추가한다.

**Files:**
- Create: `LeagueOfPhysical-Shared/Protos/WorldEventToC.proto`
- Create: `LeagueOfPhysical-Shared/Protos/WorldEventBatchToC.proto`
- Modify: `LeagueOfPhysical-Shared/Runtime.Generated/baegames.LOP.Shared.Generated.asmdef`
- Regenerate: `LeagueOfPhysical-Shared/Runtime.Generated/Scripts/Protobuf/*`, `MessageIds.cs`, `MessageInitializer.cs`

**Interfaces:**
- Produces: 전역 네임스페이스 proto 클래스 `WorldEventToC`(oneof `event`: `Damage`/`AbilityActivated`, `EventCase` 판별자), `WorldEventBatchToC`(`long Tick`, `RepeatedField<WorldEventToC> Events`, `IMessage`, `MessageIds.WorldEventBatchToC`). Task 2·3·4가 사용.

- [ ] **Step 1: 브랜치 생성 (Shared)**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared && git checkout -b feature/world-event-batch-envelope
```
Expected: `Switched to a new branch 'feature/world-event-batch-envelope'`

- [ ] **Step 2: `WorldEventToC.proto` 작성 (oneof 래퍼, @auto_generate 없음)**

Create `LeagueOfPhysical-Shared/Protos/WorldEventToC.proto`:
```proto
syntax = "proto3";

import "DamageEventToC.proto";
import "AbilityActivatedToC.proto";

// 폴리모픽 래퍼 — top-level 패킷 아님(@auto_generate 없음). WorldEventBatchToC 안에 담긴다.
// 새 WorldEvent 타입 = oneof에 한 줄 추가.
message WorldEventToC
{
	oneof event
	{
		DamageEventToC      damage            = 1;
		AbilityActivatedToC ability_activated = 2;
	}
}
```

- [ ] **Step 3: `WorldEventBatchToC.proto` 작성 (top-level 배치, @auto_generate 있음)**

Create `LeagueOfPhysical-Shared/Protos/WorldEventBatchToC.proto`:
```proto
syntax = "proto3";

import "WorldEventToC.proto";

// @auto_generate
message WorldEventBatchToC
{
	int64 tick = 1;
	repeated WorldEventToC events = 2;
}
```

- [ ] **Step 4: Generated asmdef에 GameFramework.World 참조 추가**

Modify `LeagueOfPhysical-Shared/Runtime.Generated/baegames.LOP.Shared.Generated.asmdef` — `references` 배열을 다음으로:
```json
    "references": ["baegames.LOP.Shared.Runtime", "baegames.GameFramework.Runtime", "baegames.GameFramework.World"],
```
(Task 2의 `WorldEventWire`가 `GameFramework.World.WorldEvent` 타입을 보기 위함.)

- [ ] **Step 5: 재생성 실행**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts && ./generate_protos.sh
```
Expected: `All proto-related scripts executed successfully.` (에러 없이 종료)

- [ ] **Step 6: MessageId 안정성 검증 (생존 ID 불변 + Batch 신규)**

Run:
```bash
cat /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime.Generated/Scripts/MessageIds.cs
```
Expected 확인 사항:
- `DamageEventToC = 3`, `AbilityActivatedToC = 15` **그대로 유지**(아직 @auto_generate 있으니 살아있어야 함).
- 기존 다른 ID(EntitySnapsToC=5, UserEntitySnapToC=13 등) 전부 불변.
- `WorldEventBatchToC` 항목이 **새로 추가**됨(다음 빈 번호, 예상 16 또는 1·2 등 스크립트가 고른 최소 빈 번호).
- `WorldEventToC`는 **없어야** 함(@auto_generate 없으니 top-level 아님).

만약 생존 ID가 하나라도 바뀌었으면 중단하고 원인 파악(스크립트 보존 로직 회귀).

- [ ] **Step 7: Unity에서 Shared 컴파일 확인**

UnityMCP `refresh_unity`(client 인스턴스) 후 `read_console`로 컴파일 에러 0 확인. `WorldEventToC.cs`, `WorldEventBatchToC.cs`, `WorldEventBatchToC.IMessage.cs`가 `Runtime.Generated/Scripts/Protobuf/`에 생성됐는지 확인.
Expected: 컴파일 에러 없음. (Damage/Ability는 여전히 IMessage라 기존 코드 무손상.)

- [ ] **Step 8: Commit (생성물 + .meta 포함)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Protos/WorldEventToC.proto Protos/WorldEventBatchToC.proto \
        Runtime.Generated/baegames.LOP.Shared.Generated.asmdef \
        Runtime.Generated/Scripts/
git commit -m "feat(wire): WorldEventToC oneof + WorldEventBatchToC 배치 패킷 추가

폴리모픽 envelope 스캐폴딩. Damage/Ability는 아직 top-level 유지
(@auto_generate 그대로) — 클·서 컴파일 무손상. Generated asmdef에
GameFramework.World 참조 추가(매퍼가 WorldEvent 타입 볼 수 있게)."
```

---

## Task 2: `WorldEventWire` 순수 매퍼 (LOP-Shared, TDD)

`WorldEvent` ↔ `WorldEventToC`(oneof) 순수 변환을 공유 static 매퍼로. 클·서가 같은 코드를 써 매핑 drift를 없애고 EditMode로 테스트한다.

**Files:**
- Create: `LeagueOfPhysical-Shared/Runtime.Generated/Scripts/WorldEventWire.cs`
- Test: `LeagueOfPhysical-Shared/Tests/EditMode/WorldEventWireTests.cs`

**Interfaces:**
- Consumes: Task 1의 `WorldEventToC`/`WorldEventBatchToC`; `GameFramework.World.WorldEvent`/`DamageDealtEvent`/`AbilityActivatedEvent`.
- Produces: `LOP.WorldEventWire.ToWire(GameFramework.World.WorldEvent) → WorldEventToC`(미지원 타입 null), `LOP.WorldEventWire.FromWire(WorldEventToC) → GameFramework.World.WorldEvent`(미인식 oneof null). Task 3(서버)·Task 4(클라)가 사용.

- [ ] **Step 1: 실패 테스트 작성**

Create `LeagueOfPhysical-Shared/Tests/EditMode/WorldEventWireTests.cs`:
```csharp
using NUnit.Framework;
using GameFramework.World;

namespace LOP.Tests
{
    public class WorldEventWireTests
    {
        // 매퍼가 모르는 WorldEvent — null 반환 검증용(테스트 로컬 더미)
        private sealed record UnmappedEvent : WorldEvent;

        [Test]
        public void ToWire_Damage_MapsToOneofAndFields()
        {
            var wire = WorldEventWire.ToWire(new DamageDealtEvent("t1", "a1", 30, isCritical: true, isDodged: false));
            Assert.That(wire.EventCase, Is.EqualTo(WorldEventToC.EventOneofCase.Damage));
            Assert.That(wire.Damage.TargetId, Is.EqualTo("t1"));
            Assert.That(wire.Damage.AttackerId, Is.EqualTo("a1"));
            Assert.That(wire.Damage.Damage, Is.EqualTo(30));
            Assert.That(wire.Damage.IsCritical, Is.True);
            Assert.That(wire.Damage.IsDodged, Is.False);
        }

        [Test]
        public void ToWire_Ability_MapsToOneofAndFields()
        {
            var wire = WorldEventWire.ToWire(new AbilityActivatedEvent("e1", 42));
            Assert.That(wire.EventCase, Is.EqualTo(WorldEventToC.EventOneofCase.AbilityActivated));
            Assert.That(wire.AbilityActivated.EntityId, Is.EqualTo("e1"));
            Assert.That(wire.AbilityActivated.AbilityId, Is.EqualTo(42));
        }

        [Test]
        public void ToWire_UnmappedEvent_ReturnsNull()
        {
            Assert.That(WorldEventWire.ToWire(new UnmappedEvent()), Is.Null);
        }

        [Test]
        public void FromWire_Damage_RoundTrips()
        {
            var wire = WorldEventWire.ToWire(new DamageDealtEvent("t1", "a1", 30, true, false));
            var e = (DamageDealtEvent)WorldEventWire.FromWire(wire);
            Assert.That(e.targetId, Is.EqualTo("t1"));
            Assert.That(e.attackerId, Is.EqualTo("a1"));
            Assert.That(e.amount, Is.EqualTo(30));
            Assert.That(e.isCritical, Is.True);
            Assert.That(e.isDodged, Is.False);
        }

        [Test]
        public void FromWire_Ability_RoundTrips()
        {
            var wire = WorldEventWire.ToWire(new AbilityActivatedEvent("e1", 42));
            var e = (AbilityActivatedEvent)WorldEventWire.FromWire(wire);
            Assert.That(e.entityId, Is.EqualTo("e1"));
            Assert.That(e.abilityId, Is.EqualTo(42));
        }

        [Test]
        public void FromWire_EmptyOneof_ReturnsNull()
        {
            Assert.That(WorldEventWire.FromWire(new WorldEventToC()), Is.Null);
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

UnityMCP `run_tests`(EditMode, 필터 `WorldEventWireTests`, client 인스턴스).
Expected: FAIL — `WorldEventWire` 타입 없음(컴파일 에러) 또는 미해결.

- [ ] **Step 3: 매퍼 구현**

Create `LeagueOfPhysical-Shared/Runtime.Generated/Scripts/WorldEventWire.cs`:
```csharp
using GameFramework.World;

// hand-written(생성물 아님). generate_protos.sh는 Protobuf/·MessageInitializer만 지우므로 이 파일은 보존됨.
namespace LOP
{
    /// <summary>
    /// 연출 WorldEvent ↔ 와이어(WorldEventToC oneof) 순수 변환. 클·서 공유 — 같은 매핑이라 drift 없음.
    /// 데미지/발동 같은 transient 연출만 다룬다(durable HP 등은 스냅샷 소관, 여기 없음).
    /// </summary>
    public static class WorldEventWire
    {
        /// <summary>연출 WorldEvent를 oneof 와이어 레코드로. 매핑 없는 타입은 null(서버가 무시).</summary>
        public static WorldEventToC ToWire(WorldEvent e)
        {
            switch (e)
            {
                case DamageDealtEvent d:
                    return new WorldEventToC
                    {
                        Damage = new DamageEventToC
                        {
                            AttackerId = d.attackerId,
                            TargetId   = d.targetId,
                            ActionCode = "attack",
                            DamageType = "physical",
                            Damage     = d.amount,
                            IsCritical = d.isCritical,
                            IsDodged   = d.isDodged,
                            IsBlocked  = false,
                        }
                    };
                case AbilityActivatedEvent a:
                    return new WorldEventToC
                    {
                        AbilityActivated = new AbilityActivatedToC
                        {
                            EntityId  = a.entityId,
                            AbilityId = a.abilityId,
                        }
                    };
                default:
                    return null;
            }
        }

        /// <summary>oneof 와이어 레코드를 연출 WorldEvent로. 미인식 case는 null(클라가 무시).</summary>
        public static WorldEvent FromWire(WorldEventToC rec)
        {
            switch (rec.EventCase)
            {
                case WorldEventToC.EventOneofCase.Damage:
                    return new DamageDealtEvent(
                        targetId:   rec.Damage.TargetId,
                        attackerId: rec.Damage.AttackerId,
                        amount:     (int)rec.Damage.Damage,
                        isCritical: rec.Damage.IsCritical,
                        isDodged:   rec.Damage.IsDodged);
                case WorldEventToC.EventOneofCase.AbilityActivated:
                    return new AbilityActivatedEvent(
                        rec.AbilityActivated.EntityId,
                        rec.AbilityActivated.AbilityId);
                default:
                    return null;
            }
        }
    }
}
```

- [ ] **Step 4: 테스트 통과 확인**

UnityMCP `run_tests`(EditMode, 필터 `WorldEventWireTests`, client 인스턴스).
Expected: 6 tests PASS.

- [ ] **Step 5: Commit (+.meta)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Runtime.Generated/Scripts/WorldEventWire.cs Runtime.Generated/Scripts/WorldEventWire.cs.meta \
        Tests/EditMode/WorldEventWireTests.cs Tests/EditMode/WorldEventWireTests.cs.meta
git commit -m "feat(wire): WorldEventWire 순수 매퍼 (WorldEvent <-> oneof) + EditMode 6"
```

---

## Task 3: 서버 sink → 배치 조립 (LOP-Server)

`WorldEventSink.Emit`이 이벤트마다 개별 패킷을 보내던 것을, 버퍼 전체를 `WorldEventBatchToC` 하나로 조립해 세션당 1회 송신하도록 바꾼다.

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/World/WorldEventSink.cs`

**Interfaces:**
- Consumes: `LOP.WorldEventWire.ToWire`(Task 2); `WorldEventBatchToC`(Task 1); `Runner.Time.tick`, `ISessionManager.GetAllSessions()`, `ISession.Send`.
- Produces: 와이어에 `WorldEventBatchToC` 송신(개념별 패킷 대신).

- [ ] **Step 1: 브랜치 생성 (Server)**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server && git checkout -b feature/world-event-batch-envelope
```
Expected: `Switched to a new branch 'feature/world-event-batch-envelope'`

- [ ] **Step 2: `WorldEventSink.Emit` 재작성**

Replace the whole body of `LeagueOfPhysical-Server/Assets/Scripts/World/WorldEventSink.cs` with:
```csharp
using GameFramework;
using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// WorldEventBuffer 스냅샷을 단일 폴리모픽 배치(WorldEventBatchToC)로 조립해 모든 세션에 1회 송출하는
    /// egress sink(서버). 코어 상태·새 이벤트 안 만듦. 개념별 패킷(DamageEventToC 등)은 배치 안의
    /// WorldEventToC(oneof) 레코드로 담긴다 — 새 WorldEvent 타입이 와이어 포맷을 흔들지 않음.
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
            var batch = new WorldEventBatchToC { Tick = Runner.Time.tick };
            foreach (var e in events)
            {
                var rec = WorldEventWire.ToWire(e);   // 매핑 없는 타입은 null → 무시
                if (rec != null)
                {
                    batch.Events.Add(rec);
                }
            }

            if (batch.Events.Count == 0)
            {
                return;   // 연출 이벤트 없는 틱은 송신 안 함
            }

            foreach (var session in _sessionManager.GetAllSessions())
            {
                session.Send(batch);   // 세션당 1패킷(기존 이벤트당 1패킷 → 배치)
            }
        }
    }
}
```

- [ ] **Step 3: 서버 컴파일 확인**

UnityMCP `refresh_unity` + `read_console`(server 인스턴스).
Expected: 컴파일 에러 없음. (`WorldEventBatchToC`/`WorldEventWire`가 Shared 패키지에서 보임.)

- [ ] **Step 4: Commit**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Server
git add Assets/Scripts/World/WorldEventSink.cs
git commit -m "refactor(wire): 서버 egress를 WorldEventBatchToC 단일 배치로

이벤트마다 개별 session.Send → 버퍼를 배치 하나로 조립 후 세션당 1회.
매핑은 공유 WorldEventWire.ToWire. 틱당 패킷 수 감소."
```

> 이 시점: 서버는 배치를 보내지만 클라는 아직 개념별 핸들러라 데미지/발동 연출이 표시 안 됨(Task 4에서 해소). 컴파일은 양쪽 정상. 플레이 회귀 검증은 Task 5(양쪽 완료 후).

---

## Task 4: 클라 ingress → 단일 배치 핸들러 (LOP-Client)

개념별 핸들러 2개를 단일 `GameWorldEventMessageHandler`로 통합하고, dispatcher/broker/scope 배선을 배치로 교체한다. ability self-skip(예측 중복 방지)을 보존한다.

**Files:**
- Create: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/Game.WorldEvent.MessageHandler.cs`
- Delete: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/Game.Damage.MessageHandler.cs` (+`.meta`)
- Delete: `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/Game.Ability.MessageHandler.cs` (+`.meta`)
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Messaging/NetworkMessageDispatcher.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/RootLifetimeScope.cs`
- Modify: `LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs`

**Interfaces:**
- Consumes: `WorldEventBatchToC`(Task 1), `LOP.WorldEventWire.FromWire`(Task 2), `GameFramework.World.WorldEventBuffer`, `IPlayerContext`, MessagePipe `ISubscriber<WorldEventBatchToC>`.
- Produces: `WorldEventBuffer.Append(WorldEvent)` (기존 두 핸들러와 동일한 최종 효과).

- [ ] **Step 1: 브랜치 확인 (Client — 이미 있음)**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client && git branch --show-current
```
Expected: `feature/world-event-batch-envelope` (이미 이 브랜치. 아니면 checkout.)

- [ ] **Step 2: 통합 핸들러 작성**

Create `LeagueOfPhysical-Client/Assets/Scripts/Game/MessageHandler/Game.WorldEvent.MessageHandler.cs`:
```csharp
using GameFramework;
using MessagePipe;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 서버 WorldEventBatchToC(연출 이벤트 배치)를 코어 연출 이벤트로 재수화하는 단일 어댑터(클라).
    /// oneof 레코드를 WorldEventWire.FromWire로 되돌려 WorldEventBuffer에 append.
    /// 개념별 핸들러(Damage/Ability)를 통합 — 새 WorldEvent 타입이 새 핸들러를 요구하지 않음.
    /// </summary>
    public class GameWorldEventMessageHandler : IGameMessageHandler
    {
        [Inject]
        private GameFramework.World.WorldEventBuffer worldEventBuffer;

        [Inject]
        private IPlayerContext playerContext;

        [Inject]
        private ISubscriber<WorldEventBatchToC> batchSubscriber;

        private System.IDisposable subscription;

        public void Initialize()
        {
            subscription = batchSubscriber.Subscribe(OnWorldEventBatchToC);
        }

        public void Dispose()
        {
            subscription?.Dispose();
        }

        private void OnWorldEventBatchToC(WorldEventBatchToC msg)
        {
            foreach (var rec in msg.Events)
            {
                var worldEvent = WorldEventWire.FromWire(rec);
                if (worldEvent == null)
                {
                    continue;
                }

                // 내 캐릭 발동은 로컬 예측이 이미 넣었으므로 서버 사본 skip(중복 방지). HP/죽음은 스냅샷 파생.
                if (worldEvent is GameFramework.World.AbilityActivatedEvent ability &&
                    playerContext.entity != null && ability.entityId == playerContext.entity.entityId)
                {
                    continue;
                }

                worldEventBuffer.Append(worldEvent);
            }
        }
    }
}
```

- [ ] **Step 3: 옛 핸들러 2개 삭제**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git rm Assets/Scripts/Game/MessageHandler/Game.Damage.MessageHandler.cs \
       Assets/Scripts/Game/MessageHandler/Game.Damage.MessageHandler.cs.meta \
       Assets/Scripts/Game/MessageHandler/Game.Ability.MessageHandler.cs \
       Assets/Scripts/Game/MessageHandler/Game.Ability.MessageHandler.cs.meta
```
Expected: 4개 파일 삭제 스테이징.

- [ ] **Step 4: `NetworkMessageDispatcher` 배선 교체**

In `LeagueOfPhysical-Client/Assets/Scripts/Messaging/NetworkMessageDispatcher.cs`:
- 생성자 파라미터 `IPublisher<DamageEventToC> damage,`와 `IPublisher<AbilityActivatedToC> ability,` **삭제**, 대신 `IPublisher<WorldEventBatchToC> worldEventBatch,` **추가**.
- 본문 `Register(damage);`·`Register(ability);` **삭제**, `Register(worldEventBatch);` **추가**.

교체 후 생성자·등록부(발췌):
```csharp
        [Inject]
        public NetworkMessageDispatcher(
            IPublisher<GameInfoToC> gameInfo,
            IPublisher<WorldEventBatchToC> worldEventBatch,
            IPublisher<EntitySnapsToC> snaps,
            IPublisher<EntitySpawnToC> spawn,
            IPublisher<EntityDespawnToC> despawn,
            IPublisher<UserEntitySnapToC> userSnap,
            IPublisher<StatAllocationToC> statAllocation,
            IPublisher<InputSequenceToC> inputSequence,
            IPublisher<InputTimingToC> inputTiming)
        {
            Register(gameInfo);
            Register(worldEventBatch);
            Register(snaps);
            Register(spawn);
            Register(despawn);
            Register(userSnap);
            Register(statAllocation);
            Register(inputSequence);
            Register(inputTiming);
        }
```

- [ ] **Step 5: `RootLifetimeScope` broker 등록 교체**

In `LeagueOfPhysical-Client/Assets/Scripts/RootLifetimeScope.cs` (네트워크 수신 블록):
- `builder.RegisterMessageBroker<DamageEventToC>(options);` **삭제**
- `builder.RegisterMessageBroker<AbilityActivatedToC>(options);` **삭제**
- 대신 `builder.RegisterMessageBroker<WorldEventBatchToC>(options);` **추가**(같은 블록, GameInfoToC 아래 등)

- [ ] **Step 6: `GameLifetimeScope` 엔트리포인트 교체**

In `LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs`:
- `builder.RegisterEntryPoint<GameDamageMessageHandler>();` **삭제**
- `builder.RegisterEntryPoint<GameAbilityMessageHandler>();` **삭제**
- 대신 `builder.RegisterEntryPoint<GameWorldEventMessageHandler>();` **추가**(같은 핸들러 등록 구역)

- [ ] **Step 7: 클라 컴파일 확인**

UnityMCP `refresh_unity` + `read_console`(client 인스턴스).
Expected: 컴파일 에러 없음. (Damage/Ability 타입은 아직 IMessage로 존재하나 클라에서 미참조 = 문제 없음.)

- [ ] **Step 8: Commit**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/MessageHandler/Game.WorldEvent.MessageHandler.cs \
        Assets/Scripts/Game/MessageHandler/Game.WorldEvent.MessageHandler.cs.meta \
        Assets/Scripts/Messaging/NetworkMessageDispatcher.cs \
        Assets/Scripts/RootLifetimeScope.cs \
        Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "refactor(wire): 클라 ingress를 단일 WorldEventBatch 핸들러로 통합

GameDamage/GameAbility 핸들러 2개 → GameWorldEventMessageHandler 1개
(oneof 순회 + WorldEventWire.FromWire). ability self-skip(예측 dedup) 보존.
dispatcher/broker/scope 배선을 WorldEventBatchToC로 교체."
```

---

## Task 5: Damage/Ability top-level 은퇴 + 회귀 검증 (LOP-Shared + 플레이)

이제 Damage/Ability를 top-level로 참조하는 코드가 없으니, `@auto_generate`를 떼서 nested-only(oneof 변형)로 만들고 재생성한다. 그 후 양쪽 저장소 컴파일 + 플레이 회귀 검증.

**Files:**
- Modify: `LeagueOfPhysical-Shared/Protos/DamageEventToC.proto`, `Protos/AbilityActivatedToC.proto`
- Regenerate: `Runtime.Generated/Scripts/*` (Damage/Ability의 `.IMessage.cs` 삭제, MessageIds/MessageInitializer 갱신)

**Interfaces:**
- Produces: `DamageEventToC`/`AbilityActivatedToC`가 proto 메시지로만 남고 `IMessage`·MessageId·creator에서 빠짐.

- [ ] **Step 1: 두 proto에서 `@auto_generate` 제거**

`LeagueOfPhysical-Shared/Protos/DamageEventToC.proto` — 첫 줄들의 `// @auto_generate` 주석 삭제:
```proto
syntax = "proto3";

message DamageEventToC
{
	int64 tick = 1;
	string attacker_id = 2;
	string target_id = 3;
	string action_code = 4;
	string damage_type = 5;
	int64 damage = 6;
	bool is_critical = 7;
	bool is_dodged = 8;
	bool is_blocked = 9;
}
```
`LeagueOfPhysical-Shared/Protos/AbilityActivatedToC.proto` — 동일하게 `// @auto_generate` 삭제:
```proto
syntax = "proto3";

message AbilityActivatedToC
{
	string entity_id = 1;
	int32 ability_id = 2;
}
```
(메시지 본문·필드 번호는 유지 — oneof 변형으로 계속 쓰이므로 wire 바이너리 호환.)

- [ ] **Step 2: 재생성**

Run:
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Scripts && ./generate_protos.sh
```
Expected: 성공 종료.

- [ ] **Step 3: 남은 `.IMessage.cs` 정리 확인**

`generate_imessage.sh`는 파일을 새로 만들지만 옛 `.IMessage.cs`를 지우지 않을 수 있다. 확인/삭제:
```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime.Generated/Scripts/Protobuf
ls DamageEventToC.IMessage.cs AbilityActivatedToC.IMessage.cs 2>/dev/null
```
남아 있으면 삭제(+.meta):
```bash
git rm DamageEventToC.IMessage.cs DamageEventToC.IMessage.cs.meta \
       AbilityActivatedToC.IMessage.cs AbilityActivatedToC.IMessage.cs.meta 2>/dev/null || \
  rm -f DamageEventToC.IMessage.cs DamageEventToC.IMessage.cs.meta \
        AbilityActivatedToC.IMessage.cs AbilityActivatedToC.IMessage.cs.meta
```

- [ ] **Step 4: MessageId 검증 (Damage/Ability 은퇴, 나머지 불변, Batch 유지)**

Run:
```bash
cat /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared/Runtime.Generated/Scripts/MessageIds.cs
```
Expected:
- `DamageEventToC`·`AbilityActivatedToC` 항목 **사라짐**.
- `WorldEventBatchToC` **유지**(Task 1에서 받은 ID 그대로).
- EntitySnapsToC=5, UserEntitySnapToC=13 등 **다른 생존 ID 전부 불변**.
- `MessageInitializer.cs`에 Damage/Ability creator 없음, `WorldEventBatchToC` creator 있음.

- [ ] **Step 5: 양쪽 저장소 재스캔 + 컴파일 (CS2001 방지)**

패키지 .cs 삭제라 클·서에 stale `CS2001`이 뜰 수 있다(메모리 `deleting-package-files-cs2001`). UnityMCP `refresh_unity`를 client·server 각 인스턴스에 대해 `force`로 실행 후 `read_console` 확인.
Expected: 클·서 모두 컴파일 에러 0.

- [ ] **Step 6: EditMode 재확인 (Shared)**

UnityMCP `run_tests`(EditMode, client 인스턴스). `WorldEventWireTests` 포함 전체 green.
Expected: 회귀 없음(매퍼는 proto 메시지 자체는 그대로라 무영향).

- [ ] **Step 7: 플레이 회귀 검증 (클·서 동시)**

클라+서버 에디터를 Play. 공격으로 다음이 **종전과 동일하게** 표시되는지 육안 확인:
- 데미지 숫자 뜸(피격자 위), 크리·회피 표시 정상.
- 어빌리티 발동 애니(cue 있는 것)가 남 캐릭터에서 재생, 내 캐릭은 예측 1회만(중복 없음).
- `read_console`(양쪽)에 `[NetworkMessageDispatcher] 미등록 메시지 타입` 경고 없음.
Expected: 회귀 없음.

- [ ] **Step 8: Commit (Shared)**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Shared
git add Protos/DamageEventToC.proto Protos/AbilityActivatedToC.proto Runtime.Generated/Scripts/
git commit -m "refactor(wire): Damage/Ability top-level 은퇴 → nested-only(oneof 변형)

@auto_generate 제거 → MessageId/IMessage/creator에서 빠짐. 메시지 본문·
필드 번호 유지(oneof 변형으로 계속 사용, 바이너리 호환). 와이어 연출 패킷은
이제 WorldEventBatchToC 단일 envelope 하나뿐."
```

---

## Task 6: 문서 상태 갱신 (LOP-Client)

ROADMAP #7을 완료 처리한다.

**Files:**
- Modify: `LeagueOfPhysical-Client/docs/ROADMAP.md`

- [ ] **Step 1: ROADMAP Tier-2/3 줄에서 #7 제거/완료 반영**

`docs/ROADMAP.md`의 `**Tier-2/3 (별도 슬라이스):**` 줄에서 `#7 WorldEventBatch 단일 envelope 미구현(개념별 패킷 DamageEventToC 등)` 항목을 제거하고, 위 완료 원장(구조 정리 백로그 목록)에 완료 항목 한 줄 추가:
```markdown
- ✅ **#7 WorldEventBatch 단일 envelope** (07-17) — 개념별 top-level 패킷(DamageEventToC/AbilityActivatedToC)을 단일 폴리모픽 WorldEventBatchToC(oneof)로 통합. 서버=배치 1회 송신, 클라=단일 핸들러(WorldEventWire.FromWire), 변형 2개는 nested-only 은퇴. 새 WorldEvent 타입이 MessageId/dispatcher/핸들러를 새로 안 요구. 공유 매퍼 EditMode 6. spec/plan `2026-07-17-world-event-batch-envelope*`.
```

- [ ] **Step 2: Commit**

```bash
cd /c/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add docs/ROADMAP.md
git commit -m "docs(roadmap): #7 WorldEvent 단일 envelope 완료 반영"
```

---

## 통합 / 머지 (전 태스크 후)

3개 저장소 각 피처 브랜치를 `--no-ff`로 main에 머지(순서: Shared → Server → Client, Shared가 wire 계약 소유). 각 저장소에서:
```bash
git checkout main && git merge --no-ff feature/world-event-batch-envelope && git push origin main
git branch -d feature/world-event-batch-envelope && git push origin --delete feature/world-event-batch-envelope
```
(spec의 `finishing-a-development-branch` 흐름은 사용자 확인 후.)

---

## 완료 기준 (Definition of Done)

- [ ] `WorldEventWire` EditMode 6 tests green.
- [ ] 와이어에 개념별 연출 패킷(DamageEventToC/AbilityActivatedToC) top-level 송신 0 — 배치 하나로 통합.
- [ ] `MessageIds.cs`: Damage/Ability 은퇴, Batch 추가, 나머지 생존 ID 불변.
- [ ] 클·서 클린 컴파일(CS2001 없음).
- [ ] 플레이 회귀 없음: 데미지 숫자·크리·회피·발동 애니 종전과 동일, self-skip 유지.
- [ ] ROADMAP #7 완료 반영.
