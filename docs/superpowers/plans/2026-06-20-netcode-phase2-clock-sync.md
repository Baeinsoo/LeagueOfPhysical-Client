# Netcode Phase 2 — Clock Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 클라 시뮬 클럭을 서버보다 앞쪽(client-ahead)에서 달리게 해 입력 적시 도착 + reconciliation 갭 축소. 방향 A(`predictedTime + aheadMargin`) + rate dilation(`ClockDilator`).

**Architecture:** GameFramework에 순수·테스트 가능 `ClockDilator`(rate dilation, 역행 없음, snap) 신설. 클라 `LOPTickUpdater`가 `OnElapsedTimeUpdate`에서 이 컨트롤러로 `elapsedTime`을 타깃에 수렴. 2a: 타깃 `NetworkTime.time` 유지(동작 보존). 2b: 타깃 `predictedTime + aheadMargin`로 flip + HUD Lead 정확화. 클라 전용(2a는 GameFramework도), 서버 무변경.

**Tech Stack:** C# (Unity), VContainer, Mirror NetworkTime, NUnit EditMode(GameFramework만), UnityMCP 컴파일/테스트.

**Related spec:** `docs/superpowers/specs/2026-06-20-netcode-phase2-clock-sync-design.md`

**Resolved Unity instance (매 UnityMCP 호출에 명시 — HTTP stateless):** Client — `mcpforunity://instances`에서 `LeagueOfPhysical-Client`의 `id`(현재 `LeagueOfPhysical-Client@de70658b9450cbb4`, hash 변동 가능). 클라 전용 — 서버 인스턴스 건드리지 말 것. GameFramework는 file: 공유라 클라 에디터에서 컴파일/테스트하면 됨.

> **브랜치:** 클라는 이미 `feature/netcode-phase2-clock-sync`(spec 커밋됨) — 그 브랜치에서 작업. GameFramework는 별도 repo라 자체 피처 브랜치 생성(Task 1). 세션 시작 시점 무관 dirty(`UIRoot.prefab` 등) 미접촉.

> **`.git/index.lock`:** 커밋 실패 시 `rm -f .git/index.lock` 후 재시도(이 repo 간헐 발생).

---

## File Structure

- **Create (GameFramework):** `Runtime/Scripts/Game/ClockDilator.cs` — 순수 rate-dilation 컨트롤러(`baegames.GameFramework.Runtime` assembly).
- **Create (GameFramework):** `Tests/Runtime/baegames.GameFramework.Runtime.Tests.asmdef` — Runtime용 EditMode 테스트 asmdef(신설; 기존 World.Tests는 World만 참조).
- **Create (GameFramework):** `Tests/Runtime/ClockDilatorTests.cs` — ClockDilator EditMode 테스트.
- **Modify (Client):** `Assets/Scripts/Game/LOPTickUpdater.cs` — 2a: ClockDilator 사용(타깃 `.time`). 2b: 타깃 `predictedTime + aheadMargin`.
- **Modify (Client):** `Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs` — 2b: `ServerTickEstimate`를 서버-now 추정으로 정확화.

슬라이스: **2a = Task 1(GameFramework ClockDilator+테스트) + Task 2(클라 LOPTickUpdater 메커니즘 전환)**, **2b = Task 3(클라 타깃 flip + HUD)**. 2a→2b 순서(게이트: 2b가 ClockDilator + seam에 의존).

---

## Task 1: GameFramework `ClockDilator` + EditMode 테스트 (2a, TDD)

**Files:**
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Runtime/Scripts/Game/ClockDilator.cs`
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Tests/Runtime/baegames.GameFramework.Runtime.Tests.asmdef`
- Create: `C:/Users/re5na/workspace/LOP/GameFramework/Tests/Runtime/ClockDilatorTests.cs`

- [ ] **Step 1: GameFramework 피처 브랜치**
```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" status --short
git -C "C:/Users/re5na/workspace/LOP/GameFramework" checkout -b feature/netcode-phase2-clock-sync
```
(다른 dirty 있으면 확인. 깨끗해야 정상.)

- [ ] **Step 2: 테스트 asmdef 생성** (기존 World.Tests 미러, Runtime 참조)
`Tests/Runtime/baegames.GameFramework.Runtime.Tests.asmdef`:
```json
{
    "name": "baegames.GameFramework.Runtime.Tests",
    "rootNamespace": "GameFramework.Tests",
    "references": [
        "baegames.GameFramework.Runtime",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 3: `ClockDilator` 스텁 작성** (red 유도 — Advance가 아직 미구현으로 current 그대로 반환)
`Runtime/Scripts/Game/ClockDilator.cs`:
```csharp
namespace GameFramework
{
    /// <summary>
    /// 클럭(시간 값)을 타깃 시간으로 rate time-dilation으로 수렴시킨다. 값을 직접 대입(smoothing)하지 않고
    /// 진행 *속도*를 ±MaxRate로 조정 → 절대 역행하지 않음(결정론/보간 보호). 큰 오차는 1회 snap.
    /// netcode clock sync용(클라가 predictedTime+margin으로 수렴). 순수 함수라 EditMode 테스트 가능.
    /// </summary>
    public class ClockDilator
    {
        private readonly double maxRate;
        private readonly double errorScale;
        private readonly double snapThreshold;

        public ClockDilator(double maxRate = 0.05, double errorScale = 0.1, double snapThreshold = 0.5)
        {
            this.maxRate = maxRate;
            this.errorScale = errorScale;
            this.snapThreshold = snapThreshold;
        }

        /// <summary>current를 target 쪽으로 realDelta 동안 dilation해 advance한 새 값 반환.</summary>
        public double Advance(double current, double target, double realDelta)
        {
            return current; // STUB — Step 5에서 구현
        }
    }
}
```

- [ ] **Step 4: 테스트 작성 + 컴파일 후 red 확인**
`Tests/Runtime/ClockDilatorTests.cs`:
```csharp
using NUnit.Framework;

namespace GameFramework.Tests
{
    public class ClockDilatorTests
    {
        [Test]
        public void Advance_ErrorBeyondSnapThreshold_SnapsToTarget()
        {
            var dilator = new ClockDilator(maxRate: 0.05, errorScale: 0.1, snapThreshold: 0.5);
            // error 1.0 > 0.5 → snap
            double result = dilator.Advance(current: 0.0, target: 1.0, realDelta: 0.016);
            Assert.AreEqual(1.0, result, 1e-9);
        }

        [Test]
        public void Advance_TargetAhead_SaturatesToMaxRate()
        {
            var dilator = new ClockDilator(0.05, 0.1, 0.5);
            // error 0.4 (< snap), 0.4/0.1=4 → clamp +0.05 → rate 1.05
            double result = dilator.Advance(0.0, 0.4, 0.016);
            Assert.AreEqual(0.016 * 1.05, result, 1e-9);
            Assert.Greater(result, 0.016); // 실시간보다 빠르게(가속)
        }

        [Test]
        public void Advance_TargetBehind_DeceleratesButNeverRewinds()
        {
            var dilator = new ClockDilator(0.05, 0.1, 0.5);
            // current가 target보다 0.05 앞섬 → -0.05/0.1 clamp -0.05 → rate 0.95
            double result = dilator.Advance(1.0, 0.95, 0.016);
            Assert.AreEqual(1.0 + 0.016 * 0.95, result, 1e-9);
            Assert.Greater(result, 1.0);         // 역행 없음(monotonic)
            Assert.Less(result, 1.0 + 0.016);    // 실시간보다 느리게
        }

        [Test]
        public void Advance_SmallError_ProportionalDilation()
        {
            // 넓은 errorScale로 비포화(비례) 구간 검증
            var dilator = new ClockDilator(0.05, 1.0, 0.5);
            // error 0.025, 0.025/1.0=0.025 (< maxRate) → rate 1.025
            double result = dilator.Advance(0.0, 0.025, 0.016);
            Assert.AreEqual(0.016 * 1.025, result, 1e-9);
        }
    }
}
```
- 컴파일: 클라 instance id 해석 후 `refresh_unity(mode="force", scope="all", compile="request", unity_instance="<클라 id>")` + `read_console(types=["error"], unity_instance="<클라 id>")` → 0 errors(스텁이 컴파일됨).
- 테스트 실행: `run_tests(mode="EditMode", unity_instance="<클라 id>")` (필터 가능하면 `ClockDilator`). Expected: **4개 테스트 전부 FAIL** = red(스텁이 current를 반환 → snap은 1.0 기대인데 0 반환, 나머지도 불일치).

- [ ] **Step 5: `Advance` 구현** (green)
`ClockDilator.cs`의 `Advance` 본문 교체:
```csharp
        public double Advance(double current, double target, double realDelta)
        {
            double error = target - current;
            if (System.Math.Abs(error) > snapThreshold)
            {
                return target; // 급변(경로 변화/ping spike) — 1회 snap
            }
            double dilation = System.Math.Clamp(error / errorScale, -maxRate, maxRate);
            double rate = 1.0 + dilation; // maxRate<1 → rate>0 (역행 없음)
            return current + realDelta * rate;
        }
```

- [ ] **Step 6: 테스트 green 확인**
- `refresh_unity(...)` + `read_console(types=["error"], ...)` → 0 errors.
- `run_tests(mode="EditMode", unity_instance="<클라 id>")` → ClockDilator 4 테스트 **전부 PASS**.

- [ ] **Step 7: GameFramework 커밋**
```bash
git -C "C:/Users/re5na/workspace/LOP/GameFramework" add Runtime/Scripts/Game/ClockDilator.cs Runtime/Scripts/Game/ClockDilator.cs.meta Tests/Runtime/baegames.GameFramework.Runtime.Tests.asmdef Tests/Runtime/baegames.GameFramework.Runtime.Tests.asmdef.meta Tests/Runtime/ClockDilatorTests.cs Tests/Runtime/ClockDilatorTests.cs.meta Tests/Runtime.meta
git -C "C:/Users/re5na/workspace/LOP/GameFramework" commit -m "feat(netcode): ClockDilator — rate time-dilation clock steering (no rewind) + EditMode tests

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
(`.meta` 파일들은 Step 4/6 compile 후 Unity가 생성 — `git status`로 실제 생성된 .meta만 add. `Tests/Runtime.meta`(폴더 메타)도 포함. lock 에러 시 `rm -f .git/index.lock`.)
- `git -C "..." show --stat HEAD` — ClockDilator.cs(+meta), 테스트 asmdef(+meta), 테스트 cs(+meta), 폴더 meta만. 다른 파일 없음.

---

## Task 2: 클라 `LOPTickUpdater` 메커니즘 전환 (2a, 타깃 `.time` 유지)

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/LOPTickUpdater.cs`

- [ ] **Step 1: 전체 교체** — critically-damped 값 smoothing → `ClockDilator`(타깃 `.time` 유지):
```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPTickUpdater : TickUpdaterBase
    {
        private readonly ClockDilator clockDilator = new ClockDilator();

        protected override void OnElapsedTimeUpdate()
        {
            // 2a: 타깃은 현행 NetworkTime.time 유지(lead 없음). 메커니즘만 rate dilation으로 전환.
            //     (2b에서 predictedTime + aheadMargin으로 flip)
            double target = Mirror.NetworkTime.time;
            elapsedTime = clockDilator.Advance(elapsedTime, target, Time.deltaTime);
        }
    }
}
```
(기존 `SMOOTH_TIME`/`MAX_DELTA`/`elapsedTimeVelocity`/`OnElapsedTimeUpdate`의 SmoothDamp 본문 전부 제거 — snap은 ClockDilator.snapThreshold(0.5s)가 흡수, 기존 MAX_DELTA 0.5와 동일.)

- [ ] **Step 2: 클라 컴파일 0에러**
- `refresh_unity(mode="force", scope="all", compile="request", unity_instance="<클라 id>")` + `read_console(types=["error"], unity_instance="<클라 id>")` → 0 errors.

- [ ] **Step 3: 클라 커밋**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" add Assets/Scripts/Game/LOPTickUpdater.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" commit -m "refactor(netcode): LOPTickUpdater uses ClockDilator (rate dilation, target still NetworkTime.time)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
- `git -C "..." show --stat HEAD | head -6` — `LOPTickUpdater.cs` 1파일만(무관 dirty 제외).

---

## Task 3 (사용자 검증 게이트 — 2a): 런타임 수동 검증

> Task 2 후, 2b 진입 전에 사용자가 확인. 무회귀가 2a 성공 기준.

클라 플레이:
1. 이동/점프 **부드러움**, 텔레포트/떨림 **없음**(rate dilation 전환이 기존 smoothing과 체감 동등).
2. Phase 0 HUD **Recon last/avg/max ≈ 2a 이전 baseline**(무회귀 — 타깃 동일이라 갭 안 변함).
3. **Lead ≈ 0**(아직 lead 없음).
4. 콘솔 에러 0.

통과하면 2b(Task 4)로.

---

## Task 4: 클라 타깃 flip + HUD Lead 정확화 (2b)

**Files:**
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/Game/LOPTickUpdater.cs`
- Modify: `C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client/Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs`

- [ ] **Step 1: `LOPTickUpdater` 타깃 flip** — `OnElapsedTimeUpdate`를 predictedTime + aheadMargin으로:
```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPTickUpdater : TickUpdaterBase
    {
        // 오버워치식 ahead 마진(지터/+1프레임). predictedTime에 편도지연(RTT/2)+서버피드백 이미 포함 — 마진만 추가.
        private const double AheadMargin = 0.030;

        private readonly ClockDilator clockDilator = new ClockDilator();

        protected override void OnElapsedTimeUpdate()
        {
            double target = Mirror.NetworkTime.predictedTime + AheadMargin;
            elapsedTime = clockDilator.Advance(elapsedTime, target, Time.deltaTime);
        }
    }
}
```

- [ ] **Step 2: `DebugHudViewModel.ServerTickEstimate` 정확화** — `.time` 기반 → 서버-now 추정(`predictedTime − rtt/2`). 현 라인:
```csharp
        public long ServerTickEstimate => (long)(Mirror.NetworkTime.time / GameEngine.Time.tickInterval);
```
→ 교체:
```csharp
        // 서버 현재 tick 추정 ≈ (predictedTime − 편도지연)/interval. Lead = Tick − 이것 = (AheadMargin + 편도지연)/interval = 진짜 lead.
        public long ServerTickEstimate => (long)((Mirror.NetworkTime.predictedTime - Mirror.NetworkTime.rtt * 0.5) / GameEngine.Time.tickInterval);
```
(`Lead`/나머지 getter 무변경.)

- [ ] **Step 3: 클라 컴파일 0에러** — `refresh_unity(...)` + `read_console(types=["error"], ...)` → 0 errors.

- [ ] **Step 4: 클라 커밋**
```bash
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" add Assets/Scripts/Game/LOPTickUpdater.cs Assets/Scripts/UI/DebugHud/DebugHudViewModel.cs
git -C "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Client" commit -m "feat(netcode): client-ahead clock via predictedTime + aheadMargin; HUD lead uses server-now estimate (Phase 2b)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```
- `git -C "..." show --stat HEAD | head -6` — `LOPTickUpdater.cs` + `DebugHudViewModel.cs` 2파일만.

---

## Task 5 (사용자 검증 게이트 — 2b): LatencySimulation 측정

> production 코드 아님 — Mirror `LatencySimulation` transport 컴포넌트(에디터 설정)로 RTT 주입 후 측정.

1. 클라 transport에 `LatencySimulation` 추가/활성, 예: 75ms latency(±지터). (또는 50/150ms 비교.)
2. 공중 점프 시나리오 반복.
3. HUD 확인:
   - **Lead 양수** ≈ `(AheadMargin + RTT/2)/interval` 틱(예: 30ms + 37.5ms = 67.5ms → tickInterval 기준 틱 수).
   - **Recon max/avg가 2a baseline 대비 감소**.
   - Server tick ≈ 클라보다 lead만큼 뒤(정상).
4. 고무줄/순간이동 육안 감소. 자기 캐릭 렌더 튐 없음(`SnapReconciler.LateUpdate` 상호작용 정상).
5. 콘솔 에러 0(특히 0-나눗셈/NRE 없음).
6. 측정 후 LatencySimulation 비활성(설정 원복).

동작 개선(Lead↑·Recon↓)이 2b 성공.

---

## 완료 기준

- [ ] GameFramework `feature/netcode-phase2-clock-sync`에 ClockDilator + 테스트 커밋. EditMode 4 테스트 PASS.
- [ ] 클라 `feature/netcode-phase2-clock-sync`에 2a(LOPTickUpdater) + 2b(LOPTickUpdater+HUD) 커밋.
- [ ] 클라 컴파일 0에러. GameFramework file: 공유 — 서버도 비파괴(ClockDilator 추가만).
- [ ] 2a: 무회귀(Recon≈baseline, Lead≈0). 2b: Lead 양수·Recon 감소(LatencySimulation).
- [ ] 무관 dirty(`UIRoot.prefab` 등) 미커밋 보존.

이후: 사용자 검증 후 **GameFramework + 클라 각 `feature/netcode-phase2-clock-sync` → main `--no-ff` 머지**(클라는 spec 커밋도 같은 브랜치라 함께). netcode Phase 2 완료. 이후 Phase 3(서버 input buffer 정렬) — 별도 spec.
