# 이동 재설계 — 채널 분리 (입력 + 외력), 힘 기반

**Date:** 2026-06-28
**Branch:** `feature/movement-motor` (LOP-Shared / Client / Server)
**Related:** [netcode-redesign](../../netcode-redesign.md) (예측/보정) · [World Core 연결](../../world-core-connection-architecture.md) · [Ability 페이즈 머신](2026-06-27-ability-phase-machine-design.md) (dash가 외력 채널 사용처) · 메모리 [[movement-motor-redesign-status]] · [[ability-statuseffect-world-core]]
**기반/통합:** `2026-06-27-movement-external-velocity-channel-design.md`(입력+외력 2채널 정석 — 이 문서가 그 결론을 채택·정련). 그 사이 시도한 *단일 velocity never-brake 모터 + 엔진 drag*(디딤돌)는 아래 "왜 채널 분리인가"의 딜레마로 **폐기**.

## Goal

캐릭터 수평 속도를 **두 채널로 분리**해 둘 다 만족시킨다:

- **입력 채널** = 플레이어 제어. **brake-to-desired**(목표 = 입력 방향×moveSpeed, 무입력 시 0)로 수렴 → 방향전환 깔끔(직각 관성 제동, 드리프트 없음), moveSpeed 캡, 무입력 시 제동 정지.
- **외력 채널** = 넉백·바람·대시. 누적 + 감쇠 → momentum 보존.
- **최종 수평 = 입력 + 외력 합산** → 리지드바디 적용. 수직(Y) = 중력/점프(PhysX).

## 왜 채널 분리인가 (핵심 딜레마)

**velocity 하나로는 "방향전환 깔끔"과 "외력 momentum 보존"을 동시에 못 한다** (실측 확인):

| 단일 velocity 방식 | 방향전환 | 외력(넉백/바람) |
|---|---|---|
| never-brake (입력 방향 채우기만) | ❌ 드리프트(오른쪽 가다 위 누르면 대각 미끄러짐) | ✅ 보존 |
| brake-to-desired (목표로 제동) | ✅ 깔끔 | ❌ 외력 즉시 취소 |

never-brake가 직각 성분을 안 깎아 드리프트가 났고(플레이 확인), brake-to-desired는 직각을 깎지만 외력도 같이 죽인다. **채널을 나누면** 입력 채널은 brake-to-desired로 깔끔하게, 외력은 별 채널이라 입력 제동이 안 건드려 보존된다. 넉백·바람이 확정 예정이라 이 분리가 근본 해법.

## 산업 표준 매핑 (리서치 박제)

| 개념 | 표준 (Unity 물리 이동) | 우리 |
|---|---|---|
| 입력 제어 | **목표속도로 수렴(brake-to-desired, `MoveTowards`)** — Catlike Coding 정전 | `MovementSystem` 입력 채널 |
| 속도 합성 | **입력(control) + 외력(additive) 채널 합산** — PhysX CCT "base + additive", AAA 키네마틱 컨트롤러 | 입력 + 외력 합산 |
| 외력 감쇠 | **지수 감쇠**(동적 마찰 흉내) | `ExternalVelocity` 매 틱 감쇠 (슬라이스 2) |
| 적용 | **`AddForce(VelocityChange, delta)`** — Y는 물리에 위임 | host가 (목표−현재 수평)을 AddForce |
| 외력(넉백·대시·바람) | 외력 채널에 add/set | dash=Active 동안 set, knockback=add+decay (슬라이스 2) |
| 점프 | `vy = jumpSpeed` 세팅(VelocityChange) | 동일 |
| 중력 | PhysX 기본 | 유지(수평 drag 0) |
| velocity 권위 | **리지드바디**(힘으로 적분), 게임 코드는 read-back | entity.velocity = read-back 미러 |

## 설계

### 입력 채널 (슬라이스 1) — `MovementSystem` 커널 (LOP-Shared, 결정론 공유)

```
방향 입력 있음:
  desired = dir × moveSpeed
  newHorizontal = MoveTowards(currentHorizontal, desired, maxAcceleration × dt)   // 직각 관성 상쇄 → 드리프트 없음
  rotation = facing(dir)
방향 입력 없음:
  newHorizontal = currentHorizontal   // 손 안 댐 (감속·정지는 엔진 drag). 점프만 눌러도 수평 momentum 보존
velocity = (newHorizontal.x, currentVelocity.y, newHorizontal.z)   // Y passthrough
```
- **maxAcceleration** = 목표로 수렴하는 속도 변화율(가속·턴 = traction 다이얼). 크게 → 즉발, 작게 → 묵직/미끄러짐(얼음).
- **드리프트 없음**: `MoveTowards`가 직각·대각 성분까지 목표로 끌어 제동 (= 현재 vel 고려한 "(-1,1,0)식" 보정력 = `delta = desired − current`).
- **정지·감속 = 엔진 `linearDamping`(drag)** — 무입력 시 커널은 손 안 대고 drag가 감속. 표면별 drag로 얼음("떼고 미끄러짐")/진흙 표현. *drag는 무입력일 때만 유효* — 입력 중엔 모터가 매 틱 재타깃해 무해.

### 외력 채널 (슬라이스 2) — `ExternalVelocity` 컴포넌트 (LOP-Shared)

- `ExternalVelocity : GameFramework.World.Component` (수평 `Vector3`, Y 무시). 넉백/바람/대시가 얹는 별도 velocity.
- `world.Tick`(LOPWorld.Mutation 스윕)에서 매 틱 지수 감쇠(`*= decay`, ε 이하 0). Ability/StatusEffect tick 옆.
- 커널 `MovementInput`에 `externalVelocity` 추가, 최종 = 입력 채널 + 외력 채널 합산.
- **입력 채널과 독립** — brake-to-desired는 입력 채널만 건드림 → 외력 momentum 보존.

### 적용 — host `LOPMovementManager` (클·서 바이트 동일)

```
delta = result.velocity − entity.velocity        // 수평 (Y passthrough라 0)
rb.AddForce(new Vector3(delta.x, 0, delta.z), VelocityChange)
if (jump) rb.AddForce(Vector3.up × (jumpSpeed − rb.velocity.y), VelocityChange)
```
- movement는 `entity.velocity`를 **세팅하지 않음** → 외력(슬라이스 2)이 같은 리지드바디에서 합쳐짐.
- sim 후 `SyncPhysics`(AfterPhysicsSimulation)에서 entity.velocity/position = 리지드바디 read-back.

### movement 호출 시점 (입력 있을 때만 — netcode baseline 유지)

movement는 **입력이 있는 틱에만** 호출한다(기존 검증된 netcode 구조 그대로). 무입력 틱은 movement 미호출 → 감속·정지는 엔진 drag가 담당.
- **클라** `PlayerInputManager`: 입력 있을 때만 `ApplyInput`(기존). 무입력 틱은 redundancy 재전송만.
- **서버** `LOPRunner.ProcessInput`: 입력 있을 때만 movement(기존 `input == null → continue`).
- (대안으로 "매 틱 brake-to-0" 구조도 검토했으나, drag로 정지하면 표면별 ice 튜닝 + netcode baseline 유지 이점이 커 **하이브리드**[입력=커널 턴 / 무입력=drag] 채택.)

### velocity 권위 / netcode 결합

- **리지드바디 = velocity 진실원본**(힘 적분), entity.velocity = read-back 미러.
- 보정(`SnapReconciler`/`ServerStateReconciler`)은 *관측된* per-tick delta(end−begin)를 기록·replay → 이동이 brake-to-desired로 바뀌어도 메커니즘 영향 0. movement 호출 시점도 기존(입력 있을 때만) 유지 → netcode baseline 무변경.
- 외력 채널(슬라이스 2)은 컴포넌트 상태라 현 보정이 직접 복원하진 않음(스테일은 빠르게 재수렴). 풀 예측/롤백 시 Snapshot/Restore는 **Stage④**.
- **검증**: 공중 점프 갭(netcode Phase) 무회귀, 보정 러버밴딩 무회귀.

## Phasing

1. **(슬라이스 1 — 완료) 입력 채널 brake-to-desired (턴) + drag(정지).** 커널 never-brake → 방향 입력 시 `MoveTowards`(드리프트 수정), 무입력은 손 안 댐 + `linearDamping`(drag) 정지. movement는 입력 있을 때만(baseline). EditMode 갱신. *외력 채널 없음(외력=0).*
2. **(슬라이스 2) 외력 채널 + dash.** `ExternalVelocity` 컴포넌트 + 합산 + `world.Tick` 감쇠 + `AbilityMotionSystem`(Active 동안 forward set) + `TbAbility.motion_speed` + dash 버튼/grant. **단 외력 처리 방식은 사양에 따라 분기** (아래 Open):
   - *입력과 공존*(바람 = 걸으며 밀림) → 외력 채널 + 커널 합산 필요(drag로는 입력이 외력 상쇄).
   - *제어 무시*(넉백 = 스턴) → `AddForce(Impulse)` + drag 감쇠로 충분(채널 불필요).
3. **넉백** — 전투 hit → 외력 채널 add (별도 슬라이스).
4. **wind/모래늪** — 환경 외력 (별도).

## Out of Scope
- **외력 채널 prediction/reconciliation 정밀화** — Stage④.
- **가속 기반 *공중* 정밀제어(air-strafe/bhop)** — 필요 시.
- **maxAcceleration를 stat/마스터데이터로** — 우선 상수.
- **AI(EnemyBrain) 채널 이행** — 현재 AI는 `UpdateAI` 비활성. linearDamping=0 영향 없음. 부활 시 명시적 제동/채널 필요.

## 진행
- [x] brainstorm — 단일 velocity 딜레마(드리프트 vs 외력) 실측 → 채널 분리(B) 정석 채택(리서치: Catlike brake-to-desired, PhysX base+additive)
- [x] 이 spec (B 통합·정련)
- [x] **슬라이스 1 구현·검증** — 입력 채널 brake-to-desired(턴) + drag(정지) 하이브리드 (커널+테스트 29 green, 클·서 컴파일 0). 플레이 검증(드리프트·정지·공중점프) 대기
- [ ] 슬라이스 1 플레이 검증 (사용자) + `maxAcceleration`(턴 traction)·`linearDamping`(정지) 튜닝
- [ ] 슬라이스 2 (외력 채널 + dash) — 외력 사양(공존 vs 스턴) 결정 → plan → 구현
