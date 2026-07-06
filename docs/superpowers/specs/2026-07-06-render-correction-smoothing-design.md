# 내 캐릭 예측 보정 렌더 스무딩 (Render Correction Smoothing)

**Date:** 2026-07-06
**Branch:** `feature/render-correction-smoothing`
**Related:** [netcode-redesign](../../netcode-redesign.md) · [World Core 연결 아키텍처](../../world-core-connection-architecture.md) · [넉백 슬라이스 2](2026-07-05-velocity-knockback-slice2-design.md)

## Goal

내 캐릭(로컬 예측)이 **서버발 예측-불가 보정**(대표: 넉백)을 받을 때, 시뮬 위치가 한 프레임에 튀어 **순간이동처럼 보이는** 문제를 없앤다. 시뮬(권위)은 하드 보정 그대로 두고 **보이는 메시만** 그 보정 델타를 오프셋으로 잡아 시간에 걸쳐 감쇠 → 메시가 스르륵 따라붙게. 남 캐릭(원격)이 이미 부드러운 것과 체감을 맞춘다.

## 배경 — 근본 원인 (계측·리서치로 확정)

`Reconciler`가 서버 스냅을 받으면 내 캐릭 시뮬 위치를 **하드 세팅**(`entity.position = snap.position`)하고 현재 틱까지 재생한다. 걷기·대시는 *예측 가능*이라 보정량이 ~0이라 안 튀지만, **넉백은 예측 불가**(남이 가함)라 클라가 "넉백 0"으로 예측하다 뒤늦게(~RTT) 알게 되어 **누락분을 한 프레임에 따라잡는다.**

- **계측 결과(RTT ≈ 300ms, `[KB-DIAG]` 로그):** 재조정 창 `window≈10틱`, **교정거리 ≈ 0.6m**(더블히트 0.94m)를 한 프레임에 이동. 넉백(12틱 ≈ 400ms)이 RTT 창(10틱)보다 짧아, 발견 시점엔 이미 ~80% 진행 → 그 80%를 한 번에 catch-up = 튐.
- **왜 기존 보간기가 못 잡나:** `LocalEntityInterpolator`는 인접 두 **시뮬 스냅** 사이를 lerp하는 **틱↔프레임 지터용**이다. 큰 보정은 한 틱↔틱 스텝에 통째로 박혀 있어, 그 0.6m를 1틱(~33ms)에 그림 = 여전히 튐. (남 캐릭이 부드러운 건 지연 렌더가 아니라 `ServerStateReconciler`가 시뮬을 SmoothDamp로 완만화하기 때문 — 지연 렌더는 둘 다 동일.)
- **왜 재생 방식으론 못 고치나:** 넉백의 강한 초반 틱은 이미 서버 과거라 소급 적용 불가. 위치를 맞추려면 catch-up은 불가피 → **눈에만 부드럽게**가 유일한 표준 해법.

## 산업 표준 매핑 (웹 검증 — 2026-07-06)

정공법은 하나로 수렴: **true(시뮬) 위치와 render(표시) 위치를 분리 — true는 서버로 스냅+입력 재생(정확), render는 별도로 true까지 시간에 걸쳐 감쇠.**

- **Unreal `CharacterMovementComponent`:** autonomous proxy 캡슐=권위 보정, 움직임은 `NetworkSmoothingMode`(Exponential/Linear)·`SmoothClientPosition`으로 보간. 큰 보정은 `NetworkLargeClientCorrectionThreshold` 류로 스냅. ([Unreal Networked Movement Docs](https://dev.epicgames.com/documentation/unreal-engine/understanding-networked-movement-in-the-character-movement-component-for-unreal-engine))
- **Gambetta/4AM 계열:** "true 위치와 render 위치 분리, true 스냅 후 render를 lerp." 스무딩 ~100–250ms. ([4AM Games](https://fouramgames.com/blog/fast-paced-multiplayer-implementation-smooth-server-reconciliation) · [Gambetta](https://www.gabrielgambetta.com/client-side-prediction-server-reconciliation.html))
- **일반(Wikipedia/Grokipedia):** 러버밴딩은 보정을 **100–200ms에 걸쳐 스무딩**해 완화, true/render 분리. ([Wikipedia](https://en.wikipedia.org/wiki/Client-side_prediction))

> **주의(정직):** *분리·오프셋·지수감쇠·큰보정스냅*이라는 **뼈대·곡선·범위(100–250ms)는 표준**이다. 정확한 τ(150ms)·텔레포트 임계(3m)는 **그 표준 범위 안에서 튜닝하는 값**이고 유니버설 상수가 아니다(틱레이트 30Hz·이동속도·손맛 의존). Unreal도 프로젝트별 노브로 노출한다.

## 설계

`render = 보간된_시뮬_위치 + 감쇠하는_오프셋`. 시뮬/스냅/리플레이 무영향, 순전히 렌더.

### A. `RenderCorrectionOffset` (신규 DI 싱글턴 홀더)

`ReconciliationStats`와 동일 패턴(`Reconciler`가 쓰고 다른 컴포넌트가 읽는 게임-스코프 싱글턴):

- `Vector3 offset` (현재 시각 오차 오프셋; 초기 0)
- `AddCorrection(Vector3 delta)` — 보정 발생 시 `offset += delta` (누적 — 연속 히트 대응)
- `Vector3 SampleAndDecay(float dt)` — 지수 감쇠 후 현재 offset 반환:
  `offset *= MathF.Exp(-dt / SmoothingTime)`; 크기 < ε(예: 0.001m)면 0으로 스냅. `SmoothingTime = 0.15f`(150ms, 튜닝값).
- `Reset()` — offset = 0 (로컬 엔티티 교체/리스폰/씬 정리 시).

### B. `Reconciler` — 보정 델타를 오프셋에 누적

하드 보정 직후(계측이 이미 잡는 pre/post 활용):

- `Vector3 correction = prePredicted − postReconcile` (메시가 있던 자리 − 새 시뮬 자리).
- **텔레포트 예외:** `correction.magnitude > TeleportThreshold(=3m)`면 `AddCorrection` **생략**(의도적 큰 이동은 스냅 — 리스폰 등). 이하면 `renderCorrectionOffset.AddCorrection(correction)`.
- `prePredicted` = 메서드 진입 시 `entity.position`(예측된 현재). `postReconcile` = 재생 후 `entity.position`.
- `RenderCorrectionOffset`를 `Reconciler`에 `[Inject]`.
- **모든 보정에 적용**(넉백 한정 X): errorGate(>0.06)를 넘은 모든 하드 보정이 대상. 걷기/대시 오예측(드묾·작음)도 같이 매끄러워진다 — 특수 케이스 없음.

### C. `LocalEntityInterpolator` — 렌더에 오프셋 적용+감쇠

`LateUpdate`의 렌더 위치 계산(현재 `Vector3.Lerp(prev.position, next.position, t)`)에 오프셋을 더한다:

```
Vector3 renderOffset = renderCorrectionOffset.SampleAndDecay(Time.deltaTime);   // 매 프레임 감쇠
...
visualGameObject.transform.position = Vector3.Lerp(prev.position, next.position, t) + renderOffset;
```

- **보정 순간:** offset ≈ (pre − post) → render = post + (pre − post) = **pre**(메시 안 튐).
- **이후:** offset → 0 → 메시가 감쇠하며 시뮬(넉백 따라가는)로 스르륵 수렴.
- 감쇠는 **렌더 레이트(LateUpdate `Time.deltaTime`)** 로 — 시각 스무딩이라 fps 기준이 맞다. 지수라 프레임독립.
- **스냅 부재 가드(기존):** `entityTransformSnaps` 없어 early-return하는 프레임엔 렌더를 스킵. offset의 `SampleAndDecay`는 **guard 이전에 호출**해 스킵 프레임에도 감쇠가 멈추지 않게(그 프레임 값은 안 씀).
- `RenderCorrectionOffset`를 `LocalEntityInterpolator`에 `[Inject]`. 회전은 대상 아님(넉백은 위치만; 회전은 기존 Slerp 유지).

### D. 배선 / 생명주기

- `GameLifetimeScope`에 `builder.Register<RenderCorrectionOffset>(Lifetime.Singleton)` (ReconciliationStats 옆).
- 로컬 엔티티 바인딩/해제 시 `Reset()` — `LocalEntityInterpolator`의 초기화/`Cleanup`에서 호출(스테일 오프셋이 다음 캐릭에 새지 않도록).

## 튜닝 시작값 (표준 범위 내, 플레이로 조정)

| 파라미터 | 시작값 | 표준 근거 |
|---|---|---|
| `SmoothingTime`(τ) | **0.15s** | 웹 100–250ms 중앙. 짧게=빠릿(100ms)/길게=매끈(200–250ms) |
| `TeleportThreshold` | **3m** | 넉백(~0.6m)은 녹이고 리스폰(수 m)은 스냅. 게임 스케일 맞춰 조정 |
| 감쇠 곡선 | 지수 | Unreal `NetworkSmoothingMode.Exponential` |

## 검증

- **육안(핵심):** 계측 로그(`[KB-DIAG]`) 유지한 채 플레이 — 넉백 시 `교정거리≈0.6m`가 시뮬엔 그대로 찍히되 **메시엔 ~150ms 부드럽게** 나타나는지. 순간이동 소멸 확인. RTT 50/300ms.
- **무회귀:** 걷기/대시/정지/점프 — 작은 보정 스무딩이 조작 반응성/정확성 안 해치는지(오프셋은 시뮬 무영향이라 조작감 유지). 남 캐릭 넉백 무영향(경로 안 건드림).
- **엣지:** 연속 히트(오프셋 누적), 리스폰/큰 이동(텔레포트 임계로 스냅), 스냅 부재 프레임(감쇠 지속).
- 통과 후 **계측 로그 제거**.
- (선택) EditMode: `RenderCorrectionOffset.SampleAndDecay` 지수 감쇠·ε 스냅·AddCorrection 누적을 순수 단위 테스트(홀더가 순수 C#이면). MonoBehaviour 통합은 플레이 검증.

## 영향 파일 (개략 — 세부는 plan)

- **신규:** `RenderCorrectionOffset.cs`(홀더), (선택) EditMode 테스트.
- **수정:** `Reconciler.cs`(보정 델타 → AddCorrection + 텔레포트 가드; 계측 로그 자리 재활용 후 제거), `LocalEntityInterpolator.cs`(오프셋 적용+감쇠 + Reset), `GameLifetimeScope.cs`(등록).
- **무변경:** `ServerStateReconciler`(남 캐릭), 시뮬/스냅/리플레이/넷코드, World Core.

## Out of Scope

- **남 캐릭(원격) 스무딩** — 이미 SmoothDamp로 부드러움. 무변경.
- **회전 스무딩** — 넉백은 위치만. 회전 오차 스무딩은 별건.
- **"서버발 효과는 지연 렌더로 분리"(brainstorm의 Option B)** — 업계는 로컬 캐릭에 안 씀(지연 렌더는 원격 전용). 채택 안 함. 근본은 렌더 에러 스무딩(A).
- **velocity 권위 이전·예측/롤백 재설계(Stage④)** — 별개.

## Open Questions (plan/튜닝에서 해소)

- `SmoothingTime`·`TeleportThreshold` 최종값 — 플레이 손맛으로. 시작 0.15s/3m.
- `RenderCorrectionOffset`를 `Reset`할 정확한 훅(로컬 엔티티 재바인딩 위치) — 구현 시 확인.
- 오프셋 델타 계산의 틱 정합(pre=currentTick vs post=currentTick−1의 1틱 오차) — 시각 휴리스틱이라 무시 가능 수준, 필요 시 정밀화.

## 진행

- [x] systematic-debugging: 근본 원인 확정(계측 `[KB-DIAG]` + 웹 리서치) — 예측-불가 서버 보정의 catch-up, 기존 보간기는 지터용
- [x] 업계 표준 웹 검증 (Unreal NetworkSmoothing / Gambetta / 4AM)
- [x] 이 spec 작성
- [ ] spec self-review
- [ ] 사용자 spec 리뷰
- [ ] `writing-plans`로 구현 plan 작성
