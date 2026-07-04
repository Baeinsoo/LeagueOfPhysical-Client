# Stage④ 슬라이스 ③ — 롤백/재조정 (Restore + Input Replay)

**Date:** 2026-07-04
**Branch (제안):** `feature/stage4-rollback-reconcile`
**Related:** [netcode-redesign](../../netcode-redesign.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) §6.5 · [slice ② SnapshotHistory](2026-07-04-stage4-snapshot-history-design.md) · [slice ① 원격 kinematic](2026-07-04-stage4-remote-kinematic-design.md)

## Goal

서버 권위 스냅이 도착하면 내 캐릭(로컬 플레이어)을 그 틱으로 **하드 복원(restore)** 하고, 저장해 둔 입력을 현재까지 **재생(replay)** 해 예측 오차를 보정한다. 기존 `SnapReconciler`의 delta-replay(+ sim SmoothDamp)를 **Snapshot/Restore + input replay** 표준 모델로 대체한다. slice②의 `SnapshotHistory` + 입력 파이프라인 위에 얹는다.

## 배경 — 저장소 두 개 + 지금 코드의 문제

- 재생에는 저장소 둘이 필요: **입력 로그**("매 틱 뭘 눌렀나")와 **스냅샷 로그**("그 결과 어디 있었나", slice②). 하지만 `InputBuffer.Commands`는 소비 시 **제거**되어(consume→remove) 과거 입력이 안 남는다 → **재생용 입력 히스토리가 별도로 필요**하다.
- 현 `SnapReconciler`는 **실제 값(sim)까지 `SmoothDamp`로 부드럽게 당긴다.** 재생하려면 실제 값이 *어중간한 중간값*이 아니라 *정확한 값*이어야 다음 물리·재생이 안 틀어진다 — 그래서 sim은 **하드 복원**으로 바꾼다.

## 범위 (하는 것 / 안 하는 것)

**한다:** 내 캐릭 하드 복원 + **이동·물리** 입력 재생 + 표준 재조정 루프. errorGate로 어긋날 때만 롤백. sim SmoothDamp 제거.

**안 한다 (후속 슬라이스):**
- **어빌리티/상태이상 스냅+재생 (B)** — 대시가 재생 구간에서 정확히 재현되려면 어빌리티/상태이상의 *상태*(페이즈 타이머·활성 효과)까지 스냅/복원해야 한다(재생 때 이 시스템을 다시 틱하면 타이머 이중 진행). 이번엔 **이동+물리만 재생**, 재생 루프는 B로 *덧붙여* 확장 가능하게 설계. 대시는 짧고 서버 권위라 이번 창에선 오차 미미.
- **원격 틱별 위치 정합(클라 lag-comp)** — 재생 구간(T+1~now)은 구조상 *받은 모든 스냅보다 앞선 미래 틱*이라(내·원격 스냅이 같은 틱 T로 함께 옴), 원격의 그 틱 위치는 아직 도착 안 함 → 추측(extrapolate)뿐. 주 충돌 대상인 정적 지형은 어차피 정확하고 P2P는 서버 권위라, **원격 = 현재 포즈 정적 장애물** 유지.
- **전용 보정-스무싱** — 하드 보정의 점프는 기존 지연 렌더링이 ~1틱에 미끄러뜨려 흡수. pop이 실제로 보일 때 추가.

## 설계

### A. 데이터 흐름 — 스냅 도착 프레임의 "틱 앞 한 패스"

```
LOPRunner.UpdateRunner (틱 맨 앞):
  ProcessNetworkMessage                (서버 스냅 → Reconciler에 큐잉)
  ┌─ Reconcile(currentTick, dt) ─────────────────────── [신규, 호스트 스텝]
  │   대기 중 최신 서버 스냅(틱 T) 없음 → return (no-op)
  │   errorGate: |snapshotHistory[T] − 서버스냅| ≤ threshold → return (예측 정확)
  │   그 외:
  │     1) 하드 복원: 내 캐릭을 서버스냅(T) 상태로(World+rigidbody) + Physics.SyncTransforms()
  │     2) for t = T+1 .. currentTick-1:                 // 이미 예측한 과거 틱 재구성
  │          InputBuffer.Current = inputHistory[t]  (없으면 무입력)
  │          MovementSystem.Tick → PushMotionToPhysics → physicsSimulator.Simulate → SyncPhysics
  │          snapshotHistory[t] 재기록(보정값)
  └────────────────────────────────────────────────────
  ProcessInput / world.Tick / SimulatePhysics    (currentTick은 평소대로 — 재생 대상 아님)
  EndUpdate → snapshotHistory[currentTick] 기록(slice②)
```

- **"틱 앞"이 표준**(Gambetta/DOTS): 서버 상태 받으면 → 그 상태로 세팅 → 보류 입력을 그 위에서 재생 → 현재까지 전진. 끝(End)이 아니라 앞에서, 되돌린 틱부터 현재까지 한 번에 전진 재시뮬.
- 재생은 **과거 예측 틱(T+1~currentTick-1)** 만 재구성한다. currentTick은 Reconcile 이후 `ProcessInput`/`world.Tick`으로 평소처럼 새로 시뮬 → 기존 루프 무변경.
- 재생 틱 수 ≈ RTT/틱. **errorGate로 어긋날 때만** 돌아 상시 부담이 아니다.

### B. 부품

| 부품 | 위치 | 역할 |
|---|---|---|
| **`InputHistory`** (tick→`InputCommand` 링버퍼) | **LOP-Shared** (`InputBuffer`/`InputCommand` 옆) | 재생용 "틱별 적용 입력". `PlayerInputManager.ProcessInput`에서 매 틱 기록(무입력 틱은 0-커맨드) |
| **`Reconciler`** (호스트 서비스, 클라) | **LOP-Client** | 서버 스냅 큐잉 + errorGate + 하드 복원 + 재생 루프. `LOPRunner`가 틱 앞에서 호출 |
| `SnapshotHistory` (slice②) | GameFramework.Netcode | errorGate 비교 + 재생 중 재기록 |
| 지연 렌더링 (현 `SnapReconciler.LateUpdate`) | LOP-Client 뷰 컴포넌트 | **유지** — 보이는 메시를 부드럽게(틱/프레임 주기차 흡수) |

- **입력 히스토리 위치 = LOP-Shared**: `InputCommand`가 거기라 자연스럽고, 서버도 후일 재사용 가능. (재생 *정책*은 클라지만 *데이터 컨테이너*는 공유 — slice② `SnapshotHistory`와 같은 결.)
- **`Reconciler` = 호스트 서비스**: 재생이 `MovementSystem.Tick` + `physicsSimulator.Simulate`를 반복 호출해야 하는데 이는 호스트가 쥔 자원이라, per-char MonoBehaviour보다 호스트가 orchestrate(slice② 기록을 호스트가 한 것과 동일).

### C. 복원·재생 물리 기전

- **하드 복원**: `entity.position/rotation/velocity`(파사드) = 서버스냅 값 → reactive 경로가 rigidbody 반영 → `Physics.SyncTransforms()`(autoSyncTransforms=false라 수동)로 PhysX가 새 포즈를 보게 함.
- **재생 한 틱** = 기존 틱 코드 재사용: `MovementSystem.Tick`(입력→World 속도/회전) → `LOPEntity.PushMotionToPhysics`(World→rigidbody) → `physicsSimulator.Simulate(dt)`(내 캐릭 dynamic만 적분; 원격 kinematic이라 정지) → `LOPEntity.SyncPhysics`(rigidbody→World).
- **이동만 재생**: 재생 루프는 `world.Tick`(이동+어빌리티+상태이상)이 아니라 **`MovementSystem.Tick`만** 부른다 — `world.Tick`을 부르면 어빌리티/상태이상 페이즈가 이중 진행돼 손상. (B에서 스냅 범위를 넓히고 `world.Tick` 재생으로 전환.)

### D. 뷰 (확정)

- **sim = 하드 복원+재생** — 현 `Reconcile`의 `SmoothDamp`-on-sim 제거.
- **보이는 것 = 기존 지연 렌더링 유지** — 하드 보정 점프를 ~1틱에 걸쳐 미끄러뜨림. 클럭동기(Phase2)로 보정이 이미 작아 대개 충분. 전용 보정-스무싱은 pop 관측 시 후속.

### E. 발동 조건 (errorGate)

- 서버스냅 vs `snapshotHistory[T]` 위치 차이가 **threshold 이하 → 롤백 스킵**(예측 정확 = 흔한 경우). threshold 초과 → 복원+재생.
- 근거: DOTS의 "error detection" 표준. slice② 비교를 그대로 쓰며 CPU·미세 재시뮬 지터를 아낀다. errorGate 판정은 순수 함수로 분리해 EditMode 테스트.

### F. 기존 `SnapReconciler` 정리

- **재조정 부분** (delta-replay + `SmoothDamp`-on-sim + `serverInputSequences`/`localInputSequences` 앵커 + `localEntitySnaps`) → **제거**, 새 `Reconciler`로 대체.
- **지연 렌더링** (`LateUpdate` 보간, `entityTransformSnaps`) → **유지**(뷰 전용으로 분리 잔존).
- 서버 스냅 수신 배선 `OnEntitySnapsToC` → `SnapReconciler.AddServerEntitySnap` → **새 `Reconciler`로 재연결**.
- `PlayerInputManager`의 `AddLocalInputSequence(SnapReconciler)` 호출 → 제거(앵커가 tick 기반 복원으로 대체). 입력 히스토리 기록으로 교체.

## 검증

- **EditMode(순수 부품)**: `InputHistory` 링(Record/TryGet/eviction). errorGate 판정(차이>threshold 분기) 순수 함수 테스트.
- **플레이**: 공중 점프 × RTT(50/150/300ms) × 손실 시나리오에서 러버밴딩 육안 소멸 + `ReconciliationStats` HUD로 하드 보정 전/후 distance 비교 + 이동/점프/대시/정지 무회귀.

## 영향 받는 파일 (개략 — 세부는 plan)

- **신규**: `InputHistory`(LOP-Shared) + EditMode 테스트, `Reconciler`(LOP-Client) + errorGate 순수 판정 + 테스트.
- **수정**: `LOPRunner`(틱 앞 Reconcile 스텝 + 주입), `PlayerInputManager`(입력 히스토리 기록, InputSequence 제거), `Game.Entity.MessageHandler`(스냅 배선 → Reconciler), `SnapReconciler`(재조정 제거 → 지연 렌더링 전용으로 축소/분리), `GameLifetimeScope`(등록).
- **무변경**: `WorldBase`/`IWorld`, `MovementSystem`(재사용), 서버 전체, 어빌리티/상태이상 시스템.

## 산업 표준 매핑

- **loop 순서 = Gambetta(정석) + Unity DOTS(구현)**: 서버 권위 상태 도착 → 그 상태로 세팅 → 미처리 입력을 그 위에서 재생 → 현재까지 전진. 끝이 아니라 앞. DOTS `PredictedSimulationGroup`이 "어느 틱부터 재시뮬할지 계산 → 현재 예측 틱까지 재계산"과 1:1.
- **sim 하드 + 렌더 스무싱**: DOTS가 예측 backup을 "error detection **및 smoothing**"에 쓰는 것과 동일 — 상태는 정확히, 화면은 부드럽게.
- **errorGate = DOTS error detection**: 예측이 맞으면 재시뮬 스킵.

출처:
- [Gabriel Gambetta — Client-Side Prediction and Server Reconciliation](https://www.gabrielgambetta.com/client-side-prediction-server-reconciliation.html)
- [Unity Netcode for Entities — Introduction to Prediction](https://docs.unity3d.com/Packages/com.unity.netcode@1.6/manual/intro-to-prediction.html)

## Out of Scope

- 어빌리티/상태이상 예측 롤백(B): 스냅 범위 확장 + `world.Tick` 재생.
- 원격 틱별 위치 정합(클라 lag-comp).
- 전용 다중틱 보정-스무싱.
- 서버 lag-compensation.

## 구현 후기 — 근본 원인 & 서버 변경 (2026-07-04 플레이 검증)

플레이 검증에서 이 슬라이스가 **낙하 중 점프·충돌에서 심하게 발산**했다. 계측(`[ALIGN]` 로그)으로 근본 원인을 특정:

- **서버가 클라 입력을 2틱 늦게 처리하고 있었다.** 서버 `LOPRunner.ProcessInput`이 `Consume(buffer, serverTick − InputDelayTicks(2))`로 **입력-T를 서버 물리-틱 T+2에** 적용. 클라는 입력-T를 **즉시(틱 T)** 예측 적용. → 같은 틱 번호에서 클라·서버가 쓰는 입력이 **항상 2틱 어긋남**(측정값 `srvProcessTick − cliGenTick = 2`). 지상 이동에선 작지만(≈0.07m) 고속 낙하·충돌에서 커져 하드 롤백이 폭발.
- **옛 delta-replay는 이 오프셋을 뭉갰기에(정확 비교 안 함) 멀쩡했고**, 새 하드 복원+재생은 정확 비교라 노출됐다.

**수정(서버):** `InputDelayTicks = 0` — 입력을 스탬프된 틱에 제때 처리(표준 Gambetta/Overwatch: 지터 슬랙은 **클라 lead**가 담당, 서버가 늦춰서가 아님). 측정 `offset → 0`, 이동·낙하 모두 `dist ≈ 0`, 롤백 소멸, 체감 정상 복귀.

→ 따라서 이 슬라이스는 **"클라 전용"이 아니라 서버 한 줄 변경을 포함**한다(위 "무변경: 서버 전체"는 정정). 클라 코어(snap.tick anchor)는 애초에 옳았고, 디버깅 중 시도한 입력-시퀀스 anchor는 낙하 시 얼어붙는 잘못된 우회로였다 — 최종본에서 제거(개념 자체[서버 처리 시퀀스 ack]는 클럭 드리프트 대비 더 견고한 표준 대안으로, 필요 시 되살릴 검증된 프로토타입).

**남은 인지사항(버그 아님):** ① `InputDelayTicks=0`은 지터 슬랙을 클라 lead에 의존 → 저지연·고지터 실환경에선 lead 마진(Phase 4 동적 lead) 확인. ② A1 물리 재시뮬은 충돌 시 미세 드리프트 가능(fast-paced 표준과 동일 — 눈에 띄면 뷰 스무싱). ③ 이동만 재생(어빌리티/상태이상 후속).

## Open Questions (구현 plan에서 해소)

- `InputHistory` 링 자료구조: slice② `SnapshotHistory` 링 패턴 재사용/일반화 vs 전용.
- `Physics.SyncTransforms` 호출을 `IPhysicsSimulator`에 추가 vs `LOPRunner`에서 직접.
- `SnapReconciler` 축소 형태: 지연 렌더링만 남긴 뷰 컴포넌트로 rename/분리할지, 잔존할지.
- errorGate threshold 초기값(현 `SnapReconciler`의 0.06 등 참고) + 회전/속도도 게이트에 넣을지(위치만으로 충분한지).
- 재생 틱 수 상한(비정상적으로 큰 T 격차 시 텔레포트 폴백).

## 진행

- [x] 브레인스토밍 합의 (범위 A, 틱 앞 한 패스, sim 하드+뷰 지연렌더, errorGate, InputHistory)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
