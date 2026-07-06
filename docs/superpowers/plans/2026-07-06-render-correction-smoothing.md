# 예측 보정 렌더 스무딩 Implementation Plan (corrected, TDD)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 내 캐릭이 넉백 등 예측-불가 서버 보정을 받을 때 시뮬 하드보정이 순간이동(또는 반대편으로 번쩍)처럼 보이는 것을, 시뮬은 그대로 두고 보이는 메시만 지연-보간 타깃으로 부드럽게 이징해 없앤다.

**Architecture:** 순수 스무더 `RenderCorrectionSmoother`(GameFramework.Netcode, System.Numerics, EditMode TDD)가 보정 창 동안만 render 위치를 target으로 지수 이징(오버슈트 없음), 평소 정확 추종(랙 0). `Reconciler`는 보정 "신호"만 발행, `LocalEntityInterpolator`는 지연-보간 target을 스무더에 통과시켜 렌더. 시뮬/스냅/리플레이/남 캐릭 무영향.

**Tech Stack:** C#/Unity, `System.Numerics`(순수 커널), NUnit(EditMode), VContainer.

## Global Constraints

- **브랜치:** GameFramework·LOP-Client 둘 다 `feature/render-correction-smoothing`. Client는 이미 spec/plan 커밋 있음.
- **커널 위치=제자리:** 순수 스무딩 로직은 `GameFramework/Runtime/Scripts/Netcode/`(asmdef `baegames.GameFramework.Runtime`, `System.Numerics` 순수 — `ReconcileGate`/`SnapshotHistory`와 동거). 테스트용 이동이 아니라 넷코드 primitive의 제자리.
- **TDD 필수:** 커널은 테스트 먼저(RED) → 구현(GREEN). 테스트가 **초기 번쩍 버그(오버슈트)를 인코딩**해 재발 방지.
- **시뮬 무영향:** 스무더는 `visualGameObject.transform`에만. `entity.position`/스냅/리플레이 불변.
- **보정 신호만:** `Reconciler`는 델타값이 아니라 "보정 발생"만 알림(`MarkCorrection`). 스무더는 target으로 이징하므로 델타 불필요.
- **텔레포트 예외:** 보정 크기 `> 3m`(리스폰 등)면 신호 안 함 → 스무더 정확 추종 = 스냅.
- **튜닝 시작값:** `SmoothingTime=0.15f`, `CorrectionWindow=0.3f`, `TeleportThreshold=3f`. 지수 이징 `1 − MathF.Exp(−dt/τ)`.
- **회전 무대상:** 넉백은 위치만.

경로 약칭: `GF=C:\Users\re5na\workspace\LOP\GameFramework`, `CLIENT=C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Client`. Unity 인스턴스(컨트롤러 검증): client=`LeagueOfPhysical-Client@de70658b9450cbb4`(GameFramework를 file: 참조 → GF EditMode 테스트도 여기서 실행).

---

### Task 1: RenderCorrectionSmoother 순수 커널 + EditMode TDD (GameFramework)

보정 창 동안 render를 target으로 지수 이징하는 순수 스무더. **테스트 먼저** — 오버슈트-없음/단조수렴/평소-랙0/창만료/리셋.

**Files:**
- Create: `GF\Runtime\Scripts\Netcode\RenderCorrectionSmoother.cs`
- Test: `GF\Tests\Runtime\Netcode\RenderCorrectionSmootherTests.cs`

**Interfaces:**
- Produces: `GameFramework.Netcode.RenderCorrectionSmoother` — ctor `(float smoothingTime)`, `void MarkCorrection(float window)`, `System.Numerics.Vector3 Smooth(System.Numerics.Vector3 target, float deltaTime)`, `void Reset()`.

- [ ] **Step 1: 실패 테스트 작성 (RED)**

`GF\Tests\Runtime\Netcode\RenderCorrectionSmootherTests.cs` (네임스페이스·usings는 sibling `SnapshotHistoryTests`와 동일):

```csharp
using System.Numerics;
using GameFramework.Netcode;
using NUnit.Framework;

namespace GameFramework.Tests.Netcode
{
    public class RenderCorrectionSmootherTests
    {
        [Test]
        public void Idle_ReturnsTargetExactly_NoLag()
        {
            var s = new RenderCorrectionSmoother(0.15f);
            Assert.AreEqual(new Vector3(5, 0, 0), s.Smooth(new Vector3(5, 0, 0), 0.016f));
            Assert.AreEqual(new Vector3(9, 0, 3), s.Smooth(new Vector3(9, 0, 3), 0.016f));
        }

        [Test]
        public void AfterCorrection_FirstFrame_NoOvershoot_StaysNearOld()
        {
            var s = new RenderCorrectionSmoother(0.15f);
            s.Smooth(new Vector3(0, 0, 0), 0.016f);   // 추종 → current=old(0)
            s.MarkCorrection(0.3f);
            var r = s.Smooth(new Vector3(2, 0, 0), 0.016f);   // 타깃 +2 점프
            // 버그판(2old-new = -2)이면 실패. 정상: old(0)~target(2) 사이, 반대편(-)로 안 감, 첫 프레임은 old 근처.
            Assert.Greater(r.X, -0.001f, "반대편 오버슈트 금지");
            Assert.Less(r.X, 2f, "타깃 넘지 않음");
            Assert.Less(r.X, 0.5f, "첫 프레임은 old 근처");
        }

        [Test]
        public void DuringWindow_Monotonic_NeverPastTarget()
        {
            var s = new RenderCorrectionSmoother(0.15f);
            s.Smooth(Vector3.Zero, 0.016f);
            s.MarkCorrection(0.3f);
            var target = new Vector3(2, 0, 0);
            float prev = 0f;
            for (int i = 0; i < 20; i++)
            {
                float x = s.Smooth(target, 0.016f).X;
                Assert.GreaterOrEqual(x, prev - 0.001f, "단조 증가");
                Assert.LessOrEqual(x, 2.001f, "타깃 안 넘음");
                prev = x;
            }
            Assert.Greater(prev, 1.5f, "창 동안 대부분 수렴");
        }

        [Test]
        public void WindowExpired_SnapsToTarget()
        {
            var s = new RenderCorrectionSmoother(0.15f);
            s.Smooth(Vector3.Zero, 0.016f);
            s.MarkCorrection(0.1f);
            for (int i = 0; i < 10; i++) { s.Smooth(new Vector3(2, 0, 0), 0.016f); }  // 창(0.1s) 소진
            Assert.AreEqual(new Vector3(7, 0, 0), s.Smooth(new Vector3(7, 0, 0), 0.016f), "만료 후 정확 추종");
        }

        [Test]
        public void Reset_ReinitializesToNextTarget()
        {
            var s = new RenderCorrectionSmoother(0.15f);
            s.Smooth(Vector3.Zero, 0.016f);
            s.MarkCorrection(0.3f);
            s.Smooth(new Vector3(2, 0, 0), 0.016f);
            s.Reset();
            Assert.AreEqual(new Vector3(9, 0, 0), s.Smooth(new Vector3(9, 0, 0), 0.016f), "리셋 후 첫 호출은 타깃로");
        }
    }
}
```

- [ ] **Step 2: RED 확인 (컴파일 실패)**

> 컨트롤러가 UnityMCP `run_tests`(EditMode, `baegames.GameFramework.Runtime.Tests`)로 실행 — `RenderCorrectionSmoother` 미정의라 컴파일 에러(=RED). 구현 subagent는 Unity 미실행이니 "RED=미정의 컴파일에러, 컨트롤러 확인" 기록.

- [ ] **Step 3: 커널 구현 (GREEN)**

`GF\Runtime\Scripts\Netcode\RenderCorrectionSmoother.cs`:

```csharp
using System;
using System.Numerics;

namespace GameFramework.Netcode
{
    /// <summary>
    /// 서버 보정 후 렌더 위치를 target으로 부드럽게 이징한다(타깃으로만 수렴 — 오버슈트 없음).
    /// 보정 창 동안만 지수 이징, 평소엔 target을 정확히 추종(랙 0). 시뮬(true)과 분리된 render 스무딩 —
    /// Unreal NetworkSmoothingMode(Exponential) 대응. 순수(System.Numerics) — 프레임독립·유닛 테스트 가능.
    /// </summary>
    public class RenderCorrectionSmoother
    {
        private readonly float _smoothingTime;
        private Vector3 _current;
        private bool _hasCurrent;
        private float _windowRemaining;

        public RenderCorrectionSmoother(float smoothingTime)
        {
            _smoothingTime = smoothingTime;
        }

        /// <summary>보정 발생 알림 — 이징 창을 연다(초). 평소엔 호출 안 됨 → 정확 추종.</summary>
        public void MarkCorrection(float window)
        {
            _windowRemaining = window;
        }

        /// <summary>이번 프레임 렌더 위치. 창 동안 target으로 지수 이징, 평소 target 그대로.</summary>
        public Vector3 Smooth(Vector3 target, float deltaTime)
        {
            if (!_hasCurrent)
            {
                _current = target;
                _hasCurrent = true;
                return _current;
            }
            if (_windowRemaining > 0f)
            {
                _windowRemaining -= deltaTime;
                float a = 1f - MathF.Exp(-deltaTime / _smoothingTime);
                _current = Vector3.Lerp(_current, target, a);
            }
            else
            {
                _current = target;
            }
            return _current;
        }

        public void Reset()
        {
            _hasCurrent = false;
            _windowRemaining = 0f;
        }
    }
}
```

- [ ] **Step 4: GREEN 확인**

> 컨트롤러가 `run_tests`(EditMode, `baegames.GameFramework.Runtime.Tests`) 실행. Expected: 신규 5개 + 기존 netcode 테스트 전부 PASS.

- [ ] **Step 5: 커밋 (GameFramework, .meta 포함)**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git checkout -b feature/render-correction-smoothing 2>/dev/null || git checkout feature/render-correction-smoothing
git add Runtime/Scripts/Netcode/RenderCorrectionSmoother.cs Runtime/Scripts/Netcode/RenderCorrectionSmoother.cs.meta Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs Tests/Runtime/Netcode/RenderCorrectionSmootherTests.cs.meta
git commit -m "feat(netcode): RenderCorrectionSmoother 순수 커널 + EditMode TDD (오버슈트 없는 보정 이징)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
(`.meta`는 컨트롤러 refresh가 생성 — 없으면 .cs만 커밋 후 컨트롤러가 .meta 추가.)

---

### Task 2: 클라 배선 — 등록 + Reconciler 보정 신호

스무더를 DI 등록하고, `Reconciler`가 하드 보정 후 보정 신호를 발행하게 한다.

**Files:**
- Modify: `CLIENT\Assets\Scripts\Game\GameLifetimeScope.cs:83` (등록)
- Modify: `CLIENT\Assets\Scripts\Entity\Reconciler.cs`

**Interfaces:**
- Consumes: `RenderCorrectionSmoother(float)`, `.MarkCorrection(float)` (Task 1).
- Produces: `Reconciler`가 정상 재생 경로에서 보정 신호 발행.

- [ ] **Step 1: GameLifetimeScope 등록**

`GameLifetimeScope.cs`의 `builder.Register<ReconciliationStats>(Lifetime.Singleton);`(83행) 아래에 추가(SnapshotHistory와 같은 factory 등록 형식):

```csharp
            builder.Register(_ => new GameFramework.Netcode.RenderCorrectionSmoother(0.15f), Lifetime.Singleton);
```

- [ ] **Step 2: Reconciler에 주입 + 상수**

`Reconciler.cs` 필드부 `[Inject] private ReconciliationStats reconciliationStats;` 아래:

```csharp
        [Inject] private GameFramework.Netcode.RenderCorrectionSmoother renderCorrectionSmoother;
```

클래스 상단 상수(`MaxReplayTicks` 옆):

```csharp
        private const float TeleportThreshold = 3f;    // 이보다 큰 보정(리스폰 등)은 스무딩 안 함(스냅)
        private const float CorrectionWindow = 0.3f;   // 보정 이징 창(초)
```

- [ ] **Step 3: preCorrectionPos 캡처 + 재생 후 보정 신호**

`Reconcile`의 worldEntity null 체크 직후에 캡처 추가:

```csharp
            // 예측된 현재 위치 — 하드 보정 전. 재생 후와의 차이로 보정 크기를 판정(시각 신호용).
            Vector3 preCorrectionPos = entity.position;
```

그리고 재생 `for` 루프 닫는 `}` 직후(메서드 끝, early-return 제외 = 정상 재생 경로):

```csharp
            // 하드 보정으로 시뮬이 튄 크기가 유의미하면(텔레포트 아님) 렌더 스무더에 "보정 발생"을 알린다.
            // 스무더가 보이는 메시를 창 동안 부드럽게 이징(시뮬 무영향). 큰 보정(리스폰)은 신호 안 함 → 스냅.
            if ((preCorrectionPos - entity.position).magnitude <= TeleportThreshold)
            {
                renderCorrectionSmoother.MarkCorrection(CorrectionWindow);
            }
```

- [ ] **Step 4: 컴파일 확인 (컨트롤러)**

> 컨트롤러 refresh(client) + `read_console`. Expected: 에러 0.

- [ ] **Step 5: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git checkout feature/render-correction-smoothing
git add Assets/Scripts/Game/GameLifetimeScope.cs Assets/Scripts/Entity/Reconciler.cs
git commit -m "feat(netcode): RenderCorrectionSmoother 등록 + Reconciler 보정 신호

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: LocalEntityInterpolator — 스무더 통과

지연-보간 타깃을 스무더에 통과시켜 렌더한다.

**Files:**
- Modify: `CLIENT\Assets\Scripts\Entity\LocalEntityInterpolator.cs`

**Interfaces:**
- Consumes: `RenderCorrectionSmoother.Smooth(System.Numerics.Vector3, float)→System.Numerics.Vector3`, `.Reset()` (Task 1).

- [ ] **Step 1: 주입**

`LocalEntityInterpolator.cs`의 `[Inject] private IRunner runner;` 아래:

```csharp
        [Inject] private GameFramework.Netcode.RenderCorrectionSmoother renderCorrectionSmoother;
```

- [ ] **Step 2: LateUpdate에서 스무더 통과**

`LateUpdate`의 렌더 위치 대입 라인(현재 `entityView.visualGameObject.transform.position = Vector3.Lerp(prev.position, next.position, t);`)을 교체:

```csharp
            Vector3 target = Vector3.Lerp(prev.position, next.position, t);
            entityView.visualGameObject.transform.position =
                renderCorrectionSmoother.Smooth(target.ToNumerics(), Time.deltaTime).ToUnity();
```

(회전 라인은 그대로. `Smooth`는 target(스냅 있는 프레임)에만 호출 — 스냅 부재 early-return 시 미호출, 직전 위치 유지: 기존 동작.)

- [ ] **Step 3: Cleanup에서 Reset**

`Cleanup()`를 교체:

```csharp
        public void Cleanup()
        {
            runner.RemoveListener(this);
            renderCorrectionSmoother.Reset();
        }
```

- [ ] **Step 4: 컴파일 확인 (컨트롤러)**

> 컨트롤러 refresh(client) + `read_console`. Expected: 에러 0. (`.ToNumerics()/.ToUnity()`는 GameFramework `Extensions` 확장 — `LocalEntityInterpolator`는 `using GameFramework;` 이미 있음. 없으면 추가.)

- [ ] **Step 5: 커밋**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client"
git add Assets/Scripts/Entity/LocalEntityInterpolator.cs
git commit -m "feat(netcode): LocalEntityInterpolator가 RenderCorrectionSmoother 통과 (번쩍 없는 이징)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: 끝-끝 플레이 검증 + 튜닝

번쩍/순간이동이 사라졌는지, 무회귀인지 확인하고 손맛 조정.

**Files:** (없음 — 관측/검증/튜닝)

- [ ] **Step 1: 서버·클라 Play + 접속** (auth 막히면 playerList uuid 픽스처.)

- [ ] **Step 2: 넉백 — 번쩍/순간이동 소멸 (핵심)**

RTT 300ms 주입 + 내 캐릭 넉백:
- **반대편으로 번쩍 없음**(초기 버그), 순간이동 없음, 메시가 ~150ms 부드럽게 밀림.
- DebugHud reconciliation distance는 시뮬 교정(~0.6m) 그대로 = 정상.
Expected: 번쩍·순간이동 소멸, 부드러움.

- [ ] **Step 3: RTT 50ms + 무회귀**

걷기/대시/정지/점프 조작감·정확성 그대로(스무더는 평소 랙 0), 남 캐릭 넉백 무영향.

- [ ] **Step 4: 엣지**

연속 히트(창 재세팅 매끄러움), 리스폰/큰 이동(3m 초과 → 즉시 스냅).

- [ ] **Step 5: 튜닝(필요 시)**

늘어지면 `GameLifetimeScope`의 `RenderCorrectionSmoother(0.15f)` → `0.10f`, 번쩍 흔적이면 창(`CorrectionWindow`)·τ 조정. 변경 시 커밋.

- [ ] **Step 6: 후기 + 커밋**

spec 하단에 "구현 후기"(최종 값·검증) 추가 후 커밋(client).

---

## 통합 / 머지

GameFramework(Task 1) → Client(Task 2·3). file: 참조라 브랜치 상태로 검증. 플레이 통과 후 두 레포 main에 `--no-ff` 머지(finishing-a-development-branch).

## Out of Scope (spec과 동일)

- 남 캐릭 스무딩, 회전 스무딩, Option B(지연 렌더), velocity 권위/Stage④, standalone .NET TDD 툴(별도 이니셔티브).

## Self-Review 결과

- **Spec 커버리지:** §A(커널)=Task 1, §B(TDD 테스트)=Task 1, §C(Reconciler 신호+텔레포트)=Task 2, §D(interpolator 통과+Reset)=Task 3, §E(등록)=Task 2, 튜닝=Global+Task 4, 검증=Task 1(EditMode)+Task 4(play). 전부 커버.
- **Placeholder:** 없음(튜닝은 구체 시작값 + 방향).
- **타입 일관:** `RenderCorrectionSmoother(float)`/`MarkCorrection(float)`/`Smooth(Vector3,float)→Vector3`/`Reset()` — Task 1 정의 ↔ Task 2(MarkCorrection) ↔ Task 3(Smooth/Reset) 일치. `System.Numerics.Vector3` ↔ `UnityEngine.Vector3`는 Task 3에서 `.ToNumerics()/.ToUnity()` 경계 변환.

## 진행

- [ ] Task 1~4 구현 (TDD)
- [ ] 최종 리뷰 + 머지
