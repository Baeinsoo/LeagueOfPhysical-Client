# Plan — 어빌리티 발동 연출 와이어 (slice 4)

**Spec:** [2026-06-29-ability-activation-cue-wire-design.md](../specs/2026-06-29-ability-activation-cue-wire-design.md)
**Branch:** `feature/ability-activation-cue` (GameFramework / LOP-Shared / infrastructure / MD-Client·Server / Client / Server)
**핵심:** 발동 시 `AbilityActivatedEvent` → (서버 broadcast / 클라 예측) → 클라 cue 해석 → 애니. `DamageDealtEvent` 몰드 그대로.

> 명명: `AbilityActivatedEvent`(WorldEvent) / `AbilityActivatedToC`(proto) / `AbilityActivated`(클라 EventBus struct) / `TbAbility.cue`(client-only).

## 의존 순서 (구현/머지)
GameFramework → LOP-Shared(proto) → infra → MD-Client/Server → Server/Client.

## S1 — 코어 이벤트 (GameFramework)
- [ ] `Runtime/Scripts/World/Events/AbilityActivatedEvent.cs`: `public sealed record AbilityActivatedEvent(string EntityId, int AbilityId) : WorldEvent;` (DamageDealtEvent 스타일 — 필드명 케이스는 기존 record 따름).

## S2 — 와이어 proto (LOP-Shared)
- [ ] `Protos/AbilityActivatedToC.proto`: `// @auto_generate` + `message AbilityActivatedToC { string entity_id = 1; int32 ability_id = 2; }`.
- [ ] regen — ⚠️ 마스터 `generate_protos.sh` 쓰지 말 것(MessageIds.cs rm→재번호). **서브 스크립트 개별 실행**(compile_protos→generate_imessage→generate_message_ids→generate_message_initializer) 후 `MessageIds.cs` diff로 **기존 14개 id 불변** 확인, 신규=15.

## S3 — cue 마스터데이터 (infra → MD-Client/Server)
- [ ] `convert_source_to_luban.py`: TbAbility `#Ability.xlsx`에 `cue` 컬럼(`##type=string`, `##group=c`). (Ability는 수기 author 경로라 `#Ability.xlsx` 직접 편집.)
- [ ] `#Ability.xlsx`: `cue` 컬럼 추가, attack(id3)=`"attack"`, haste/dash=빈값.
- [ ] `gen.sh` → MD-Client `Ability.Cue` 생성 / MD-Server 미생성(group c) 확인. .meta 복원(gen rm 패턴, B1 교훈) + 신규 .meta만 Unity 생성.

## S4 — 서버 발동→이벤트 (Server)
- [ ] `AbilityActivator.TryActivate` → **bool 반환**(코어 `AbilitySystem.TryActivate` bool 표면화).
- [ ] `LOPRunner.ProcessInput`: ability 분기에서 `TryActivate` 성공 시 `worldEventBuffer.Append(new AbilityActivatedEvent(entityId, abilityId))`.
- [ ] `WorldEventSink`(서버): `case AbilityActivatedEvent ae` → `new AbilityActivatedToC { EntityId=ae.EntityId, AbilityId=ae.AbilityId }` → 전 세션 `Send`.

## S5 — 클라 수신/예측/연출 (Client)
- [ ] `AbilityActivator.TryActivate` → bool(서버와 동일).
- [ ] `PlayerInputManager.ApplyInput`: ability 분기 `TryActivate` 성공 시 `worldEventBuffer.Append(new AbilityActivatedEvent(localEntityId, abilityId))`(예측, 자기 캐릭).
- [ ] 메시지 핸들러(`Game.*.MessageHandler`): `AbilityActivatedToC` 구독 → **entityId == 내 플레이어면 스킵**(예측 중복 방지) → `worldEventBuffer.Append(AbilityActivatedEvent)`.
- [ ] `MessageInitializer`/factory는 S2 regen이 처리(구독 핸들러만 추가).
- [ ] `WorldEventSink`(클라): `case AbilityActivatedEvent ae` → `md.Tables.TbAbility.Get(ae.AbilityId)?.Cue` 조회 → 빈값이면 무시, 아니면 `EventBus.Publish(EntityId<LOPEntity>(ae.EntityId), new AbilityActivated(cue))`.
- [ ] `Event.Entity.cs`: `public struct AbilityActivated { public string cue; ... }`.
- [ ] `LOPEntityView`: `OnAbilityActivated` 구독 → `if (cue=="attack") { SetTrigger("Attack 01"); SetTrigger("Attack"); SetTrigger("Melee Attack"); }`. (cue→트리거 매핑; 향후 cue 종류 확장.)

## 검증
- [ ] 클·서 `refresh_unity`(force/all) 0에러 + MessageId diff(기존 불변).
- [ ] 플레이: 내 공격 즉시 스윙 애니 / 남 공격도 애니(서버 경유) / 데미지·사망 무회귀 / dash·haste 연출 없음.

## 커밋/머지
- 레포: GameFramework·LOP-Shared·infra·MD-Client·MD-Server·Client·Server(최대 7).
- 슬라이스 후 컴파일+플레이 검증. 머지는 사용자 요청 시 `--no-ff`(의존순). 픽스처 커밋 제외. EOL 교훈(커밋 후 정리는 *특정 파일만* checkout).

## 진행
- [ ] S1 코어 이벤트 → S2 proto → S3 cue MD → S4 서버 → S5 클라 → 검증
