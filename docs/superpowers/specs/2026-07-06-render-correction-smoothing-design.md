# 내 캐릭 예측 보정 렌더 스무딩 (Render Correction Smoothing)

**Date:** 2026-07-06
**Branch:** `feature/render-correction-smoothing`
**Related:** [netcode-redesign](../../netcode-redesign.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [넉백 슬라이스 2](2026-07-05-velocity-knockback-slice2-design.md)

## Goal

내 캐릭(로컬 예측)이 **서버발 예측-불가 보정**(대표: 넉백)을 받을 때, 시뮬 위치가 한 프레임에 튀어 **순간이동처럼 보이는** 문제를 없앤다. 시뮬(권위)은 하드 보정 그대로 두고 **보이는 메시만** 지연 렌더 타깃으로 부드럽게 이징한다. 남 캐릭(원격)이 이미 부드러운 것과 체감을 맞춘다.

## 배경 — 근본 원인 (계측·리서치로 확정)

`Reconciler`가 서버 스냅을 받으면 내 캐릭 시뮬 위치를 **하드 세팅**하고 현재 틱까지 재생한다. 걷기·대시는 *예측 가능*이라 보정량이 ~0이라 안 튀지만, **넉백은 예측 불가**(남이 가함)라 클라가 "넉백 0"으로 예측하다 뒤늦게(~RTT) 알게 되어 **누락분을 한 프레임에 따라잡는다.**

- **계측(RTT ≈ 300ms):** 재조정 창 `~10틱`, **교정거리 ≈ 0.6m**(더블히트 0.94m)를 한 프레임에 이동. 넉백(12틱 ≈ 400ms)이 RTT 창보다 짧아 발견 시점엔 이미 ~80% 진행 → 그 80%를 한 번에 catch-up = 튐.
- **강한 초반 틱은 이미 서버 과거**라 소급 적용 불가 → catch-up 불가피 → **눈에만 부드럽게**가 유일한 표준 해법.

## 산업 표준 매핑 (웹 검증 — 2026-07-06)

정공법: **true(시뮬) 위치와 render(표시) 위치를 분리 — true는 서버로 스냅+입력 재생(정확), render는 별도로 true로 시간에 걸쳐 이징.**

- **Unreal CMC:** autonomous proxy 캡슐=권위 보정, 움직임은 `NetworkSmoothingMode`(Exponential/Linear)·`SmoothClientPosition`으로 보간. 큰 보정은 `NetworkLargeClientCorrectionThreshold` 류로 스냅. ([Unreal Networked Movement](https://dev.epicgames.com/documentation/unreal-engine/understanding-networked-movement-in-the-character-movement-component-for-unreal-engine))
- **Gambetta/4AM:** "true 위치와 render 위치 분리, true 스냅 후 render를 lerp." 스무딩 ~100–250ms. ([4AM Games](https://fouramgames.com/blog/fast-paced-multiplayer-implementation-smooth-server-reconciliation) · [Gambetta](https://www.gabrielgambetta.com/client-side-prediction-server-reconciliation.html))

## ⚠️ 설계 수정 (2026-07-06) — 초기 "오프셋 더하기"가 번쩍 버그를 냄

**초기 구현(폐기):** `render = 지연보간(시뮬 스냅) + 감쇠 오프셋(old−new)`. 이건 **틀렸다.** 지연 보간 base가 *이미* 보정을 품고 old→new로 전이하는데(보정된 스냅이 들어오니까), 거기에 오프셋(old−new)을 **또** 더해 **이중 계산** → 보정 순간 `render = old + (old−new) = 2·old − new` = **new의 반대편으로 오버슈트**. 넉백 반대 방향으로 한순간 튀었다 돌아오는 **"번쩍"** 이 이것. (플레이에서 사용자 관찰.)

- **왜 표준을 따랐는데 틀렸나:** 표준은 렌더 위치 *하나*를 true로 이징한다. 우리는 기존 지연 보간(jitter용) *위에* 오프셋을 얹어 **스무더 2개를 잘못 합성**했다. 오프셋의 기준계(시뮬 공간 old−new)가 base(지연 보간, 전이 중)와 안 맞음.
- **교훈:** 순수 합성 로직 버그라 **유닛 테스트로 사전에 잡혔어야** 했다. 클라 Assembly-CSharp라 EditMode 불가를 이유로 테스트를 건너뛴 게 구멍. → 아래 corrected 설계는 **순수 커널을 `GameFramework.Netcode`(테스트 가능 제자리)에 두고 TDD**한다.

## 설계 (corrected)

렌더 위치를 **하나의 스무더**로 관리한다: 보정 직후 짧은 창 동안만 지연-보간 타깃으로 이징, 평소엔 타깃을 정확히 추종(랙 0). `render = 지연보간 target` 을 스무더에 통과.

### A. `RenderCorrectionSmoother` — 순수 커널 (GameFramework.Netcode, System.Numerics, TDD)

`ReconcileGate`/`SnapshotHistory`와 같은 자리(`Runtime/Scripts/Netcode/`, 순수 `System.Numerics`, app-비종속 넷코드 primitive). **테스트하려고 옮기는 게 아니라 원래 제자리** — 넷코드 보정 스무딩은 그 계열이다.

상태 + 순수 로직:
- ctor `RenderCorrectionSmoother(float smoothingTime)`.
- `void MarkCorrection(float window)` — 보정 발생 시 이징 창 타이머 세팅.
- `Vector3 Smooth(Vector3 target, float dt)`:
  - 첫 호출: `_current = target` 초기화 후 반환.
  - 창 활성(`_windowRemaining > 0`): `_windowRemaining -= dt`; `_current = Vector3.Lerp(_current, target, 1 − MathF.Exp(−dt/smoothingTime))` (지수 이징, 프레임독립, **타깃으로만 수렴 — 오버슈트 없음**).
  - 창 비활성: `_current = target` (정확 추종, 랙 0).
  - `_current` 반환.
- `void Reset()` — 초기화(로컬 엔티티 교체 시).

`Vector3` = `System.Numerics.Vector3`(`Lerp` 있음), `MathF.Exp`. UnityEngine 무의존 → 순수 테스트.

### B. EditMode 테스트 (TDD — 버그를 인코딩)

`GameFramework/Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs` (`baegames.GameFramework.Runtime.Tests`, 기존 `ReconcileGateTests`/`SnapshotHistoryTests`와 동거):

- **오버슈트 없음(핵심 — 초기 버그를 잡는 테스트):** `MarkCorrection` 후 target이 old→new로 점프한 상태에서 첫 `Smooth`는 **old 근처**(old와 new 사이)를 반환하고, 반복 호출 시 **단조로 new에 수렴하며 new를 넘지 않는다.** (버그판 `2old−new`는 이 단언에서 실패.)
- **평소 랙 0:** `MarkCorrection` 없이 `Smooth(target, dt)`는 매번 `target`을 그대로 반환.
- **창 만료:** 창 시간 경과 후 `Smooth`는 target로 스냅.
- **프레임독립:** 다른 dt 분할로 같은 총시간 이징 시 결과 근사 동일.
- **Reset:** 재초기화 후 첫 호출이 target로.

### C. `Reconciler` — 보정 "신호" 발행 (Vector3 오프셋 아님)

하드 보정+재생 완료(메서드 끝, early-return 제외) 후:
- `Vector3 correction = preCorrectionPos − entity.position`(크기 판정용, preCorrectionPos는 하드 복원 전 캡처).
- `correction.magnitude <= TeleportThreshold(3m)`면 `renderCorrectionSmoother.MarkCorrection(CorrectionWindow)`. 초과(리스폰 등)면 신호 안 함 → 스무더가 정확 추종 = 스냅.
- 신호만 — 델타값은 스무더가 안 씀(타깃으로 이징하니까).

### D. `LocalEntityInterpolator` — 스무더 통과

`LateUpdate`에서 지연-보간 타깃을 스무더에 통과시켜 렌더:
```
Vector3 target = Vector3.Lerp(prev.position, next.position, t);   // 기존 지연 보간(jitter)
render = renderCorrectionSmoother.Smooth(target.ToNumerics(), Time.deltaTime).ToUnity();
visualGameObject.transform.position = render;
```
- 스냅 부재 가드로 렌더 스킵 시에도 스무더 상태가 어긋나지 않게, **가드 통과해 target이 있을 때만** `Smooth` 호출(타깃 없으면 직전 위치 유지 — 기존 동작).
- 회전은 기존 Slerp 유지(넉백은 위치만).
- `Cleanup`에서 `renderCorrectionSmoother.Reset()`.

### E. 배선 / 생명주기

- `GameLifetimeScope`에 `builder.Register(_ => new GameFramework.Netcode.RenderCorrectionSmoother(SmoothingTime), Lifetime.Singleton)` (SmoothingTime 상수 주입, `ReconciliationStats` 옆).
- `Reconciler`·`LocalEntityInterpolator`가 `[Inject]`.

## 튜닝 시작값 (표준 범위 내, 플레이로 조정)

| 파라미터 | 시작값 | 근거 |
|---|---|---|
| `SmoothingTime`(τ) | **0.15s** | 웹 100–250ms 중앙 |
| `CorrectionWindow` | **0.3s** | τ의 ~2배 — 만료 전 대부분 수렴(스냅 안 보이게). 창 > τ |
| `TeleportThreshold` | **3m** | 넉백(~0.6m) 녹이고 리스폰(수 m) 스냅 |
| 이징 곡선 | 지수 | Unreal `NetworkSmoothingMode.Exponential` |

## 검증

- **EditMode(핵심):** B의 순수 커널 테스트 — 특히 오버슈트-없음(초기 버그 재발 방지).
- **육안:** 넉백 시 시뮬 교정거리(DebugHud reconciliation distance)는 ~0.6m 그대로지만 **메시는 번쩍 없이 ~150ms 부드럽게** 이동. RTT 50/300ms.
- **무회귀:** 걷기/대시/정지/점프 조작감·정확성 그대로(스무더는 시뮬 무영향, 평소 랙 0), 남 캐릭 무영향.
- **엣지:** 연속 히트(창 재세팅), 리스폰/큰 이동(임계 초과 → 스냅), 스냅 부재 프레임.

## 영향 파일 (개략 — 세부는 plan)

- **신규(GameFramework):** `Runtime/Scripts/Netcode/RenderCorrectionSmoother.cs`, `Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs`.
- **수정(클라):** `Reconciler.cs`(보정 신호 + 텔레포트 가드), `LocalEntityInterpolator.cs`(스무더 통과 + Reset), `GameLifetimeScope.cs`(등록).
- **무변경:** `ServerStateReconciler`(남 캐릭), 시뮬/스냅/리플레이/넷코드, World Core.

## Out of Scope

- 남 캐릭 스무딩(이미 SmoothDamp), 회전 스무딩, "서버발 효과 지연 렌더(Option B)"(로컬엔 비표준), velocity 권위/Stage④.
- standalone .NET TDD 툴 셋업(별도 이니셔티브 — SDK 설치 + Unity 패키지 순수 소스 테스트 패턴).

## Open Questions (plan/튜닝에서 해소)

- `SmoothingTime`·`CorrectionWindow`·`TeleportThreshold` 최종값 — 플레이 손맛.
- `Reset` 훅 위치(로컬 엔티티 재바인딩) — Cleanup으로 충분한지 구현 시 확인.

## 진행

- [x] systematic-debugging: 순간이동 근본 원인 확정(계측 + 웹) + **번쩍 버그 근본 원인 확정**(오프셋/지연base 이중계산 오버슈트)
- [x] 업계 표준 웹 검증
- [x] 이 spec 작성 + **corrected 설계로 수정**
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성 (TDD)
