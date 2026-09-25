# 레거시 Action 시스템 은퇴 (페이즈 머신 slice 5 / 3f)

**Date:** 2026-06-30
**Branch (제안):** `feature/legacy-action-retire` (GameFramework / LOP-Shared / infrastructure / MD-Client·Server / Client / Server)
**Related:** [어빌리티 페이즈 머신](2026-06-27-ability-phase-machine-design.md) (slice 5) · [behavior 조합 B](2026-06-28-ability-behavior-composition-design.md) · [발동 연출 cue](2026-06-29-ability-activation-cue-wire-design.md) · 메모리 [[ability-statuseffect-world-core]]

## Goal

어빌리티 시스템(페이즈 머신 + effect 조합 + cue 와이어)이 attack/dash/haste를 전부 대체했으니, **죽은 레거시 `Action` 시스템을 일괄 제거**한다. + slice 4에서 미룬 **cue→트리거 하드코딩 정리**.

## 감사 결과 (탐색, 2026-06-30) — 유일 차단 = AI 공격

| 레거시 | 상태 |
|---|---|
| `Dash`/`Spawn` 클래스, 플레이어 dash actionCode | 죽음(dash=AbilityId 2; Spawn 클래스 생성 안 됨) |
| **AI 공격** `EnemyBrain → actionManager.TryStartAction("*_attack_001")` | **🔴 유일 차단** — 마이그레이션 선행 |
| `LOPEntityView.OnActionStart` 애니 | 살아있음(AI 공격이 `ActionStartToC`로 이걸로 애니) |
| `spawn_001` 디버그 버튼(주변 적 AoE 킬) | 디버그 — **제거 결정** |
| Action base/`IActionManager`/와이어/`TbAction` | 위에 의해 전이 생존 |

→ **AI 공격을 어빌리티(AttackAbilityId=3)로 옮기면 나머지 전부 사망 → 삭제 가능.**

## 결정 (사용자 확정)
- **Spawn 디버그 버튼 = 제거**(레거시와 함께). 필요 시 추후 깔끔히 재추가.
- **cue 정리 = 최소**: 중복(레거시 `OnActionStart`) 삭제 + cue→트리거 하드코딩 if → 코드 내 `Dictionary<string,string[]>` 매핑(한 곳, if 누적 없음). 캐릭터별 샷건(트리거 3개)은 유지(없는 트리거=no-op). 완전 데이터드리븐((캐릭터×cue) 테이블)은 과할 수 있어 보류.
- **cue 발화 중앙화**: slice 4에서 `ProcessInput`/`ApplyInput`에 흩어둔 `AbilityActivatedEvent` append를 **`AbilityActivator`로 이전**(worldEventBuffer 주입) → 플레이어·AI 모든 발동이 자동으로 cue 송출(AI 공격 애니도 새 cue 경로 통일).

## 산업 표준 매핑
- strangler-fig 마지막 단계 = **레거시 경로 은퇴**(신규가 트래픽을 다 받은 뒤 구 경로 제거). Fowler StranglerFigApplication.
- AI도 같은 어빌리티 시스템으로 발동 = "플레이어/AI 공용 어빌리티 파이프"(GAS는 AI도 `TryActivateAbility` 동일 사용).

## 작업 (단계 — 세부는 plan)

**Phase 1 — 마이그레이션(레거시를 unused로):**
- cue 발화를 `AbilityActivator`로 중앙화(클·서). `ProcessInput`/`ApplyInput`의 append 제거.
- `EnemyBrain`(서버): `actionManager.TryStartAction("*_attack_001")` → `abilityActivator.TryActivate(entityId, 3, tick)`. IActionManager 의존 제거.
- Spawn 디버그 제거: `GamePadViewModel.Spawn()` + GamePadView spawn 버튼.

**Phase 2 — C# 레거시 삭제:**
- 삭제: `Action`(base, 클·서), `Attack`/`Dash`/`Spawn`(클·서), `LOPActionManager`(클·서), `IActionManager`(GameFramework).
- DI 제거: `IActionManager` 등록(클·서 GameLifetimeScope) + 주입처(클 `PlayerInputManager` ctor, 서 `LOPRunner` 필드, `Game.Entity.MessageHandler`, `EnemyBrain`).
- `LOPEntityView`: `OnActionStart` + 구독 제거. cue 핸들러를 Dictionary 매핑으로 정리.
- `Event.Entity.cs`(클·서): `ActionStart`/`ActionEnd` 구조체 제거.
- 입력 라우팅: `SetActionCode` + actionCode 분기(클 `ApplyInput`, 서 `ProcessInput`) 제거. 클 `PlayerInput` 모델 `actionCode` 제거.
- 메시지 핸들러: `Game.Entity.MessageHandler`의 `OnActionStartToC`/`OnActionEndToC` + 구독 + IActionManager 제거.

**Phase 3 — 와이어 + 마스터데이터 제거(생성물, 주의):**
- proto 삭제: `ActionStartToC`/`ActionEndToC`/`ActionData`. `PlayerInput.proto`의 `action_code` 제거. `UserEntitySnapToC`의 `ActionDatas` 제거 + 서버 `LOPRunner.EndUpdate` 채움 제거. ⚠️**MessageId**: 서브 스크립트 재생성은 기존 id 보존(제거된 것만 사라짐, 나머지 불변; gap 허용). master `generate_protos.sh`(MessageIds.cs rm) 금지. 클·서 같은 Shared로 재생성.
- 마스터데이터: convert `TABLES["Action"]`/`FIELD_RENAME["Action"]` 제거, `#Action.xlsx`/`source/Action.xlsx` 삭제, `gen.sh` 재생성(→ `Action.cs`/`TbAction.cs`/`tbaction.bytes`/`Tables.cs` 항목 소멸). `LOPMasterData.TableFiles`에서 `"tbaction"` 제거.

## 검증
- 각 Phase 후 클·서 컴파일 0에러. **플레이**: 플레이어 공격(데미지+애니), **AI 공격(데미지+애니 — 새 cue 경로)**, dash/haste 무회귀, spawn 버튼 사라짐. MessageId diff(Action* 제거, 나머지 불변).

## Out of Scope
- 캐릭터별 어빌리티 로드아웃(grant-all 유지) · 완전 데이터드리븐 cue 테이블 · Stage④(예측/롤백/취소).

## 진행
- [x] 탐색·감사(유일 차단=AI 공격) + 사용자 결정(Spawn 제거 / cue 최소 정리)
- [ ] plan → 구현(Phase 1→2→3)
