# Flappy 맵 플레이 가능성 검사 — 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 맵 씬을 열고 메뉴 하나를 누르면 그 맵을 플레이할 수 있는지(클린런 · 낌 · 스턴 예산)를 재서 콘솔에 리포트 하나를 찍는 에디터 도구를 만든다.

**Architecture:** 순수 계산(탐색 · 산수 · 리포트 문자열)은 새 asmdef `LOP.MapTools`에 두고 자유공간 판정을 델리게이트로 주입받아 유니티 없이 테스트한다. 콜라이더 · 마커 · 마스터데이터를 읽는 껍데기만 기존 `FlappyMapTrapScanner`를 개명해 이어받는다. 탐색은 격자로 빠르게 하되 찾은 경로는 게임의 진짜 커널로 재생해 증명한다.

**Tech Stack:** Unity 6 EditMode · NUnit · `KinematicMover`(LOP-Shared 공유 커널) · `FlappyChaserCurve`(LOP-Shared) · Luban 마스터데이터

**Spec:** `docs/superpowers/specs/2026-09-07-flappy-map-playability-check-design.md`

## Global Constraints

- **커널을 복제하지 않는다.** 검증 재생은 반드시 `LOP.KinematicMover.Move`를 부른다. 물리 값은 반드시 마스터데이터(`tbflappyconfig.bytes`)에서 읽는다. 추격자 위치는 반드시 `LOP.FlappyChaserCurve.XAt`을 부른다. — spec §1
- **`KinematicMoveInput`에는 `stepOffset: 0f, groundProbe: 0f`를 준다.** 새는 턱을 오르지 않고 발밑 땅을 미리 훑지도 않는다. 하나라도 빠지면 이 도구의 "닿았다"가 게임의 스턴 판정과 달라진다. — spec §3.1
- **낌 판정 로직(`IsContactPoint` · `Escapes` · `EscapesWithFlap` · `EscapesWithPeriod` · `EscapesBySearch` · `StateKey` · `TrapClustering` 호출)은 한 줄도 고치지 않는다.** 파일과 이름만 옮긴다. — spec §5
- **대시는 탐색에 넣지 않는다.** — spec §10
- **테스트는 반드시 일부러 깨뜨려 빨강을 확인하고 넘어간다.** — spec §7
- **`.meta` 파일을 함께 커밋한다.** 직접 만들지 않고 유니티가 만든 것만 커밋한다. 파일을 옮길 때는 `.cs`와 짝 `.meta`를 함께 `git mv` 한다.
- **`git add -A` / `git commit -a` 금지.** 바꾼 파일만 경로로 지정하고 커밋 전에 `git status --short`로 확인한다. 워킹트리에 항상 있는 로컬 픽스처(`Assets/Art`, `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`, `ProjectSettings/PackageManagerSettings.asset`, `ProjectSettings/ProjectSettings.asset`)를 절대 스테이징하지 않는다.
- **`run_tests`는 재컴파일하지 않는다.** 소스를 고친 뒤에는 반드시 `unity cmd recompile` → `unity cmd recompile_status`가 `completed`/`failed:false`가 될 때까지 폴링 → 그 다음에 `run_tests`. `unity` CLI는 `$HOME/.unity/bin`에 있으니 `export PATH="$PATH:$HOME/.unity/bin"` 를 먼저 한다. 모든 호출에 `--project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client`를 준다.

## 실측값 (이 계획이 근거로 삼는 것)

`FlappyRaceMap.unity` · `tbflappyconfig.bytes` 실측. 테스트가 이 값을 그대로 쓴다.

```
전진 11   날갯짓 23   중력 70   최대낙하 30   몸 r0.45 h0.9
스턴 0.8   무적 0.6    틱 0.02
추격자  시작x −60   초기 7   가속 0.075   상한 10
스폰 4개  x=−2, y = −6 / −1 / 4 / 9        결승선 x=632
맵 범위  x −10~632   y −66~23
```

> **spec §3.4의 메모리 추정 정정.** spec은 통로 높이 20m를 가정해 열당 12,000상태 · 전체 4MB로
> 적었다. 실측 y 범위(89m)로는 열당 890×2×39 ≈ 69,000상태 = 8.7KB, 전체 **약 25MB**다.
> 여전히 문제없다. 이 계획은 25MB 기준으로 짠다.

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/MapTools/LOP.MapTools.asmdef` | 순수 계산 어셈블리. Shared 참조, autoReferenced로 에디터가 씀 |
| `Assets/Scripts/MapTools/StunBudget.cs` | 추격자 곡선 + 스턴 손해 + 무적 상한 → 구간별 허용 곡선, 최속 탈락 |
| `Assets/Scripts/MapTools/CleanRunSearch.cs` | 상태공간 정방향 도달성 + 역방향 경로 추출. 자유공간은 주입 |
| `Assets/Scripts/MapTools/PlayabilityReport.cs` | 세 결과 → 콘솔 리포트 문자열 |
| `Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs` | 메뉴 · 콜라이더 · 마커 · 마스터데이터 · 게으른 격자 · 검증 재생 (기존 `FlappyMapTrapScanner` 개명) |
| `Assets/Tests/EditMode/MapTools/LOP.MapTools.Tests.EditMode.asmdef` | 테스트 어셈블리 |
| `Assets/Tests/EditMode/MapTools/StunBudgetTests.cs` | Task 1 테스트 |
| `Assets/Tests/EditMode/MapTools/CleanRunSearchTests.cs` | Task 2·3 테스트 |
| `Assets/Tests/EditMode/MapTools/PlayabilityReportTests.cs` | Task 4 테스트 |

---

## Task 1: StunBudget — 어셈블리 + 예산 산수

**Files:**
- Create: `Assets/Scripts/MapTools/LOP.MapTools.asmdef`
- Create: `Assets/Scripts/MapTools/StunBudget.cs`
- Create: `Assets/Tests/EditMode/MapTools/LOP.MapTools.Tests.EditMode.asmdef`
- Test: `Assets/Tests/EditMode/MapTools/StunBudgetTests.cs`

**Interfaces:**
- Consumes: `LOP.FlappyConfig`, `LOP.FlappyChaserCurve.XAt(in FlappyConfig, float elapsedSeconds, float stopAtX)` — 둘 다 `baegames.LOP.Shared.Runtime`
- Produces:
  - `LOP.MapTools.StunBudgetPoint` — `readonly struct { float ElapsedSeconds; float CleanRunX; int AllowedStuns; int PossibleStuns; }`
  - `LOP.MapTools.EarliestCatch` — `readonly struct { bool Caught; float Seconds; int StunCount; }`
  - `LOP.MapTools.StunBudget.AllowedStallSeconds(in FlappyConfig, float elapsedSeconds, float startX, float finishX) → float`
  - `LOP.MapTools.StunBudget.AllowedStuns(in FlappyConfig, float elapsedSeconds, float startX, float finishX) → int`
  - `LOP.MapTools.StunBudget.PossibleStuns(in FlappyConfig, float elapsedSeconds) → int`
  - `LOP.MapTools.StunBudget.FindEarliestCatch(in FlappyConfig, float startX, float finishX) → EarliestCatch`
  - `LOP.MapTools.StunBudget.Curve(in FlappyConfig, float startX, float finishX, float stepSeconds) → List<StunBudgetPoint>`

---

- [ ] **Step 1: 두 asmdef를 만든다**

`Assets/Scripts/MapTools/LOP.MapTools.asmdef`:

```json
{
    "name": "LOP.MapTools",
    "rootNamespace": "LOP.MapTools",
    "references": [
        "baegames.LOP.Shared.Runtime"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

`Assets/Tests/EditMode/MapTools/LOP.MapTools.Tests.EditMode.asmdef`:

```json
{
    "name": "LOP.MapTools.Tests.EditMode",
    "rootNamespace": "",
    "references": [
        "LOP.MapTools",
        "baegames.LOP.Shared.Runtime",
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

`autoReferenced: true`인 이유: 에디터 껍데기(`Assets/Scripts/Editor/`)에는 asmdef가 없어 미리 정의된 `Assembly-CSharp-Editor`에 들어간다. 미리 정의된 어셈블리는 auto-referenced asmdef만 볼 수 있다.

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`Assets/Tests/EditMode/MapTools/StunBudgetTests.cs`:

```csharp
using System.Collections.Generic;
using LOP;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class StunBudgetTests
    {
        const float Tolerance = 0.05f;

        //  FlappyRaceMap 실측값. 도구가 spec §4.3의 손계산과 같은 답을 내는지가 이 스위트의 목적이다.
        const float StartX = -2f;
        const float FinishX = 632f;

        static FlappyConfig Config(float invulnTime = 0.6f)
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                                stunTime: 0.8f, invulnTime: invulnTime,
                                dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0.13f, dashChargeDive: 1.2f,
                                chaserStartX: -60f, chaserInitialSpeed: 7f,
                                chaserAcceleration: 0.075f, chaserMaxSpeed: 10f);

        [Test]
        public void 허용_정지시간은_추격자와의_간격을_전진속도로_나눈_것이다()
        {
            //  20초 시점: 추격자 = −60 + 7×20 + 0.0375×400 = 95.
            //  새 뒷면이 그 자리에 오려면 −2 + 11×(20−S) − 0.45 = 95  →  S = 20 − 97.45/11 = 11.14초.
            float allowed = StunBudget.AllowedStallSeconds(Config(), 20f, StartX, FinishX);

            Assert.AreEqual(11.14f, allowed, Tolerance);
        }

        [Test]
        public void 허용_스턴_횟수는_허용_정지시간을_스턴시간으로_나눈_몫이다()
        {
            //  11.14 ÷ 0.8 = 13.9 → 13번.
            Assert.AreEqual(13, StunBudget.AllowedStuns(Config(), 20f, StartX, FinishX));
        }

        [Test]
        public void 무적_때문에_그_시점까지_맞을_수_있는_횟수에_상한이_있다()
        {
            //  n번 맞으려면 (0.8+0.6)n − 0.6 초가 든다. 10초 안에 들어가는 최대 n은 7이다(1.4×7−0.6=9.2).
            Assert.AreEqual(7, StunBudget.PossibleStuns(Config(), 10f));
        }

        [Test]
        public void 무적이_없으면_상한이_달라진다()
        {
            //  이 테스트가 없으면 무적 항이 죽어 있어도 아무도 모른다.
            //  무적 0이면 0.8n ≤ 10 → n = 12.
            Assert.AreEqual(12, StunBudget.PossibleStuns(Config(invulnTime: 0f), 10f));
        }

        [Test]
        public void 가장_빨리_잡히는_경우는_19초_14번째다()
        {
            //  spec §4.3의 손계산. 도구가 이 값을 재현하는지가 첫 검증이다.
            EarliestCatch catchInfo = StunBudget.FindEarliestCatch(Config(), StartX, FinishX);

            Assert.IsTrue(catchInfo.Caught);
            Assert.AreEqual(14, catchInfo.StunCount);
            Assert.AreEqual(19.0f, catchInfo.Seconds, Tolerance);
        }

        [Test]
        public void 추격자가_안_움직이면_영영_안_잡힌다()
        {
            var still = new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                         bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                                         stunTime: 0.8f, invulnTime: 0.6f,
                                         dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0.13f, dashChargeDive: 1.2f,
                                         chaserStartX: -60f, chaserInitialSpeed: 0f,
                                         chaserAcceleration: 0f, chaserMaxSpeed: 0f);

            Assert.IsFalse(StunBudget.FindEarliestCatch(still, StartX, FinishX).Caught);
        }

        [Test]
        public void 곡선은_경과시간마다_허용과_가능을_함께_낸다()
        {
            List<StunBudgetPoint> curve = StunBudget.Curve(Config(), StartX, FinishX, stepSeconds: 10f);

            Assert.AreEqual(10f, curve[0].ElapsedSeconds, Tolerance);
            Assert.AreEqual(108f, curve[0].CleanRunX, 1f);   // −2 + 11×10
            Assert.AreEqual(10, curve[0].AllowedStuns);
            Assert.AreEqual(7, curve[0].PossibleStuns);
            //  마지막 점은 골인 시각(634 ÷ 11 = 57.6초)을 넘지 않는다.
            Assert.LessOrEqual(curve[curve.Count - 1].ElapsedSeconds, 57.7f);
        }
    }
}
```

- [ ] **Step 3: 컴파일하고 실패를 확인한다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
# status가 completed 가 될 때까지 폴링
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

Expected: `failed:true` — `StunBudget` 타입이 없다는 컴파일 에러.

- [ ] **Step 4: StunBudget을 구현한다**

`Assets/Scripts/MapTools/StunBudget.cs`:

```csharp
using System.Collections.Generic;

namespace LOP.MapTools
{
    /// <summary>구간별 예산 한 점 — 그 시점의 클린런 위치와, 허용/가능 스턴 횟수.</summary>
    public readonly struct StunBudgetPoint
    {
        public readonly float ElapsedSeconds;
        public readonly float CleanRunX;
        public readonly int AllowedStuns;
        public readonly int PossibleStuns;

        public StunBudgetPoint(float elapsedSeconds, float cleanRunX, int allowedStuns, int possibleStuns)
        {
            ElapsedSeconds = elapsedSeconds;
            CleanRunX = cleanRunX;
            AllowedStuns = allowedStuns;
            PossibleStuns = possibleStuns;
        }
    }

    /// <summary>가장 빨리 잡히는 경우. <see cref="Caught"/>가 false면 골인 전에 잡힐 수 없다.</summary>
    public readonly struct EarliestCatch
    {
        public readonly bool Caught;
        public readonly float Seconds;
        public readonly int StunCount;

        public EarliestCatch(bool caught, float seconds, int stunCount)
        {
            Caught = caught;
            Seconds = seconds;
            StunCount = stunCount;
        }
    }

    /// <summary>
    /// 추격자에게 잡히기 전까지 몇 번이나 스턴을 먹어도 되는가.
    ///
    /// <para>가정: 스턴이 아닌 시간엔 전진 속도로 온전히 나아간다. 실제로는 무적 중에도 벽에
    /// 막히면 못 나가므로 <b>실제는 이보다 나쁘다</b> — 즉 여기 나오는 횟수는 상한이다.
    /// "이 횟수를 넘으면 반드시 잡힌다"이지 "넘지 않으면 안 잡힌다"가 아니다.
    /// 막혀서 기는 경우는 낌 검사가 따로 잡는다.</para>
    /// </summary>
    public static class StunBudget
    {
        /// <summary>이 시점까지 멈춰 있어도 되는 총 시간. 음수면 이미 잡혔다는 뜻이다.</summary>
        public static float AllowedStallSeconds(in FlappyConfig config, float elapsedSeconds,
                                                float startX, float finishX)
        {
            float wallX = FlappyChaserCurve.XAt(config, elapsedSeconds, finishX);
            //  잡힘 판정이 "새 뒷면 ≤ 벽"이라 반지름만큼 더 가 있어야 한다.
            float needed = (wallX - startX + config.BodyRadius) / config.ForwardSpeed;
            return elapsedSeconds - needed;
        }

        public static int AllowedStuns(in FlappyConfig config, float elapsedSeconds, float startX, float finishX)
        {
            float allowed = AllowedStallSeconds(config, elapsedSeconds, startX, finishX);
            if (allowed <= 0f)
            {
                return 0;
            }
            return (int)(allowed / config.StunTime);
        }

        /// <summary>
        /// 이 시점까지 물리적으로 맞을 수 있는 최대 횟수. 스턴이 끝나면 무적이 붙어 그 사이엔
        /// 다시 안 걸리므로, n번 맞으려면 최소 (스턴+무적)×n − 무적 초가 든다.
        /// </summary>
        public static int PossibleStuns(in FlappyConfig config, float elapsedSeconds)
        {
            float cycle = config.StunTime + config.InvulnTime;
            if (cycle <= 0f)
            {
                return 0;
            }
            int count = (int)((elapsedSeconds + config.InvulnTime) / cycle);
            return count < 0 ? 0 : count;
        }

        public static EarliestCatch FindEarliestCatch(in FlappyConfig config, float startX, float finishX)
        {
            float cycle = config.StunTime + config.InvulnTime;
            for (int n = 1; n <= 10000; n++)
            {
                //  n번째 스턴이 끝나는 가장 이른 시각. 그 순간 총 정지시간은 스턴×n이다.
                float t = cycle * n - config.InvulnTime;
                float stalled = config.StunTime * n;

                //  그 시각에 이미 골인했으면 더 볼 것이 없다 — 완주자는 판정에서 빠진다.
                float x = startX + config.ForwardSpeed * (t - stalled);
                if (x >= finishX)
                {
                    return new EarliestCatch(false, 0f, 0);
                }
                if (stalled >= AllowedStallSeconds(config, t, startX, finishX))
                {
                    return new EarliestCatch(true, t, n);
                }
            }
            return new EarliestCatch(false, 0f, 0);
        }

        /// <summary>출발부터 클린런 골인 시각까지 <paramref name="stepSeconds"/> 간격으로 훑는다.</summary>
        public static List<StunBudgetPoint> Curve(in FlappyConfig config, float startX, float finishX,
                                                  float stepSeconds)
        {
            var points = new List<StunBudgetPoint>();
            float cleanRunSeconds = (finishX - startX) / config.ForwardSpeed;
            for (float t = stepSeconds; t < cleanRunSeconds; t += stepSeconds)
            {
                points.Add(new StunBudgetPoint(t, startX + config.ForwardSpeed * t,
                                               AllowedStuns(config, t, startX, finishX),
                                               PossibleStuns(config, t)));
            }
            //  골인 시각은 간격에 안 걸려도 반드시 넣는다 — 마지막 여유가 얼마인지가 필요하다.
            points.Add(new StunBudgetPoint(cleanRunSeconds, finishX,
                                           AllowedStuns(config, cleanRunSeconds, startX, finishX),
                                           PossibleStuns(config, cleanRunSeconds)));
            return points;
        }
    }
}
```

- [ ] **Step 5: 컴파일하고 테스트를 돌려 통과를 확인한다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client   # completed / failed:false
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --async_tests true --filter "StunBudgetTests"
unity cmd test_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

Expected: 7 passed, 0 failed. `run_tests` 응답의 `Total:0`은 "시작했다"는 뜻이지 결과가 아니다 — 반드시 `test_status`가 `completed`가 될 때까지 폴링해서 읽는다.

- [ ] **Step 6: 테스트가 실제로 실패할 수 있는지 확인한다**

`StunBudget.PossibleStuns`의 `+ config.InvulnTime`을 지운다 → recompile → 같은 필터로 재실행.

Expected: `무적_때문에...`(7 → 6)과 `가장_빨리_잡히는_경우는_19초_14번째다`가 **실패해야 한다.** 실패하지 않으면 그 테스트는 아무것도 안 지키는 것이니 테스트를 고친다. 확인했으면 지운 항을 되돌리고 다시 초록을 확인한다.

- [ ] **Step 7: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/MapTools Assets/Tests/EditMode/MapTools
git status --short   # 스테이징된 것이 MapTools 파일과 그 .meta 뿐인지 확인
git commit -m "$(cat <<'MSG'
feat(maptools): 추격자에게 잡히기 전 스턴 예산을 센다

예산은 고정값이 아니다 — 간격이 출발 직후 가장 좁고 계속 벌어지므로, 같은 난이도
구간도 앞에 있으면 치명적이고 뒤에 있으면 여유다. 그래서 한 숫자가 아니라 구간별
곡선으로 낸다.

무적 상한을 같이 센다. 스턴이 끝나면 무적이 붙어 연속으로 맞아도 한 번당 1.4초가
최소 주기다 — 이걸 빼면 예산을 과소평가한다.

spec §4.3의 손계산(19.0초 · 14번째)을 테스트가 그대로 못박는다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Y7nzicQU3Pxb8ng5XntfD6
MSG
)"
```

---

## Task 2: CleanRunSearch — 정방향 도달성

**Files:**
- Create: `Assets/Scripts/MapTools/CleanRunSearch.cs`
- Test: `Assets/Tests/EditMode/MapTools/CleanRunSearchTests.cs`

**Interfaces:**
- Consumes: 없음 (순수). 자유공간은 델리게이트로 주입받는다.
- Produces:
  - `LOP.MapTools.FreeSpaceProbe` — `delegate bool FreeSpaceProbe(float x, float y)` — "발밑이 (x,y)일 때 몸이 아무데도 안 닿고 들어가나"
  - `LOP.MapTools.CleanRunOptions` — `readonly struct`, 생성자 `CleanRunOptions(float startX, float startY, float finishX, float minY, float maxY, float forwardSpeed, float flapImpulse, float gravity, float maxFallSpeed, float tickSeconds, float heightGrid)`
  - `LOP.MapTools.CleanRunResult` — `readonly struct { bool Reachable; IReadOnlyList<bool> Flaps; float BlockedX; float NarrowestX; int NarrowestCount; float NarrowestHeightSpan; }`
  - `LOP.MapTools.CleanRunSearch.Run(in CleanRunOptions, FreeSpaceProbe) → CleanRunResult` (이 태스크에서는 `Flaps`가 항상 빈 리스트, Task 3에서 채운다)

**설계 메모 — 상태 encoding (spec §3.3):**

세로 속도는 연속값이 아니라 사다리다. 사다리가 둘 있다.

```
ladder 0 (날갯짓 뒤)  : [0]=FlapImpulse, [i+1]=clamp(ladder[i] − Gravity×dt, −MaxFallSpeed)
ladder 1 (아직 안 함) : [0]=0(출발 세로속도),  같은 규칙
```

각 사다리는 `−MaxFallSpeed`에 닿으면 그 뒤로 안 변하므로 마지막 칸이 흡수 상태다. 상태는
`(높이버킷, 사다리, 칸)` 셋이고 이걸 하나의 정수로 눌러 비트 한 장에 담는다.

```
stateIndex = (heightBucket × 2 + ladder) × RungCount + rung
```

---

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/EditMode/MapTools/CleanRunSearchTests.cs`:

```csharp
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class CleanRunSearchTests
    {
        //  실측 물리값. 사다리 간격이 70×0.02 = 1.4가 되도록 맞춰 둔다.
        static CleanRunOptions Options(float startY, float finishX, float minY = -40f, float maxY = 40f)
            => new CleanRunOptions(startX: 0f, startY: startY, finishX: finishX,
                                   minY: minY, maxY: maxY,
                                   forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                   tickSeconds: 0.02f, heightGrid: 0.1f);

        static bool OpenSky(float x, float y) => true;

        [Test]
        public void 빈_하늘이면_결승선까지_간다()
        {
            CleanRunResult result = CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), OpenSky);

            Assert.IsTrue(result.Reachable);
        }

        [Test]
        public void 바닥과_천장_사이가_몸보다_좁으면_못_간다()
        {
            //  x ≥ 20 부터 높이 0.4m 띠만 비어 있다. 몸(높이 0.9)이 안 들어가는 폭이라
            //  자유공간 함수가 그 구간에서 전부 false를 준다.
            bool IsFree(float x, float y) => x < 20f;

            CleanRunResult result = CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), IsFree);

            Assert.IsFalse(result.Reachable);
            //  전진 11 × 0.02 = 0.22씩 가므로 20 근처에서 끊긴다.
            Assert.AreEqual(20f, result.BlockedX, 1f);
        }

        [Test]
        public void 날갯짓해야만_넘는_턱을_찾아낸다()
        {
            //  x ∈ [20, 22] 구간은 y ≥ 3 만 비어 있다. 가만히 있으면 떨어져서 못 넘고,
            //  미리 날갯짓해 떠 있어야 넘는다.
            bool IsFree(float x, float y) => (x < 20f || x > 22f) || y >= 3f;

            CleanRunResult result = CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), IsFree);

            Assert.IsTrue(result.Reachable);
        }

        [Test]
        public void 출발_높이가_다르면_답이_다를_수_있다()
        {
            //  y ≥ 5 는 통째로 막힌 하늘. 아래에서 출발하면 가고, 위에서 출발하면 시작부터 막힌다.
            bool IsFree(float x, float y) => y < 5f;

            Assert.IsTrue(CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), IsFree).Reachable);
            Assert.IsFalse(CleanRunSearch.Run(Options(startY: 9f, finishX: 50f), IsFree).Reachable);
        }

        [Test]
        public void 한_틱_이동보다_얇은_벽도_통과하지_못한다()
        {
            //  두께 0.15m 벽. 한 틱에 0.22m를 가므로 끝점만 검사하면 그대로 뚫고 지나간다.
            //  선분을 눈금 간격으로 찍어 봐야만 걸린다.
            bool IsFree(float x, float y) => x < 20f || x > 20.15f;

            CleanRunResult result = CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), IsFree);

            Assert.IsFalse(result.Reachable);
        }

        [Test]
        public void 막히면_그_직전_최협_회랑을_보고한다()
        {
            //  x ∈ [20, 30] 은 y ∈ [0, 0.5] 만 비고, x > 30 은 완전히 막힌다.
            bool IsFree(float x, float y)
            {
                if (x > 30f) { return false; }
                if (x < 20f) { return true; }
                return y >= 0f && y <= 0.5f;
            }

            CleanRunResult result = CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), IsFree);

            Assert.IsFalse(result.Reachable);
            //  좁은 목이 있었다는 사실이 남아야 고칠 자리를 찾는다.
            Assert.Greater(result.NarrowestCount, 0);
            Assert.LessOrEqual(result.NarrowestHeightSpan, 1f);
        }
    }
}
```

- [ ] **Step 2: 컴파일해 실패를 확인한다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

Expected: `failed:true` — `CleanRunOptions` / `CleanRunSearch` 타입이 없다.

- [ ] **Step 3: CleanRunSearch를 구현한다**

`Assets/Scripts/MapTools/CleanRunSearch.cs`:

```csharp
using System.Collections.Generic;

namespace LOP.MapTools
{
    /// <summary>발밑이 (x, y)일 때 몸이 아무데도 안 닿고 들어가는가.</summary>
    public delegate bool FreeSpaceProbe(float x, float y);

    public readonly struct CleanRunOptions
    {
        public readonly float StartX, StartY, FinishX;
        public readonly float MinY, MaxY;
        public readonly float ForwardSpeed, FlapImpulse, Gravity, MaxFallSpeed;
        public readonly float TickSeconds;
        public readonly float HeightGrid;

        public CleanRunOptions(float startX, float startY, float finishX, float minY, float maxY,
                               float forwardSpeed, float flapImpulse, float gravity, float maxFallSpeed,
                               float tickSeconds, float heightGrid)
        {
            StartX = startX; StartY = startY; FinishX = finishX;
            MinY = minY; MaxY = maxY;
            ForwardSpeed = forwardSpeed; FlapImpulse = flapImpulse;
            Gravity = gravity; MaxFallSpeed = maxFallSpeed;
            TickSeconds = tickSeconds; HeightGrid = heightGrid;
        }
    }

    public readonly struct CleanRunResult
    {
        public readonly bool Reachable;
        /// <summary>열마다 "이 틱에 날갯짓했나". 도달 가능할 때만 채워진다.</summary>
        public readonly IReadOnlyList<bool> Flaps;
        public readonly float BlockedX;
        public readonly float NarrowestX;
        public readonly int NarrowestCount;
        public readonly float NarrowestHeightSpan;

        public CleanRunResult(bool reachable, IReadOnlyList<bool> flaps, float blockedX,
                              float narrowestX, int narrowestCount, float narrowestHeightSpan)
        {
            Reachable = reachable;
            Flaps = flaps;
            BlockedX = blockedX;
            NarrowestX = narrowestX;
            NarrowestCount = narrowestCount;
            NarrowestHeightSpan = narrowestHeightSpan;
        }
    }

    /// <summary>
    /// 한 번도 안 부딪히고 결승선까지 갈 경로가 있는가를 상태공간 탐색으로 답한다.
    ///
    /// <para>세 가지 덕에 문제가 작다. ① 안 닿는 동안 커널이 하는 일은 <c>위치 += 속도 × dt</c>뿐이라
    /// 물리가 정확히 포물선이다. ② 전진이 상수라 x는 선택의 대상이 아니고 "열 = 틱"이다.
    /// ③ 세로 속도가 연속값이 아니라 사다리라(날갯짓 뒤 몇 틱 지났나로 완전히 결정) 근사가 필요 없다.</para>
    ///
    /// <para>높이만 눈금으로 뭉개므로, 찾은 경로는 부르는 쪽이 진짜 커널로 재생해 증명해야 한다.</para>
    /// </summary>
    public static class CleanRunSearch
    {
        public static CleanRunResult Run(in CleanRunOptions options, FreeSpaceProbe isFree)
        {
            var grid = new SearchGrid(options);
            var columns = new List<System.Collections.BitArray>(grid.ColumnCount + 1);

            var current = new System.Collections.BitArray(grid.StateCount);
            //  출발: 아직 날갯짓 안 한 사다리의 첫 칸.
            if (isFree(options.StartX, options.StartY) == false)
            {
                return new CleanRunResult(false, System.Array.Empty<bool>(), options.StartX, 0f, 0, 0f);
            }
            current.Set(grid.StateIndex(grid.HeightBucket(options.StartY), ladder: 1, rung: 0), true);
            columns.Add(current);

            float narrowestX = 0f, narrowestSpan = 0f;
            int narrowestCount = int.MaxValue;

            for (int column = 0; column < grid.ColumnCount; column++)
            {
                float x = options.StartX + grid.StepX * column;
                float nextX = x + grid.StepX;
                var next = new System.Collections.BitArray(grid.StateCount);
                bool any = false;

                for (int state = 0; state < grid.StateCount; state++)
                {
                    if (current.Get(state) == false)
                    {
                        continue;
                    }
                    grid.Decode(state, out int heightBucket, out int ladder, out int rung);
                    float y = grid.HeightOf(heightBucket);

                    //  날갯짓 안 함 — 같은 사다리의 다음 칸.
                    if (TryAdvance(grid, isFree, x, y, ladder, rung + 1, options, next))
                    {
                        any = true;
                    }
                    //  날갯짓 — 사다리 0의 첫 칸으로 갈아탄다.
                    if (TryAdvance(grid, isFree, x, y, ladder: 0, rung: 0, options, next))
                    {
                        any = true;
                    }
                }

                if (any == false)
                {
                    return new CleanRunResult(false, System.Array.Empty<bool>(), nextX,
                                              narrowestX, narrowestCount == int.MaxValue ? 0 : narrowestCount,
                                              narrowestSpan);
                }

                //  최협 회랑 — 출발 직후 과도기(앞 60열)는 시드가 하나뿐이라 제외한다.
                if (column > 60)
                {
                    grid.Measure(next, out int count, out float span);
                    if (count < narrowestCount)
                    {
                        narrowestCount = count;
                        narrowestSpan = span;
                        narrowestX = nextX;
                    }
                }

                columns.Add(next);
                current = next;
            }

            return new CleanRunResult(true, System.Array.Empty<bool>(), 0f,
                                      narrowestX, narrowestCount == int.MaxValue ? 0 : narrowestCount,
                                      narrowestSpan);
        }

        //  한 스텝 나아가 본다. 몸이 스치면 그 갈래를 버린다.
        static bool TryAdvance(SearchGrid grid, FreeSpaceProbe isFree, float x, float y,
                               int ladder, int rung, in CleanRunOptions options,
                               System.Collections.BitArray next)
        {
            int clamped = grid.ClampRung(rung);
            float vy = grid.Speed(ladder, clamped);
            float ny = y + vy * options.TickSeconds;
            if (ny < options.MinY || ny > options.MaxY)
            {
                return false;
            }
            if (SegmentIsFree(isFree, x, y, x + grid.StepX, ny, options.HeightGrid) == false)
            {
                return false;
            }
            next.Set(grid.StateIndex(grid.HeightBucket(ny), ladder, clamped), true);
            return true;
        }

        //  한 틱 사이 몸이 지나는 선분을 눈금 간격으로 찍어 본다. 끝점만 보면 얇은 벽을 통과한다.
        static bool SegmentIsFree(FreeSpaceProbe isFree, float x0, float y0, float x1, float y1, float grid)
        {
            float dx = x1 - x0, dy = y1 - y0;
            float length = UnityEngine.Mathf.Sqrt(dx * dx + dy * dy);
            int samples = UnityEngine.Mathf.CeilToInt(length / grid) + 1;
            for (int i = 0; i <= samples; i++)
            {
                float t = i / (float)samples;
                if (isFree(x0 + dx * t, y0 + dy * t) == false)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
```

그리고 같은 파일 안에 격자 헬퍼를 둔다 — 상태를 정수로 눌렀다 푸는 규칙이 한 곳에만 있어야 한다:

```csharp
namespace LOP.MapTools
{
    /// <summary>상태 (높이버킷, 사다리, 칸)을 정수 하나로 누르는 규칙과 세로 속도 사다리.</summary>
    internal sealed class SearchGrid
    {
        readonly CleanRunOptions options;
        readonly float[] afterFlap;
        readonly float[] beforeFlap;

        public readonly int HeightBucketCount;
        public readonly int RungCount;
        public readonly int StateCount;
        public readonly int ColumnCount;
        public readonly float StepX;

        public SearchGrid(in CleanRunOptions options)
        {
            this.options = options;
            StepX = options.ForwardSpeed * options.TickSeconds;
            ColumnCount = UnityEngine.Mathf.CeilToInt((options.FinishX - options.StartX) / StepX);
            HeightBucketCount = UnityEngine.Mathf.CeilToInt((options.MaxY - options.MinY) / options.HeightGrid) + 1;

            //  사다리는 −MaxFallSpeed에 닿으면 더 안 변한다. 거기까지만 만들고 그 뒤는 흡수 상태다.
            float drop = options.Gravity * options.TickSeconds;
            RungCount = UnityEngine.Mathf.CeilToInt((options.FlapImpulse + options.MaxFallSpeed) / drop) + 2;
            afterFlap = BuildLadder(options.FlapImpulse, drop, options.MaxFallSpeed, RungCount);
            beforeFlap = BuildLadder(0f, drop, options.MaxFallSpeed, RungCount);

            StateCount = HeightBucketCount * 2 * RungCount;
        }

        static float[] BuildLadder(float first, float drop, float maxFall, int count)
        {
            var ladder = new float[count];
            ladder[0] = first;
            for (int i = 1; i < count; i++)
            {
                float v = ladder[i - 1] - drop;
                ladder[i] = v < -maxFall ? -maxFall : v;
            }
            return ladder;
        }

        public int ClampRung(int rung) => rung >= RungCount ? RungCount - 1 : rung;

        public float Speed(int ladder, int rung) => ladder == 0 ? afterFlap[rung] : beforeFlap[rung];

        public int HeightBucket(float y)
        {
            int bucket = UnityEngine.Mathf.RoundToInt((y - options.MinY) / options.HeightGrid);
            if (bucket < 0) { return 0; }
            if (bucket >= HeightBucketCount) { return HeightBucketCount - 1; }
            return bucket;
        }

        public float HeightOf(int bucket) => options.MinY + bucket * options.HeightGrid;

        public int StateIndex(int heightBucket, int ladder, int rung)
            => (heightBucket * 2 + ladder) * RungCount + rung;

        public void Decode(int state, out int heightBucket, out int ladder, out int rung)
        {
            rung = state % RungCount;
            int rest = state / RungCount;
            ladder = rest % 2;
            heightBucket = rest / 2;
        }

        /// <summary>이 열에 살아남은 상태 수와, 그것들이 걸친 높이 폭.</summary>
        public void Measure(System.Collections.BitArray column, out int count, out float span)
        {
            count = 0;
            int lo = int.MaxValue, hi = int.MinValue;
            for (int state = 0; state < StateCount; state++)
            {
                if (column.Get(state) == false) { continue; }
                count++;
                Decode(state, out int bucket, out _, out _);
                if (bucket < lo) { lo = bucket; }
                if (bucket > hi) { hi = bucket; }
            }
            span = count == 0 ? 0f : (hi - lo) * options.HeightGrid;
        }
    }
}
```

- [ ] **Step 4: 컴파일하고 테스트를 돌린다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --async_tests true --filter "CleanRunSearchTests"
unity cmd test_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

Expected: 6 passed, 0 failed.

- [ ] **Step 5: 테스트가 실제로 실패할 수 있는지 확인한다**

`SegmentIsFree`를 `return isFree(x1, y1);`(끝점만 검사)로 바꾼다 → recompile → 재실행.

Expected: `한_틱_이동보다_얇은_벽도_통과하지_못한다`가 **실패해야 한다** — 한 틱에 0.22m를 가는데 벽이 0.15m라 끝점만 보면 그대로 뚫는다. 안 깨지면 선분 표본이 아무것도 안 지키는 것이니 그 테스트를 고친다. 확인했으면 되돌리고 초록을 다시 확인한다.

- [ ] **Step 6: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/MapTools/CleanRunSearch.cs Assets/Scripts/MapTools/CleanRunSearch.cs.meta Assets/Tests/EditMode/MapTools/CleanRunSearchTests.cs Assets/Tests/EditMode/MapTools/CleanRunSearchTests.cs.meta
git status --short
git commit -m "$(cat <<'MSG'
feat(maptools): 무충돌 경로가 있는지 상태공간으로 답한다

세 가지 덕에 문제가 작다. 안 닿는 동안 커널이 하는 일은 위치 += 속도 × dt뿐이라
물리가 정확히 포물선이고, 전진이 상수라 x가 선택의 대상이 아니라 "열 = 틱"이며,
세로 속도가 연속값이 아니라 사다리다 — 날갯짓 뒤 몇 틱 지났나로 완전히 결정된다.

그래서 프로토타입이 vy를 눈금으로 뭉개야 했던 자리에서 근사가 사라졌다. 남은 근사는
높이 눈금 하나뿐이고, 그건 나중에 진짜 커널로 재생해 증명한다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Y7nzicQU3Pxb8ng5XntfD6
MSG
)"
```

---

## Task 3: CleanRunSearch — 역방향 경로 추출

**Files:**
- Modify: `Assets/Scripts/MapTools/CleanRunSearch.cs`
- Modify: `Assets/Tests/EditMode/MapTools/CleanRunSearchTests.cs`

**Interfaces:**
- Consumes: Task 2의 `CleanRunSearch.Run` 내부 열별 비트 배열
- Produces: `CleanRunResult.Flaps`가 도달 가능할 때 `ColumnCount` 길이로 채워진다. `Flaps[i] == true`면 i번째 틱에 날갯짓한다는 뜻이다.

**설계 메모 (spec §3.6):** 사다리 덕에 되짚기가 결정적이라 부모 포인터가 필요 없다.

```
지금 (높이버킷 hb', 사다리 L', 칸 r') 일 때 직전은

   L'=0, r'=0  (날갯짓했다)  →  y = y' − afterFlap[0] × dt,  직전 사다리·칸은 아무거나
   그 외        (안 했다)     →  y = y' − Speed(L', r') × dt,  사다리 = L', 칸 = r'−1
```

각 단계에서 직전 열의 후보(사다리 2 × 칸 RungCount)를 앞으로 한 번 굴려 목표 버킷에 떨어지는지
확인한다. 열당 후보가 80개 남짓이라 전체가 20만 번 정도로 끝난다.

---

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`CleanRunSearchTests.cs`에 추가:

```csharp
        [Test]
        public void 도달_가능하면_날갯짓_순서를_돌려준다()
        {
            CleanRunResult result = CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), OpenSky);

            Assert.IsTrue(result.Reachable);
            //  50m를 0.22씩 가므로 228열(올림). 열마다 눌렀나/안 눌렀나가 하나씩 있어야 한다.
            Assert.AreEqual(228, result.Flaps.Count);
        }

        [Test]
        public void 돌려준_날갯짓_순서를_다시_굴리면_같은_길을_간다()
        {
            //  x ∈ [20, 22] 은 y ≥ 3 만 빈다 — 날갯짓 없이는 못 넘는 턱.
            bool IsFree(float x, float y) => (x < 20f || x > 22f) || y >= 3f;
            var options = Options(startY: 0f, finishX: 50f);

            CleanRunResult result = CleanRunSearch.Run(options, IsFree);
            Assert.IsTrue(result.Reachable);

            //  탐색이 준 순서 그대로 포물선을 굴린다. 한 번이라도 막힌 자리를 지나면 안 된다.
            float y = options.StartY, vy = 0f;
            for (int i = 0; i < result.Flaps.Count; i++)
            {
                float x = options.StartX + 0.22f * i;
                vy -= options.Gravity * options.TickSeconds;
                if (vy < -options.MaxFallSpeed) { vy = -options.MaxFallSpeed; }
                if (result.Flaps[i]) { vy = options.FlapImpulse; }
                y += vy * options.TickSeconds;
                Assert.IsTrue(IsFree(x + 0.22f, y),
                              $"{i}번째 틱에서 막힌 자리를 지났다 (x={x + 0.22f:F2} y={y:F2})");
            }
        }

        [Test]
        public void 못_가면_날갯짓_순서는_비어_있다()
        {
            bool IsFree(float x, float y) => x < 20f;

            CleanRunResult result = CleanRunSearch.Run(Options(startY: 0f, finishX: 50f), IsFree);

            Assert.IsFalse(result.Reachable);
            Assert.AreEqual(0, result.Flaps.Count);
        }
```

- [ ] **Step 2: 컴파일해 실패를 확인한다**

Expected: `도달_가능하면_날갯짓_순서를_돌려준다`가 `Expected: 227 But was: 0`으로 실패.

- [ ] **Step 3: 되짚기를 구현한다**

`CleanRunSearch.Run`에서 성공 반환 직전에 아래를 부르고, 그 결과를 `Flaps`에 담는다.
(`columns`는 이미 열마다 쌓고 있다 — Task 2에서 `columns.Add(next)`를 하고 있으니 그대로 쓴다.)

```csharp
        //  뒤에서 앞으로 한 경로를 뽑는다. 사다리 덕에 직전 상태가 계산으로 나와 부모 포인터가 필요 없다.
        //  마지막 열의 아무 생존 상태에서 시작해, 매 단계 직전 열의 후보를 앞으로 굴려 맞는 것을 고른다.
        static bool[] ExtractFlaps(SearchGrid grid, FreeSpaceProbe isFree,
                                   List<System.Collections.BitArray> columns, in CleanRunOptions options)
        {
            int last = columns.Count - 1;
            int target = -1;
            for (int state = 0; state < grid.StateCount; state++)
            {
                if (columns[last].Get(state)) { target = state; break; }
            }
            if (target < 0)
            {
                return System.Array.Empty<bool>();
            }

            var flaps = new bool[last];
            for (int column = last; column > 0; column--)
            {
                float previousX = options.StartX + grid.StepX * (column - 1);
                bool found = false;
                for (int state = 0; state < grid.StateCount && found == false; state++)
                {
                    if (columns[column - 1].Get(state) == false) { continue; }
                    grid.Decode(state, out int heightBucket, out int ladder, out int rung);
                    float y = grid.HeightOf(heightBucket);

                    //  두 갈래를 그대로 굴려 목표 상태에 떨어지는지 본다 — 정방향과 같은 규칙이라
                    //  둘이 어긋날 수 없다.
                    for (int flap = 0; flap < 2 && found == false; flap++)
                    {
                        int nextLadder = flap == 1 ? 0 : ladder;
                        int nextRung = grid.ClampRung(flap == 1 ? 0 : rung + 1);
                        float ny = y + grid.Speed(nextLadder, nextRung) * options.TickSeconds;
                        if (ny < options.MinY || ny > options.MaxY) { continue; }
                        if (grid.StateIndex(grid.HeightBucket(ny), nextLadder, nextRung) != target) { continue; }
                        if (SegmentIsFree(isFree, previousX, y, previousX + grid.StepX, ny,
                                          options.HeightGrid) == false) { continue; }

                        flaps[column - 1] = flap == 1;
                        target = state;
                        found = true;
                    }
                }
                if (found == false)
                {
                    //  일어나면 정방향과 역방향이 다른 규칙을 쓴다는 뜻이다 — 조용히 넘기지 않는다.
                    return System.Array.Empty<bool>();
                }
            }
            return flaps;
        }
```

성공 반환을 이렇게 바꾼다:

```csharp
            bool[] flaps = ExtractFlaps(grid, isFree, columns, options);
            return new CleanRunResult(true, flaps, 0f,
                                      narrowestX, narrowestCount == int.MaxValue ? 0 : narrowestCount,
                                      narrowestSpan);
```

- [ ] **Step 4: 컴파일하고 테스트를 돌린다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --async_tests true --filter "CleanRunSearchTests"
unity cmd test_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

Expected: 9 passed, 0 failed.

- [ ] **Step 5: 테스트가 실제로 실패할 수 있는지 확인한다**

`ExtractFlaps`에서 `flaps[column - 1] = flap == 1;`을 `flaps[column - 1] = false;`로 바꾼다 → recompile → 재실행.

Expected: `돌려준_날갯짓_순서를_다시_굴리면_같은_길을_간다`가 **실패해야 한다**(턱을 못 넘어 막힌 자리를 지난다). 되돌리고 초록을 확인한다.

- [ ] **Step 6: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/MapTools/CleanRunSearch.cs Assets/Tests/EditMode/MapTools/CleanRunSearchTests.cs
git status --short
git commit -m "$(cat <<'MSG'
feat(maptools): 찾은 무충돌 경로의 날갯짓 순서를 뽑는다

부모 포인터를 안 든다. 세로 속도가 사다리라 직전 상태가 계산으로 나오기 때문이다 —
날갯짓했으면 사다리 첫 칸, 아니면 같은 사다리의 한 칸 앞이다. 부모를 들면 열 2,880개에
상태 수만큼 곱해져 메모리가 수백 MB로 뛴다.

뽑은 순서는 다음 슬라이스에서 진짜 커널로 재생해 증명한다. 높이를 눈금으로 뭉갰으니
탐색이 "된다"고 한 것만으로는 아직 증거가 아니다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Y7nzicQU3Pxb8ng5XntfD6
MSG
)"
```

---

## Task 4: PlayabilityReport — 리포트 문자열

**Files:**
- Create: `Assets/Scripts/MapTools/PlayabilityReport.cs`
- Test: `Assets/Tests/EditMode/MapTools/PlayabilityReportTests.cs`

**Interfaces:**
- Consumes: `StunBudgetPoint`, `EarliestCatch` (Task 1), `CleanRunResult` (Task 2·3)
- Produces:
  - `LOP.MapTools.SpawnCleanRun` — `readonly struct { string Name; float Y; CleanRunResult Result; bool VerifiedByReplay; }`
  - `LOP.MapTools.PlayabilityReport.Build(string mapName, float startX, float finishX, in FlappyConfig config, IReadOnlyList<SpawnCleanRun> cleanRuns, string trapSection, IReadOnlyList<StunBudgetPoint> budget, EarliestCatch earliest) → string`

---

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/EditMode/MapTools/PlayabilityReportTests.cs`:

```csharp
using System.Collections.Generic;
using LOP;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class PlayabilityReportTests
    {
        static FlappyConfig Config()
            => new FlappyConfig(forwardSpeed: 11f, flapImpulse: 23f, gravity: 70f, maxFallSpeed: 30f,
                                bodyRadius: 0.45f, bodyHeight: 0.9f, restitution: 0.35f,
                                stunTime: 0.8f, invulnTime: 0.6f,
                                dashMult: 2f, dashDuration: 0.2f, dashChargeBase: 0.13f, dashChargeDive: 1.2f,
                                chaserStartX: -60f, chaserInitialSpeed: 7f,
                                chaserAcceleration: 0.075f, chaserMaxSpeed: 10f);

        static string Build(params SpawnCleanRun[] runs)
            => PlayabilityReport.Build("FlappyRaceMap", -2f, 632f, Config(), runs,
                                       trapSection: "  낀 자리 없음.",
                                       budget: new List<StunBudgetPoint>
                                       {
                                           new StunBudgetPoint(10f, 108f, 10, 7),
                                       },
                                       earliest: new EarliestCatch(true, 19.0f, 14));

        [Test]
        public void 자리마다_한_줄씩_찍는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 380f, 34, 0.7f), true),
                new SpawnCleanRun("PlayerSpawn_4", 9f, new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f), false));

            StringAssert.Contains("PlayerSpawn_1", report);
            StringAssert.Contains("PlayerSpawn_4", report);
        }

        [Test]
        public void 자리마다_답이_다르면_공정성_경고를_찍는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 380f, 34, 0.7f), true),
                new SpawnCleanRun("PlayerSpawn_4", 9f, new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f), false));

            //  "넷 중 하나라도 되면 통과"로 읽히면 안 된다 — 자리 배정이 곧 불이익이다.
            StringAssert.Contains("자리 배정", report);
        }

        [Test]
        public void 모두_같으면_공정성_경고가_없다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 380f, 34, 0.7f), true),
                new SpawnCleanRun("PlayerSpawn_2", -1f, new CleanRunResult(true, new bool[209], 0f, 380f, 34, 0.7f), true));

            StringAssert.DoesNotContain("자리 배정", report);
        }

        [Test]
        public void 막힌_자리는_어디서_끊겼는지와_최협_회랑을_같이_찍는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_4", 9f, new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f), false));

            StringAssert.Contains("38.2", report);
            StringAssert.Contains("최협 회랑", report);
            //  spec §8 — 실패는 눈금 탓일 수도 있어 되짚어 볼 안내를 같이 준다.
            StringAssert.Contains("눈금", report);
        }

        [Test]
        public void 재생으로_증명되지_않은_성공은_그렇다고_적는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 380f, 34, 0.7f), false));

            StringAssert.Contains("재생이 어긋", report);
        }

        [Test]
        public void 예산에_상한_가정을_함께_적는다()
        {
            string report = Build(
                new SpawnCleanRun("PlayerSpawn_1", -6f, new CleanRunResult(true, new bool[214], 0f, 380f, 34, 0.7f), true));

            StringAssert.Contains("19.0", report);
            StringAssert.Contains("14", report);
            //  이 숫자가 상한이라는 사실을 안 적으면 읽는 사람이 안전선으로 오해한다.
            StringAssert.Contains("실제는 이보다 나쁘다", report);
        }
    }
}
```

- [ ] **Step 2: 컴파일해 실패를 확인한다**

Expected: `failed:true` — `SpawnCleanRun` / `PlayabilityReport` 타입이 없다.

- [ ] **Step 3: PlayabilityReport를 구현한다**

`Assets/Scripts/MapTools/PlayabilityReport.cs`:

```csharp
using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>스폰 한 자리의 클린런 결과. <see cref="VerifiedByReplay"/>는 진짜 커널로 재생해 확인했는가.</summary>
    public readonly struct SpawnCleanRun
    {
        public readonly string Name;
        public readonly float Y;
        public readonly CleanRunResult Result;
        public readonly bool VerifiedByReplay;

        public SpawnCleanRun(string name, float y, CleanRunResult result, bool verifiedByReplay)
        {
            Name = name;
            Y = y;
            Result = result;
            VerifiedByReplay = verifiedByReplay;
        }
    }

    public static class PlayabilityReport
    {
        public static string Build(string mapName, float startX, float finishX, in FlappyConfig config,
                                   IReadOnlyList<SpawnCleanRun> cleanRuns, string trapSection,
                                   IReadOnlyList<StunBudgetPoint> budget, EarliestCatch earliest)
        {
            var text = new StringBuilder();
            float cleanRunSeconds = (finishX - startX) / config.ForwardSpeed;

            text.AppendLine("════ Flappy 맵 검사 ════");
            text.AppendLine($"맵: {mapName}        코스 x {startX:F0} → {finishX:F0}"
                          + $" ({finishX - startX:F0}m)   클린런 {cleanRunSeconds:F1}초");
            text.AppendLine($"물리: 전진 {config.ForwardSpeed:F0}  날갯짓 {config.FlapImpulse:F0}"
                          + $"  중력 {config.Gravity:F0}  최대낙하 {config.MaxFallSpeed:F0}"
                          + $"  몸 r{config.BodyRadius:F2} h{config.BodyHeight:F2}");
            text.AppendLine($"추격자: 시작 {config.ChaserStartX:F0}  초기 {config.ChaserInitialSpeed:F0}"
                          + $"  가속 {config.ChaserAcceleration}  상한 {config.ChaserMaxSpeed:F0}"
                          + $"      스턴 {config.StunTime} + 무적 {config.InvulnTime}");
            text.AppendLine();

            text.AppendLine("── ① 클린런 (자리별) ──────────────────");
            bool anyPass = false, anyFail = false;
            for (int i = 0; i < cleanRuns.Count; i++)
            {
                SpawnCleanRun run = cleanRuns[i];
                if (run.Result.Reachable)
                {
                    anyPass = true;
                    text.AppendLine($"  {run.Name} (y={run.Y:F0})   ✅  날갯짓 {CountFlaps(run.Result)}회"
                                  + (run.VerifiedByReplay ? "" : "   ⚠️ 탐색은 찾았으나 재생이 어긋남"));
                }
                else
                {
                    anyFail = true;
                    text.AppendLine($"  {run.Name} (y={run.Y:F0})   ❌  x={run.Result.BlockedX:F1}에서 막힘");
                    text.AppendLine($"                             최협 회랑 x={run.Result.NarrowestX:F0}"
                                  + $"  생존 {run.Result.NarrowestCount}"
                                  + $"  높이 폭 {run.Result.NarrowestHeightSpan:F1}m");
                }
            }
            if (anyPass && anyFail)
            {
                text.AppendLine("  ⚠️ 일부 자리만 불가 — 자리 배정이 곧 불이익이다");
            }
            if (anyFail)
            {
                text.AppendLine("  (❌는 높이 눈금이 굵어 생긴 오탐일 수 있다 — 눈금을 0.05로 줄여 다시 눌러 볼 것)");
            }
            text.AppendLine();

            text.AppendLine("── ② 낌 지점 ─────────────────────────");
            text.AppendLine(trapSection);
            text.AppendLine();

            text.AppendLine("── ③ 스턴 예산 ───────────────────────");
            text.AppendLine("  경과   클린런위치   허용    가능");
            for (int i = 0; i < budget.Count; i++)
            {
                StunBudgetPoint point = budget[i];
                string mark = point.PossibleStuns > point.AllowedStuns ? "  ⚠️" : "";
                text.AppendLine($"  {point.ElapsedSeconds,4:F1}초   {point.CleanRunX,7:F0}m"
                              + $"   {point.AllowedStuns,3}번  {point.PossibleStuns,3}번{mark}");
            }
            if (earliest.Caught)
            {
                text.AppendLine($"  ⚠️ 최속 탈락 {earliest.Seconds:F1}초 · {earliest.StunCount}번째"
                              + $" — 앞 {earliest.Seconds / cleanRunSeconds * 100f:F0}%는 압박 없음");
            }
            else
            {
                text.AppendLine("  골인 전에는 잡힐 수 없다 — 추격자가 압박이 되지 않는다");
            }
            text.AppendLine($"  (가정: 스턴 아닌 시간은 {config.ForwardSpeed:F0}으로 온전히 전진."
                          + " 실제는 이보다 나쁘다)");
            text.AppendLine("════════════════════════");
            return text.ToString();
        }

        static int CountFlaps(in CleanRunResult result)
        {
            int count = 0;
            for (int i = 0; i < result.Flaps.Count; i++)
            {
                if (result.Flaps[i]) { count++; }
            }
            return count;
        }
    }
}
```

- [ ] **Step 4: 컴파일하고 테스트를 돌린다**

Expected: 6 passed, 0 failed.

- [ ] **Step 5: 테스트가 실제로 실패할 수 있는지 확인한다**

`if (anyPass && anyFail)` 을 `if (false)` 로 바꾼다 → recompile → 재실행.

Expected: `자리마다_답이_다르면_공정성_경고를_찍는다`가 **실패해야 한다**. 되돌리고 초록을 확인한다.

- [ ] **Step 6: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/MapTools/PlayabilityReport.cs Assets/Scripts/MapTools/PlayabilityReport.cs.meta Assets/Tests/EditMode/MapTools/PlayabilityReportTests.cs Assets/Tests/EditMode/MapTools/PlayabilityReportTests.cs.meta
git status --short
git commit -m "$(cat <<'MSG'
feat(maptools): 세 검사 결과를 한 리포트로 묶는다

셋을 한 판에 찍는 이유는 서로를 해석해 주기 때문이다. 클린런만 봐서는 그 경로가 낌
구간을 지나는지 모르고, 예산만 봐서는 0번이 가능한 맵인지 모른다.

두 가지를 반드시 같이 적는다. 자리마다 답이 다르면 공정성 경고를 찍고(넷 중 하나라도
되면 통과로 읽히면 안 된다), 예산에는 그 숫자가 상한이라는 가정을 붙인다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Y7nzicQU3Pxb8ng5XntfD6
MSG
)"
```

---

## Task 5: 에디터 껍데기 — 개명 · 배선 · 검증 재생

**Files:**
- Rename: `Assets/Scripts/Editor/FlappyMapTrapScanner.cs` → `Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs` (짝 `.meta`도 함께 `git mv`)
- Modify: 위 파일 (메뉴 이름 · 클래스 이름 · 마커 읽기 · 게으른 격자 · 검증 재생 · 리포트 조립)

**Interfaces:**
- Consumes: `StunBudget.Curve` · `StunBudget.FindEarliestCatch` (Task 1), `CleanRunSearch.Run` (Task 2·3), `PlayabilityReport.Build` · `SpawnCleanRun` (Task 4), 기존 `Step` · `TryReadFlappyConfig` · `TryReadBounds` · `EscapesWithFlap` · `HitWatcher` · `FlappyShape`
- Produces: 메뉴 `LOP/Debug/Flappy 맵 검사`

**주의 — 이 태스크가 건드리지 않는 것:** 낌 판정 로직(`IsContactPoint` · `Escapes` · `EscapesWithFlap` · `EscapesWithPeriod` · `EscapesBySearch` · `StateKey` · `NamesAround`)은 한 줄도 고치지 않는다. `BuildReport`는 리포트의 ② 절만 만들도록 이름을 `BuildTrapSection`으로 바꾸고 머리말 두 줄(코스 범위 · 물리)을 뺀다 — 그 정보는 `PlayabilityReport`가 찍는다.

---

- [ ] **Step 1: 파일과 클래스를 개명한다**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git mv Assets/Scripts/Editor/FlappyMapTrapScanner.cs Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs
git mv Assets/Scripts/Editor/FlappyMapTrapScanner.cs.meta Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs.meta
```

파일 안에서:
- `public static class FlappyMapTrapScanner` → `public static class FlappyMapPlayabilityCheck`
- `[MenuItem("LOP/Debug/맵 낌 지점 스캔")]` → `[MenuItem("LOP/Debug/Flappy 맵 검사")]`
- `public static void Scan()` → `public static void Check()`
- 클래스 XML 주석을 새 책임으로 고쳐 쓴다 — 낌 스캔은 이제 세 검사 중 하나다.
- `EditorUtility.DisplayDialog("맵 낌 지점 스캔", ...)` 안의 제목 문자열도 `"Flappy 맵 검사"`로.

`.cs`와 짝 `.meta`를 함께 옮기는 이유: GUID가 보존돼야 씬·프리팹 참조가 안 끊긴다.

- [ ] **Step 2: 스폰·결승선 마커를 읽는 함수를 더한다**

```csharp
        //  출발점과 결승선은 맵이 정한다 — 서버 룰(FlappyRaceRuleSystem)이 읽는 것과 같은 마커를
        //  같은 방법으로 읽는다. 비활성 마커까지 찾는 것도 같다: 마커는 보일 필요가 없어 꺼 둘 수 있다.
        private static List<(string Name, Vector3 Position)> ReadSpawns()
        {
            var points = Object.FindObjectsByType<LOP.SpawnPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var list = new List<(string, Vector3)>();
            foreach (var point in points)
            {
                if (point != null)
                {
                    list.Add((point.name, point.transform.position));
                }
            }
            list.Sort((left, right) => string.CompareOrdinal(left.Item1, right.Item1));
            return list;
        }

        private static bool TryReadFinishX(out float finishX)
        {
            finishX = 0f;
            var markers = Object.FindObjectsByType<LOP.FinishLine>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (markers.Length == 0)
            {
                return false;
            }
            //  형상이 있으면 그 자리, 없으면 트랜스폼. FinishLine이 스스로 등록할 때와 같은 규칙이다.
            var renderer = markers[0].GetComponentInChildren<Renderer>();
            finishX = renderer != null ? renderer.bounds.center.x : markers[0].transform.position.x;
            return true;
        }
```

- [ ] **Step 3: 게으른 자유공간 격자를 더한다**

```csharp
        //  "이 자리에 몸이 들어가나"를 매번 물리엔진에 묻지 않고 격자에 캐시한다.
        //  전체를 미리 채우면 코스 전체가 570만 칸이라, 탐색이 실제로 밟는 칸만 채운다.
        private sealed class FreeSpaceGrid
        {
            const float Cell = 0.1f;

            private readonly Dictionary<long, bool> cache = new Dictionary<long, bool>();
            private readonly FlappyShape shape;
            private readonly int mapMask;

            public int Queries;

            public FreeSpaceGrid(in FlappyShape shape, int mapMask)
            {
                this.shape = shape;
                this.mapMask = mapMask;
            }

            public bool IsFree(float x, float y)
            {
                long key = ((long)Mathf.RoundToInt(x / Cell) << 32) ^ (uint)Mathf.RoundToInt(y / Cell);
                if (cache.TryGetValue(key, out bool free))
                {
                    return free;
                }
                Queries++;
                var p = new Vector3(x, y, 0f);
                free = Physics.CheckCapsule(shape.Lower(p), shape.Upper(p), shape.Radius,
                                            mapMask, QueryTriggerInteraction.Ignore) == false;
                cache[key] = free;
                return free;
            }
        }
```

- [ ] **Step 4: 검증 재생을 더한다**

```csharp
        //  탐색이 준 날갯짓 순서를 게임의 진짜 커널로 그대로 굴린다. 한 번이라도 닿으면 증명 실패다.
        //  탐색은 높이를 눈금으로 뭉개므로, 이 재생만이 "정말 무충돌인가"의 증거다.
        private static bool VerifyByReplay(Vector3 start, IReadOnlyList<bool> flaps,
                                           in FlappyShape shape, int mapMask,
                                           GameFramework.Physics.ICollisionQuery inner)
        {
            var query = new HitWatcher(inner);
            var state = new BirdState { Position = start };
            for (int i = 0; i < flaps.Count; i++)
            {
                state = Step(state, flaps[i], shape, mapMask, query);
                if (state.Stun > 0f)
                {
                    return false;   // 닿았다 = 무충돌이 아니다
                }
            }
            return true;
        }
```

- [ ] **Step 5: `Check()`를 세 검사로 조립한다**

기존 `Scan()` 본문의 1·2단계(낌 스캔)는 그대로 두고, 앞뒤로 ①③을 붙여 리포트 하나로 합친다.

```csharp
        [MenuItem("LOP/Debug/Flappy 맵 검사")]
        public static void Check()
        {
            int mapMask = LayerMask.GetMask("Default");
            if (TryReadBounds(mapMask, out Bounds bounds) == false)
            {
                EditorUtility.DisplayDialog("Flappy 맵 검사",
                    "Default 레이어에 콜라이더가 없다 — 맵 씬을 먼저 열어라.\n" +
                    "예: Assets/Art/Scenes/FlappyRaceMap.unity", "확인");
                return;
            }
            if (TryReadFlappyConfig(out FlappyShape shape) == false)
            {
                EditorUtility.DisplayDialog("Flappy 맵 검사",
                    "MasterData에서 FlappyConfig를 못 읽었다 — 패키지 StreamingAssets를 확인하라.", "확인");
                return;
            }
            if (TryReadFullConfig(out LOP.FlappyConfig config) == false)
            {
                EditorUtility.DisplayDialog("Flappy 맵 검사",
                    "추격자 값을 못 읽었다 — MasterData의 FlappyConfig를 확인하라.", "확인");
                return;
            }
            var spawns = ReadSpawns();
            if (spawns.Count == 0 || TryReadFinishX(out float finishX) == false)
            {
                EditorUtility.DisplayDialog("Flappy 맵 검사",
                    "맵에 SpawnPoint 또는 FinishLine 마커가 없다 — 게임과 같은 마커를 읽는다.", "확인");
                return;
            }

            var query = new GameFramework.Physics.UnityCollisionQuery();
            var grid = new FreeSpaceGrid(shape, mapMask);
            var cleanRuns = new List<LOP.MapTools.SpawnCleanRun>();
            string trapSection;
            try
            {
                //  ① 자리마다 따로 — 넷 중 하나라도 되면 통과로 뭉치면 공정성 문제가 안 보인다.
                for (int i = 0; i < spawns.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Flappy 맵 검사 (1/3 클린런)",
                        $"{spawns[i].Name}", i / (float)spawns.Count);

                    var options = new LOP.MapTools.CleanRunOptions(
                        startX: spawns[i].Position.x, startY: spawns[i].Position.y, finishX: finishX,
                        minY: bounds.min.y, maxY: bounds.max.y,
                        forwardSpeed: shape.ForwardSpeed, flapImpulse: shape.FlapImpulse,
                        gravity: shape.Gravity, maxFallSpeed: shape.MaxFallSpeed,
                        tickSeconds: TickSeconds, heightGrid: 0.1f);
                    var result = LOP.MapTools.CleanRunSearch.Run(options, grid.IsFree);
                    bool verified = result.Reachable
                        && VerifyByReplay(spawns[i].Position, result.Flaps, shape, mapMask, query);
                    cleanRuns.Add(new LOP.MapTools.SpawnCleanRun(
                        spawns[i].Name, spawns[i].Position.y, result, verified));
                }

                //  ② 기존 낌 스캔 — 본문은 그대로다.
                trapSection = ScanTraps(shape, bounds, mapMask, query);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            //  ③ 산수라 진행률이 필요 없다.
            var budget = LOP.MapTools.StunBudget.Curve(config, spawns[0].Position.x, finishX, stepSeconds: 10f);
            var earliest = LOP.MapTools.StunBudget.FindEarliestCatch(config, spawns[0].Position.x, finishX);

            string report = LOP.MapTools.PlayabilityReport.Build(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                spawns[0].Position.x, finishX, config, cleanRuns, trapSection, budget, earliest);
            Debug.Log(report);
            EditorGUIUtility.systemCopyBuffer = report;
        }
```

기존 `Scan()`의 1·2단계 본문을 `ScanTraps(in FlappyShape, in Bounds, int, ICollisionQuery) → string`으로 잘라 옮긴다. **판정 로직은 손대지 않고** 진행률 문구만 `(2/3 낌 지점)`으로 바꾸고, 마지막에 `BuildTrapSection`이 만든 문자열을 돌려준다.

`TryReadFullConfig`는 `TryReadFlappyConfig`와 같은 `.bytes`를 읽되 추격자 열까지 담아 `LOP.FlappyConfig`를 만든다:

```csharp
        //  ③은 추격자 값이 필요하고 그건 공유 FlappyConfig에만 있다. 같은 행을 두 번 읽는 셈이지만,
        //  FlappyShape는 낌 스캔이 쓰던 모양이라 그대로 두고 여기만 더한다.
        private static bool TryReadFullConfig(out LOP.FlappyConfig config)
        {
            config = default;
            string path = Path.GetFullPath(
                "Packages/com.baegames.lop.masterdata.client/Runtime.Generated/StreamingAssets/MasterData/tbflappyconfig.bytes");
            if (File.Exists(path) == false)
            {
                return false;
            }
            var row = new LOP.MasterData.TbFlappyConfig(new Luban.ByteBuf(File.ReadAllBytes(path))).GetOrDefault(1);
            if (row == null)
            {
                return false;
            }
            config = new LOP.FlappyConfig(
                row.ForwardSpeed, row.FlapImpulse, row.Gravity, row.MaxFallSpeed,
                row.BodyRadius, row.BodyHeight, row.Restitution,
                row.StunTime, row.InvulnTime,
                row.DashMult, row.DashDuration, row.DashChargeBase, row.DashChargeDive,
                row.ChaserStartX, row.ChaserInitialSpeed, row.ChaserAcceleration, row.ChaserMaxSpeed,
                row.FinishBrake);
            return true;
        }
```

> 필드 이름이 생성 코드와 다르면 `Packages/com.baegames.lop.masterdata.client/Runtime.Generated/Scripts/MasterData/FlappyConfig.cs`를 열어 실제 프로퍼티 이름으로 맞춘다.

- [ ] **Step 6: 컴파일하고 전체 EditMode를 돌린다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --async_tests true --timeout 600
unity cmd test_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

Expected: 기존 1043 + 이번 신규(7 + 9 + 6 = 22) = **1065 passed, 0 failed.** 낌 스캔 로직을 안 고쳤으므로 기존 테스트가 하나도 안 깨져야 한다 — 깨졌다면 손대면 안 될 곳을 건드린 것이다.

- [ ] **Step 7: 실제 맵에서 돌려 spec의 손계산과 대조한다**

사용자에게 `Assets/Art/Scenes/FlappyRaceMap.unity`를 열고 `LOP/Debug/Flappy 맵 검사`를 눌러 달라고 요청한 뒤, 콘솔 출력을 받아 아래를 확인한다.

| 확인할 것 | 기대값 (spec §4.3) |
|---|---|
| 코스 | `x −2 → 632 (634m)  클린런 57.6초` |
| 최속 탈락 | `19.0초 · 14번째` |
| 10초 행 | `허용 10번 / 가능 7번` |
| 20초 행 | `허용 13번 / 가능 14번` + ⚠️ |
| 자리 | `PlayerSpawn_1~4` 넷이 y = −6 / −1 / 4 / 9 로 찍힌다 |

**③의 숫자가 spec과 다르면 도구가 틀린 것이다** — 손계산이 먼저 나와 있으므로 도구를 고친다.
①의 결과(어느 자리가 되는지)는 아무도 모르던 값이라 **무엇이 나오든 그것이 답이고, 로드맵에 기록한다.**

- [ ] **Step 8: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs.meta
git status --short
git commit -m "$(cat <<'MSG'
feat(editor): 낌 스캐너를 맵 플레이 가능성 검사로 넓힌다

세 검사가 한 리포트가 됐다. 낌 판정 로직은 한 줄도 안 고쳤고 파일과 이름만 옮겼다 —
.cs와 짝 .meta를 함께 옮겨 GUID를 보존한다.

출발점과 결승선은 서버 룰이 읽는 것과 같은 마커를 같은 방법으로 읽는다. 도구가 자기
좌표를 들고 있으면 맵을 새로 만들 때마다 도구를 고쳐야 하고, 그 순간 도구와 게임이
다른 코스를 보게 된다.

클린런은 자리마다 따로 돌린다. 스폰 넷의 높이가 15m 벌어져 있어 답이 자리마다 다를
수 있고, 그건 밸런싱이 아니라 공정성 문제다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Y7nzicQU3Pxb8ng5XntfD6
MSG
)"
```

---

## Task 6: 결과를 로드맵에 기록

**Files:**
- Modify: `docs/ROADMAP.md`

**Interfaces:**
- Consumes: Task 5 Step 7의 실제 콘솔 출력

---

- [ ] **Step 1: 로드맵에 절을 더한다**

`## 📋 Flappy — spec들이 열어 둔 것 전수 (2026-09-06 훑음)` 절 **앞**에 아래를 넣는다. 대괄호 안은
Task 5 Step 7에서 실제로 나온 값으로 채운다 — 추측으로 쓰지 않는다.

```markdown
## ✅ Flappy 맵 플레이 가능성 검사 (2026-09-07)

밸런싱 근거가 하나도 없던 상태를 끝냈다. 맵 씬을 열고 `LOP/Debug/Flappy 맵 검사`를 누르면
셋을 한 판에 찍는다 — **① 무충돌 경로가 있나(자리별) · ② 낌 자리가 있나 · ③ 몇 번까지
부딪혀도 안 잡히나.**

프로토타입의 판정기(`FlappySimJudge`)를 옮긴 것이지만 **판정을 게임의 진짜 값·진짜 커널 위에
다시 세웠다.** 그 판정기는 자기 주석에 "시뮬과 게임이 어긋나면 튜닝이 무의미하다"고 적어
뒀는데, 그것이 씬의 `FlappyPlayer`를 읽는 사이 게임은 `FlappyMoveSystem` + `KinematicMover` +
마스터데이터로 옮겨 가 **그 전제가 이미 깨져 있었다.**

**설계 중 종이로 낸 값을 도구가 확인했다**: 추격자는 **출발 후 19.0초 · 14번째 스턴**까지
위협이 아니다 — **코스의 33%**. 의도한 것인지는 게임 디자인 판단으로 남긴다.

**실측 결과**: [자리별 클린런 결과] / [낌 자리 수] / [예산 곡선 요약]

**구조에서 얻은 것**: 무충돌 경로 탐색에는 collide-and-slide가 필요 없다(안 닿는 동안 커널이
하는 일은 `위치 += 속도 × dt`뿐). 전진이 상수라 "열 = 틱"이고, **세로 속도가 연속값이 아니라
사다리**(날갯짓 뒤 몇 틱 지났나로 완전히 결정)라 프로토타입이 vy를 뭉개야 했던 자리에서 근사가
사라졌다. 남은 근사는 높이 눈금 하나뿐이고 그건 **찾은 경로를 진짜 커널로 재생해 증명**한다.

**알고 남긴 것**: 재생 검증은 한 방향만 잡는다 — "된다"는 증명하지만 "안 된다"는 눈금 탓일 수
있다. 그래서 ❌에는 눈금을 줄여 다시 보라는 안내를 같이 찍는다.

spec `docs/superpowers/specs/2026-09-07-flappy-map-playability-check-design.md`,
plan `docs/superpowers/plans/2026-09-07-flappy-map-playability-check.md`
```

- [ ] **Step 2: 남은 일감 표를 갱신한다**

같은 파일의 `### 살아 있는데 로드맵에 없던 것` 표에서 **`오토파일럿이 스턴 예산(22번)을 못 지킨다`**
행의 크기 칸 뒤에 한 줄 더한다 — 예산의 실제 값이 나왔으므로 그 22가 어디서 온 수인지 다시 봐야 한다.

```markdown
| **오토파일럿이 스턴 예산(22번)을 못 지킨다** — 봇을 고칠지, 값을 낮출지, 사람이 검증할지. **2026-09-07 갱신: 도구가 낸 실제 예산은 골인 시점 19번이고 최속 탈락은 14번째다 — "22번"이 어디서 온 수인지 먼저 확인할 것** | 09-03 §11 | 중간 |
```

- [ ] **Step 3: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add docs/ROADMAP.md
git status --short
git commit -m "$(cat <<'MSG'
docs(roadmap): 맵 플레이 가능성 검사와 그것이 낸 첫 숫자를 기록한다

밸런싱 근거가 하나도 없던 상태가 끝났다. 첫 수확은 추격자가 코스의 앞 33% 동안
위협이 아니라는 것 — 설계 중 종이로 낸 값을 도구가 확인했다.

곁들여 "오토파일럿이 스턴 예산 22번을 못 지킨다"는 옛 항목에 단서를 붙인다. 실제
예산은 골인 시점 19번이고 최속 탈락은 14번째라, 그 22가 어디서 온 수인지가 먼저다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Y7nzicQU3Pxb8ng5XntfD6
MSG
)"
```

---

## 마무리 — 푸시

**모든 태스크가 끝나고 전체 EditMode가 초록일 때만** 아래를 밟는다. 한 줄씩 결과를 확인하고
넘어간다. `&&`로 길게 이어 붙이지 않는다.

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff <feature-branch>
git push origin main
```

**`git push --force` / `--force-with-lease` 금지.** 푸시가 거절되면 힘으로 밀지 말고 다시
`fetch` → 리베이스 → 재시도한다. 이 프로젝트는 머신이 둘이라 그 사이 원격이 움직이는 일이 잦다.

리베이스 후에는 로컬 픽스처가 제자리에 돌아왔는지 `git status --short`로 확인한다.

**LOP-Shared는 이 계획에서 바뀌지 않는다** — 커널도 추격자 곡선도 읽기만 한다. 푸시는 클라
저장소 하나뿐이다.

---

## 실행 중 드러난 이 계획의 결함 (2026-09-07 기록)

계획을 고쳐 쓰지 않고 **무엇이 틀렸는지만** 남긴다 — 실행된 것과 문서가 어긋나면 다음 사람이
어느 쪽을 믿을지 알 수 없기 때문이다. 아래는 전부 리뷰나 구현 중에 잡혀 고쳐졌다.

| 자리 | 무엇이 틀렸나 | 어떻게 됐나 |
|---|---|---|
| Task 1 Step 6 | 지목한 변이가 `t=10`에서 검출되지 않는다. 올바른 식 `(10+0.6)/1.4=7.57→7`과 버그 식 `10/1.4=7.14→7`이 같은 정수로 내림된다. 두 번째로 예측한 실패는 **구조적으로 불가능**했다 — `FindEarliestCatch`가 `PossibleStuns`를 부르지 않는다 | 테스트 입력을 `9.5`로 정정(7 vs 6으로 갈림). 그 "안 부르는" 사실 자체가 로직 중복이라 공용 헬퍼로 묶음 |
| Task 2 Step 1 vs Step 2 | 열 개수가 227과 228로 서로 다르게 적혀 있다. **228이 맞다** (`ceil(50/0.22)`) | 테스트가 228로 갔다 |
| Task 2 §3.4 근거 | "앞 60열은 시드가 하나뿐이라"가 **사실이 아니다.** 실측하면 그 60열 동안 프론티어가 2개 → 13,640개로 자란다 | 임의의 60열 건너뛰기를 "프론티어가 안 늘어날 때까지"로 교체. 최협 회랑은 막힌 경로 전용 진단으로 좁힘 |
| Task 2 Step 5 | 얇은 벽 `[20, 20.15]`이 틱 격자점 `0.22×91 = 20.02`를 품는다. 끝점만 검사해도 우연히 걸려 **변이가 빨강이 안 된다** | 벽을 `[20.05, 20.20]`으로 옮김(두께 유지, 양 끝점이 다 벗어남) |
| Task 3 테스트 #2 | 뽑아낸 경로가 **연속 물리로 재생해도 통과한다**고 단언했다. 설계는 그걸 약속한 적이 없다 — spec §3.7이 "탐색은 근사, 진짜 커널 재생이 증명이고 어긋나면 눈금을 조여라"라고 정해 뒀다 | 단언을 둘로 나눔: 모델 일관성(항상 참) + 여유 있는 맵에서의 연속 재생 |
| Task 4 §6 리포트 | 최협 회랑 줄을 무조건 숫자로 찍는다. 성장 구간에서 막힌 맵은 "안 쟀다"인데 `생존 0 · 높이 폭 0.0m`로 나온다 | `NarrowestCount == 0`이면 "측정 안 됨"과 이유를 찍도록 분기 |
| Task 6 브리프 | 브리프 추출기가 문서 꼬리의 "마무리 — 푸시" 절까지 가져가, 구현자가 **main에 머지·푸시할 지시**를 쥐게 됐다 | 브리프에서 잘라냄. 푸시는 컨트롤러가 규약대로 밟는다 |

**공통 교훈 하나.** 위 일곱 중 넷이 "일부러 깨뜨려 빨강을 확인한다" 절차에서 잡혔다. 그 절차가
없었으면 **아무것도 지키지 않는 테스트 넷**이 초록으로 통과했을 것이다.
