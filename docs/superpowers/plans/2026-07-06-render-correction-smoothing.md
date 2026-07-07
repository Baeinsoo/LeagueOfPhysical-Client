# Render Error Smoothing 표준 재설계 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 내 캐릭 서버발 보정(넉백)을 시각에서만 부드럽게 흡수한다 — 시뮬은 하드 보정 그대로, 렌더는 `render = simPos + errorOffset`(감쇠)로 따라잡아 순간이동/번쩍/걷기-끊김을 없앤다.

**Architecture:** 순수 커널 `RenderCorrectionSmoother`(GameFramework.Netcode, System.Numerics)가 errorOffset 상태 + 감쇠를 소유한다. `Reconciler`가 보정 순간 `OnCorrection(oldSim, newSim)`으로 seed하고, `LocalEntityInterpolator`가 매 틱 `Target(simPos)`로 연속 renderTarget을 기록·`DecayTick`으로 감쇠시키며, 그 renderTarget들 사이를 프레임마다 보간해 렌더한다. 커널이 마지막 낸 위치(`_lastTarget`)를 캐시해 seed 기준으로 쓰므로 렌더 위치를 외부에서 주입할 필요가 없다.

**Tech Stack:** C# / Unity / GameFramework 패키지(System.Numerics 순수 커널 + NUnit EditMode 테스트) / VContainer DI / Mirror 넷코드.

## Global Constraints

- **네임스페이스/이름 확정 (spec 준수, 임의 변경 금지):** 커널 `GameFramework.Netcode.RenderCorrectionSmoother`. 메서드 = `Target(Vector3)`, `OnCorrection(Vector3 oldSimPos, Vector3 newSimPos)`, `DecayTick(float)`, `Reset()`. `Vector3` = `System.Numerics.Vector3`.
- **튜닝 상수 (spec 시작값 verbatim):** `Tau = 0.1f`, `MinCorrection = 0.025f`, `Teleport = 3f`. ctor `RenderCorrectionSmoother(float tau, float minCorrection, float teleport)`.
- **시뮬은 절대 스무딩하지 않는다** — `Reconciler`의 하드 복원·재생 로직은 그대로. 스무딩은 오직 렌더(errorOffset).
- **LOP 측 파일 World 타입은 풀 네임스페이스 한정** (`GameFramework.World.Entity` 등) — 기존 컨벤션.
- **`Vector3` 변환**: 클라(UnityEngine.Vector3) ↔ 커널(System.Numerics.Vector3)은 `GameFramework`의 `.ToNumerics()` / `.ToUnity()` 확장 사용(파일 상단 `using GameFramework;` 이미 있음).
- **테스트 실행 = 클라 인스턴스 명시** (CLAUDE.md): UnityMCP 호출마다 `unity_instance="LeagueOfPhysical-Client@<hash>"` 전달. hash는 `mcpforunity://instances`에서 name=`LeagueOfPhysical-Client`로 조회.
- **main 직접 커밋 금지** — 작업 브랜치 `feature/render-correction-smoothing`(양쪽 레포 이미 체크아웃됨). GameFramework 변경은 그 레포에서, 클라 변경은 클라 레포에서 커밋.
- **주석은 최소화·일상어** (CLAUDE.md). public 타입/멤버는 `/// <summary>`.
- **Unity 전체 컴파일 결합:** 커널 API를 바꾸면 클라의 기존 호출부(`MarkCorrection`/`Smooth`/구 ctor)가 깨져 **프로젝트 전체가 컴파일 실패 → EditMode 테스트도 못 돎.** 따라서 커널 재작성과 3개 소비자 이관을 **한 태스크**로 원자 처리해 항상 컴파일 green을 유지한다.

---

### Task 1: `RenderCorrectionSmoother` 커널을 offset-decay 모델로 재작성(EditMode TDD) + 소비자 3곳 이관

**Files:**
- Modify(전면 교체): `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Netcode/RenderCorrectionSmoother.cs`
- Modify(전면 교체): `C:/Users/re5na/workspace/LOP/GameFramework/Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/GameLifetimeScope.cs:84` (ctor 인자)
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Entity/Reconciler.cs` (seed 교체, 구 상수/진단 제거)
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Entity/LocalEntityInterpolator.cs` (renderTarget 기록+감쇠, 구 Smooth 제거)

**Interfaces:**
- Produces (커널 공개 API — 소비자가 의존):
  - `RenderCorrectionSmoother(float tau, float minCorrection, float teleport)`
  - `System.Numerics.Vector3 Target(System.Numerics.Vector3 simPos)` — 렌더 위치(simPos+offset) 반환, 내부 `_lastTarget` 캐시
  - `void OnCorrection(System.Numerics.Vector3 oldSimPos, System.Numerics.Vector3 newSimPos)` — 보정 seed
  - `void DecayTick(float deltaTime)` — offset 지수 감쇠
  - `void Reset()`
- Consumes: `GameFramework`의 `Vector3Extensions.ToNumerics()` / `.ToUnity()` (기존).

---

- [ ] **Step 1: 새 EditMode 테스트를 먼저 작성(전면 교체)**

`GameFramework/Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs` 파일 전체를 아래로 교체:

```csharp
using System.Numerics;
using GameFramework.Netcode;
using NUnit.Framework;

namespace GameFramework.Tests.Netcode
{
    public class RenderCorrectionSmootherTests
    {
        // 보정이 없으면 렌더는 sim을 정확히 따른다(지연 0).
        [Test]
        public void NoCorrection_TargetEqualsSim()
        {
            var s = new RenderCorrectionSmoother(0.1f, 0.025f, 3f);
            Assert.AreEqual(new Vector3(5, 0, 0), s.Target(new Vector3(5, 0, 0)));
            s.DecayTick(0.033f);
            Assert.AreEqual(new Vector3(9, 0, 3), s.Target(new Vector3(9, 0, 3)));
        }

        // 핵심(번쩍 회귀 방지): 보정 직후 첫 렌더는 '있던 자리(old)'에 머문다 — 반대편 오버슈트(2old-new) 아님.
        [Test]
        public void OnCorrection_FirstTarget_StaysAtOld_NoOvershoot()
        {
            var s = new RenderCorrectionSmoother(0.1f, 0.025f, 3f);
            s.Target(new Vector3(0, 0, 0));                       // _lastTarget = old(0)
            s.OnCorrection(new Vector3(0, 0, 0), new Vector3(0.6f, 0, 0)); // 0.6m 보정(밴드 내)
            var r = s.Target(new Vector3(0.6f, 0, 0));            // 감쇠 전이므로 정확히 old
            Assert.AreEqual(0f, r.X, 1e-4f, "보정 직후 렌더는 old(0)에 유지 — 버그판 -0.6(오버슈트) 아님");
        }

        // 보정 후 감쇠하면 렌더가 단조로 새 위치에 수렴하며 넘지 않는다.
        [Test]
        public void AfterCorrection_Decay_MonotonicConvergeToNew_NeverPast()
        {
            var s = new RenderCorrectionSmoother(0.1f, 0.025f, 3f);
            s.Target(new Vector3(0, 0, 0));
            s.OnCorrection(new Vector3(0, 0, 0), new Vector3(0.6f, 0, 0));
            var target = new Vector3(0.6f, 0, 0);
            float prev = s.Target(target).X;                     // 0
            for (int i = 0; i < 30; i++)
            {
                s.DecayTick(0.033f);
                float x = s.Target(target).X;
                Assert.GreaterOrEqual(x, prev - 1e-4f, "단조 증가");
                Assert.LessOrEqual(x, 0.6f + 1e-4f, "새 위치를 넘지 않음");
                prev = x;
            }
            Assert.Greater(prev, 0.55f, "충분히 감쇠하면 새 위치에 근접");
        }

        // 무시할 만큼 작은 보정(< min)은 offset 0 → 즉시 새 위치 채택.
        [Test]
        public void SmallCorrection_BelowMin_AdoptsImmediately()
        {
            var s = new RenderCorrectionSmoother(0.1f, 0.025f, 3f);
            s.Target(new Vector3(0, 0, 0));
            s.OnCorrection(new Vector3(0, 0, 0), new Vector3(0.01f, 0, 0)); // 0.01m < 0.025
            Assert.AreEqual(new Vector3(0.01f, 0, 0), s.Target(new Vector3(0.01f, 0, 0)));
        }

        // 너무 큰 보정(> teleport, 리스폰 등)은 offset 0 → 즉시 스냅.
        [Test]
        public void LargeCorrection_AboveTeleport_Snaps()
        {
            var s = new RenderCorrectionSmoother(0.1f, 0.025f, 3f);
            s.Target(new Vector3(0, 0, 0));
            s.OnCorrection(new Vector3(0, 0, 0), new Vector3(5, 0, 0)); // 5m > 3
            Assert.AreEqual(new Vector3(5, 0, 0), s.Target(new Vector3(5, 0, 0)));
        }

        // 같은 총 감쇠시간이면 dt 분할이 달라도 결과가 근사 동일(프레임독립).
        [Test]
        public void Decay_FrameRateIndependent()
        {
            var target = new Vector3(1, 0, 0);

            var coarse = new RenderCorrectionSmoother(0.1f, 0.025f, 3f);
            coarse.Target(new Vector3(0, 0, 0));
            coarse.OnCorrection(new Vector3(0, 0, 0), target);
            for (int i = 0; i < 5; i++) coarse.DecayTick(0.02f);   // 5 × 0.02 = 0.1s

            var fine = new RenderCorrectionSmoother(0.1f, 0.025f, 3f);
            fine.Target(new Vector3(0, 0, 0));
            fine.OnCorrection(new Vector3(0, 0, 0), target);
            for (int i = 0; i < 10; i++) fine.DecayTick(0.01f);    // 10 × 0.01 = 0.1s

            Assert.AreEqual(coarse.Target(target).X, fine.Target(target).X, 1e-4f);
        }

        // Reset 후 offset이 0으로 초기화되어 sim을 정확히 따른다.
        [Test]
        public void Reset_ClearsOffset()
        {
            var s = new RenderCorrectionSmoother(0.1f, 0.025f, 3f);
            s.Target(new Vector3(0, 0, 0));
            s.OnCorrection(new Vector3(0, 0, 0), new Vector3(0.6f, 0, 0));
            s.Target(new Vector3(0.6f, 0, 0));
            s.Reset();
            Assert.AreEqual(new Vector3(9, 0, 0), s.Target(new Vector3(9, 0, 0)), "리셋 후 sim 정확 추종");
        }
    }
}
```

- [ ] **Step 2: 커널을 아직 안 고친 채 컴파일 → 테스트 실패 확인**

이 시점엔 구 커널(`MarkCorrection`/`Smooth`)이라 새 테스트가 `Target`/`OnCorrection`/`DecayTick`을 못 찾아 **컴파일 에러**가 나야 정상. UnityMCP `read_console`(unity_instance=클라)로 `RenderCorrectionSmoother' does not contain a definition for 'Target'`(또는 OnCorrection/DecayTick) 류 에러 확인.
기대: 컴파일 실패(새 API 미존재).

- [ ] **Step 3: 커널을 offset-decay 모델로 전면 교체**

`GameFramework/Runtime/Scripts/Netcode/RenderCorrectionSmoother.cs` 파일 전체를 아래로 교체:

```csharp
using System;
using System.Numerics;

namespace GameFramework.Netcode
{
    /// <summary>
    /// 서버 보정을 '보이는 위치'에서만 부드럽게 흡수한다. 시뮬(권위) 위치는 하드 보정 그대로 두고,
    /// 렌더 위치가 (보이던 위치 − 새 권위 위치)만큼의 오차 offset을 잡은 뒤 0으로 감쇠시켜 새 위치로
    /// 수렴한다 — Unreal MeshTranslationOffset / Fiedler render=simPos+errorOffset 대응.
    /// 순수(System.Numerics) — 프레임독립·유닛 테스트 가능.
    /// </summary>
    public class RenderCorrectionSmoother
    {
        private readonly float _tau;             // 감쇠 시간상수(초) — 클수록 천천히 녹음
        private readonly float _minCorrection;   // 이보다 작은 보정은 무시(즉시 채택)
        private readonly float _teleport;        // 이보다 큰 보정은 스무딩 안 함(즉시 스냅)

        private Vector3 _offset;                 // 렌더 = simPos + _offset
        private Vector3 _lastTarget;             // 마지막으로 낸 렌더 위치(보정 seed 기준)
        private bool _hasTarget;

        public RenderCorrectionSmoother(float tau, float minCorrection, float teleport)
        {
            _tau = tau;
            _minCorrection = minCorrection;
            _teleport = teleport;
        }

        /// <summary>이번 틱 렌더 위치 = simPos + offset. 낸 값을 seed 기준으로 캐시한다.</summary>
        public Vector3 Target(Vector3 simPos)
        {
            _lastTarget = simPos + _offset;
            _hasTarget = true;
            return _lastTarget;
        }

        /// <summary>
        /// 서버 보정 발생. 보정 크기가 밴드 안이면 렌더를 직전에 보이던 자리에 붙들어 두는 offset을
        /// 잡고(이후 감쇠로 새 위치에 수렴), 너무 작거나(무시) 너무 크면(리스폰 등) offset을 0으로 두어
        /// 렌더가 즉시 새 위치를 따르게 한다.
        /// </summary>
        public void OnCorrection(Vector3 oldSimPos, Vector3 newSimPos)
        {
            if (!_hasTarget)
            {
                _offset = Vector3.Zero;
                return;
            }
            float mag = Vector3.Distance(oldSimPos, newSimPos);
            if (mag < _minCorrection || mag > _teleport)
            {
                _offset = Vector3.Zero;
            }
            else
            {
                _offset = _lastTarget - newSimPos;
            }
        }

        /// <summary>offset을 0으로 한 틱만큼 지수 감쇠(프레임독립).</summary>
        public void DecayTick(float deltaTime)
        {
            _offset *= MathF.Exp(-deltaTime / _tau);
        }

        public void Reset()
        {
            _offset = Vector3.Zero;
            _lastTarget = Vector3.Zero;
            _hasTarget = false;
        }
    }
}
```

- [ ] **Step 4: 소비자 ①/③ 이관 — `GameLifetimeScope` ctor 인자 교체**

`Assets/Scripts/Game/GameLifetimeScope.cs` 84번째 줄을 교체:

기존:
```csharp
            builder.Register(_ => new GameFramework.Netcode.RenderCorrectionSmoother(0.15f), Lifetime.Singleton);
```
신규:
```csharp
            builder.Register(_ => new GameFramework.Netcode.RenderCorrectionSmoother(0.1f, 0.025f, 3f), Lifetime.Singleton);
```

- [ ] **Step 5: 소비자 ② 이관 — `Reconciler` seed 교체(구 상수/진단 제거)**

`Assets/Scripts/Entity/Reconciler.cs`에서 두 곳을 수정.

(5-1) 구 상수 3개를 제거. 아래 블록을
```csharp
        private const float TeleportThreshold = 3f;    // 이보다 큰 보정(리스폰 등)은 스무딩 안 함(스냅)
        private const float CorrectionSignalThreshold = 0.3f;  // 이보다 작은 보정은 스무딩 안 함(정확 추종) — 걷기 중 잦은 작은 보정이 이동을 지연시켜 끊기는 것 방지
        private const float CorrectionWindow = 0.3f;   // 보정 이징 창(초)
```
로 교체(세 줄 삭제):
```csharp
        // 렌더 보정 임계(minCorrection/teleport)는 RenderCorrectionSmoother가 소유 — 여기선 seed만 한다.
```

(5-2) 메서드 끝의 진단+MarkCorrection 블록을 OnCorrection seed로 교체. 아래 블록을
```csharp
            // 하드 보정으로 시뮬이 튄 크기가 유의미하면(텔레포트 아님) 렌더 스무더에 "보정 발생"을 알린다.
            // 스무더가 보이는 메시를 창 동안 부드럽게 이징(시뮬 무영향). 큰 보정(리스폰)은 신호 안 함 → 스냅.
            float smoothDiagMag = (preCorrectionPos - entity.position).magnitude;
            bool smoothDiagMark = smoothDiagMag >= CorrectionSignalThreshold && smoothDiagMag <= TeleportThreshold;
            UnityEngine.Debug.Log($"[SMOOTH-DIAG] correction={smoothDiagMag:F3}m window={currentTick - anchorTick} mark={smoothDiagMark}");
            if (smoothDiagMark)
            {
                renderCorrectionSmoother.MarkCorrection(CorrectionWindow);
            }
```
로 교체:
```csharp
            // 하드 보정으로 시뮬 위치가 튄 것을 렌더 스무더에 알린다. 스무더가 보이는 위치를
            // (보정 전 예측 → 보정 후 권위)만큼 부드럽게 흡수한다(시뮬 무영향). 크기별 스냅/무시는 스무더가 판단.
            renderCorrectionSmoother.OnCorrection(preCorrectionPos.ToNumerics(), entity.position.ToNumerics());
```

- [ ] **Step 6: 소비자 ② 이관 — `LocalEntityInterpolator`가 renderTarget을 기록·감쇠**

`Assets/Scripts/Entity/LocalEntityInterpolator.cs`에서 두 곳을 수정.

(6-1) `OnEnd`가 raw 위치 대신 renderTarget(=sim+offset)을 기록하고 offset을 한 틱 감쇠. 아래를
```csharp
        [RunnerListen(typeof(End))]
        private void OnEnd()
        {
            entityTransformSnaps[Runner.Time.tick] = new EntityTransform
            {
                position = entity.position,
                rotation = entity.rotation,
                velocity = entity.velocity,
            };
        }
```
로 교체:
```csharp
        [RunnerListen(typeof(End))]
        private void OnEnd()
        {
            // renderTarget = 시뮬 위치 + 감쇠 중인 보정 offset. offset이 시뮬 스텝과 상쇄되어
            // 이 스트림은 보정 순간에도 연속 → 아래 LateUpdate 보간이 튀지 않는다(걷기 지연도 없음).
            entityTransformSnaps[Runner.Time.tick] = new EntityTransform
            {
                position = renderCorrectionSmoother.Target(entity.position.ToNumerics()).ToUnity(),
                rotation = entity.rotation,
                velocity = entity.velocity,
            };
            renderCorrectionSmoother.DecayTick((float)Runner.Time.tickInterval);
        }
```

(6-2) `LateUpdate`에서 구 `Smooth` 통과를 제거(위치는 이미 스무딩된 renderTarget). 아래를
```csharp
            Vector3 target = Vector3.Lerp(prev.position, next.position, t);
            entityView.visualGameObject.transform.position =
                renderCorrectionSmoother.Smooth(target.ToNumerics(), Time.deltaTime).ToUnity();
            entityView.visualGameObject.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(prev.rotation), Quaternion.Euler(next.rotation), t);
```
로 교체:
```csharp
            // prev/next는 이미 스무딩된 renderTarget이라 그대로 보간하면 된다.
            entityView.visualGameObject.transform.position = Vector3.Lerp(prev.position, next.position, t);
            entityView.visualGameObject.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(prev.rotation), Quaternion.Euler(next.rotation), t);
```

(`using GameFramework;`가 파일 상단에 있어 `.ToNumerics()`/`.ToUnity()` 사용 가능 — 없으면 추가. `Cleanup()`의 `renderCorrectionSmoother.Reset();`는 그대로 유지.)

- [ ] **Step 7: 컴파일 확인 + EditMode 테스트 통과 확인**

UnityMCP `refresh_unity` 후 `read_console`(unity_instance=클라)로 컴파일 에러 0 확인.
그다음 `run_tests`(mode=`EditMode`, filter=`RenderCorrectionSmootherTests`, unity_instance=클라) 실행.
기대: 컴파일 에러 0, 7개 테스트 전부 PASS(특히 `OnCorrection_FirstTarget_StaysAtOld_NoOvershoot`).

- [ ] **Step 8: 커밋(양쪽 레포)**

GameFramework 레포:
```bash
cd C:/Users/re5na/workspace/LOP/GameFramework
git add Runtime/Scripts/Netcode/RenderCorrectionSmoother.cs Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs
git commit -m "$(cat <<'EOF'
refactor(netcode): RenderCorrectionSmoother를 offset-decay 표준 모델로 재작성

기존 '움직이는 타깃 Lerp'(걷기 지연) 폐기. render=simPos+errorOffset,
seed=(직전렌더-새권위), exp 감쇠, min/teleport 임계. EditMode 7테스트(번쩍 회귀 포함).

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```
클라 레포:
```bash
cd C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client
git add Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Entity/Reconciler.cs Assets/Scripts/Entity/LocalEntityInterpolator.cs
git commit -m "$(cat <<'EOF'
feat(netcode): 내 캐릭 렌더 보정을 offset-decay로 이관

Reconciler가 OnCorrection(oldSim,newSim)으로 seed, LocalEntityInterpolator가
renderTarget(=sim+offset) 스트림을 기록·감쇠·보간. 구 MarkCorrection/Smooth/
[SMOOTH-DIAG]/밴드 상수 제거. 번쩍·걷기끊김 원천 제거.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: 플레이 검증 + 튜닝 (사용자 수행)

**Files:** (코드 변경 없음 — 필요 시 `Tau`/`MinCorrection`/`Teleport` 상수만 조정, 위치 = `GameLifetimeScope.cs:84`)

클라 Assembly-CSharp라 EditMode 불가 — 육안+HUD 검증. RTT는 Mirror LatencySimulation으로 주입.

- [ ] **Step 1: 두 에디터 접속(클라+서버), RTT 300ms 설정, 콘솔 클리어**
- [ ] **Step 2: 평지 걷기 — 끊김(stutter) 없이 매끄러운지.** DebugHud reconciliation distance가 0이 아니어도 화면은 매끄러워야 함(offset이 걷기를 지연 안 시킴).
- [ ] **Step 3: 넉백 한 번 — 번쩍/순간이동 없이 ~0.1s 부드럽게 밀리는지.** DebugHud 교정거리(~0.6m)는 그대로여도 메시는 부드럽게 이동.
- [ ] **Step 4: 엣지 — 연속 넉백(offset 재seed 자연 흡수), 리스폰/큰 이동(>3m 즉시 스냅).**
- [ ] **Step 5: RTT 50ms에서도 재확인.**
- [ ] **Step 6(필요 시): 튜닝.** 넉백이 너무 느리게 녹으면 `Tau` ↓(예 0.08), 너무 빨라 튐이 남으면 ↑(예 0.15). 작은 보정이 거슬리면 `MinCorrection` ↑. 리스폰이 슬라이드로 보이면 `Teleport` ↓.

기대: 번쩍 없음 + 걷기 끊김 없음 + 넉백 부드러움 + 리스폰 스냅. 검증 후 `finishing-a-development-branch`로 양쪽 레포 main 머지.

---

## Self-Review

**1. Spec coverage:**
- A. 커널(offset-decay, Target/OnCorrection/DecayTick/Reset, _lastTarget 캐시) → Task 1 Step 3. ✅
- B. EditMode 테스트(오버슈트 없음/지연0/감쇠수렴/즉시채택/스냅/프레임독립/Reset) → Task 1 Step 1(7개). ✅
- C. Reconciler seed(OnCorrection(oldSim,newSim)) → Task 1 Step 5. ✅
- D. LocalEntityInterpolator renderTarget 기록·감쇠·보간 → Task 1 Step 6. ✅
- E. 배선(ctor 상수 주입) → Task 1 Step 4. ✅
- 튜닝 시작값(τ=0.1/min=0.025/teleport=3) → Global Constraints + Step 3/4. ✅
- 검증(EditMode 핵심 + 육안) → Task 1 Step 7 + Task 2. ✅

**2. Placeholder scan:** 모든 코드 스텝에 실제 코드 포함. 튜닝(Task 2 Step 6)은 사용자 판단 영역이라 조건부 방향만 — 플레이 검증 성격상 정상. TBD/TODO 없음. ✅

**3. Type consistency:** `Target(Vector3)→Vector3`, `OnCorrection(Vector3,Vector3)→void`, `DecayTick(float)`, `Reset()` — Interfaces·Step 3 커널·Step 5/6 호출부 전부 일치. `System.Numerics.Vector3` ↔ Unity는 `.ToNumerics()`/`.ToUnity()`로 변환(Step 5/6). ctor `(float,float,float)` — Step 3 정의 ↔ Step 4 호출 일치. ✅
