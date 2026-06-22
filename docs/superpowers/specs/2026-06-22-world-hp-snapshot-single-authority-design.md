# HP 스냅샷 단일권위 — 중복 event-apply 제거 (cleanup backlog #1 + #3)

**Date:** 2026-06-22
**Branch:** `feature/world-hp-snapshot-single-authority`
**Related:** [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [Netcode 재설계](../../netcode-redesign.md)

## Goal

HP의 진실원본(authority)을 **스냅샷 한 경로**로 단일화한다. 서버·클라가 데미지 이벤트로 HP를 *또* 쓰던 중복 apply를 제거하고, 그로 인해 호출처가 사라지는 Application 코드(`WorldEventApplicator`, `HealthSystem.ApplyDamageDealt`)를 삭제한다.

`world-core-connection-architecture.md`의 "알려진 괴리(cleanup backlog)" 중 **#1 이중 apply**와 **#3 이중 HP 경로**를 해소한다.

## 배경 — durable 값은 스냅샷, 이벤트는 연출만

업계 표준(Overwatch/Source/Fiedler)과 우리 문서가 이미 못박은 와이어 규칙:

- **durable 값**(잃으면 영구 desync — HP·Transform·MP·Stats) → **스냅샷(절대값)** 으로만 복제. 패킷이 유실돼도 다음 스냅샷이 올바른 절대값을 들고 와 자가보정.
- **transient 연출**(데미지 숫자·크리/회피·사망 연출) → **이벤트**(fire-and-forget, 클라가 *상태로 안 씀*).

HP는 durable이다. 따라서 HP는 스냅샷에만 실려야 하고, 이벤트는 "−40 숫자 띄워라" 같은 연출만 날라야 한다. **HP를 이벤트로 보내면 유실 시 HP가 영구히 어긋난다 — 모델이 금지하는 바로 그것.**

### 현재 코드가 어긋난 지점

한 번의 공격이 흐르는 실제 경로:

**서버**
- 전투(resolve = generation): `LOPCombatSystem.TakeDamage`가 권위 HP를 깎고 `DamageDealtEvent`를 버퍼에 쌓음 ✅
- `LOPGameEngine.ProcessEvent`:
  - `worldEventApplicator.Apply` → HP를 `remaining`으로 **또 씀** 🔴 #1
  - `eventSink.Emit` → `DamageEventToC`에 `RemainingHP`·`IsDead` 실어 전송 🔴 #3
  - `reactor.React` → death cascade
- EndUpdate: `UserEntitySnapToC.CurrentHP`(durable 스냅샷) 전송 ✅

**클라**
- `Game.Damage.MessageHandler` → `DamageEventToC`를 `DamageDealtEvent`(+`remaining`)로 변환해 버퍼에 append 🔴 #3
- `LOPGameEngine.ProcessEvent`:
  - `worldEventApplicator.Apply` → 클라 `World.Health`에 `remaining` 씀 🔴 #3
  - `eventSink.Emit` → `EntityDamage` 팬아웃 → 숫자 팝업 ✅ + HP바/HUD
- `Game.Entity.MessageHandler` → `HealthSystem.ApplyAuthoritativeState`로 스냅샷에서 `World.Health` 씀 ✅ (옳은 경로)

→ 클라 `World.Health`가 **이벤트와 스냅샷 두 군데서** 써진다. 이게 이중 HP 경로.

## 결정 (브레인스토밍 합의)

| 축 | 결정 | 비고 |
|---|---|---|
| 범위 | **scope A — 모델만 단일화** | UI를 모델로 옮기고 이벤트에서 HP 흔적 완전 제거(scope B)는 다음 슬라이스 |
| 고아 Application 코드 | **완전 삭제** | `WorldEventApplicator` + 테스트 + `HealthSystem.ApplyDamageDealt` + 테스트. Stage④에서 새 모양으로 복귀 |
| 죽음 | **안 건드림** | despawn은 이미 스냅샷 파생, 사망 연출은 이벤트 `isDead` |
| `DamageEventToC`/`EntityDamage` | **유지** | UI가 계속 이 이벤트로 굴러감(연출). HP 권위만 아님 |

## 변경 사항

### 서버 (LeagueOfPhysical-Server)

1. `Assets/Scripts/Game/LOPGameEngine.cs`
   - `[Inject] WorldEventApplicator worldEventApplicator;` 필드 삭제
   - `ProcessEvent()`에서 `worldEventApplicator.Apply(snapshot);` 삭제
   - 결과: `ProcessEvent` = `eventSink.Emit(snapshot)` + `reactor.React(snapshot)` + `worldEventBuffer.Clear()`
2. `Assets/Scripts/Game/GameLifetimeScope.cs`
   - `builder.Register<GameFramework.World.WorldEventApplicator>(Lifetime.Singleton);` 삭제
3. `Assets/Scripts/CombatSystem/LOPCombatSystem.cs`
   - HP 흐름 주석(`WorldEventApplicator(ProcessEvent)가 ...`)을 stale하지 않게 정리: generation이 HP를 mutate하고 스냅샷이 권위임을 반영

### 클라 (LeagueOfPhysical-Client)

4. `Assets/Scripts/Game/LOPGameEngine.cs`
   - `[Inject] WorldEventApplicator worldEventApplicator;` 필드 삭제
   - `ProcessEvent()`에서 `worldEventApplicator.Apply(snapshot);` 삭제
   - 결과: `ProcessEvent` = `eventSink.Emit(snapshot)` + `worldEventBuffer.Clear()`
5. `Assets/Scripts/Game/GameLifetimeScope.cs`
   - `builder.Register<GameFramework.World.WorldEventApplicator>(Lifetime.Singleton);` 삭제

### GameFramework

6. `Runtime/Scripts/World/Systems/WorldEventApplicator.cs` (+ `.meta`) 삭제
7. `Tests/World/WorldEventApplicatorTests.cs` (+ `.meta`) 삭제
8. `Runtime/Scripts/World/Systems/HealthSystem.cs` — `ApplyDamageDealt(Health, DamageDealtEvent)` 메서드 삭제. **`ApplyAuthoritativeState`는 유지**(스냅샷 경로)
9. `Tests/World/HealthSystemTests.cs` — `ApplyDamageDealt_*` 테스트 3개 삭제

### 문서

10. `docs/world-core-connection-architecture.md`
    - "알려진 괴리(cleanup backlog)" 섹션에서 **#1·#3 해소** 표시
    - Application 코드는 **Stage④(Death 컴포넌트·클라 예측-apply)에서 새 모양으로 복귀** 한 줄

## 일부러 안 바꾸는 것 (기대치 조율)

- **`DamageEventToC` / `EntityDamage` 이벤트** — 그대로. 숫자 팝업·크리·HP바·HUD가 계속 이 이벤트로 갱신. UI를 스냅샷 모델로 옮기는 것은 scope B.
- **죽음/despawn** — 안 건드림. despawn은 이미 스냅샷 파생(엔티티가 스냅샷에서 사라짐 → 클라 뷰가 치움). 사망 연출은 이벤트 `isDead`.
- **백로그 #2 (despawn이 fan-out에)** — 그대로. 서버 `LOPGame.HandleDeath`가 reactor 경로에 있는 건 별도 슬라이스.

## 알려진 경계 (솔직하게)

- 이 슬라이스가 단일화하는 건 **모델(`World.Health`)** 이다. 모델은 이제 스냅샷만 쓴다(로컬 플레이어 = `UserEntitySnap`). UI는 아직 이벤트로 굴러가므로 **화면 동작은 그대로** — 눈에 보이는 변화 0, 구조만 깨끗해짐.
- 원격 엔티티의 `World.Health` 모델은 이제 라이브로 안 먹여진다(현재 스냅샷에 원격 HP가 없음). 하지만 **아무도 그 모델을 라이브로 읽지 않음**(HP바도 이벤트 사용) → 비가시. "원격 HP까지 스냅샷에 싣기"는 durable→스냅샷 push의 후속 거리.

## 검증

삭제 슬라이스라 새 테스트는 추가하지 않는다(삭제된 테스트만). 플레이 검증:

1. 데미지 시 숫자 팝업이 뜬다
2. HP바·HUD가 줄어든다
3. 죽으면 엔티티가 사라지고 사망 연출이 난다
4. 내 캐릭터 HP가 정상 표시된다

**성공 기준 = 동작이 이전과 동일** (구조만 바뀜). 양쪽 에디터 컴파일 통과 + GameFramework EditMode 테스트(남은 것) 통과.

## Out of Scope

- **scope B** — HP UI를 스냅샷-fed 모델로 옮기고, 와이어 이벤트에서 `RemainingHP`/`IsDead` 제거(순수 연출화). "HP 변경" 프레젠테이션 신호 메커니즘 설계 필요(World 코어는 프로퍼티별 옵저버를 안 둠).
- **백로그 #2** — death→despawn cascade를 fan-out에서 generation으로 이전.
- **원격 엔티티 HP 스냅샷화** — durable→스냅샷 push의 별도 거리.

## 산업 표준 매핑

- **durable=snapshot 단일권위**: Overwatch ECS 컴포넌트 스냅샷 / Source entity-state / Fiedler state synchronization. HP처럼 "유실 시 영구 desync"되는 값은 절대상태로만 복제.
- **이벤트=연출**: fire-and-forget 프레젠테이션 힌트(서버만 아는 attribution 운반). 클라가 상태로 쓰지 않음.
- 이 슬라이스는 "도메인 이벤트로 durable 상태 전송"이라는 비전형 경로(현 `DamageDealtEvent.remaining` → HP)를 제거해 표준에 맞춘다.

## Progress

- [x] 브레인스토밍 합의 (scope A, Application 코드 삭제, death 무변경)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
