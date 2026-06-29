# 어빌리티 발동 연출 와이어 — GameplayCue (페이즈 머신 slice 4)

**Date:** 2026-06-29
**Branch (제안):** `feature/ability-activation-cue` (GameFramework / LOP-Shared / infrastructure / MasterData-Client·Server / Client / Server)
**Related:** [어빌리티 페이즈 머신](2026-06-27-ability-phase-machine-design.md) (slice 4 = 이 문서) · [behavior 조합 B](2026-06-28-ability-behavior-composition-design.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (연출=event, durable=snapshot) · [아키텍처 가이드라인](../../architecture-guidelines.md) · 메모리 [[ability-statuseffect-world-core]]

## Goal

어빌리티 **발동 시 연출(애니/VFX)** 을 클라가 재생하도록 잇는다. B1에서 공격을 `abilityId=3`로 옮기며 레거시 연쇄(`ActionStartToC → ActionStart → LOPEntityView.OnActionStart → Animator.SetTrigger("Attack")`)가 끊겨, **데미지는 들어가는데 스윙 애니가 안 나온다.** 이 슬라이스가 그 연출을 **어빌리티 기반 GameplayCue**로 복원한다.

## 범위

- **바꿈**: 어빌리티 발동 → 연출 이벤트(`AbilityActivatedEvent`) → 클라가 애니 재생. 레거시 `ActionStartToC` 연출 경로를 어빌리티 경로로 대체(레거시 코드 자체 은퇴는 slice 5).
- **유지**: 데미지/사망 연출(`DamageDealtEvent` = 피격자 측 숫자/Hit)은 이미 동작 — 그대로. 이 슬라이스는 **시전자 측 발동 연출**만 추가.
- **Out of scope**: AI 발동 연출(slice 5/별도), VFX 파티클(애니 트리거만; 같은 cue 경로로 후속 확장), 발동 외 cue(히트 cue·종료 cue), 캐릭터별 로드아웃.

## 산업 표준 매핑 (GameplayCue)

| 개념 | 표준 | 우리 |
|---|---|---|
| 연출 단위 | Unreal GAS **GameplayCue** (cosmetic 전용, fire-and-forget, 서버→클라 복제) | `AbilityActivatedEvent`(연출 전용 `WorldEvent`) |
| durable vs cosmetic | durable=복제 상태(snapshot) / cosmetic=cue | HP=snapshot / 발동연출=event (연결 아키텍처 정합) |
| 내 캐릭 예측 | GAS local-predicted cue | 클라 로컬 발동 시 즉시 재생 + 서버 이벤트는 자기 스킵 |
| cue 식별 | GameplayTag (ability와 분리) | abilityId 운반 → 클라가 cue 해석(향후 tag 일반화) |

> 기존 `DamageDealtEvent → DamageEventToC` 연출 와이어와 **동일 몰드**. (연결 아키텍처가 권장하는 단일 폴리모픽 `WorldEventBatch` 봉투는 코드에 아직 없음 — per-event `*ToC`가 현 코드베이스 패턴이라 그것을 추종. 단일 봉투화는 별도 후속 정리.)

## 설계 (locked)

### A. 연출 이벤트 + 와이어
- 신규 `AbilityActivatedEvent(string entityId, int abilityId)` — `GameFramework.World.Events`(`DamageDealtEvent`/`DeathEvent` 옆, 데이터 전용 record).
- 신규 proto `AbilityActivatedToC { string entity_id = 1; int32 ability_id = 2; }` (`// @auto_generate`). MessageId는 다음 빈 정수(현재 14 다음). ⚠️ **MessageId regen 주의**(메모리 [[proto-message-id-regen-gotcha]]): 마스터 `generate_protos.sh`는 `MessageIds.cs`를 지워 재번호 → **서브 스크립트 개별 실행 + 기존 id 불변 diff 검증**.
- 흐름(= `DamageDealtEvent` 그대로): 발동 → `WorldEventBuffer.Append` → `WorldEventSink`(서버: `AbilityActivatedToC` 브로드캐스트 / 클라: EventBus 발행) → `LOPEntityView` 애니.

### B. 발화 시점 = 발동 순간(Startup 시작)
- 윈드업부터 애니가 돌고 타격은 스윙 중간(Active)에 꽂힘 = 레거시(`Duration*0.5` 타격)와 동일.
- 서버: `LOPRunner.ProcessInput`에서 `AbilityActivator.TryActivate` **성공 시** `worldEventBuffer.Append(AbilityActivatedEvent)`. (runner가 버퍼 보유 — `LOPCombatSystem`가 `DamageDealtEvent` 넣는 것과 동일 책임 배치.)
- `AbilityActivator.TryActivate`가 **bool 반환**하도록(현재 코어 `AbilitySystem.TryActivate`가 이미 bool — 표면화만).

### C. 내 캐릭 예측 / 남 서버권위
- **내 캐릭(예측)**: 클라 `PlayerInputManager.ApplyInput`이 이미 로컬 발동(`AbilityActivator.TryActivate`) → 성공 시 **클라 `WorldEventBuffer`에 직접 append** → 즉시 애니(반응성).
- **남(서버권위)**: 서버가 `AbilityActivatedToC` 브로드캐스트 → 클라 메시지 핸들러가 **자기 엔티티면 스킵**(레거시 `OnActionStartToC`와 동일) → 클라 버퍼 append.
- 둘 다 **같은 클라 `WorldEventSink` fan-out**으로 수렴(예측/수신 출처만 다름, 처리 동일).

### D. abilityId → 애니 매핑 = 클라 전용 마스터데이터 cue 컬럼
- `TbAbility`에 **client-only(group `c`) `cue` 컬럼** 추가. attack(id3)=`"attack"`, haste/dash=빈값.
- 클라 `WorldEventSink`(또는 cue 해석 지점)가 `abilityId → TbAbility.cue` 조회 → EventBus `AbilityActivated(cue)` 발행 → `LOPEntityView`가 `cue=="attack"` → `SetTrigger("Attack 01"/"Attack"/"Melee Attack")`(레거시 3트리거 그대로 = 캐릭터별 컨트롤러 호환). 빈 cue = 연출 없음(dash/haste).
- 서버는 cue 미보유(group `c`) — 항상 abilityId만 브로드캐스트(연출 해석은 클라 책임).

## 영향 파일 (개략 — 세부는 plan)

- **GameFramework**: 신규 `World/Events/AbilityActivatedEvent.cs`.
- **LOP-Shared**: 신규 `Protos/AbilityActivatedToC.proto` + regen(Protobuf/MessageIds/MessageInitializer, id 보존).
- **infrastructure**: `convert_source_to_luban.py`/`#Ability.xlsx`에 `cue`(group c) 컬럼 + attack 행 값.
- **MasterData-Client**: 재생성(`Ability.Cue`). **MasterData-Server**: 재생성(cue 없음).
- **Server**: `AbilityActivator`(bool 반환), `LOPRunner.ProcessInput`(성공 시 append), `WorldEventSink`(case → `AbilityActivatedToC` 송신).
- **Client**: `AbilityActivator`(bool), `PlayerInputManager.ApplyInput`(self append), 메시지 핸들러(`AbilityActivatedToC` 수신·자기 스킵·재수화), `WorldEventSink`(case → cue 해석 → EventBus), `Event.Entity.cs`(신규 `AbilityActivated` struct), `LOPEntityView`(`OnAbilityActivated` → 트리거).

## 검증
- 플레이: 공격 시 **스윙 애니 재생**(내 캐릭 즉시 / 남 서버 경유), 데미지/사망 무회귀. dash/haste 연출 없음(빈 cue) 확인. 클·서 0에러.
- MessageId 불변 diff(기존 14개 id 보존).

## Open Questions (plan에서 해소)
- cue 해석 지점: 클라 `WorldEventSink`(masterdata 주입) vs `LOPEntityView`. → 권고: sink에서 cue 문자열 해석해 struct에 담아 view는 cue→트리거만(legacy view 책임과 정합).
- `AbilityActivatedEvent` 위치: GameFramework(`DamageDealtEvent` precedent) 채택. LOP 도메인색 있으나 데이터 전용이라 family 일관 우선.
- AI 발동 연출 — slice 5/별도(서버 AI도 같은 append 지점 추가하면 자동).

## 진행
- [x] 탐색 — 레거시 ActionStartToC 와이어 + WorldEvent 인프라 + 애니 재생 매핑
- [x] 설계 합의 (A 와이어=DamageDealt 몰드 / B 발동순간 / C 예측+자기스킵 / D 클라전용 cue 컬럼 — 사용자 확정)
- [ ] plan 작성 → 구현
