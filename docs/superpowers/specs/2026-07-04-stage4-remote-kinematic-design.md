# Stage④ 첫 슬라이스 — 원격 엔티티 kinematic 장애물화 (예측/롤백 토대)

**Date:** 2026-07-04
**Branch (제안):** `feature/stage4-remote-kinematic` (LeagueOfPhysical-Client 단독. **서버 무변경.**)
**Related:** [netcode-redesign](../../netcode-redesign.md) (Stage④ · §6.5 Snapshot/Restore=클라 외각) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) (동기화 모델) · 선행 [입력-as-데이터](2026-07-02-input-as-data-design.md) · 메모리 [[netcode-migration-status]] · [[world-core-runner-world-naming]]

## Goal

원격(남) 플레이어 엔티티의 클라이언트 rigidbody를 **dynamic → kinematic**으로 바꿔, **스냅 위치로 직접 세팅되는 장애물**로 만든다. 내 캐릭만 dynamic으로 남긴다. 동작 보존(플레이어끼리 충돌·원격 이동 보간 유지). **클라 단독, 넷코드 프로토콜 무변경.**

이건 Stage④(예측·롤백)의 **토대 슬라이스**다 — 롤백 재시뮬(내 캐릭만 되돌려 다시 굴림)이 성립하려면 "씬을 재스텝해도 남은 안 움직인다"가 보장돼야 하고, 그건 남이 kinematic일 때만 참이다.

## 배경 — Stage④ 아키텍처 결정 (브레인스토밍 2026-07-04)

Stage④의 접근을 리서치(Fusion/Fish-Net/Netick/Mirror/Quake·Source·Overwatch) 기반으로 확정했다. 물리 재시뮬 방식은 3갈래:

| | 무엇 | 서버 일치 | 채택 |
|---|---|---|---|
| **A1** | 씬 PhysX 재시뮬 | 드리프트(비결정론) → 스무딩 | **채택** |
| A2 | 자체 결정론 이동 커널 재실행(kinematic 컨트롤러) | 정확 일치, 드리프트 0 | 미채택(이동 재작성 큼) |
| B | 속도만 C# 재적분(현 delta-replay) | 접촉 시 오예측 | 현재 방식(대체 대상) |

**A1 채택 + 단일 씬(패턴 1)**:
- **A1 근거**: A2(kinematic 컨트롤러)는 이동을 자체 충돌 커널로 재작성해야 해 규모가 큼. A1은 PhysX 재시뮬을 유지하되 비결정론 드리프트를 스무딩으로 덮음. 우리 게임(캐릭터 이동 위주, 소수 엔티티)엔 A1로 충분.
- **단일 씬(패턴 1) 근거 — 별도 물리 씬 불필요(실측)**: 별도 물리 씬(패턴 2)의 3대 이점이 우리에겐 다 무효 — ① **클라 물리 콜백 0개**(OnTrigger/OnCollision grep=0, 게임플레이는 전부 서버 권위) → 재시뮬 콜백 중복 격리 불필요, ② 엔티티 소수 + dynamic은 내 캐릭 1개뿐 → 재시뮬 비용 이미 낮음, ③ 남 kinematic이면 이미 "고정". 별도 씬은 콜라이더 복제 비용만 남고 이점 0 → **YAGNI**. 단일 수동 스텝 씬(현재 구조) 유지 + 남 kinematic + 내 캐릭 rewind·재스텝.
- 대규모 AoI(수백 엔티티)가 실제 안건이 되면 그때 패턴 2(별도 씬)+컬링으로 승격.

### 산업 표준 매핑

원격 엔티티를 클라에서 **kinematic**으로 두는 것 = 표준. Photon Fusion 공식: *"비권위 피어에서 rigidbody는 kinematic으로 설정 — 클라-서버 게임에 가장 적합"*, *"프록시는 기본적으로 kinematic, 모든 물리 상호작용은 권위(서버)에서"*. dynamic 원격 예측은 물리 샌드박스 전용 최상위 옵션. 캐릭터 이동 게임(우리)은 kinematic+보간이 정석. → 이 슬라이스는 Fusion 기본 동작으로의 정렬.

## 현재 상태 (실측)

- **모든 캐릭터가 dynamic**: `CharacterCreator`(클) line 59 `physicsComponent.Initialize(false, false)` — isKinematic=false, 로컬·원격 공통. (아이템만 `Initialize(true, true)` = kinematic+trigger.)
- **원격 구동 = `ServerStateReconciler`**: 매 틱 `End`에서 `entity.position/rotation/velocity`를 `SmoothDamp`로 서버 스냅 쪽으로 당김. `entity.velocity` 세팅 → 브릿지(`PushMotionToPhysics`, `BeforePhysicsSimulation`) → `rigidbody.linearVelocity`. 그런데 rigidbody가 **dynamic**이라 `Physics.Simulate`가 그 velocity를 또 적분 → **SmoothDamp 위치세팅 + 물리 적분이 겹치는 지저분한 상태.** (지연 렌더는 `LateUpdate`에서 별도.)
- **물리 구동**: `LOPRunner`가 `Physics.simulationMode=Script`, 매 틱 `IPhysicsSimulator.Simulate(dt)` → `Physics.Simulate(dt)`(기본 씬 전체). 로컬·원격 rigidbody 모두 이 한 번에 적분됨.
- **로컬 = `SnapReconciler`**: dynamic 유지(이 슬라이스 무변경).
- **브릿지**(`LOPEntity.PushMotionToPhysics`): `rigidbody.linearVelocity = velocity; rigidbody.rotation = Quaternion.Euler(rotation);` — velocity 기반. kinematic 바디엔 velocity가 안 먹으므로 원격은 position 기반으로 바꿔야 함.

## 설계

### ① 원격 캐릭터 = kinematic 생성

`CharacterCreator`(클): 원격(비-user) 캐릭터의 PhysicsComponent를 kinematic으로.
- 현재: `physicsComponent.Initialize(false, false)` (공통).
- 변경: `isUserEntity`면 `(false, false)`(dynamic 유지), 아니면 `(true, false)`(kinematic, non-trigger).
- kinematic 바디는 `Physics.Simulate`에 의해 적분되지 않고, 우리가 `rigidbody.position`을 세팅해 움직인다. dynamic 바디(내 캐릭)와는 **여전히 충돌**(장애물로 작용).

### ② 원격 모션 적용 = 위치 직접 (velocity 아님)

kinematic 바디는 `linearVelocity`로 안 움직이므로, 원격은 position/rotation을 직접 세팅한다.
- **`ServerStateReconciler`**: `SmoothDamp`로 계산한 목표를 `entity.position`/`entity.rotation`에 쓰는 흐름은 유지(파사드 → World). `entity.velocity`는 물리 적분용이 아니라 SmoothDamp 계산·스냅 보간용으로만 남는다(rigidbody에 안 먹어도 무해).
- **브릿지 분기 필요**: `PushMotionToPhysics`가 원격에선 `rigidbody.position = position`(kinematic 이동), 로컬에선 현행 `linearVelocity = velocity`. 
  - 방식 A: `PushMotionToPhysics`에서 `rigidbody.isKinematic`로 분기 — kinematic이면 position, dynamic이면 velocity 세팅.
  - 방식 B(추천): kinematic 바디는 `rigidbody.position`(+`MovePosition`) 세팅이 자연스러움. `isKinematic` 체크 한 줄로 분기. 로컬 경로 무변경.
- `SyncPhysics`(물리 후 rb→World): 원격은 우리가 세팅한 값이 그대로라 무해(왕복 일치). 로컬은 현행.

> **정밀화 포인트(구현 시 검증)**: 현재 원격도 브릿지가 `BeforePhysicsSimulation`에 velocity를 밀어넣는데, kinematic이면 그게 무시된다. 원격 position 세팅 타이밍을 `Physics.Simulate` 전/후 어디에 두든 kinematic이라 적분 영향이 없어 안전. 지연 렌더(`LateUpdate`)는 `entityTransformSnaps`(우리가 쓴 값)를 읽으므로 무변경.
>
> **페이즈 순서 주의(무해)**: 런너 루프에서 `ServerStateReconciler.Reconcile`은 `End`(=`SimulatePhysics` 뒤)에서 `entity.position`을 갱신하고, 브릿지는 다음 틱 `BeforePhysicsSimulation`에서 그 값을 rigidbody에 민다. 즉 내 캐릭의 `Simulate`는 원격의 *직전 틱 reconcile 위치*에 대해 충돌 판정한다(1틱 지연). 원격은 어차피 보간(과거)이라 이 1틱 지연은 수용 가능 — 명시만 하고 이 슬라이스에선 교정하지 않는다.

### ③ 아이템 — 무변경

아이템은 이미 kinematic(`Initialize(true,true)`). 원격 캐릭과 같은 부류(장애물/트리거). 무변경.

### 안 바뀌는 것 (중요)

- **내 캐릭(로컬)**: dynamic 유지, `SnapReconciler` 유지, 브릿지 velocity 경로 유지. 이 슬라이스는 로컬 예측/롤백을 아직 안 건드림.
- **넷코드 프로토콜/서버**: 무변경. 원격 kinematic화는 순수 클라 표현 방식. 와이어(EntitySnap) 그대로.
- **단일 물리 씬 · `Physics.Simulate` 구동**: 무변경(별도 씬 안 만듦). 남이 kinematic이 되어 이 한 번의 Simulate가 이제 dynamic=내 캐릭만 적분.
- **플레이어끼리 충돌**: 유지 — kinematic 원격도 dynamic 로컬을 막는다(kinematic↔dynamic 충돌은 성립).

## 클라 단독인 이유

원격 kinematic화는 "남을 어떻게 클라에서 표현하나"의 문제 — 서버는 자기 권위 물리(전부 dynamic, 자기 시뮬)를 그대로 유지한다. 와이어는 위치값만 건너므로 클라 표현 변경과 무관. 예측/롤백은 클라 외각 책임(netcode §6.5).

## 동작 보존 / 검증

- **보존 논거**: 원격은 이미 `SmoothDamp`로 position이 직접 세팅되고 있었다(dynamic이지만 사실상 위치 주도). kinematic 전환은 "물리 적분이 그 위에 겹치던 것"을 제거해 오히려 깨끗해진다. 렌더/보간 경로(`LateUpdate` 지연 렌더)는 우리가 쓴 값을 읽어 무변경.
- **검증**: 클라 컴파일 클린(`read_console`). 플레이 — ① 원격 플레이어 이동/보간이 여전히 부드러운지, ② **내 캐릭이 남과 부딪혀 막히는지**(kinematic 원격이 장애물로 작동), ③ 내 캐릭 이동/점프/대시/reconciliation 무회귀, ④ 아이템 정상, 콘솔 신규 에러 0. 서버 무변경이라 서버 영향 0.
- **유닛 테스트**: 클라 Assembly-CSharp(asmdef 테스트 불가) + 물리 통합이라 신규 EditMode 없음. 페이오프는 후속 슬라이스(롤백)에서 이 kinematic 전제가 "재스텝해도 남 불변"을 보장.

## Out of Scope (후속 Stage④ 슬라이스)

- **② 내 캐릭 Snapshot/Restore 골격** — 매 틱 로컬 모션 상태 기록 + 특정 틱 복원(클라 외각). 별도 슬라이스.
- **③ 롤백 재스텝 + reconciliation 재작성** — 서버 스냅 도착 → 로컬 rewind → 저장 입력으로 `Physics.Simulate` N회 replay(원격 프록시는 그 과거 틱 위치로) → delta-replay(`SnapReconciler`) 대체. 별도 슬라이스.
- **드리프트 스무딩 튜닝** · **확정 게이트(commit gate) 재설계** · **결정론 RNG**.
- **대규모 AoI 시 별도 물리 씬(패턴 2) + 컬링** — YAGNI, 스케일이 실제 문제될 때.
- **A2(kinematic 컨트롤러) 승격** — A1 드리프트가 실제로 거슬릴 때 재검토.

## Open Questions (구현 plan 전 확인)

- **브릿지 분기 방식** — `PushMotionToPhysics`에서 `isKinematic` 체크로 position/velocity 분기(추천) vs 원격 전용 컴포넌트/경로 분리. (추천: 최소 변경 = `isKinematic` 한 줄 분기.)
- **kinematic 이동 API** — `rigidbody.position =` (텔레포트) vs `rigidbody.MovePosition()` (보간 이동). 원격은 이미 SmoothDamp로 부드럽게 계산된 값이라 `position =`로 충분할 듯. 구현 시 떨림 확인.
- **원격 `velocity` 잔존 필요성** — SmoothDamp 계산에 쓰이므로 `entity.velocity`는 유지하되 물리엔 미적용. 완전 제거 가능 여부는 구현 시 판단.

## 진행

- [x] 브레인스토밍 — Stage④ 접근(A1+단일씬+원격 kinematic) 리서치 기반 확정, 첫 슬라이스=원격 kinematic 합의
- [x] 현재 물리·원격 구동·브릿지 실측
- [x] 이 spec 작성
- [x] spec self-review (플레이스홀더·일관성·스코프·모호성 점검, 페이즈 순서 주의 보강)
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
