# WorldEvent 단일 폴리모픽 envelope 통합 (#7)

**Date:** 2026-07-17
**Branch (제안):** `feature/world-event-batch-envelope`
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md)(§와이어 추상·와이어 envelope) · [LOP 저장소 토폴로지](../../lop-repo-topology.md) · [ROADMAP #7](../../ROADMAP.md)

## Goal

`WorldEvent` egress 경로를 타는 **개념별 top-level 와이어 패킷**(`DamageEventToC`,
`AbilityActivatedToC`)을 **단일 폴리모픽 Mirror 메시지 `WorldEventBatchToC`** 하나로 통합한다.
이후 새 `WorldEvent` 타입을 추가해도 **MessageId / dispatcher / broker 등록 / 클라 핸들러를 새로
만들지 않는다** — oneof 변형 한 줄 + 재생성이면 끝.

연결 아키텍처 문서(`world-core-connection-architecture.md`)가 잠근 결정의 구현이다:

> **와이어 envelope** (`WorldEventBatch`): 서버↔클라 이벤트 전송은 단일 폴리모픽 Mirror 메시지에
> 여러 `WorldEvent` 레코드를 담아 보낸다. 개념별 패킷 타입 (`DamageEventToC`, `DeathEventToC` 등)을
> 와이어에 박지 않음. 새 이벤트 타입 추가 시 와이어 포맷이 흔들리지 않음.

## 배경 — 현재 구조

"WorldEvent 이벤트 → 와이어" 경로를 타는 개념별 패킷은 **정확히 2개**다:

```
서버 WorldEventSink                       클라 (개념별 핸들러 2개)
────────────────────────────             ────────────────────────────────────────
DamageDealtEvent
  → DamageEventToC ────session.Send────→ GameDamageMessageHandler
                                            → WorldEventBuffer.Append(DamageDealtEvent)
AbilityActivatedEvent
  → AbilityActivatedToC ──session.Send──→ GameAbilityMessageHandler
                                            → WorldEventBuffer.Append(AbilityActivatedEvent)
```

**새 이벤트 타입 하나 추가 시 지금 드는 비용(= #7이 없애려는 것):**
1. 새 `.proto` + 재생성
2. 새 `MessageId`
3. `MessageInitializer`에 creator 등록
4. `NetworkMessageDispatcher` 생성자에 `IPublisher<T>` 추가
5. `RootLifetimeScope`에 `RegisterMessageBroker<T>`
6. 서버 `WorldEventSink`에 `session.Send`
7. 클라에 새 메시지 핸들러

통합 후에는 2~5가 **한 번만**(WorldEventBatchToC 등록) 끝나고, 새 이벤트는 proto oneof 한 줄 +
서버 sink 조립 한 case + 클라 배치 핸들러 한 case만 는다.

**경로가 다른 것(범위 밖 — 그대로 둠):**
- `EntityDespawnToC` — `LOPEntityManager`에서 직접 송신(구조적 despawn, 생명주기). 설계상 죽음
  wire = despawn + HP 스냅샷. 이벤트 fan-out이 아님.
- `EntitySnapsToC` / `UserEntitySnapToC` — 상태 스냅샷.
- `EntitySpawnToC` — 구조적 spawn.

## 설계 결정 (브레인스토밍 합의)

| 축 | 결정 | 근거 |
|---|---|---|
| envelope 범위 | **이벤트 경로만** (Damage + AbilityActivated) | 아키텍처 문서의 "WorldEvent fan-out" 정의와 정확히 일치, 경계 깔끔. despawn/snap은 개념이 다름 |
| proto 폴리모피즘 | **oneof** (`WorldEventToC { oneof event }`) | proto3 정석 폴리모픽. 타입안전·스키마 검증. Any=IL2CPP 리스크·과임, tag+bytes=스키마 상실·수기 보일러플레이트 |
| 변형 메시지 처리 | `DamageEventToC`/`AbilityActivatedToC`는 proto로 남되 `@auto_generate` **제거** → nested-only | top-level 패킷(MessageId/IMessage/creator)에서 빠짐. 기존 필드 구조는 유지 |
| 클라 핸들러 | 2개(Damage/Ability) → **1개**(`GameWorldEventMessageHandler`) | 배치 하나 구독 + oneof 순회. ability self-skip(예측 dedup) per-record 보존 |
| 배치 tick | 배치 레벨 `int64 tick` 1개 (EntitySnapsToC 동형) | DamageEventToC의 개별 tick 승격. 현재 클라 미소비(연출 전용)이나 Stage④ tick-stamp 방향과 정렬 |
| 전송 채널/신뢰성 | **현행 유지** | reliability 튜닝은 이 슬라이스 밖(#4 계열) |

## 와이어 포맷 (proto)

```proto
// WorldEventToC.proto — 폴리모픽 래퍼 (top-level 아님, @auto_generate 없음)
syntax = "proto3";
import "DamageEventToC.proto";
import "AbilityActivatedToC.proto";

message WorldEventToC {
  oneof event {
    DamageEventToC      damage            = 1;
    AbilityActivatedToC ability_activated = 2;
    // 새 WorldEvent 타입 = 여기 한 줄 추가
  }
}
```

```proto
// WorldEventBatchToC.proto — 유일한 top-level 패킷 (@auto_generate)
syntax = "proto3";
import "WorldEventToC.proto";

// @auto_generate
message WorldEventBatchToC {
  int64 tick = 1;                 // 배치당 1 (EntitySnapsToC와 동형)
  repeated WorldEventToC events = 2;
}
```

- `DamageEventToC.proto` / `AbilityActivatedToC.proto`: `// @auto_generate` 주석 **제거**(메시지
  본문은 유지). 재생성 시 이 둘은 MessageId/IMessage/creator를 더 이상 받지 않는다.
- `WorldEventBatchToC`만 `@auto_generate` → 새 MessageId 1개.

## 서버 egress — `WorldEventSink.Emit`

지금은 이벤트마다 `session.Send(개별패킷)`. 바뀌면 **버퍼 전체를 한 번 순회해 배치 하나를 조립하고,
세션당 1회 Send**:

```
Emit(events):
  batch = new WorldEventBatchToC { Tick = Runner.Time.tick }
  foreach e in events:
    switch e:
      DamageDealtEvent      → batch.Events.Add(new WorldEventToC { Damage = <기존 매핑> })
      AbilityActivatedEvent → batch.Events.Add(new WorldEventToC { AbilityActivated = <기존 매핑> })
      // 미지원 타입은 무시(현행과 동일)
  if batch.Events.Count > 0:
    foreach session in _sessionManager.GetAllSessions():
      session.Send(batch)          // 세션당 N패킷 → 1패킷
```

- `DamageEventToC` 필드 매핑(ActionCode="attack", DamageType="physical" 등)은 기존 그대로 옮긴다.
- 부수 이점: 틱당 여러 이벤트가 1 패킷으로 → 패킷 수 감소.

## 클라 ingress — 핸들러 통합

`GameDamageMessageHandler` + `GameAbilityMessageHandler` **2개 삭제**, `GameWorldEventMessageHandler`
**1개 신설**. `WorldEventBatchToC` 하나만 구독하고 oneof를 순회하며 `WorldEventBuffer.Append`:

```
OnWorldEventBatchToC(msg):
  foreach rec in msg.Events:
    switch rec.EventCase:
      Damage:
        worldEventBuffer.Append(new DamageDealtEvent(
          targetId, attackerId, amount, isCritical, isDodged))   // 기존 Damage 핸들러 그대로
      AbilityActivated:
        if playerContext.entity != null && rec.AbilityActivated.EntityId == playerContext.entity.entityId:
          continue;   // 내 캐릭은 로컬 예측이 이미 넣음 → 서버 사본 skip(예측 dedup 보존)
        worldEventBuffer.Append(new AbilityActivatedEvent(entityId, abilityId))
```

- 두 핸들러의 변환 로직을 그대로 옮기고, **ability self-skip(예측 중복 방지)** 을 per-record로 유지.

## 메시지 인프라 변경 (한 번만)

| 위치 | 변경 |
|---|---|
| `MessageIds.cs` (생성) | `DamageEventToC`(3)·`AbilityActivatedToC`(15) 사라짐, `WorldEventBatchToC` 신규 ID |
| `MessageInitializer.cs` (생성) | Damage/Ability creator 제거, `WorldEventBatchToC` 추가 |
| `NetworkMessageDispatcher` (클라) | `IPublisher<DamageEventToC>`·`<AbilityActivatedToC>` 제거, `<WorldEventBatchToC>` 추가 |
| `RootLifetimeScope` (클라) | `RegisterMessageBroker<DamageEventToC>`·`<AbilityActivatedToC>` 제거, `<WorldEventBatchToC>` 추가 |
| `GameLifetimeScope` (클라) | 엔트리포인트 `GameDamageMessageHandler`·`GameAbilityMessageHandler` → `GameWorldEventMessageHandler` |

## 마이그레이션 / gen 스크립트 주의

- proto 재생성은 `LeagueOfPhysical-Shared/Scripts/generate_protos.sh`(부모 오케스트레이터). MessageIds
  보존 로직이 작동함(부모 스크립트의 `rm MessageIds.cs`는 #5에서 제거됨 — 파일을 읽어 기존 ID 보존).
  재생성 후 **생존 메시지 ID 불변 diff-verify**(메모리 `proto-message-id-regen-gotcha` 관례).
- gen 스크립트가 `@auto_generate` 붙은 message에만 MessageId/IMessage/creator를 만든다. Damage/Ability에서
  주석을 떼면 자동으로 top-level에서 빠진다. `WorldEventBatchToC`가 새 top-level 하나.
- **IL2CPP**: `RegisterMessageBroker`는 top-level 수신 메시지만 필요 → Damage/Ability 제거 + Batch 추가.
- **클·서 `file:` 패키지 동시 배포**라 wire 브레이킹 변경 무해(버전 스큐 없음). 이 슬라이스가 wire를
  바꾸는 게 목적이므로 Damage/Ability MessageId 은퇴는 의도된 변경.

## 영향 받는 파일 (touchpoint — 구현 plan에서 단계화)

**LOP-Shared:**
- 신규 proto: `Protos/WorldEventToC.proto`, `Protos/WorldEventBatchToC.proto`
- 수정 proto: `Protos/DamageEventToC.proto`, `Protos/AbilityActivatedToC.proto` (`@auto_generate` 제거)
- 재생성 산출물: `Runtime.Generated/Scripts/Protobuf/*`, `MessageIds.cs`, `MessageInitializer.cs`

**LOP-Server:**
- `Assets/Scripts/World/WorldEventSink.cs` — 배치 조립 + 세션당 1 Send

**LOP-Client:**
- 신규: `Assets/Scripts/Game/MessageHandler/Game.WorldEvent.MessageHandler.cs`
- 삭제: `Game.Damage.MessageHandler.cs`, `Game.Ability.MessageHandler.cs`
- 수정: `Messaging/NetworkMessageDispatcher.cs`, `RootLifetimeScope.cs`, `Game/GameLifetimeScope.cs`

## 테스트

- **서버 EditMode**: `WorldEventSink.Emit`이 N 이벤트 → 1 배치(올바른 oneof 매핑, 빈 배치 미송신).
  세션 mock로 Send 호출 횟수·페이로드 검증.
- **클라**: 배치 → `WorldEventBuffer`에 올바른 `WorldEvent` N개 append + ability self-skip.
  클라 코드는 Assembly-CSharp라 EditMode 제약(메모리 `client-test-infra-constraint`) → **순수 변환
  로직(배치→WorldEvent 리스트)을 테스트 가능한 형태로 분리**하거나 PlayMode 리플렉션.
- **플레이 검증**: 데미지 숫자·크리·회피·발동 애니가 종전과 동일하게 뜨는지(회귀 없음).

## Out of Scope

- despawn / snapshot / spawn 통합 — 이벤트 fan-out 경로 아님.
- 전송 reliability 변경(#4 계열).
- `WorldEvent`에 tick-stamp / source-tag(Predicted/Confirmed) 추가 — Stage④.
- 새 `WorldEvent` 타입(DeathEvent 와이어화, Buff 등) 실제 추가 — 콘텐츠 착수 시. 이 슬라이스는
  **기존 2개의 통합만**.

## 진행

- [x] 브레인스토밍 합의 (범위=이벤트 경로만, proto=oneof, 변형 nested-only, 핸들러 2→1)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
