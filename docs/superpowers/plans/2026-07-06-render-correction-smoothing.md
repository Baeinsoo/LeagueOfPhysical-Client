# 예측 보정 렌더 스무딩 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 내 캐릭이 넉백 등 예측-불가 서버 보정을 받을 때 시뮬 하드보정이 순간이동처럼 보이는 것을, 시뮬은 그대로 두고 보이는 메시만 오차 오프셋을 지수 감쇠시켜 부드럽게 만든다.

**Architecture:** `render = 보간된_시뮬 + 감쇠_오프셋`. 신규 `RenderCorrectionOffset` 홀더(게임-스코프 Singleton, `ReconciliationStats`와 동일 패턴)에 `Reconciler`가 하드 보정 델타를 누적하고, `LocalEntityInterpolator`가 `LateUpdate`에서 렌더에 더한 뒤 지수 감쇠(Unreal `NetworkSmoothingMode.Exponential` 대응)시킨다. 시뮬/스냅/리플레이/남 캐릭은 무영향.

**Tech Stack:** C#/Unity(클라 Assembly-CSharp), VContainer(DI), `UnityEngine.Vector3`/`Mathf.Exp`.

## Global Constraints

- **브랜치:** LOP-Client `feature/render-correction-smoothing` (단일 레포). 이미 spec 커밋 있음.
- **시뮬 무영향:** 시뮬 위치/스냅/리플레이/`entity.position` 권위 경로를 바꾸지 않는다. 오프셋은 **보이는 메시(`visualGameObject.transform`)에만** 적용.
- **모든 보정에 적용(넉백 한정 X):** errorGate(>0.06)를 넘어 하드 보정+재생이 완료된 모든 경우가 대상. 걷기/대시 오예측(작음)도 같이 매끄러워진다.
- **텔레포트 예외:** 보정 델타 크기 `> 3m`(리스폰 등 의도적 큰 이동)면 오프셋에 넣지 않는다(즉시 스냅).
- **감쇠:** 지수 `Offset *= Mathf.Exp(-dt / SmoothingTime)`, `SmoothingTime = 0.15f`(150ms, 표준 100–250ms 범위 내 튜닝값), 크기 < `0.001m`면 0 스냅.
- **회전 무대상:** 넉백은 위치만. 회전은 기존 Slerp 유지.
- **테스트 경계:** 클라 코드는 Assembly-CSharp라 EditMode 단위 테스트 불가(asmdef가 Assembly-CSharp 참조 불가 — 프로젝트 기존 제약). **컴파일 검증(컨트롤러) + 플레이 검증**으로 확인(기존 netcode와 동일). 보정 거리는 이미 `ReconciliationStats`→DebugHud가 수치로 표시.
- **임시 계측 제거:** 현재 `Reconciler.cs` working tree에 `[KB-DIAG]` 임시 진단 로그가 있다(미커밋). Task 2에서 제거한다.
- **네이밍:** `RenderCorrectionOffset`(서술적, Unreal `NetworkSmoothing` 대응 — spec 산업표준 절 참고).

경로 약칭: `CLIENT=C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client`. Unity 인스턴스(컨트롤러 검증): `LeagueOfPhysical-Client@de70658b9450cbb4`(해시 라이브 확인).

---

### Task 1: RenderCorrectionOffset 홀더 + DI 등록

시각 오차 오프셋 홀더를 만들고 게임 스코프에 등록한다. `ReconciliationStats`와 동일 패턴(순수 C# Singleton).

**Files:**
- Create: `CLIENT\Assets\Scripts\Entity\RenderCorrectionOffset.cs`
- Modify: `CLIENT\Assets\Scripts\Game\GameLifetimeScope.cs:83` (등록)

**Interfaces:**
- Produces: `RenderCorrectionOffset` — `Vector3 Offset { get; }`, `void AddCorrection(Vector3 delta)`, `Vector3 SampleAndDecay(float deltaTime)`, `void Reset()`.

- [ ] **Step 1: 홀더 클래스 작성**

`CLIENT\Assets\Scripts\Entity\RenderCorrectionOffset.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 내 캐릭 예측 보정의 "시각 오차 오프셋" 홀더(게임 스코프 Singleton). Reconciler가 하드 보정 시 델타를
    /// 누적하고, LocalEntityInterpolator가 render에 더한 뒤 지수 감쇠시켜 메시를 부드럽게 따라붙게 한다.
    /// 시뮬(권위)은 무영향 — 순전히 보이는 위치만. 산업표준: Unreal NetworkSmoothingMode(Exponential).
    /// </summary>
    public class RenderCorrectionOffset
    {
        private const float SmoothingTime = 0.15f;   // 오차 감쇠 시간상수(초). 100~250ms 표준 범위 내 튜닝값
        private const float SnapEpsilon = 0.001f;    // 이보다 작으면 0으로 스냅

        public Vector3 Offset { get; private set; }

        /// <summary>하드 보정 델타(메시가 있던 자리 − 새 시뮬 자리)를 누적한다(연속 히트 대응).</summary>
        public void AddCorrection(Vector3 delta)
        {
            Offset += delta;
        }

        /// <summary>오프셋을 지수 감쇠시키고 현재값을 반환한다. 렌더 프레임마다(LateUpdate) 호출.</summary>
        public Vector3 SampleAndDecay(float deltaTime)
        {
            Offset *= Mathf.Exp(-deltaTime / SmoothingTime);
            if (Offset.sqrMagnitude < SnapEpsilon * SnapEpsilon)
            {
                Offset = Vector3.zero;
            }
            return Offset;
        }

        public void Reset()
        {
            Offset = Vector3.zero;
        }
    }
}
```

- [ ] **Step 2: GameLifetimeScope에 등록**

`CLIENT\Assets\Scripts\Game\GameLifetimeScope.cs`의 `builder.Register<ReconciliationStats>(Lifetime.Singleton);`(83행) 바로 아래에 추가:

```csharp
            builder.Register<RenderCorrectionOffset>(Lifetime.Singleton);
```

- [ ] **Step 3: 컴파일 확인 (컨트롤러)**

> 컨트롤러가 UnityMCP로 클라 refresh + `read_console`(error). Expected: 에러 0. `.meta`(신규 파일)는 refresh가 생성 → 커밋에 포함.

- [ ] **Step 4: 커밋 (.meta 포함)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/Entity/RenderCorrectionOffset.cs Assets/Scripts/Entity/RenderCorrectionOffset.cs.meta Assets/Scripts/Game/GameLifetimeScope.cs
git commit -m "feat(netcode): RenderCorrectionOffset 홀더 (예측 보정 렌더 스무딩)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: Reconciler — 보정 델타 누적 + 임시 계측 제거

하드 보정+재생이 끝나면 보정 델타(예측 위치 − 재생 후 위치)를 오프셋에 누적한다. 큰 보정(텔레포트)은 제외. 현재 있는 `[KB-DIAG]` 임시 로그는 제거한다.

**Files:**
- Modify: `CLIENT\Assets\Scripts\Entity\Reconciler.cs`

**Interfaces:**
- Consumes: `RenderCorrectionOffset.AddCorrection(Vector3)` (Task 1).
- Produces: (동작만 — 후속 태스크가 소비하는 새 시그니처 없음.)

> 배경: `Reconcile`엔 여러 early-return(errorGate, abilityState 부재, MaxReplayTicks 텔레포트 폴백, buffer null)이 있다. **정상 경로(재생 완료)에서만** 오프셋을 먹인다 — 텔레포트 폴백 등 degenerate 경로는 스냅(오프셋 미적용)이 맞다. 그래서 `AddCorrection`은 메서드 **맨 끝**(재생 루프 뒤)에 둔다.

- [ ] **Step 1: RenderCorrectionOffset 주입 + 텔레포트 상수 추가**

`Reconciler.cs`의 필드 선언부, `[Inject] private ReconciliationStats reconciliationStats;`(28행) 아래에 추가:

```csharp
        [Inject] private RenderCorrectionOffset renderCorrectionOffset;
```

그리고 클래스 상단 상수(`MaxReplayTicks` 옆, 15행 근처)에 추가:

```csharp
        private const float TeleportThreshold = 3f;   // 이보다 큰 보정(리스폰 등)은 스무딩 안 하고 스냅
```

- [ ] **Step 2: 임시 [KB-DIAG] 계측 제거 + preCorrectionPos 캡처로 대체**

`Reconcile`의 worldEntity null 체크 직후에 있는 **`[KB-DIAG 임시계측]` 블록 전체**(`Vector3 kbPrePredicted = entity.position;` … `bool kbHasKnockback = false;` … `if (snap.contributions != null) { foreach ... }`)를 아래 한 줄로 교체:

```csharp
            // 예측된 현재 위치 — 하드 보정 전. 재생 후와의 차이가 보정 델타(시각 스무딩용).
            Vector3 preCorrectionPos = entity.position;
```

- [ ] **Step 3: 게이트 스킵 로그 제거**

errorGate 스킵 블록의 `[KB-DIAG] GATE-SKIP` `if (kbHasKnockback) { Debug.Log(...); }` 를 제거하고 원래대로 되돌린다:

```csharp
                if (!GameFramework.Netcode.ReconcileGate.ShouldReconcile(predicted.Position, authoritative, Threshold))
                {
                    return;
                }
```

- [ ] **Step 4: 재생 후 보정 델타를 오프셋에 누적 (+ 끝 로그 제거)**

재생 루프 닫는 `}` 직후에 있는 **`[KB-DIAG] REPLAYED` 로그 블록 전체**(`if (kbHasKnockback) { … Debug.Log(...) }`)를 아래로 교체:

```csharp
            // 하드 보정으로 시뮬이 튄 만큼(예측 위치 − 재생 후 위치)을 시각 오프셋에 누적한다.
            // 메시는 이 오프셋만큼 뒤처져 그려지다 지수 감쇠로 시뮬에 부드럽게 수렴(시뮬 무영향).
            // 리스폰 같은 큰 보정은 스냅(오프셋 미적용). 정상 재생 경로에서만 실행됨(early-return 제외).
            Vector3 correction = preCorrectionPos - entity.position;
            if (correction.magnitude <= TeleportThreshold)
            {
                renderCorrectionOffset.AddCorrection(correction);
            }
```

- [ ] **Step 5: 컴파일 확인 (컨트롤러)**

> 컨트롤러 refresh + `read_console`. Expected: 에러 0. `[KB-DIAG]` 로그가 코드에서 사라졌는지 확인(`grep KB-DIAG` = 0건).

- [ ] **Step 6: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/Entity/Reconciler.cs
git commit -m "feat(netcode): Reconciler가 보정 델타를 RenderCorrectionOffset에 누적 (+임시계측 제거)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: LocalEntityInterpolator — 렌더에 오프셋 적용 + 감쇠

보이는 메시 위치에 감쇠하는 오프셋을 더해, 큰 보정이 여러 프레임에 걸쳐 부드럽게 나타나게 한다.

**Files:**
- Modify: `CLIENT\Assets\Scripts\Entity\LocalEntityInterpolator.cs`

**Interfaces:**
- Consumes: `RenderCorrectionOffset.SampleAndDecay(float)`, `.Reset()` (Task 1).

- [ ] **Step 1: RenderCorrectionOffset 주입**

`LocalEntityInterpolator.cs`의 `[Inject] private IRunner runner;`(15행) 아래에 추가:

```csharp
        [Inject] private RenderCorrectionOffset renderCorrectionOffset;
```

- [ ] **Step 2: LateUpdate에서 오프셋 적용 + 감쇠**

`LateUpdate` 전체를 아래로 교체(오프셋 감쇠는 렌더 스킵 가드 **이전**에 호출해 스킵 프레임에도 감쇠가 계속되게):

```csharp
        private void LateUpdate()
        {
            // 시각 오차 오프셋은 렌더 프레임마다 감쇠(아래 가드로 렌더를 스킵해도 감쇠는 멈추지 않게 먼저 호출).
            Vector3 renderOffset = renderCorrectionOffset.SampleAndDecay(Time.deltaTime);

            if (entityView.visualGameObject == null || entityTransformSnaps.Count < 2)
            {
                return;
            }

            double tickInterval = Runner.Time.tickInterval;
            double renderTime = Runner.Time.elapsedTime - tickInterval;

            long tickPrev = (long)(renderTime / tickInterval);
            long tickNext = tickPrev + 1;
            float t = (float)((renderTime % tickInterval) / tickInterval);

            // 역산 틱이 버퍼에 없으면 이번 프레임 보간 스킵(직전 위치 유지).
            if (entityTransformSnaps.TryGetValue(tickPrev, out var prev) == false ||
                entityTransformSnaps.TryGetValue(tickNext, out var next) == false)
            {
                return;
            }

            // render = 보간된 시뮬 위치 + 감쇠하는 오차 오프셋. 보정 순간 메시는 이전 자리에 머물고,
            // 오프셋이 0으로 줄며 시뮬로 스르륵 수렴한다(시뮬은 무영향).
            entityView.visualGameObject.transform.position =
                Vector3.Lerp(prev.position, next.position, t) + renderOffset;
            entityView.visualGameObject.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(prev.rotation), Quaternion.Euler(next.rotation), t);
        }
```

- [ ] **Step 3: 정리 시 오프셋 리셋**

홀더는 게임 스코프 Singleton이라 로컬 엔티티가 교체돼도 살아있다 → 스테일 오프셋이 다음 캐릭에 새지 않게, `Cleanup`에서 리셋. `Cleanup` 메서드를 아래로 교체:

```csharp
        public void Cleanup()
        {
            runner.RemoveListener(this);
            renderCorrectionOffset.Reset();
        }
```

- [ ] **Step 4: 컴파일 확인 (컨트롤러)**

> 컨트롤러 refresh + `read_console`. Expected: 에러 0.

- [ ] **Step 5: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/Entity/LocalEntityInterpolator.cs
git commit -m "feat(netcode): LocalEntityInterpolator가 render에 감쇠 오프셋 적용 (튐 스무딩)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: 끝-끝 플레이 검증 + 튜닝

넉백 튐이 시각적으로 사라졌는지, 시뮬/조작/남 캐릭 무회귀인지 확인하고 손맛을 조정한다.

**Files:** (없음 — 관측/검증/튜닝)

- [ ] **Step 1: 서버·클라 Play + 접속**

(auth 막히면 서버 playerList uuid 픽스처.) DebugHud의 reconciliation distance(last/avg/max) 표시 확인.

- [ ] **Step 2: 로컬 넉백 튐 소멸 (핵심)**

내 캐릭이 넉백 맞게 하고 **RTT 300ms 주입**(LatencySimulation)으로 재현:
- DebugHud의 recon distance는 여전히 **~0.6m 튐을 수치로** 기록(시뮬은 그대로 하드 보정 = 정상)하지만,
- **보이는 메시는 순간이동 없이 ~150ms에 걸쳐 부드럽게** 밀리는지 육안 확인.
Expected: 시뮬 수치=튐 그대로 / 메시=부드러움. 순간이동 소멸.

- [ ] **Step 3: RTT 50ms + 무회귀**

RTT 50ms에서도 정상(작은 보정이라 원래도 미미), 걷기/대시/정지/점프 조작 반응성·정확성 무회귀(오프셋은 시뮬 무영향), 남 캐릭 넉백 무영향.
Expected: 모든 이동 무회귀, 남 캐릭 동일.

- [ ] **Step 4: 엣지 확인**

- 연속 히트(더블 넉백): 오프셋 누적돼도 부드럽게 수렴.
- 리스폰/큰 이동: 3m 초과라 스냅(안 늘어짐).
Expected: 연속 히트 매끄러움, 리스폰 즉시 스냅.

- [ ] **Step 5: 튜닝 (필요 시)**

손맛이 늘어지면 `RenderCorrectionOffset.SmoothingTime`을 **0.10f**로, 튐 흔적이 남으면 **0.20f**로. 리스폰이 늘어지면 `Reconciler.TeleportThreshold` 하향. (표준 범위 100–250ms 내에서.) 변경 시 해당 파일 커밋.

- [ ] **Step 6: 후기 기록 + 커밋**

`CLIENT\docs\superpowers\specs\2026-07-06-render-correction-smoothing-design.md` 하단에 "구현 후기"(최종 τ/threshold 값, 검증 결과) 추가 후 커밋.

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add docs/superpowers/specs/2026-07-06-render-correction-smoothing-design.md
git commit -m "docs(netcode): 렌더 스무딩 구현 후기 + 튜닝 결과

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## 통합 / 머지

단일 레포(LOP-Client) `feature/render-correction-smoothing`. Task 1→2→3(코드)→4(플레이). 플레이 통과 후 main에 `--no-ff` 머지(superpowers:finishing-a-development-branch).

## Out of Scope (spec과 동일)

- 남 캐릭(원격) 스무딩(이미 SmoothDamp), 회전 스무딩, "서버발 효과 지연 렌더(Option B)", velocity 권위/Stage④.

## Self-Review 결과

- **Spec 커버리지:** §A(홀더)=Task 1, §B(Reconciler 누적+텔레포트가드)=Task 2, §C(Interpolator 적용+감쇠+Reset)=Task 3, §D(등록/생명주기)=Task 1 Step2 + Task 3 Step3, 튜닝값=Global Constraints + Task 4 Step5, 검증=Task 4, 계측 제거=Task 2. 전부 커버.
- **Placeholder:** 없음(튜닝은 구체 시작값 + 조정 방향).
- **타입 일관:** `RenderCorrectionOffset.AddCorrection(Vector3)`/`SampleAndDecay(float)→Vector3`/`Reset()`/`Offset` — Task 1 정의 ↔ Task 2(AddCorrection) ↔ Task 3(SampleAndDecay/Reset) 일치. `preCorrectionPos`/`correction`/`TeleportThreshold`/`renderOffset` 명명 일관.

## 진행

- [ ] Task 1~4 구현
- [ ] 최종 리뷰 + 머지
