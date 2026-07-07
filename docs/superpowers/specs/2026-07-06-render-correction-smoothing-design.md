# 내 캐릭 예측 보정 렌더 스무딩 (Render Error Smoothing) — 표준 재설계

**Date:** 2026-07-06 (표준 리서치 기반 전면 재설계)
**Branch:** `feature/render-correction-smoothing`
**Related:** [netcode-redesign](../../netcode-redesign.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [넉백 슬라이스 2](2026-07-05-velocity-knockback-slice2-design.md)

## Goal

내 캐릭(로컬 예측)이 **서버발 예측-불가 보정**(대표: 넉백)을 받을 때 시뮬 위치가 한 프레임에 튀어 **순간이동처럼 보이는** 문제를 없앤다. 시뮬(권위)은 하드 보정 그대로 두고, **보이는 위치만** 감쇠 오차(errorOffset)로 부드럽게 따라잡는다. 남 캐릭(원격)이 이미 부드러운 것과 체감을 맞춘다.

이 문서는 **4개 독립 소스(Unreal CMC · Fiedler/Gambetta/Source · Overwatch/Valorant · Photon Fusion/Quantum) 병렬 리서치로 만장일치 확인된 업계 표준**에 맞춘 재설계다. 초기 두 시도(오프셋 더하기 / 움직이는 타깃 Lerp)는 폐기한다.

## 배경 — 두 번의 실패와 근본 원인

1. **초기 "오프셋 더하기"** — `render = 지연보간(sim) + 감쇠오프셋(old−new)`. 지연보간 base가 *이미* 보정을 품고 전이하는데 거기에 sim공간 오프셋을 **또** 더해 이중계산 → `2·old − new` 오버슈트 = **번쩍**(넉백 반대편으로 튐).
2. **움직이는 타깃 Lerp** — `render = Lerp(render, 움직이는 지연보간 타깃, a)`. 오버슈트는 없앴지만 창 동안 **걷기까지 지연**시켜 stutter. 이를 "밴드 임계"로 덮으려 함 = workaround.

**두 실패의 공통 근원:** 보정(correction)을 *잘못된 기준계로, 잘못된 타이밍에* 렌더에 주입한 것. 순수 합성 로직 버그라 **유닛 테스트로 사전에 잡혔어야** 했다(→ 순수 커널 TDD로 교정).

## 산업 표준 매핑 (2026-07-06 병렬 리서치 — 4소스 만장일치)

정공법은 어디서나 **동일한 한 가지 형태**다: **권위(sim) 상태와 렌더(시각) 상태를 분리 — sim은 권위로 하드 스냅(절대 스무딩 X), 렌더는 sim으로 향하는 감쇠 오차 오프셋을 얹는다.**

| 소스 | 메커니즘 | 공식 / 상수 |
|---|---|---|
| **Unreal CMC** | `MeshTranslationOffset` (캡슐=권위 스냅, mesh만 오프셋) | 보정 시 `offset += (old − new)`; 감쇠 `offset *= (1 − dt/τ)`; τ=`NetworkSimulatedSmoothLocationTime` **0.1s**(정지 시 0.05s), 회전 0.033s; `NoSmoothNetUpdateDist` 초과 시 스냅 |
| **Fiedler (State Sync)** | `render = simPos + errorOffset` | 오프셋 갱신 `errorOffset = (oldSimPos + oldErrorOffset) − newSimPos`; 감쇠 프레임당 `*0.9`(적응형: 작은 오차≤0.25m는 0.95 부드럽게 / 큰 오차≥1m는 0.85 빠르게). 대못: *"시뮬 레벨엔 스무딩 걸지 마라 — extrapolation을 망친다"* |
| **Overwatch/Valorant** | 권위 스냅 + 시각 오프셋 감쇠(cl_smoothtime류) | `renderErrorOffset += (predicted − authoritative)`, 짧은 창(~0.1s) 감쇠. **넉백=별도 경로 없음**(일반 misprediction과 동일 기계) |
| **Fusion/Quantum** | Interpolated Error Correction | rate 3.3→10Hz(오차 크기별 램프), blend 0.25→1m, **minCorrection 0.025m**(이하 즉시채택), **teleport 2m**(초과 스냅) |

핵심 합의점:
- **sim은 절대 스무딩하지 않는다** — 하드 스냅. (우리 `Reconciler`가 이미 그렇게 함 — 유지)
- **보정은 오직 errorOffset에만** 표현 → 시각만 부드럽게, 권위는 정확.
- **오프셋 seed 공식이 번쩍을 원천 차단**: `errorOffset = (직전 렌더위치) − (새 권위위치)`. 이건 *실제 렌더-권위 격차*를 재는 값이라 base의 타이밍/램프와 무관하게 항상 연속.
- **sub-tick(고정틱 사이) = 보간**(Fiedler alpha) 표준. 로컬은 현재 근처, 원격은 과거. 우리 지연보간 유지 정당.

> **참고 — 우리와 동일 구조의 정본:** Unreal Chaos Networked Physics 문서: *"재시뮬 끝에 오브젝트가 튀면, 그 모드는 **렌더링만** 현재→새 상태로 보간한다."* 고정틱+재시뮬+렌더전용 스무딩 = 우리 케이스 그대로.

## 설계 (표준)

### 데이터 흐름

```
sim(권위)   Reconciler가 서버 스냅으로 하드 복원 + 입력 재생 → entity.position  (스무딩 X, 그대로)
                │
            보정 순간: smoother.OnCorrection(직전 렌더위치, 보정 전 sim, 보정 후 sim)
                │
render(시각) 매 틱: renderTarget = entity.position + smoother.Target-offset   (연속!)
            매 프레임: 렌더 = 보간(renderTarget[prev], renderTarget[curr], alpha)
```

**왜 stutter가 없나:** `renderTarget = sim + offset`이라 보정 순간 sim의 +d 스텝과 offset의 −d 스텝이 **상쇄되어 renderTarget이 연속**이다. 걷기 중 offset은 sim과 독립적으로 감쇠하므로 **걷기를 지연시키지 않는다**(sim은 제 속도로 흐르고 offset만 0으로 줄어듦). 그래서 작은 보정도 자연히 녹고, 하위 deadzone 밴드 같은 workaround가 불필요하다.

### A. `RenderCorrectionSmoother` — 순수 커널 (GameFramework.Netcode, System.Numerics, TDD)

기존 커널(움직이는 타깃 Lerp)을 **감쇠 오프셋 모델로 전면 교체**. `ReconcileGate`/`SnapshotHistory`와 같은 자리(`Runtime/Scripts/Netcode/`).

상태 + 순수 로직. 커널이 **마지막으로 낸 렌더 위치를 캐시**(`_lastTarget`)해서 보정 seed의 기준으로 쓴다 — Reconciler가 렌더 위치를 따로 조회할 필요 없음(배선 단순화):
- ctor `RenderCorrectionSmoother(float tau, float minCorrection, float teleport)`.
- `Vector3 Target(Vector3 simPos)`: `_lastTarget = simPos + _offset; return _lastTarget;` (렌더 컴포넌트가 매 틱 호출; 캐시 갱신).
- `void OnCorrection(Vector3 oldSimPos, Vector3 newSimPos)`:
  - `mag = Distance(oldSimPos, newSimPos)` (보정 크기).
  - `mag < minCorrection` (무시할 만큼 작음) 또는 `mag > teleport` (리스폰 등 너무 큼) → `_offset = 0` (렌더가 sim을 정확히 따름 = 즉시채택/스냅).
  - 그 사이 → `_offset = _lastTarget − newSimPos` (렌더를 직전 표시 위치에 유지 → 이후 감쇠로 new에 수렴). **오버슈트 불가**(new를 향한 볼록 감쇠). *즉 다음 `Target(newSimPos)`이 정확히 `_lastTarget`을 돌려줌 → renderTarget 연속.*
- `void DecayTick(float dt)`: `_offset *= MathF.Exp(-dt / tau)` (프레임독립 지수 감쇠, 0으로).
- `void Reset()`: `_offset = 0; _lastTarget = 0;` (또는 미초기화 플래그).

`Vector3` = `System.Numerics.Vector3`. UnityEngine 무의존 → 순수 테스트.

### B. EditMode 테스트 (TDD — 버그를 인코딩)

`GameFramework/Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs`:

- **오버슈트 없음 (핵심 — 번쩍 회귀 방지):** `Target(old)`(캐시 seed) → `OnCorrection(old, new)` (mag 밴드 내, 예 0.6m) 직후 `Target(new)` == **old**(2·old−new 아님). 반복 `DecayTick` 시 `Target(new)`이 **단조로 new에 수렴하며 new를 넘지 않는다.**
- **정상 걷기 지연 0:** `OnCorrection` 없이 `Target(sim)` == `sim` 그대로(오프셋 0).
- **감쇠 수렴:** 여러 `DecayTick` 후 `Target(new)` → new(오프셋 → 0).
- **프레임독립:** 다른 dt 분할로 같은 총시간 감쇠 시 결과 근사 동일.
- **즉시채택(작은 보정):** `Target(old)` → `mag < minCorrection`인 `OnCorrection(old, new)` 후 `Target(new)` == new(오프셋 0).
- **스냅(큰 보정):** `Target(old)` → `mag > teleport`인 `OnCorrection(old, new)` 후 `Target(new)` == new(오프셋 0).
- **Reset:** 재초기화 후 `Target(sim)` == sim.

### C. `Reconciler` — 보정 순간 seed

하드 복원+재생 완료 후(메서드 끝, early-return 제외):
- `oldSimPos` = 하드 복원 **전** 예측 위치(`preCorrectionPos`, 이미 캡처 중).
- `newSimPos` = 재생 후 `entity.position`.
- `smoother.OnCorrection(oldSimPos, newSimPos)`. (직전 렌더 위치는 커널이 `_lastTarget`로 캐시 중이라 넘기지 않음.)

### D. `LocalEntityInterpolator` → 렌더 타깃 보간 (전면 재작성)

기존 지연보간 컴포넌트를 **renderTarget(=sim+offset) 스트림 보간**으로 재작성:
- 매 틱 End: `smoother.DecayTick(tickInterval)` 후 `renderTargetSnap[tick] = smoother.Target(entity.position)` 기록. (renderTarget은 연속이므로 보정이 스텝으로 안 들어감.)
- 매 프레임 LateUpdate: 기존과 동일하게 `renderTime = elapsed − tickInterval`로 prev/next 틱을 찾아 `Vector3.Lerp(renderTargetSnap[prev], renderTargetSnap[next], alpha)`로 렌더. 스냅 부재 시 스킵(직전 위치 유지).
- 회전은 기존 Slerp 유지(넉백은 위치만).
- `Cleanup`에서 `smoother.Reset()`.

> seed 타이밍: `OnCorrection`(Reconcile, 틱 시작)과 renderTargetSnap 기록(End, 틱 끝)의 순서로 seed 틱의 renderTarget이 직전과 연속임을 보장. seed 틱은 감쇠를 건너뛰거나 1틱 감쇠 후 기록(오차 무시 가능) — 구현 plan에서 TDD로 확정.

### E. 배선 / 생명주기

- `GameLifetimeScope`에 `builder.Register(_ => new GameFramework.Netcode.RenderCorrectionSmoother(Tau, MinCorrection, Teleport), Lifetime.Singleton)` (`ReconciliationStats` 옆).
- `Reconciler`·`LocalEntityInterpolator`가 `[Inject]`.

## 튜닝 시작값 (표준 범위 내, 플레이로 조정)

| 파라미터 | 시작값 | 근거 |
|---|---|---|
| `Tau`(τ) | **0.1s** | Unreal `NetworkSimulatedSmoothLocationTime` / Source `cl_smoothtime` 정확히 이 값 |
| `MinCorrection` | **0.025m** | Fusion `PosMinCorrection`. 이하 즉시채택 |
| `Teleport` | **3m** | 넉백(~0.6m) 녹이고 리스폰(수 m) 스냅. Fusion 2m~우리 3m |
| 감쇠 곡선 | 지수 `exp(−dt/τ)` | Unreal Exponential 모드 |

> 적응형 rate(작은 오차 부드럽게 / 큰 오차 빠르게, Fiedler·Fusion)는 시작값에선 생략. 단일 τ로 충분한지 플레이로 보고, 부족하면 오차 크기별 τ 램프를 후속 튜닝으로 추가.

## 검증

- **EditMode(핵심):** B의 순수 커널 테스트 — 특히 오버슈트-없음(번쩍 회귀 방지) + 정상 걷기 지연 0.
- **육안:** 넉백 시 DebugHud reconciliation distance(~0.6m)는 그대로지만 **메시는 번쩍 없이 ~0.1s 부드럽게** 이동. 걷기 stutter 없음. RTT 50/300ms.
- **무회귀:** 걷기/대시/정지/점프 조작감·정확성 그대로(스무더는 시뮬 무영향, 오프셋 0), 남 캐릭 무영향.
- **엣지:** 연속 히트(오프셋 재seed), 리스폰/큰 이동(teleport 초과 → 스냅), 스냅 부재 프레임.

## 영향 파일

- **재작성(GameFramework):** `Runtime/Scripts/Netcode/RenderCorrectionSmoother.cs`(감쇠 오프셋 모델로 전면 교체), `Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs`(테스트 재작성).
- **재작성(클라):** `LocalEntityInterpolator.cs`(renderTarget 스트림 보간), `Reconciler.cs`(OnCorrection seed로 교체 — 기존 MarkCorrection/[SMOOTH-DIAG] 제거), `GameLifetimeScope.cs`(ctor 시그니처 갱신).
- **무변경:** `ServerStateReconciler`(남 캐릭), 시뮬/스냅/리플레이/넷코드, World Core.

## Out of Scope

- 남 캐릭 스무딩(이미 SmoothDamp), 회전 스무딩(위치만).
- **sub-tick을 외삽/부분틱 재시뮬로 바꾸기** — 지연보간 유지가 최소 변경이고 Fiedler 표준. 내 캐릭 33ms 지연을 깎는 별도 니즈가 생기면 독립 결정(Fusion "Render Prediction" / N4E partial-tick).
- 적응형 오차별 rate 램프(Fiedler·Fusion) — 단일 τ로 부족할 때 후속 튜닝.
- standalone .NET TDD 툴 셋업(별도 이니셔티브).

## 진행

- [x] systematic-debugging: 번쩍/stutter 근본 원인 확정
- [x] 4소스 병렬 리서치 — 업계 표준 만장일치 확인(offset-decay, sim 하드스냅, seed 공식, sub-tick 보간)
- [x] 이 spec 작성 (표준 재설계)
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성 (TDD)
