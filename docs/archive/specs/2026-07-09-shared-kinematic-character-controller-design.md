# 공유 키네마틱 캐릭터 컨트롤러 — 다이나믹 PhysX 이동 대체

**Date:** 2026-07-09
**Branch (제안):** `feature/shared-kinematic-controller` (LOP-Client + LOP-Server + LOP-Shared + GameFramework)
**Related:** [netcode-redesign](../../netcode-redesign.md) (Stage④ · velocity 권위 이전) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (동기화 모델 · Engine↔Simulation) · 선행 [원격 kinematic](2026-07-04-stage4-remote-kinematic-design.md) · [이동 커널 공유](2026-06-24-world-movement-kernel-design.md) · [velocity 모터 기여](2026-07-05-velocity-motor-contribution-slice1-design.md) · 메모리 [[remote-entity-interpolation-and-collision-rubberband]] · [[netcode-migration-status]] · [[velocity-motor-contribution-slice]]

## Goal

캐릭터 이동을 **다이나믹 PhysX 리지드바디**(속도를 넣고 `Physics.Simulate`가 적분·충돌·밀림)에서 **키네마틱 캐릭터 컨트롤러**(속도·중력을 게임 코드가 계산 → 캡슐 sweep collide-and-slide → 포지션 직접 제어)로 바꾼다. 컨트롤러는 **클·서 공유 구체 코드**라 클라 예측이 서버 권위와 구조적으로 일치한다.

이동 실행부만 바꾸고 **reconcile·롤백은 이번 범위 밖**(Stage④ 본편). 부수 다이나믹 물체(던진 아이템·파편)는 PhysX가 계속 담당한다.

## 배경 — 왜 키네마틱인가 (리서치 확정 2026-07-09)

권위 원문 4개(Photon Quantum KCC, 언리얼 CMC, Valve Source `gamemovement`, coherence)로 재검증한 결과, **넷코드 게임 캐릭터 이동의 정석 = 키네마틱 커스텀 컨트롤러**임이 확인됐다. 다이나믹 리지드바디에 속도를 넣고 물리엔진이 밀게 하는 방식은 비표준이며, LOP가 현재 그 비표준 방식이다.

| 근거 | 원문 |
|---|---|
| 롤백이 싸고 깨끗 | coherence: 키네마틱 캐릭터는 물리 솔버 되감기가 없어 재생이 저렴 |
| 조작감이 타이트 | 오버워치: 영웅별 커스텀 가·감속(솔저 0.2s 정지, 루시우 0.6s) = 작성된 곡선 |
| 밀림·터널링 없음 | 다이나믹의 예측 못 할 밀림·고속 관통 부재 |

**기계적 확인 (원문 인용):**
- **Quantum KCC**: `movement = Velocity * dt` → `Position += movement`, 충돌은 `ShapeOverlap` 쿼리로 `Correction`. 물리엔진은 살아있고 **다른 다이나믹 물체는 정상 처리**.
- **언리얼 CMC**: `PhysWalking`/`PhysFalling`가 **속도·중력을 자체 계산**하는 키네마틱. sweep으로 벽에 막힘.
- **Source `TryPlayerMove`**: *"slides along multiple planes"*(collide-and-slide) via `TracePlayerBBox`(트레이스=sweep, 리지드바디 아님).

**뉘앙스:** "키네마틱"은 *캐릭터 locomotion*에 한정. 던진 물체·래그돌·파편은 다이나믹 물리 유지. (로켓리그처럼 물리 기반 게임도 있으나 결정론 락스텝이 아니라 예측+권위상태 복제 — `netcode-redesign` §4.3에 정정됨.)

### 산업 표준 매핑

이 컨트롤러 = Source `gamemovement`(collide-and-slide) / 언리얼 CMC(kinematic sweeps) / Quantum KCC의 직접 대응. **엔진 내장 컨트롤러(Unity `CharacterController`)는 미채택** — 위 레퍼런스가 셋 다 *커스텀* collide-and-slide를 짠 이유(넷코드 통제: 롤백 복원·클서 일치를 블랙박스로는 보장 못 함)를 그대로 따른다.

## 현재 상태 (실측)

### 서버 틱 (`LOPRunner.UpdateRunner`)

```
ProcessInput          → 엔티티별 이번 틱 InputCommand 확정(Current)
UpdateEntity          → entityManager.UpdateEntities()
world.Tick            → LOPWorld.Mutation → MovementSystem.Tick → World.Velocity 계산(수평만; y 보존)
DriveAbilityEffects
SimulatePhysics       → BeforePhysicsSimulation(PushMotionToPhysics: World.Velocity → rb.linearVelocity)
                        Physics.Simulate(dt)   ← PhysX가 [중력 + 적분 + 충돌 + 밀림]
                        AfterPhysicsSimulation(SyncPhysics: rb.position/rotation/velocity → World)
```

- **중력 = PhysX**: `Physics.gravity = (0, -9.81*2, 0)` = -19.62. `Physics.simulationMode = Script`, `autoSyncTransforms = false`.
- **velocity 권위 = Rigidbody**: `SyncPhysics`가 `velocity = rb.linearVelocity`로 되읽음. MovementSystem은 y를 보존만 하고 중력은 PhysX 몫.
- **모든 캐릭터 dynamic**: `CharacterCreator.Initialize(false, false)`. 아이템만 `(true, true)` = kinematic+trigger.

### 클라 (요약 — 슬라이스 3에서 상세 실측)

- 내 캐릭 = dynamic + `SnapReconciler`(delta-replay). 전방 예측 = `PlayerInputManager`가 입력 즉시 적용 + `Physics.Simulate`.
- 원격 = 이미 kinematic follower(스냅 위치로 `SmoothDamp`, [[remote-entity-interpolation-and-collision-rubberband]] 슬라이스 완료).
- 아이템 = kinematic+trigger.

## End-state 설계

### 새 틱 파이프라인 (캐릭터)

```
MovementSystem.Tick   → World.Velocity 계산 (기존 수평 + 외부기여)
                        + velocity.y += gravity*dt        ← NEW (PhysX가 하던 중력)
                        + grounded면 y 클램프              ← NEW
                        + 점프: velocity.y = JumpPower      (기존, semantics 보존)
KinematicMoveSystem   → collide-and-slide sweep           ← NEW (핵심)
   (신규, 공유 커널)     desiredDelta = velocity*dt
                        CapsuleCast로 벽까지만 이동 + 미끄러짐 (N회 반복)
                        바닥 감지(hit.normal.y > 임계) → grounded, velocity.y=0(또는 접지 유지)
                        → World.Transform.Position 직접 씀   (권위 = World)
Physics.Simulate(dt)  → 다이나믹 물체(던진 아이템·파편)만 적분. 캐릭터 미접촉
Bridge                → World.Position → rb.position       (kinematic rb = follower/장애물)
(SyncPhysics 캐릭터 스킵 — 이미 kinematic 가드 있음)
```

### 신규 부품

#### ① `ICollisionQuery` 포트 (GameFramework, I/O seam)

물리 충돌 쿼리 추상. `IPhysicsSimulator`(스텝 구동)와 짝을 이루는 별도 포트. 물리 쿼리는 본질상 사이드 I/O(PhysX 구체)라 인터페이스가 맞다.

- 최소 시그니처: `bool CapsuleCast(Vector3 point1, Vector3 point2, float radius, Vector3 direction, float distance, int layerMask, out CollisionHit hit)`
- 필요 시 `ComputePenetration`(초기 관통 해소)도 추가 — 커널 구현 중 실측으로 결정.
- `CollisionHit` = 얇은 값 타입(거리·법선·포인트). Unity `RaycastHit` 직노출 대신 포트 경계에서 격리(공유 커널이 UnityEngine.Physics 미참조).
- 구현: 클·서 각자 `UnityCollisionQuery`(`Physics.CapsuleCast` 래핑). **동일 구체 로직**이지만 사이드별 인스턴스(I/O 어댑터 관례 = `Register<ICollisionQuery, UnityCollisionQuery>`).

> **네이밍 근거(산업 표준 매핑):** 쿼리 포트 = 물리엔진의 scene query 추상. Quantum `ShapeOverlap`/`ShapeCast`, Unity `Physics.CapsuleCast`, Unreal `SweepSingle`에 대응. 짝 이름 `IPhysicsSimulator`(스텝) ↔ `ICollisionQuery`(쿼리)로 GameFramework 짝 일관.

#### ② collide-and-slide 커널 (LOP-Shared, 공유 구체)

sweep+미끄러짐 반복 로직. **클·서 동일 코드 = 예측이 권위와 일치**(결정론은 공유 구체 코드가 보장 — `world-core-connection-architecture.md` "시뮬 코드 형태" 컨벤션). 포트만 호출하고 UnityEngine.Physics는 미참조(LOP-Shared는 `UnityEngine` Vector3/Mathf만 사용, Physics는 포트 뒤).

- 입력: 시작 position, 이동 delta(velocity*dt), 캡슐 파라미터(radius/height), `ICollisionQuery`.
- 알고리즘: 표준 collide-and-slide — `CapsuleCast(delta)` → hit면 hit 지점까지 이동 + 남은 delta를 충돌면(plane)에 투영해 미끄러짐 → N회 반복(과도 회전 방지 상한). 바닥 hit(`normal.y > slopeLimit cos`)면 grounded 반환.
- 출력: 최종 position, grounded, (선택) 충돌로 소실된 속도 보정.
- **`*System`이 아니라 커널**: 컨텍스트 없는 순수 함수라면 static 커널(예: `KinematicMover.Move(...)`). 상태·DI 필요분(캡슐 파라미터 조회 등)은 인스턴스 `KinematicMoveSystem`로 감쌈 — `world-core-connection-architecture.md` "System vs 커널" 관례.

#### ③ MovementSystem 확장 (LOP-Shared, 기존)

- 중력 적분: 대시 등 Override가 아닌 일반 이동에서 `velocity.y += Physics 중력*dt`(공유 상수). grounded면 클램프.
- grounded 상태 = World에 최소 표현 추가(예: `Motion`/`Transform` 부속 플래그 또는 별도 컴포넌트) — 슬라이스 1에서 위치 확정.

### 배선 변경

- **PhysicsComponent**: 캐릭터 `Initialize(true, false)` = kinematic, non-trigger(클·서 양쪽). dynamic↔kinematic 충돌은 성립하므로 캐릭터끼리 막힘·던진 물체 상호작용 유지.
- **PushMotionToPhysics**: 캐릭터는 velocity를 rb에 안 밈(kinematic). rotation만. position은 reactive(`OnPropertyChange`) 또는 Bridge가 `rb.position = World.Position`.
- **SimulatePhysics**: `Physics.Simulate(dt)`는 유지(다이나믹 물체용). 캐릭터 이동은 그 *전에* KinematicMoveSystem이 처리.
- **중력 소유**: `Physics.gravity`는 다이나믹 물체용으로 남기고, 캐릭터 중력은 MovementSystem이 같은 상수로 직접 적용.

## 슬라이싱

넷코드 원칙(§6.1 "한 번에 한 변경")대로. **커널 격리 검증 → 서버 → 클라** 순서.

| 슬라이스 | 내용 | 검증 | 클라 영향 |
|---|---|---|---|
| **1. 커널 (dormant)** | `ICollisionQuery` + `UnityCollisionQuery`(양쪽) + collide-and-slide 공유 커널 + MovementSystem 중력·grounded. **라이브 틱에 안 물림, 순수 추가.** | **EditMode 테스트**(mock 포트로 sweep/미끄러짐/중력/바닥) — [[tdd-first-always]] 페이오프 | 없음 |
| **2. 서버 키네마틱** | 서버 캐릭터 → kinematic + 커널을 서버 틱에 배선(PhysX 다이나믹 적분 대체) + 서버 중력 MovementSystem으로 | 플레이: 서버 캐릭 걷기·점프·낙하·벽 막힘 정상(스냅으로 확인) | **미변경**(내 캐릭 dynamic+SnapReconciler 유지, 원격 kinematic follower). 서버 kinematic화로 예측 격차 *일시 증가* — 측정만, 슬라이스 3에서 해소 |
| **3. 클라 내 캐릭 키네마틱 예측** | 내 캐릭 → kinematic, 전방 예측을 **같은 공유 커널**로 | 예측이 권위와 일치 → reconcile 격차·러버밴드 감소(육안+HUD) | 예측 경로 교체. **SnapReconciler delta-replay는 그대로**(잔여 스무딩) |

각 슬라이스는 독립 검증 가능. 슬라이스 2에서 클라가 잠시 다이나믹 예측 vs 서버 kinematic 권위로 divergence가 늘 수 있으나(주로 충돌 지점), 중력 상수가 같고 정상 플레이엔 외력 push가 없어 reconcile이 흡수한다 — 알려진 일시 상태, 측정 후 슬라이스 3으로 해소.

## 주의점 (박제)

- **중력**: 현 `Physics.gravity=(0,-19.62,0)`를 PhysX가 캐릭터에 적용 → SyncPhysics 되읽기. 키네마틱은 MovementSystem이 `velocity.y += -19.62*dt` 직접. **같은 값이라 낙하 체감 동일.** 다이나믹 물체용 `Physics.gravity`는 유지.
- **점프 semantics 보존**: 현재 점프는 grounded 체크 없이 무조건 `velocity.y=JumpPower`. 이 리팩터에선 **그대로 유지**(공중점프 허용 여부 = 게임 디자인 콜, 별건). grounded는 *중력 무한누적 방지·정지 판정*에만.
- **결정론 주의**: `CapsuleCast`는 PhysX라 완벽 결정론 아님(부동소수점). 목적은 **키네마틱 블로킹**(밀림 제거)이지 완벽 일치가 아니며, 잔여 미세 차이는 기존 reconcile이 흡수 — Source/언리얼 CMC도 같은 방식. 완전 결정론(Quantum식 자체 물리)은 비목표.
- **velocity 권위 이전 = Stage④ 잠금해제**: 이 작업이 `world-core-connection-architecture.md` 4e·Stage④가 대기하던 "velocity 권위 Rigidbody → World.Entity" 키스톤이다. 완료 시 4e(어빌리티 effect·물리 페이즈의 `LOPWorld.Tick` 흡수)도 자연히 열림 — 단 그 흡수는 별건.
- **아이템/AI**: 아이템 이미 kinematic-trigger, 무변경. AI 캐릭(InputBuffer 보유 시)은 같은 커널을 자동 사용 — 통일.
- **대시(어빌리티 Override 이동)**: 현재 `AbilitySystem.TryGetActiveMotionEffect`로 수평 override. 키네마틱에서도 그 override velocity가 collide-and-slide로 흘러 벽에 막힘 — 자연 통합. 무변경.
- **외부 기여(MotionContributions, 넉백)**: Additive 수평 기여도 velocity에 합성 후 커널로 흘러감 — 통합. [[velocity-motor-contribution-slice]] 슬라이스 2(넉백)와 정합.

## Out of Scope (Stage④ 본편)

- **reconcile 재설계** — delta-replay(`SnapReconciler`) → Snapshot/Restore + input replay. 커널이 깔리면 자연 수렴하나 별건.
- **하드 롤백 재스텝 + 확정 게이트(commit gate) 재설계**.
- **결정론 RNG · 완전 결정론 물리**.
- **경사/계단 고급 처리 튜닝** — 슬라이스 1에서 기본(slopeLimit·stepOffset)만, 세밀 튜닝은 필요 시.
- **다이나믹 물체 다양화**(던지기·파편 실제 콘텐츠) — 인프라는 유지되나 신규 콘텐츠는 별건.

## Open Questions (구현 slice에서 해소)

- **grounded 상태의 World 표현 위치** — `Transform` 부속 플래그 vs 별도 `Motion`/`Grounded` 컴포넌트. 슬라이스 1.
- **`ComputePenetration`(depenetration) 필요 여부** — 초기 관통·끼임 해소가 CapsuleCast만으로 부족하면 추가. 커널 구현 중 실측.
- **캡슐 파라미터 출처** — 현재 하드코딩(radius 0.35, height 1.5). 커널이 어디서 읽을지(상수 vs 컴포넌트). 슬라이스 1.
- **collide-and-slide 반복 횟수 상한 · slopeLimit · stepOffset 기본값** — 튜닝값, 슬라이스 1에서 초기값 잡고 슬라이스 2 플레이로 조정.
- **KinematicMoveSystem을 `LOPWorld.Tick`에 넣을지 별도 호스트 스텝으로 둘지** — velocity 권위가 World로 오면 `LOPWorld.Mutation` 내부가 자연스러우나, 포트(`ICollisionQuery`) 주입 경로 확인 필요. 슬라이스 2.

## 진행

- [x] 브레인스토밍 — 전제 재검증(리서치 4원문), end-state 아키텍처 합의, 슬라이싱 합의(커널→서버→클라, reconcile Stage④ 유보)
- [x] 현재 서버 틱·물리·브릿지 실측
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 슬라이스 1 구현 plan 작성
