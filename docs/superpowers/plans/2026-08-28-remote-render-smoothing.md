# 남의 새 렌더 보정 재설계 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 남의 새를 보정할 때 화면이 "확 튕겨 나가는" 것을 없앤다 — 지수 감쇠를 속도까지 맞추는 3차 에르미트 보간으로 바꾸고, 내 새는 스무딩을 끈다.

**Architecture:** `GameFramework.Netcode.RenderCorrectionSmoother` 하나를 다시 쓴다. 시뮬 위치에 얹는 오차 `E(t)`를 지수 감쇠 대신 3차 에르미트로 만들어, 보정 이음매에서 **위치와 속도가 모두 연속**이 되게 한다. 문턱 상수는 언리얼 이름을 따르고, 스무딩 시간 0을 "끔"으로 정의해 내 새에 쓴다. 시뮬은 한 줄도 안 바꾼다.

**Tech Stack:** C# / Unity 6000.3 / NUnit EditMode. 순수 `System.Numerics` (엔진 비의존).

**Spec:** `docs/superpowers/specs/2026-08-28-remote-render-smoothing-design.md`

## Global Constraints

- **시뮬 변경 금지.** `FlappyWorld`, `FlappyBodyCollisionSystem`, `Reconciler`의 롤백/재생 로직은 건드리지 않는다. 이 계획이 만지는 것은 렌더 보정 한 층뿐이다.
- **상수 값 (spec §6에서 확정, 그대로 쓴다):**
  - `MinCorrection = 0.025f` (m)
  - `NetworkSimulatedSmoothLocationTime = 0.1f` (초)
  - `NetworkMaxSmoothUpdateDistance = 5f` (m)
  - `NetworkNoSmoothUpdateDistance = 8f` (m)
- **네이밍은 언리얼 `UCharacterMovementComponent`를 따른다** — 임의 명명 금지(`architecture-guidelines.md`의 "업계 표준 구조·네이밍").
- **`RenderCorrectionSmoother`는 `noEngineReferences` 어셈블리에 있다** — `UnityEngine` 타입을 쓰지 말고 `System.Numerics.Vector3`만 쓴다.
- **주석은 비자명한 "왜"만.** 코드로 자명한 것은 쓰지 않는다.

## File Structure

| 파일 | 책임 | 변경 |
|---|---|---|
| `GameFramework/Runtime/Scripts/Netcode/RenderCorrectionSmoother.cs` | 시뮬 위치 위에 얹는 렌더 오차를 만든다 | **전면 수정** |
| `GameFramework/Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs` | 위 클래스의 성질을 고정 | **전면 수정** |
| `LOP-Client/Assets/Scripts/Netcode/RenderCorrectionSmootherFactory.cs` | 내 것/남의 것에 맞는 스무더를 만든다 | 상수 교체 + 내 새는 끔 |
| `LOP-Client/Assets/Scripts/Netcode/Reconciler.cs` | 보정 사실을 렌더에 통지 | 속도도 함께 넘김 |
| `LOP-Client/Assets/Scripts/Netcode/PredictedEntityInterpolator.cs` | 통지 중계 | 시그니처만 따라감 |
| `LOP-Client/Assets/Tests/Editor/RenderCorrectionSmootherFactoryTests.cs` | 내 새 스무딩이 꺼져 있음을 고정 | **신규** |

`EntityBinder`는 **바꾸지 않는다.** 내 새를 "스무더 없음"이 아니라 "꺼진 스무더"로 다루면 널 검사가 퍼지지 않는다(spec §7은 `EntityBinder` 수정을 예상했으나, 꺼진 스무더 쪽이 접점이 적어 그렇게 간다).

---

### Task 1: 렌더 보정 계산을 에르미트로 바꾼다

**Files:**
- Modify: `/Users/insoobae/workspace/LOP/GameFramework/Runtime/Scripts/Netcode/RenderCorrectionSmoother.cs` (전체)
- Test: `/Users/insoobae/workspace/LOP/GameFramework/Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs` (전체)

**Interfaces:**
- Produces (Task 2가 쓴다):
  - `RenderCorrectionSmoother(float smoothTime, float minCorrection, float maxSmoothDistance, float noSmoothDistance)`
  - `Vector3 Target(Vector3 simPosition)`
  - `void OnCorrection(Vector3 oldSimPosition, Vector3 newSimPosition, Vector3 newSimVelocity)`
  - `void Advance(float deltaTime)` — 옛 이름 `DecayTick`을 대체한다(더는 감쇠가 아니다)
  - `void Reset()`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`RenderCorrectionSmootherTests.cs`를 아래로 **완전히 교체**한다.

```csharp
using System.Numerics;
using GameFramework.Netcode;
using NUnit.Framework;

namespace GameFramework.Tests.Netcode
{
    public class RenderCorrectionSmootherTests
    {
        private const float SmoothTime = 0.1f;
        private const float MinCorrection = 0.025f;
        private const float MaxSmooth = 5f;
        private const float NoSmooth = 8f;

        private static RenderCorrectionSmoother Make()
            => new RenderCorrectionSmoother(SmoothTime, MinCorrection, MaxSmooth, NoSmooth);

        //  보정이 없으면 렌더는 sim을 정확히 따른다(지연 0).
        [Test]
        public void NoCorrection_TargetEqualsSim()
        {
            var s = Make();
            Assert.AreEqual(new Vector3(5, 0, 0), s.Target(new Vector3(5, 0, 0)));
            s.Advance(0.033f);
            Assert.AreEqual(new Vector3(9, 0, 3), s.Target(new Vector3(9, 0, 3)));
        }

        //  보정 직후 첫 렌더는 '있던 자리'에 머문다 — 반대편 오버슈트가 아니다.
        [Test]
        public void OnCorrection_FirstTarget_StaysAtOld()
        {
            var s = Make();
            s.Target(new Vector3(0, 0, 0));
            s.OnCorrection(new Vector3(0, 0, 0), new Vector3(0.6f, 0, 0), Vector3.Zero);
            var r = s.Target(new Vector3(0.6f, 0, 0));
            Assert.AreEqual(0f, r.X, 1e-4f);
        }

        //  ★ 이 슬라이스의 핵심. 보정 직후 렌더 속도가 보정 직전 렌더 속도와 이어진다.
        //     지수 감쇠는 여기서 (갭 / 시간상수)만큼 튀어 올랐다.
        [Test]
        public void OnCorrection_RenderVelocity_IsContinuous()
        {
            const float dt = 0.02f;
            var s = Make();

            //  렌더가 +x로 10 m/s로 달리는 상태를 만든다(두 프레임이면 속도를 알 수 있다).
            s.Target(new Vector3(0f, 0, 0));
            s.Advance(dt);
            s.Target(new Vector3(0.2f, 0, 0));
            s.Advance(dt);
            s.Target(new Vector3(0.4f, 0, 0));   // 직전 렌더 위치(세로 속도 0)

            //  세로로 4.788m 튀는 보정(실측 최대치). 권위 속도는 위로 23(=FlapImpulse).
            s.OnCorrection(new Vector3(0.4f, 0, 0), new Vector3(0.4f, 4.788f, 0), new Vector3(0f, 23f, 0));

            //  보정 직후의 '순간' 기울기를 재야 한다. 간격이 크면 3차식이 그 사이에 휘어서
            //  평균이 잡히므로(0.02초로 재면 33 m/s가 나온다) 아주 짧은 간격으로 잰다.
            const float probeDt = 0.001f;
            var afterPos = s.Target(new Vector3(0.4f, 4.788f, 0));
            s.Advance(probeDt);
            var nextPos = s.Target(new Vector3(0.4f, 4.788f + 23f * probeDt, 0));

            float renderVelY = (nextPos.Y - afterPos.Y) / probeDt;

            //  직전 렌더는 세로로 안 움직였다(0). 연속이면 보정 직후에도 0 근처에서 출발해야 한다.
            //  지수 감쇠였다면 4.788 / 0.1 = 47.9 m/s가 나온다.
            Assert.Less(System.Math.Abs(renderVelY), 5f,
                "보정 직후 세로 렌더 속도가 직전(0)에서 크게 튀면 안 된다 — 지수 감쇠면 47.9가 나온다");
        }

        //  정지 상태에서의 보정은 넘지 않고 단조롭게 수렴한다.
        [Test]
        public void StaticCorrection_ConvergesMonotonically_NeverPast()
        {
            var s = Make();
            s.Target(new Vector3(0, 0, 0));
            s.OnCorrection(new Vector3(0, 0, 0), new Vector3(0.6f, 0, 0), Vector3.Zero);
            var target = new Vector3(0.6f, 0, 0);
            float prev = s.Target(target).X;
            for (int i = 0; i < 10; i++)
            {
                s.Advance(0.02f);
                float x = s.Target(target).X;
                Assert.GreaterOrEqual(x, prev - 1e-4f, "단조 증가");
                Assert.LessOrEqual(x, 0.6f + 1e-4f, "새 위치를 넘지 않음");
                prev = x;
            }
        }

        //  보간 시간이 지나면 렌더가 sim과 정확히 같아진다.
        [Test]
        public void AfterSmoothTime_ConvergesExactly()
        {
            var s = Make();
            s.Target(new Vector3(0, 0, 0));
            s.OnCorrection(new Vector3(0, 0, 0), new Vector3(1f, 0, 0), Vector3.Zero);
            for (int i = 0; i < 6; i++)   // 6 × 0.02 = 0.12s > SmoothTime
            {
                s.Advance(0.02f);
            }
            Assert.AreEqual(1f, s.Target(new Vector3(1f, 0, 0)).X, 1e-4f);
        }

        //  아주 작은 보정은 스무딩 없이 즉시 채택 — 숨길 튐이 없는데 녹이면 오차만 는다.
        [Test]
        public void SmallCorrection_BelowMin_AdoptsImmediately()
        {
            var s = Make();
            s.Target(new Vector3(0, 0, 0));
            s.OnCorrection(new Vector3(0, 0, 0), new Vector3(0.01f, 0, 0), Vector3.Zero);
            Assert.AreEqual(new Vector3(0.01f, 0, 0), s.Target(new Vector3(0.01f, 0, 0)));
        }

        //  아주 큰 보정(리스폰 등)은 즉시 스냅 — 녹이면 맵을 가로질러 미끄러진다.
        [Test]
        public void LargeCorrection_AboveNoSmoothDistance_Snaps()
        {
            var s = Make();
            s.Target(new Vector3(0, 0, 0));
            s.OnCorrection(new Vector3(0, 0, 0), new Vector3(9f, 0, 0), Vector3.Zero);   // 9 > 8
            Assert.AreEqual(new Vector3(9f, 0, 0), s.Target(new Vector3(9f, 0, 0)));
        }

        //  목줄: 보간 도중에도 렌더가 sim에서 MaxSmoothDistance 이상 뒤처지지 않는다.
        [Test]
        public void DuringSmoothing_NeverLagsMoreThanMaxSmoothDistance()
        {
            var s = Make();
            s.Target(new Vector3(0, 0, 0));
            //  7m 보정: NoSmooth(8) 아래라 녹이지만, Max(5)보다 크므로 첫 프레임부터 목줄이 당긴다.
            s.OnCorrection(new Vector3(0, 0, 0), new Vector3(7f, 0, 0), Vector3.Zero);
            var sim = new Vector3(7f, 0, 0);
            for (int i = 0; i < 6; i++)
            {
                float lag = (sim - s.Target(sim)).Length();
                Assert.LessOrEqual(lag, MaxSmooth + 1e-4f, $"i={i}: 목줄보다 더 뒤처지면 안 된다");
                s.Advance(0.02f);
            }
        }

        //  스무딩 시간 0 = 끔(언리얼 NetworkSmoothingMode.Disabled). 내 새가 쓴다.
        [Test]
        public void SmoothTimeZero_Disabled_AlwaysFollowsSim()
        {
            var s = new RenderCorrectionSmoother(0f, MinCorrection, MaxSmooth, NoSmooth);
            s.Target(new Vector3(0, 0, 0));
            s.OnCorrection(new Vector3(0, 0, 0), new Vector3(2f, 0, 0), new Vector3(0f, 23f, 0));
            Assert.AreEqual(new Vector3(2f, 0, 0), s.Target(new Vector3(2f, 0, 0)));
        }

        //  같은 총 시간이면 dt 분할이 달라도 결과가 같다(프레임독립).
        [Test]
        public void Smoothing_FrameRateIndependent()
        {
            var target = new Vector3(1, 0, 0);

            var coarse = Make();
            coarse.Target(new Vector3(0, 0, 0));
            coarse.OnCorrection(new Vector3(0, 0, 0), target, Vector3.Zero);
            for (int i = 0; i < 5; i++) coarse.Advance(0.01f);    // 0.05s

            var fine = Make();
            fine.Target(new Vector3(0, 0, 0));
            fine.OnCorrection(new Vector3(0, 0, 0), target, Vector3.Zero);
            for (int i = 0; i < 10; i++) fine.Advance(0.005f);    // 0.05s

            Assert.AreEqual(coarse.Target(target).X, fine.Target(target).X, 1e-4f);
        }

        //  Reset 후에는 sim을 정확히 따른다.
        [Test]
        public void Reset_ClearsOffset()
        {
            var s = Make();
            s.Target(new Vector3(0, 0, 0));
            s.OnCorrection(new Vector3(0, 0, 0), new Vector3(0.6f, 0, 0), Vector3.Zero);
            s.Target(new Vector3(0.6f, 0, 0));
            s.Reset();
            Assert.AreEqual(new Vector3(9, 0, 0), s.Target(new Vector3(9, 0, 0)));
        }
    }
}
```

- [ ] **Step 2: 빨간불을 확인한다**

에디터가 떠 있으면:
```bash
. "$HOME/.unity/env"
C=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path "$C"
# recompile_status가 completed가 될 때까지 폴링
unity cmd recompile_status --project-path "$C"
```
Expected: **컴파일 실패** — `OnCorrection`이 인자 3개를 안 받고, `Advance`/새 생성자가 없다.

- [ ] **Step 3: 구현한다**

`RenderCorrectionSmoother.cs`를 아래로 **완전히 교체**한다.

```csharp
using System;
using System.Numerics;

namespace GameFramework.Netcode
{
    /// <summary>
    /// 서버 보정을 '보이는 위치'에서만 흡수한다. 시뮬(권위) 위치는 하드 보정 그대로 두고, 렌더가
    /// 시뮬 위에 오차 offset을 얹어 그린 뒤 그 offset을 0으로 몰아간다.
    ///
    /// offset은 <b>3차 에르미트</b>로 만든다 — 이음매에서 위치뿐 아니라 <b>속도까지</b> 이어지게
    /// 하려는 것이다. 지수 감쇠는 시작 기울기가 (갭 / 시간상수)라 갭이 클수록 화면이 튕겨 나갔다
    /// (실측: 4.788m 갭에서 47.9 m/s). 목표는 Murphy의 projective velocity blending과 같고
    /// (Believable Dead Reckoning for Networked Games, Game Engine Gems 2), 그 조건을 정확히
    /// 만족시키는 3차식이 아래 <see cref="Evaluate"/>다.
    ///
    /// 문턱 이름은 언리얼 UCharacterMovementComponent를 따른다. 순수(System.Numerics) — 프레임독립·유닛 테스트 가능.
    /// </summary>
    public class RenderCorrectionSmoother
    {
        //  0이면 스무딩을 아예 안 한다(언리얼 NetworkSmoothingMode.Disabled). 내가 조종하는 몸이
        //  그렇다 — 녹이는 동안 입력과 화면이 어긋나 조작감이 무너진다.
        private readonly float _smoothTime;
        private readonly float _minCorrection;      // 이보다 작으면 숨길 튐이 없다 → 즉시 채택
        private readonly float _maxSmoothDistance;  // 보간 도중 렌더가 sim에서 이 이상 뒤처지지 않게 붙잡는다
        private readonly float _noSmoothDistance;   // 이보다 크면 녹이지 않고 즉시 채택

        private Vector3 _errorStart;        // E(0) — 보정 순간의 위치 갭
        private Vector3 _errorSlopeStart;   // E'(0)·T — 위치와 같은 단위로 미리 곱해 둔다
        private float _elapsed;             // 보정 후 경과(초)
        private bool _smoothing;

        private Vector3 _lastTarget;        // 마지막으로 낸 렌더 위치
        private Vector3 _prevTarget;        // 그 직전 것 — 둘의 차로 렌더 속도를 구한다
        private Vector3 _renderVelocity;
        private bool _hasTarget;
        private bool _hasPrev;

        public RenderCorrectionSmoother(float smoothTime, float minCorrection,
                                        float maxSmoothDistance, float noSmoothDistance)
        {
            _smoothTime = smoothTime;
            _minCorrection = minCorrection;
            _maxSmoothDistance = maxSmoothDistance;
            _noSmoothDistance = noSmoothDistance;
        }

        /// <summary>이번 프레임 렌더 위치 = 시뮬 위치 + 남은 오차.</summary>
        public Vector3 Target(Vector3 simPosition)
        {
            Vector3 error = Vector3.Zero;
            if (_smoothing)
            {
                error = Evaluate(_elapsed / _smoothTime);

                //  목줄: 보간 중이라도 이 이상은 뒤처지지 않는다.
                float lag = error.Length();
                if (lag > _maxSmoothDistance)
                {
                    error *= _maxSmoothDistance / lag;
                }
            }

            _lastTarget = simPosition + error;
            _hasTarget = true;
            return _lastTarget;
        }

        /// <summary>
        /// 서버 보정이 시뮬 위치를 옮겼음을 알린다. 렌더는 <paramref name="oldSimPosition"/> 쪽에
        /// 잠시 머물렀다 새 자리로 이어 간다. <paramref name="newSimVelocity"/>는 권위 속도로,
        /// 이음매에서 속도를 잇는 데 쓴다.
        /// </summary>
        public void OnCorrection(Vector3 oldSimPosition, Vector3 newSimPosition, Vector3 newSimVelocity)
        {
            _elapsed = 0f;

            //  아직 한 프레임도 안 그렸으면 이을 과거가 없다.
            if (_hasTarget == false || _smoothTime <= 0f)
            {
                _smoothing = false;
                return;
            }

            float gap = Vector3.Distance(oldSimPosition, newSimPosition);
            if (gap < _minCorrection || gap > _noSmoothDistance)
            {
                _smoothing = false;
                return;
            }

            _errorStart = _lastTarget - newSimPosition;
            //  E'(0) = (렌더가 가던 속도) − (권위 속도). 시간 단위를 없애려 T를 곱해 둔다.
            _errorSlopeStart = (_renderVelocity - newSimVelocity) * _smoothTime;
            _smoothing = true;
        }

        /// <summary>한 프레임 진행. 렌더 속도도 여기서 갱신한다(다음 보정의 이음매에 쓴다).</summary>
        public void Advance(float deltaTime)
        {
            if (_hasTarget && _hasPrev && deltaTime > 0f)
            {
                _renderVelocity = (_lastTarget - _prevTarget) / deltaTime;
            }
            if (_hasTarget)
            {
                _prevTarget = _lastTarget;
                _hasPrev = true;
            }

            if (_smoothing == false)
            {
                return;
            }
            _elapsed += deltaTime;
            if (_elapsed >= _smoothTime)
            {
                _smoothing = false;
            }
        }

        public void Reset()
        {
            _smoothing = false;
            _elapsed = 0f;
            _errorStart = Vector3.Zero;
            _errorSlopeStart = Vector3.Zero;
            _lastTarget = Vector3.Zero;
            _prevTarget = Vector3.Zero;
            _renderVelocity = Vector3.Zero;
            _hasTarget = false;
            _hasPrev = false;
        }

        //  E(0)=시작오차, E'(0)=시작기울기, E(1)=0, E'(1)=0 을 만족하는 유일한 3차식.
        //  끝에서 기울기까지 0이라 보간이 끝나는 순간에도 속도가 안 튄다.
        private Vector3 Evaluate(float u)
        {
            if (u <= 0f)
            {
                return _errorStart;
            }
            if (u >= 1f)
            {
                return Vector3.Zero;
            }
            float u2 = u * u;
            float u3 = u2 * u;
            return _errorStart * (2f * u3 - 3f * u2 + 1f)
                 + _errorSlopeStart * (u3 - 2f * u2 + u);
        }
    }
}
```

- [ ] **Step 4: 초록불을 확인한다**

```bash
. "$HOME/.unity/env"
C=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path "$C"     # completed / failed:false 확인
unity cmd run_tests --async_tests true --mode editor --project-path "$C"
# test_status가 completed가 될 때까지 폴링한 뒤 Temp/pipeline_test_status.json 을 읽는다
```
Expected: `RenderCorrectionSmootherTests` **11개 전부 통과.**

> ⚠️ **클라 프로젝트가 컴파일 실패한다.** `Reconciler`/`Factory`가 아직 옛 시그니처를 부르기 때문이다.
> 그건 Task 2가 고친다. **Task 1은 여기서 커밋하지 않고 Task 2까지 마친 뒤 함께 커밋한다** —
> 중간 커밋이 컴파일 안 되는 상태로 남으면 안 된다.

- [ ] **Step 5: Task 2로 넘어간다 (커밋하지 않는다)**

---

### Task 2: 클라 배선 — 속도 전달 + 내 새 스무딩 끄기

**Files:**
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Netcode/RenderCorrectionSmootherFactory.cs` (전체)
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Netcode/Reconciler.cs` — `NotifyRenderCorrections` 안쪽
- Modify: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Netcode/PredictedEntityInterpolator.cs` — `OnCorrection` 시그니처와 `DecayTick` 호출
- Create: `/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client/Assets/Tests/Editor/RenderCorrectionSmootherFactoryTests.cs`

**Interfaces:**
- Consumes (Task 1이 만든 것): `RenderCorrectionSmoother(float smoothTime, float minCorrection, float maxSmoothDistance, float noSmoothDistance)`, `OnCorrection(Vector3, Vector3, Vector3)`, `Advance(float)`
- Produces: `RenderCorrectionSmootherFactory.Create(bool local)` — 시그니처 그대로 유지(호출부 `EntityBinder.cs:122` 무변경)

- [ ] **Step 1: 실패하는 테스트를 쓴다**

새 파일 `Assets/Tests/Editor/RenderCorrectionSmootherFactoryTests.cs`:

```csharp
using System.Numerics;
using NUnit.Framework;

namespace LOP.Tests
{
    public class RenderCorrectionSmootherFactoryTests
    {
        //  내 새는 스무딩을 안 한다(언리얼: 스무딩은 simulated proxy에만).
        //  내가 조종 중이라, 녹이는 동안 입력과 화면 속 내 몸이 어긋난다.
        [Test]
        public void 내_새는_보정을_녹이지_않고_즉시_따른다()
        {
            var smoother = new RenderCorrectionSmootherFactory().Create(local: true);
            smoother.Target(new Vector3(0, 0, 0));
            smoother.OnCorrection(new Vector3(0, 0, 0), new Vector3(2f, 0, 0), new Vector3(0f, 23f, 0));

            Assert.AreEqual(new Vector3(2f, 0, 0), smoother.Target(new Vector3(2f, 0, 0)),
                "내 새는 보정 즉시 그 자리여야 한다");
        }

        //  남의 새는 녹인다 — 실측 최대 오차(4.788m)가 순간이동으로 보이면 안 된다.
        [Test]
        public void 남의_새는_실측_최대오차를_녹인다()
        {
            var smoother = new RenderCorrectionSmootherFactory().Create(local: false);
            smoother.Target(new Vector3(0, 0, 0));
            smoother.OnCorrection(new Vector3(0, 0, 0), new Vector3(0f, 4.788f, 0), Vector3.Zero);

            var rendered = smoother.Target(new Vector3(0f, 4.788f, 0));
            Assert.Less(rendered.Y, 4.788f - 0.5f,
                "4.788m는 정상 날갯짓 범위 — 즉시 스냅하면 안 되고 녹아야 한다");
        }

        //  그 위(리스폰·큰 랙)는 녹이지 않고 즉시 간다 — 녹이면 맵을 가로질러 미끄러진다.
        [Test]
        public void 남의_새도_아주_먼_보정은_즉시_간다()
        {
            var smoother = new RenderCorrectionSmootherFactory().Create(local: false);
            smoother.Target(new Vector3(0, 0, 0));
            smoother.OnCorrection(new Vector3(0, 0, 0), new Vector3(0f, 20f, 0), Vector3.Zero);

            Assert.AreEqual(20f, smoother.Target(new Vector3(0f, 20f, 0)).Y, 1e-4f);
        }
    }
}
```

- [ ] **Step 2: 빨간불을 확인한다**

Expected: 컴파일 실패 — 팩토리가 아직 옛 생성자(인자 3개)를 부른다.

- [ ] **Step 3: 팩토리를 교체한다**

`RenderCorrectionSmootherFactory.cs` **전체 교체**:

```csharp
namespace LOP
{
    /// <summary>
    /// 예측 엔티티마다 자기 렌더 보정 스무더를 하나씩 만들어 준다.
    ///
    /// 내 것과 남의 것을 다르게 만든다 — 언리얼이 스무딩을 simulated proxy에만 거는 것과 같은 이유다.
    /// 내가 조종하는 몸을 녹이면 그 시간 동안 입력과 화면이 어긋나 조작감이 무너진다. 남의 몸은
    /// 아무도 조종하지 않으니 그 대가가 없다.
    /// 상수 근거는 docs/superpowers/specs/2026-08-28-remote-render-smoothing-design.md §6.
    /// </summary>
    public class RenderCorrectionSmootherFactory
    {
        //  2.5cm는 눈에 안 보인다. 숨길 튐이 없는데 녹이면 그동안 계속 조금씩 틀린 자리에 있게 돼
        //  오히려 오차가 는다.
        private const float MinCorrection = 0.025f;

        //  언리얼 NetworkSimulatedSmoothLocationTime.
        private const float SmoothTime = 0.1f;

        //  언리얼 NetworkMaxSmoothUpdateDistance. 남의 새 정상 오차 최대가 4.788m(실측)이라
        //  그 위로 잡아 정상 구간에서는 목줄이 당겨지지 않게 한다.
        private const float MaxSmoothUpdateDistance = 5f;

        //  언리얼 NetworkNoSmoothUpdateDistance. 이 위로 벌어지는 것은 날갯짓으로 설명되지 않는다
        //  (리스폰·스폰 직후·큰 랙) — 녹이면 맵을 가로질러 미끄러지므로 즉시 간다.
        private const float NoSmoothUpdateDistance = 8f;

        //  0 = 스무딩 끔(언리얼 NetworkSmoothingMode.Disabled).
        private const float LocalSmoothTime = 0f;

        /// <param name="local">내가 조작하는 엔티티인가.</param>
        public GameFramework.Netcode.RenderCorrectionSmoother Create(bool local)
        {
            return new GameFramework.Netcode.RenderCorrectionSmoother(
                local ? LocalSmoothTime : SmoothTime,
                MinCorrection, MaxSmoothUpdateDistance, NoSmoothUpdateDistance);
        }
    }
}
```

- [ ] **Step 4: 보간기가 속도를 받도록 고친다**

`PredictedEntityInterpolator.cs`에서 두 곳:

`OnCorrection` 메서드를 아래로 바꾼다.
```csharp
        /// <summary>
        /// 시뮬 위치가 하드 보정으로 튀었음을 알린다. 보이는 메시가 그 차이를 부드럽게 흡수한다
        /// (시뮬에는 영향 없음). 크기별로 스냅/무시를 판단하는 것은 스무더 몫이다.
        /// <paramref name="authoritativeVelocity"/>는 이음매에서 렌더 속도를 잇는 데 쓴다.
        /// </summary>
        public void OnCorrection(System.Numerics.Vector3 before, System.Numerics.Vector3 after,
                                 System.Numerics.Vector3 authoritativeVelocity)
        {
            renderCorrectionSmoother.OnCorrection(before, after, authoritativeVelocity);
        }
```

`Tick` 끝의 `renderCorrectionSmoother.DecayTick(deltaTime);` 를 아래로 바꾼다.
```csharp
            renderCorrectionSmoother.Advance(deltaTime);
```

- [ ] **Step 5: Reconciler가 속도를 넘기게 고친다**

`Reconciler.cs`의 `NotifyRenderCorrections` 안, `actor.GetComponent<PredictedEntityInterpolator>()?.OnCorrection(...)` 호출을 아래로 바꾼다.

```csharp
                    actor.GetComponent<PredictedEntityInterpolator>()?.OnCorrection(
                        pair.Value,
                        GameFramework.World.EntityMotionExtensions.GetPosition(target).ToNumerics(),
                        GameFramework.World.EntityMotionExtensions.GetVelocity(target).ToNumerics());
```

> `NotifyRenderCorrections`는 권위 값을 덮은 **뒤**에 불리므로, 여기서 읽는 속도가 곧 서버가 준 속도다.

- [ ] **Step 6: 초록불을 확인한다**

```bash
. "$HOME/.unity/env"
C=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile --project-path "$C"     # failed:false 확인
unity cmd run_tests --async_tests true --mode editor --project-path "$C"
```
Expected: **전체 스위트 통과**(기존 683 + 새 팩토리 테스트 3 − 옛 스무더 테스트 7 + 새 스무더 테스트 11).
`RenderCorrectionSmootherTests` 11개와 `RenderCorrectionSmootherFactoryTests` 3개가 모두 Passed인지 이름으로 확인한다.

- [ ] **Step 7: 서버 프로젝트도 컴파일되는지 확인한다**

`RenderCorrectionSmoother`는 `GameFramework`에 있어 서버도 참조한다.
```bash
S=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Server
unity cmd recompile --project-path "$S"     # failed:false 확인
unity cmd run_tests --async_tests true --mode editor --project-path "$S"
```
Expected: 컴파일 오류 0, 서버 스위트 통과.

- [ ] **Step 8: 커밋 (GameFramework → Client 순서)**

```bash
G=/Users/insoobae/workspace/LOP/GameFramework
git -C $G checkout -b feature/remote-render-smoothing origin/main
git -C $G add Runtime/Scripts/Netcode/RenderCorrectionSmoother.cs Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs
git -C $G commit -F - <<'EOF'
fix(netcode): 렌더 보정을 속도까지 잇는 3차 에르미트로 바꾼다

지수 감쇠는 시작 기울기가 (갭 / 시간상수)라 갭이 클수록 화면이 튕겨 나갔다 — 실측 4.788m
갭에서 47.9 m/s가 얹혀, 남의 새 날갯짓(23 m/s)이 2~4배로 보였다.

E(0)=위치갭, E'(0)=속도갭, E(1)=0, E'(1)=0 을 만족하는 3차식으로 바꾼다. 그러면 보정 직후
렌더 속도가 직전 렌더 속도와 그대로 이어져 갭/시간 항이 사라진다. 목표는 Murphy의 projective
velocity blending과 같다.

문턱 이름을 언리얼 UCharacterMovementComponent에 맞추고, 없던 목줄
(NetworkMaxSmoothUpdateDistance)을 넣는다. 스무딩 시간 0을 "끔"으로 정의한다.
EOF

C=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git -C $C add Assets/Scripts/Netcode/RenderCorrectionSmootherFactory.cs \
              Assets/Scripts/Netcode/PredictedEntityInterpolator.cs \
              Assets/Scripts/Netcode/Reconciler.cs \
              Assets/Tests/Editor/RenderCorrectionSmootherFactoryTests.cs \
              Assets/Tests/Editor/RenderCorrectionSmootherFactoryTests.cs.meta
git -C $C commit -F - <<'EOF'
fix(netcode): 내 새는 보정을 녹이지 않고, 남의 새 문턱을 측정치로 맞춘다

언리얼은 스무딩을 simulated proxy에만 건다 — 내가 조종하는 몸을 녹이면 그 시간 동안 입력과
화면이 어긋난다. 지금은 내 새를 3m까지 녹이고 있었는데, 실측된 내 새 오차 최대가 2.776m라
몸싸움마다 전부 녹고 있었다.

남의 새 컷오프는 무제한(∞)이었다. 예전 3m가 정상 날갯짓보다 작아 날갯짓마다 순간이동으로
보였던 것이 이유인데, 답은 ∞가 아니라 제대로 된 크기였다. 실측 최대 4.788m를 기준으로
목줄 5m / 즉시이동 8m로 잡는다.
EOF
```

> `.meta`는 유니티가 만든 것만 커밋한다. `git status --short`로 **스테이지된 것이 의도한 파일뿐인지**
> 반드시 확인하고 커밋한다(`git add -A` 금지).

---

### Task 3: 진단용 프로브를 버린다

**Files:**
- Delete (브랜치째): `LeagueOfPhysical-Shared` 의 `probe/flappy-remote-sync` 브랜치

**Interfaces:**
- Consumes: 없음
- Produces: 없음

프로브(`FlappyRemoteSyncProbe.cs`)는 원인 파악용 임시 코드이고 **`probe/flappy-remote-sync`
브랜치에만 있다 — `main`에는 없다.** 측정 수치는 spec §2에 박제됐으므로 코드는 남길 이유가 없다.
남기면 "지키는 것이 없는 초록 테스트"가 하나 늘 뿐이다.

- [ ] **Step 1: 브랜치가 main에 안 들어갔는지 확인한다**

```bash
SH=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git -C $SH fetch origin
git -C $SH log --oneline origin/main --all --grep="FlappyRemoteSyncProbe"
git -C $SH ls-tree --name-only origin/main Tests/EditMode/ | grep -i probe
```
Expected: 둘 다 **빈 출력** — 프로브가 origin/main에 없다는 뜻이다. 출력이 있으면 멈추고 보고한다.

- [ ] **Step 2: 브랜치를 지운다**

```bash
SH=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git -C $SH branch -D probe/flappy-remote-sync
git -C $SH status --short     # 워킹트리에 프로브가 안 남았는지 확인(빈 출력이어야 한다)
```

- [ ] **Step 3: Shared가 여전히 깨끗한지 확인한다**

```bash
SH=/Users/insoobae/workspace/LOP/LeagueOfPhysical-Shared
git -C $SH rev-list --left-right --count origin/main...HEAD
```
Expected: `0	0` — 이 슬라이스는 Shared에 코드 변경이 없다.

---

## 검증 (전 태스크 완료 후)

- [ ] 클라 컴파일 오류 0 / 서버 컴파일 오류 0
- [ ] 클라 EditMode 전부 통과, 그중 `RenderCorrectionSmootherTests` 11개 + `RenderCorrectionSmootherFactoryTests` 3개가 이름으로 확인됨
- [ ] 서버 EditMode 전부 통과
- [ ] **일부러 깨서 빨강을 본다**: `RenderCorrectionSmoother.Evaluate`에서 `_errorSlopeStart` 항을 지우면
      `OnCorrection_RenderVelocity_IsContinuous`가 실패해야 한다(그 테스트가 실제로 이 슬라이스의
      핵심을 지키는지 확인). 확인 후 되돌린다.

## 라이브 확인 (사람)

2인(폰 + 에디터)으로 dev에서:

1. **남의 새 날갯짓이 진짜 속도로 보이는가** — 이 슬라이스의 목적
2. **몸싸움 때 내 새가 미끄러지지 않는가** — 즉시 반영으로 바뀌었다. 표준이지만 이 게임에서
   더 나은지는 눈으로 봐야 한다(spec §10)
3. **리스폰·랙에서 남의 새가 맵을 가로질러 미끄러지지 않는가** — 8m 컷오프가 하는 일
