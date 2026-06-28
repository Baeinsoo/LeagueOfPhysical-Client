# 이동 재설계 — 표준 모터(brake-to-desired) + drag, 단일 속도

**Date:** 2026-06-28
**Branch:** `feature/movement-motor` (LOP-Shared / Client / Server)
**Related:** [netcode-redesign](../../netcode-redesign.md) (예측/보정) · [World Core 연결](../../world-core-connection-architecture.md) · [Ability 페이즈 머신](2026-06-27-ability-phase-machine-design.md) · 메모리 [[movement-motor-redesign-status]]

## 결정 — 단순 표준 하나로 (A: 깔끔 제어)

리지드바디 이동 표준은 두 갈래다:
- **A. 깔끔 제어** (모터 brake-to-desired + drag): 칼같이 멈추고 안 미끄러짐. 외력은 물리가 자동으로 안 밀어줌 → 밀림은 *설계된 것만* 명시 처리.
- **B. 순수 물리** (AddForce + drag): 굴러온 돌·폭탄에 자동으로 밀림(공짜). 대신 약간 미끄러짐.

**A를 채택**한다(이미 Slice 1로 구현·검증됨). 둘 다(깔끔+외력자동) 가지려면 입력/외력 2채널이 필요한데, 지금 그건 과투자라 **폐기**(필요해지면 그때 추가). emergent 물리(굴러온 돌)에 밀리는 건 표준적으로 *관리하지 않는다*(플레이어를 물리 샌드백으로 안 만듦) — YAGNI.

> 한때 입력/외력 2채널 + 물리 흡수를 설계했으나(걷는 중 바람·굴러온 돌까지 다 받으려고), 복잡도 대비 불필요해 단순 표준으로 회귀. 외력 채널이 정말 필요해지면 단일 속도 위에 깔끔히 얹을 수 있음(구조 무변경).

## 산업 표준 매핑

| 개념 | 표준 (Unity 물리 이동) | 우리 |
|---|---|---|
| 입력 제어 | 목표속도로 `MoveTowards`(brake-to-desired) — Catlike Coding | `MovementSystem` |
| 적용 | `AddForce(VelocityChange)` (Y는 물리에 위임) | host가 (새속도−현재) AddForce |
| 정지·감속 | Rigidbody `linearDamping`(drag) | 엔진 drag |
| 외력(설계된 넉백) | `AddForce(Impulse)` + 잠깐 조작 차단 | 후속(전투 슬라이스) |
| 점프 | 위쪽 속도 세팅 | 동일 |
| velocity 권위 | 리지드바디 + read-back | entity.velocity = read-back |

## 설계 (구현된 형태)

### 이동 계산 (`MovementSystem`, LOP-Shared, 클·서 공통)
- 방향 입력 있으면: 목표 = 입력방향×moveSpeed, `MoveTowards(현재 수평, 목표, maxAcceleration×dt)` → 직각 관성까지 제동(드리프트 없음).
- 방향 입력 없으면: 수평 손 안 댐 → 정지·감속은 엔진 `linearDamping`(drag)가 담당. (점프만 눌러도 momentum 보존.)
- `maxAcceleration` = 가속·턴 빠르기(클수록 즉발). `linearDamping` = 뗐을 때 정지/미끄러짐(지형별 튜닝 여지: 얼음↓).

### 적용 (host `LOPMovementManager`, 클·서 동일)
- `delta = 새속도 − entity.velocity` → `AddForce(delta, VelocityChange)`(수평만). 점프 = 위쪽 속도 세팅.
- movement는 **입력 있을 때만** 호출(netcode baseline 유지). 무입력은 drag가 정지.

### 대시 (Slice 2 — "속도만 키운 이동")
일반 이동을 그대로 재사용하되, 대시 어빌리티 **Active 페이즈** 동안:
- **방향** = 바라보는 쪽 고정(플레이어 입력 무시), **속도** = `moveSpeed` 대신 `dashSpeed`(더 큼).
- 즉 이동 계산에 들어가는 *목표 방향·속도*만 대시값으로. 새 구조 없음.
- Active 동안 매 틱 적용돼야 하므로(입력 없어도) **대시 모션 적용 지점**이 매 틱 도는 곳이어야 함(설계 시 위치 확정).
- 데이터: `TbAbility.motion_speed`(대시속도) + `AbilityData.MotionSpeed` + `AbilityDataProvider.Map`. dash 행 + 버튼/grant.

## netcode
- 위치 보정(`SnapReconciler`/`ServerStateReconciler`)은 관측 delta 기반이라 무변경. movement 호출 시점도 기존(입력 시만) 유지 → baseline 무회귀. 공중 점프 갭 무회귀 검증.

## Slices
- **Slice 1 (완료·커밋)**: 모터 brake-to-desired(드리프트 수정) + drag 정지. 커널/테스트(29 green), 클·서. 커밋 `4f3018b`/`0746f17`/`2fb37d4`.
- **Slice 2 (진행)**: 대시 = 속도 키운 이동(Active 동안 앞 방향 고정). TbAbility.motion_speed + 모션 적용 + 버튼/grant.
- **후속**: 넉백(전투 hit → 임펄스 + 잠깐 조작 차단). 외력 채널/2채널은 *정말 필요해질 때* 재검토.

## Out of Scope
- 입력/외력 2채널, 물리 힘 흡수(B 통로), emergent 물리 밀림 관리 — 폐기(필요 시 재도입).
- 지형별 traction/마찰 — 구조 여지만, 값은 후속.
- 채널 snapshot/rollback — Stage④.

## 진행
- [x] brainstorm — 단일 velocity 모터+drag(A)가 표준이고 충분. 2채널은 과투자라 폐기(웹 재확인).
- [x] 이 spec
- [x] Slice 1 구현·검증·커밋
- [ ] Slice 2 대시 구현
