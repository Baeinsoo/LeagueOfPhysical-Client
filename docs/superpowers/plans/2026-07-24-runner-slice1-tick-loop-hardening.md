# Runner Slice 1 — 틱 루프 강화 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `TickUpdaterBase`의 상한 없는 캐치업 루프(spiral of death 위험)를 프레임당 틱 상한으로 막고, 문자열 코루틴을 참조 기반으로 정리한다.

**Architecture:** 한 프레임에 처리할 틱 수를 계산하는 **순수 static 커널**(`TickCatchUp.ClampTarget`)을 추가해 EditMode로 TDD한다. `TickUpdaterBase`의 코루틴 루프가 이 커널을 호출해 캐치업을 상한으로 자른다(초과 시 1회 경고 로그). 문자열 코루틴(`StartCoroutine("...")`)은 저장된 `Coroutine` 참조로 교체한다. 변경은 GameFramework(공유 패키지)에 있어 클·서가 자동으로 받는다 — 클라 코드 변경 0, 서버는 죽은 base 호출 1줄 제거만.

**Tech Stack:** Unity, C#, NUnit(EditMode), Unity coroutine. GameFramework 패키지(`namespace GameFramework`).

## Global Constraints

- **브랜치 규율**: main 직접 커밋 금지. 레포별 피처 브랜치에서 작업 (`docs/architecture-guidelines.md` Git 워크플로우).
- **.meta**: 새 `.cs` 파일은 Unity가 생성한 `.meta`와 **함께** 커밋 (`.meta` 직접 생성·수정 금지).
- **순수 커널**: 컨텍스트 없는 순수 로직은 `static`으로 두고 GameFramework EditMode로 단위 테스트 (`world-core-connection-architecture.md` 시뮬 코드 형태 규약).
- **이 슬라이스는 호스트 배선만** — 틱당 sim 로직은 불변. 캐치업 상한은 *한 프레임에 몇 틱을 도느냐*만 바꾸고 *각 틱의 처리*는 그대로라 결정론 영향 없음.
- **`MaxTicksPerFrame = 8`** — 50Hz(interval 0.02s) 기준 프레임당 최대 160ms 캐치업. 상수(필요 시 후일 튜닝, YAGNI).

## Repos & Branches

이 슬라이스는 두 레포를 건드린다. 착수 전 각 레포에 피처 브랜치를 만든다(이름 통일):

- **GameFramework** `C:\Users\re5na\workspace\LOP\GameFramework` — Task 1, 2. 브랜치 `refactor/runner-slice1-tick-loop`.
- **LeagueOfPhysical-Server** `C:\Users\re5na\workspace\LOP\LeagueOfPhysical-Server` — Task 3. 브랜치 `refactor/runner-slice1-tick-loop`.
- **LeagueOfPhysical-Client** — 코드 변경 없음(공유 base를 자동 소비). 이 plan/spec 문서만 현재 worktree 브랜치에 있음.

> GameFramework는 클·서가 `file:` 로 참조하는 공유 폴더라, 편집하면 두 에디터가 같은 코드를 본다. GF EditMode 테스트는 클라 에디터의 Test Runner(또는 UnityMCP `run_tests`, `unity_instance`=클라)로 돌린다.

**브랜치 셋업 (착수 전 1회):**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework" && git switch -c refactor/runner-slice1-tick-loop
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server" && git switch -c refactor/runner-slice1-tick-loop
```

## File Structure

| 파일 | 책임 | 작업 |
|---|---|---|
| `GameFramework/Runtime/Scripts/Game/TickCatchUp.cs` | 프레임당 틱 캐치업 상한 계산(순수 static). MonoBehaviour 비의존. | Create (Task 1) |
| `GameFramework/Tests/Runtime/TickCatchUpTests.cs` | 위 커널 EditMode 단위 테스트. | Create (Task 1) |
| `GameFramework/Runtime/Scripts/Game/TickUpdaterBase.cs` | 코루틴 틱 루프가 커널로 캐치업을 상한, 문자열 코루틴 → 참조. | Modify (Task 2) |
| `LeagueOfPhysical-Server/Assets/Scripts/Netcode/LOPTickUpdater.cs` | 덮어써지는 죽은 `base.OnElapsedTimeUpdate()` 호출 제거. | Modify (Task 3) |

---

### Task 1: `TickCatchUp` 순수 커널 (TDD)

**Files:**
- Create: `GameFramework/Runtime/Scripts/Game/TickCatchUp.cs`
- Test: `GameFramework/Tests/Runtime/TickCatchUpTests.cs`

**Interfaces:**
- Produces: `public static long GameFramework.TickCatchUp.ClampTarget(long tick, long processibleTick, int maxTicksPerFrame)` — 이번 프레임에 처리할 틱의 **포함 상한**을 반환. 처리할 틱이 없으면 `tick - 1`(호출부의 `while (tick <= 반환값)` 루프가 0회 실행). `maxTicksPerFrame < 1`은 1로 취급.

- [ ] **Step 1: 실패하는 테스트 작성**

Create `GameFramework/Tests/Runtime/TickCatchUpTests.cs`:

```csharp
using NUnit.Framework;

namespace GameFramework.Tests
{
    public class TickCatchUpTests
    {
        [Test]
        public void NothingDue_ReturnsBelowTick_SoLoopSkips()
        {
            // processibleTick가 tick보다 작으면 루프가 안 돌도록 tick-1 반환
            Assert.AreEqual(4, TickCatchUp.ClampTarget(tick: 5, processibleTick: 4, maxTicksPerFrame: 8));
        }

        [Test]
        public void CaughtUp_ReturnsProcessibleTick()
        {
            Assert.AreEqual(5, TickCatchUp.ClampTarget(5, 5, 8));
        }

        [Test]
        public void BehindWithinCap_ReturnsProcessibleTick()
        {
            // 0..3 = 4틱, 상한 8 이내 → 전부 처리
            Assert.AreEqual(3, TickCatchUp.ClampTarget(0, 3, 8));
        }

        [Test]
        public void BehindBeyondCap_ClampsToCap()
        {
            // 0..99 밀렸지만 상한 8 → 0..7만
            Assert.AreEqual(7, TickCatchUp.ClampTarget(0, 100, 8));
        }

        [Test]
        public void ExactlyAtCapBoundary_ClampsOneShortOfProcessible()
        {
            // 밀린 양 == 상한: frameEnd = 0+8-1 = 7 < 8 → 8틱 처리, 나머지는 다음 프레임
            Assert.AreEqual(7, TickCatchUp.ClampTarget(0, 8, 8));
        }

        [Test]
        public void CapBelowOne_TreatedAsOne()
        {
            Assert.AreEqual(0, TickCatchUp.ClampTarget(0, 100, 0));
        }
    }
}
```

- [ ] **Step 2: 테스트 실패 확인**

Unity 클라 에디터 Test Runner(EditMode) 또는 UnityMCP `run_tests(mode="EditMode", filter="TickCatchUpTests", unity_instance=<client>)`.
Expected: 컴파일 에러 — `TickCatchUp` 타입 없음.

- [ ] **Step 3: 최소 구현 작성**

Create `GameFramework/Runtime/Scripts/Game/TickCatchUp.cs`:

```csharp
namespace GameFramework
{
    /// <summary>
    /// 프레임당 틱 캐치업 상한 계산(순수). 한 프레임에 처리할 틱을 상한으로 잘라,
    /// 히칭·큰 시간 점프 때 무한 틱을 돌다 멈추는 spiral of death를 막는다.
    /// </summary>
    public static class TickCatchUp
    {
        /// <summary>
        /// 이번 프레임에 처리할 틱의 (포함) 상한. 처리할 게 없으면 tick-1을 반환해
        /// 호출부의 while (tick &lt;= 반환값) 루프가 0회 돌게 한다.
        /// </summary>
        public static long ClampTarget(long tick, long processibleTick, int maxTicksPerFrame)
        {
            if (processibleTick < tick)
            {
                return tick - 1;
            }

            int cap = maxTicksPerFrame < 1 ? 1 : maxTicksPerFrame;
            long frameEnd = tick + cap - 1;
            return frameEnd < processibleTick ? frameEnd : processibleTick;
        }
    }
}
```

- [ ] **Step 4: Unity 리프레시(.meta 생성) 후 테스트 통과 확인**

UnityMCP `refresh_unity(unity_instance=<client>)` → 컴파일 대기(`editor_state.isCompiling == false`) → `read_console`로 에러 없음 확인 → `run_tests(mode="EditMode", filter="TickCatchUpTests")`.
Expected: 6개 테스트 PASS.

- [ ] **Step 5: 커밋 (GameFramework 레포)**

새 `.cs` 2개 + Unity 생성 `.meta` 2개를 함께 커밋.

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git add Runtime/Scripts/Game/TickCatchUp.cs Runtime/Scripts/Game/TickCatchUp.cs.meta \
        Tests/Runtime/TickCatchUpTests.cs Tests/Runtime/TickCatchUpTests.cs.meta
git commit -m "feat(runner): 프레임당 틱 캐치업 상한 순수 커널 + EditMode 테스트

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 2: `TickUpdaterBase`에 상한 배선 + 코루틴 정리

**Files:**
- Modify: `GameFramework/Runtime/Scripts/Game/TickUpdaterBase.cs`

**Interfaces:**
- Consumes: `GameFramework.TickCatchUp.ClampTarget(long, long, int)` (Task 1).

이 태스크는 MonoBehaviour 코루틴 배선이라 EditMode 단위 테스트 대상이 아니다. 검증 = 컴파일 그린 + 플레이 스모크(캡 로직은 Task 1이 이미 단위 검증).

- [ ] **Step 1: `TickUpdaterBase.cs` 수정**

`TickUpdaterBase.cs` 전체를 아래로 교체:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameFramework
{
    public class TickUpdaterBase : MonoBehaviour, ITickUpdater
    {
        // 프레임당 캐치업 상한. 50Hz 기준 최대 160ms. 초과분은 다음 프레임으로 이월.
        private const int MaxTicksPerFrame = 8;

        public event Action<long> onTick;

        public long tick { get; private set; }
        public double interval { get; private set; }
        public double elapsedTime { get; protected set; }

        public long processibleTick
        {
            get
            {
                var processibleTick = (long)(elapsedTime / interval);
                return processibleTick;
            }
        }

        private Coroutine loop;
        private bool loggedCatchUpWarning;

        public void Run(long tick, double interval, double elapsedTime)
        {
            this.tick = tick;
            this.interval = interval;
            this.elapsedTime = elapsedTime;

            if (loop != null)
            {
                StopCoroutine(loop);
            }
            loop = StartCoroutine(TickUpdateLoop());
        }

        public void Stop()
        {
            if (loop != null)
            {
                StopCoroutine(loop);
                loop = null;
            }
        }

        private IEnumerator TickUpdateLoop()
        {
            while (true)
            {
                long frameEnd = TickCatchUp.ClampTarget(tick, processibleTick, MaxTicksPerFrame);

                // 이번 프레임에 밀린 틱을 다 못 따라잡으면(상한에 걸리면) 1회만 경고.
                bool capped = frameEnd < processibleTick;
                if (capped)
                {
                    if (loggedCatchUpWarning == false)
                    {
                        Debug.LogWarning($"[TickUpdater] catch-up capped at {MaxTicksPerFrame} ticks/frame (behind by {processibleTick - tick}).");
                        loggedCatchUpWarning = true;
                    }
                }
                else
                {
                    loggedCatchUpWarning = false;
                }

                while (tick <= frameEnd)
                {
                    TickBody();
                }

                yield return null;

                OnElapsedTimeUpdate();
            }
        }

        private void TickBody()
        {
            onTick?.Invoke(tick);
            tick++;
        }

        protected virtual void OnElapsedTimeUpdate()
        {
            // 비네트워크(오프라인) 기본값. 클·서는 override해 네트워크 시간으로 대체한다.
            // 고정 틱 누적기라 smoothDeltaTime(평활 평균)이 아닌 실제 deltaTime을 쓴다.
            elapsedTime += UnityEngine.Time.deltaTime;
        }
    }
}
```

변경 요약: ① 문자열 코루틴 → 저장된 `loop` 참조. ② 내부 while 상한을 `TickCatchUp.ClampTarget`으로. ③ 상한 걸릴 때 1회 경고. ④ base 기본값 `smoothDeltaTime` → `deltaTime`(LOP는 override라 무영향, 하위 클래스 없는 사용만 해당).

- [ ] **Step 2: 컴파일 확인**

UnityMCP `refresh_unity(unity_instance=<client>)` → `isCompiling==false` 대기 → `read_console(unity_instance=<client>)` 에러 0. 서버 에디터도 같은 GF를 보므로 `read_console(unity_instance=<server>)`도 에러 0 확인.

- [ ] **Step 3: 플레이 스모크**

클·서 2에디터로 룸 접속(로컬 픽스처) → 이동/점프가 이전과 동일하게 매끈한지 육안 확인(정상 틱은 밀린 양 0~1이라 상한 미발동, 동작 무변). Console에 `[TickUpdater] catch-up capped` 경고가 **정상 플레이 중엔 안 떠야** 정상.

- [ ] **Step 4: 커밋 (GameFramework 레포)**

```bash
cd "C:/Users/re5na/workspace/LOP/GameFramework"
git add Runtime/Scripts/Game/TickUpdaterBase.cs
git commit -m "refactor(runner): 틱 루프 캐치업 상한(spiral 방지) + 문자열 코루틴 제거

- TickCatchUp.ClampTarget으로 프레임당 MaxTicksPerFrame(8) 상한
- StartCoroutine(\"...\") 문자열형 → 저장된 Coroutine 참조
- 상한 걸릴 때 1회 경고 로그
- base OnElapsedTimeUpdate 기본값 smoothDeltaTime→deltaTime(오프라인 경로)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: 서버 `LOPTickUpdater` 죽은 base 호출 제거

**Files:**
- Modify: `LeagueOfPhysical-Server/Assets/Scripts/Netcode/LOPTickUpdater.cs`

서버는 `base.OnElapsedTimeUpdate()`(= `elapsedTime += deltaTime`)를 부른 직후 `elapsedTime = serverNow`로 **덮어쓴다** → base 호출은 죽은 코드. 제거해도 관측 동작 불변.

- [ ] **Step 1: 파일 수정**

`LeagueOfPhysical-Server/Assets/Scripts/Netcode/LOPTickUpdater.cs`에서 `base.OnElapsedTimeUpdate();` 줄 제거:

```csharp
using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPTickUpdater : TickUpdaterBase
    {
        protected override void OnElapsedTimeUpdate()
        {
            elapsedTime = Runner.NetworkTime.serverNow;
        }
    }
}
```

- [ ] **Step 2: 컴파일 확인**

UnityMCP `refresh_unity(unity_instance=<server>)` → `isCompiling==false` → `read_console(unity_instance=<server>)` 에러 0.

- [ ] **Step 3: 커밋 (Server 레포)**

```bash
cd "C:/Users/re5na/workspace/LOP/LeagueOfPhysical-Server"
git add Assets/Scripts/Netcode/LOPTickUpdater.cs
git commit -m "refactor(runner): 서버 LOPTickUpdater 죽은 base 호출 제거

base.OnElapsedTimeUpdate()의 결과가 곧바로 serverNow로 덮어써져 무의미.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## 완료 후 (통합 검증 & 머지)

- [ ] GF EditMode 전체 스위트 그린 (`run_tests(mode="EditMode", unity_instance=<client>)`) — 기존 테스트 무회귀 확인.
- [ ] 클·서 플레이 스모크 1회 — 이동/점프 정상, capped 경고 미출현.
- [ ] `superpowers:finishing-a-development-branch`로 GF·Server 각 브랜치를 `--no-ff` 머지 (spec/plan 문서는 클라 worktree 브랜치에서 별도 머지). 3레포 커밋을 한 논리 슬라이스로 취급.

## 스코프 밖 (의도적)

- **큰 시간 점프 시 틱 snap-forward(드롭)** — 상한은 *멈춤(freeze)* 만 막는다. 큰 점프를 "밀린 틱을 빠르게 다 시뮬"로 처리할지 "틱을 앞으로 snap"할지는 reconciliation/Stage④ 소관이라 이번 슬라이스에서 안 한다. 클라의 `ClockDilator`가 이미 predictedTime 점프를 상류에서 평활/snap하므로 극단 점프는 드물다.
- **`OnElapsedTimeUpdate` 기본값 자체를 abstract로** — LOP 양쪽이 override하나, 프레임워크 오프라인 기본값은 남겨 둔다(비-LOP 소비자용).
- 나머지 findings(A/B/D/E/F/G/H/I) — 각자 슬라이스(umbrella spec 참조).
