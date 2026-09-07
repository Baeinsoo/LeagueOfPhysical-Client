# Flappy 맵 검사 — 봇으로 증명하기 + 형상으로 낌 후보 찾기

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ①(무충돌 통과 가능한가)이 **증명된 답**을 내게 하고, ②(낌 지점)가 격자에 걸리지 않는 자리도 놓치지 않게 한다.

**Architecture:** ①은 전수 탐색 대신 **봇 한 마리를 진짜 커널로 날린다** — 궤적이 하나뿐이라 상태를 묶을 이유가 없고, 그래서 반올림도 표류도 생기지 않는다. 봇이 통과하면 그 자리는 증명 완료이고, 실패할 때만 기존 전수 탐색을 돌려 "맵이 불가능"인지 "봇이 못 한 것"인지 가른다. ②는 판정 로직을 그대로 두고 **씨앗만 늘린다** — 지형 표면을 훑어 "덫이 될 만한 모양"을 찾아 기존 격자 씨앗에 더한다.

**Tech Stack:** Unity 6 EditMode · NUnit · `KinematicMover`(LOP-Shared 공유 커널) · `FlappyGapAiming`(이미 이관됨, 테스트 16개) · `Physics.Raycast`/`CheckCapsule`

**Spec:** 없음. 설계 결정은 아래 §설계 결정에 박아 둔다 — 앞선 슬라이스(`2026-09-07-flappy-map-playability-check-design.md`)가 이 문제를 열어 두었고, 그 문서 §8·§12의 정정 블록이 배경이다.

## Global Constraints

- **커널을 복제하지 않는다.** 봇의 물리는 `FlappyMapPlayabilityCheck.Step`(이미 `KinematicMover`를 부른다)을 그대로 쓴다. 새로 짜지 않는다.
- **`KinematicMoveInput`에는 `stepOffset: 0f, groundProbe: 0f`.** 하나라도 빠지면 "닿았다"가 게임과 달라진다.
- **낌 판정 로직(`IsContactPoint`·`Escapes`·`EscapesWithFlap`·`EscapesWithPeriod`·`EscapesBySearch`·`StateKey`·`NamesAround`)은 한 줄도 고치지 않는다.** ②는 그 앞단에 씨앗을 더할 뿐이다.
- **`run_tests`는 재컴파일하지 않는다.** 소스 수정 후 반드시 `unity cmd recompile` → `unity cmd recompile_status`가 `completed`/`failed:false`가 될 때까지 폴링 → 그 다음 `run_tests`. `unity` CLI는 `export PATH="$PATH:$HOME/.unity/bin"`. 모든 호출에 `--project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client`.
- **`run_tests`의 즉시 응답 `Total:0`은 "시작됨"이지 결과가 아니다.** `unity cmd test_status`가 `completed`가 될 때까지 폴링해서 읽는다.
- **`git add -A` / `git commit -a` 금지.** 경로를 지정해 스테이징하고 커밋 전에 `git status --short`로 확인한다. 다음 넷은 절대 스테이징하지 않는다: `Assets/Art`, `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`, `ProjectSettings/PackageManagerSettings.asset`, `ProjectSettings/ProjectSettings.asset`.
- **`.meta`는 유니티가 만든 것만 커밋한다.** 직접 쓰지 않는다.
- **테스트는 반드시 일부러 깨뜨려 빨강을 확인한다.** 앞 플랜에서 브리프가 지정한 변이 넷이 실제로는 안 깨졌다 — 픽스처가 틱 격자에 우연히 맞아떨어지는 일이 실재한다. 예측이 빗나가면 산수로 진단하고 픽스처를 고친 뒤 빨강을 보고한다.
- **`StringAssert.Contains`는 문화권 비교라 이모지만으로 된 검색어가 항상 매칭된다.** 이모지를 찾을 땐 `report.Contains("✅", System.StringComparison.Ordinal)`.
- **씬을 열거나 메뉴를 실행하지 않는다.** 실제 맵 실행은 컨트롤러가 사용자와 함께 한다.

## 설계 결정 (spec 대신)

**결정 1 — ①은 봇이 먼저, 탐색은 뒤.** 봇이 통과하면 실제 물리로 끝까지 간 궤적이 있으므로 **증명**이다. 실패하면 전수 탐색을 돌린다: 탐색이 ❌면 "맵이 불가능", 탐색이 ✅면 "모름(봇 한계)". 봇은 몇 초, 탐색은 자리당 몇 분이라 평소 비용이 크게 준다.

**결정 2 — 탐색을 지우지 않는다.** ❌를 보일 수 있는 **유일한** 수단이다. 봇은 "가능"만 증명한다.

**결정 3 — 봇은 지연도 오차도 없이 완벽하게 겨냥한다.** ①이 묻는 것이 "사람이 아주 잘하면 되는가"이기 때문이다. 프로토타입의 반응 지연·조준 오차는 *난이도* 측정용이고 그건 다른 질문이다.

**결정 4 — ②의 형상 검출은 씨앗만 만든다.** "이 모양은 덫이다"라고 단정하지 않는다. 후보를 굴려 보기에 넘겨 **기존 판정이 확정**한다. 그래야 오탐이 결과를 오염시키지 않고, 기존 결과도 안 흔들린다.

**결정 5 — 형상 규칙은 열린 목록이다.** 맵이 아직 완성 형태가 아니다. 규칙 하나를 박아 넣지 않고 `ITrapShapeRule` 목록으로 두어 나중에 규칙을 더할 수 있게 한다. 표면 훑기도 **콜라이더 종류에 의존하지 않는 방식**(레이캐스트)으로 해서 지금의 BoxCollider 119개든 나중의 메시든 같은 코드가 돈다.

**오늘 넣는 규칙은 하나 — 앞으로 기운 덮개(overhang).** 프로젝트가 이미 실측으로 알아낸 유일한 덫 모양이다: 머리 위를 앞으로 기울어 덮은 면이 있으면, 커널이 세로를 따로 쓸어 올리는 탓에 **날갯짓이 통째로 무력해진다.** 뒤로 기운 끝면은 안전하다 — 실측에서 **1.68m 계단은 멀쩡했고 0.55m 계단이 덫**이었다. 크기가 아니라 기울기다.

## 실측값 (이 계획이 근거로 삼는 것)

```
전진 11   날갯짓 23   중력 70   최대낙하 30   몸 r0.45 h0.9   틱 0.02
날갯짓 한 번의 상승 폭 = 4.012m (17틱)      ← FlappyGapAiming.AimHeight의 flapArc
코스 x −2 → 632 (634m) = 2,882틱           스폰 4자리 y = −6 / −1 / 4 / 9
맵 콜라이더 = BoxCollider 119개 (메시 없음)  탐색 대역 y[−79.0 ~ 36.0]
```

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/MapTools/BotPilot.cs` | 봇의 순수 판단: 막힘 표 → 겨냥 높이 → 이번 틱에 날갯짓할까. 물리도 씬도 모른다 |
| `Assets/Scripts/MapTools/TrapShapeRules.cs` | `SurfaceSample`, `ITrapShapeRule`, `ForwardOverhangRule`. 순수 판정 |
| `Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs` | 봇 비행 루프(진짜 커널) · 표면 훑기(레이캐스트) · 씨앗 합치기 · 리포트 조립 |
| `Assets/Scripts/MapTools/PlayabilityReport.cs` | ① 결과에 "봇 통과"를 표현 |
| `Assets/Tests/EditMode/MapTools/BotPilotTests.cs` | Task 1 |
| `Assets/Tests/EditMode/MapTools/TrapShapeRulesTests.cs` | Task 3 |
| `Assets/Tests/EditMode/MapTools/PlayabilityReportTests.cs` | Task 5에서 추가 |

---

## Task 1: BotPilot — 봇의 판단

**Files:**
- Create: `Assets/Scripts/MapTools/BotPilot.cs`
- Test: `Assets/Tests/EditMode/MapTools/BotPilotTests.cs`

**Interfaces:**
- Consumes: `FlappyRace.FlappyGapAiming.TryFindGap(IReadOnlyList<bool> blocked, float bottomY, float step, float currentY, float bodyRadius, out float low, out float high)` 와 `FlappyGapAiming.AimHeight(float low, float high, float flapArc)` — 둘 다 어셈블리 `FlappyRaceSlice.Logic`
- Produces:
  - `LOP.MapTools.BotDecision` — `readonly struct { bool Flap; bool GapFound; float AimY; }`
  - `LOP.MapTools.BotPilot.FlapArc(float flapImpulse, float gravity, float tickSeconds) → float`
  - `LOP.MapTools.BotPilot.Decide(IReadOnlyList<bool> blockedAhead, float bottomY, float step, float currentY, float verticalSpeed, float bodyRadius, float flapArc) → BotDecision`

**설계 메모.** 봇의 규칙은 한 줄이다: **"이번 틱에 날갯짓을 안 하면 겨냥 높이 아래로 내려가는가."** 날갯짓은 세로 속도를 23으로 덮어쓰므로, 안 눌렀을 때의 다음 높이가 목표보다 낮으면 지금 눌러야 한다. 이보다 정교하게 만들지 않는다 — 결정 3.

`FlappyRaceSlice.Logic`을 참조하려면 `Assets/Scripts/MapTools/LOP.MapTools.asmdef`의 `references`에 `"FlappyRaceSlice.Logic"`을 더한다. 그 asmdef는 `includePlatforms: ["Editor"]`이고 `FlappyRaceSlice.Logic`은 플랫폼 제약이 없으므로 참조가 성립한다.

---

- [ ] **Step 1: asmdef에 참조를 더한다**

`Assets/Scripts/MapTools/LOP.MapTools.asmdef`의 `references` 배열에 `"FlappyRaceSlice.Logic"`을 추가한다. 다른 필드는 건드리지 않는다. 테스트 asmdef(`Assets/Tests/EditMode/MapTools/LOP.MapTools.Tests.EditMode.asmdef`)에도 같은 항목을 더한다 — 테스트가 `FlappyGapAiming`을 직접 부르지는 않지만 `BotPilot`의 반환 타입 경유로 필요할 수 있다.

- [ ] **Step 2: 실패하는 테스트를 쓴다**

`Assets/Tests/EditMode/MapTools/BotPilotTests.cs`:

```csharp
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class BotPilotTests
    {
        const float BodyRadius = 0.45f;
        const float Step = 0.1f;
        const float BottomY = 0f;

        //  아래에서 위로 0.1m 간격. true = 막힘.
        static bool[] Column(params bool[] cells) => cells;

        //  아래 n칸이 뚫리고 그 위가 막힌 기둥. 폭은 (n−1) × 0.1m 다.
        static bool[] Free(int cells)
        {
            var column = new bool[cells + 1];
            column[cells] = true;
            return column;
        }

        [Test]
        public void 날갯짓_한_번의_상승_폭은_속도가_0이_될_때까지_더한_값이다()
        {
            //  23, 21.6, 20.2 … 를 0.02씩 곱해 더하면 4.012m (17틱).
            Assert.AreEqual(4.012f, BotPilot.FlapArc(flapImpulse: 23f, gravity: 70f, tickSeconds: 0.02f), 0.005f);
        }

        [Test]
        public void 목표보다_아래로_떨어질_참이면_누른다()
        {
            //  0.0~1.0이 뚫려 있고 새는 0.5에 있는데 세로 속도가 −10이면 다음 틱에 0.3으로 떨어진다.
            //  겨냥 높이는 AimHeight(0, 1.0, 4.012) = 0(틈이 아치보다 좁으니 바닥에 붙인다).
            //  0.3은 아직 0보다 위지만, 그 다음 틱을 기다리면 이미 늦다 — 규칙은 "한 틱 뒤"로 본다.
            //  뚫린 폭을 1.4m로 잡는다. 최소 통과 폭(지름 0.9)과 정확히 같으면 부동소수 경계에 걸린다.
            var decision = BotPilot.Decide(Free(15), BottomY, Step, currentY: 0.5f, verticalSpeed: -30f,
                                           BodyRadius, flapArc: 4.012f);

            Assert.IsTrue(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 목표_위에_충분히_있으면_안_누른다()
        {
            var decision = BotPilot.Decide(Free(15), BottomY, Step, currentY: 0.9f, verticalSpeed: 0f,
                                           BodyRadius, flapArc: 4.012f);

            Assert.IsTrue(decision.GapFound);
            Assert.IsFalse(decision.Flap);
        }

        [Test]
        public void 지나갈_틈이_없으면_그렇다고_알린다()
        {
            //  몸이 0.9m(지름)인데 뚫린 곳이 0.2m뿐이면 통과할 틈이 아니다.
            var decision = BotPilot.Decide(Column(true, false, false, true, true),
                                           BottomY, Step, currentY: 0.2f, verticalSpeed: 0f,
                                           BodyRadius, flapArc: 4.012f);

            Assert.IsFalse(decision.GapFound);
        }

        [Test]
        public void 틈이_없으면_고도를_지키려_누른다()
        {
            //  판단할 근거가 없을 때 떨어지게 두면 바닥에 부딪힌다 — 통과 가능성을 묻는 검사이므로
            //  아무 정보가 없을 땐 떠 있는 쪽을 고른다.
            var decision = BotPilot.Decide(Column(true, true, true), BottomY, Step,
                                           currentY: 0.2f, verticalSpeed: -30f, BodyRadius, flapArc: 4.012f);

            Assert.IsFalse(decision.GapFound);
            Assert.IsTrue(decision.Flap);
        }

        [Test]
        public void 겨냥_높이는_틈이_넓으면_아치가_들어갈_자리로_내려_잡는다()
        {
            //  0~10m가 뚫려 있으면 AimHeight(0, 10, 4.012) = (10−4.012)/2 = 2.994.
            //  0~10m가 뚫려 있으면 AimHeight(0, 10, 4.012) = (10−4.012)/2 = 2.994.
            var decision = BotPilot.Decide(Free(101), BottomY, Step, currentY: 5f, verticalSpeed: 0f,
                                           BodyRadius, flapArc: 4.012f);

            Assert.IsTrue(decision.GapFound);
            Assert.AreEqual(2.994f, decision.AimY, 0.01f);
        }
    }
}
```

- [ ] **Step 3: 컴파일해 실패를 확인한다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

Expected: `failed:true` — `BotPilot` 타입이 없다.

- [ ] **Step 4: BotPilot을 구현한다**

`Assets/Scripts/MapTools/BotPilot.cs`:

```csharp
using System.Collections.Generic;
using FlappyRace;

namespace LOP.MapTools
{
    /// <summary>봇이 이번 틱에 내린 판단.</summary>
    public readonly struct BotDecision
    {
        public readonly bool Flap;
        /// <summary>앞을 훑어 지나갈 만한 틈을 찾았는가. 못 찾았으면 <see cref="AimY"/>는 뜻이 없다.</summary>
        public readonly bool GapFound;
        public readonly float AimY;

        public BotDecision(bool flap, bool gapFound, float aimY)
        {
            Flap = flap;
            GapFound = gapFound;
            AimY = aimY;
        }
    }

    /// <summary>
    /// 앞을 세로로 훑은 막힘 표를 보고 이번 틱에 날갯짓할지 정한다. 물리도 씬도 모른다 —
    /// 표는 부르는 쪽이 실제 콜라이더로 재서 넘긴다.
    ///
    /// <para>겨냥은 지연도 오차도 없이 완벽하다. 이 봇이 답하려는 질문이 "사람이 아주 잘하면
    /// 통과할 수 있는가"이기 때문이다. 반응 지연을 넣는 것은 난이도를 재는 다른 질문이다.</para>
    /// </summary>
    public static class BotPilot
    {
        /// <summary>날갯짓 한 번으로 오르는 높이. 세로 속도가 0이 될 때까지 더한 값이다.</summary>
        public static float FlapArc(float flapImpulse, float gravity, float tickSeconds)
        {
            float rise = 0f;
            float speed = flapImpulse;
            while (speed > 0f)
            {
                rise += speed * tickSeconds;
                speed -= gravity * tickSeconds;
            }
            return rise;
        }

        public static BotDecision Decide(IReadOnlyList<bool> blockedAhead, float bottomY, float step,
                                         float currentY, float verticalSpeed, float bodyRadius,
                                         float flapArc)
        {
            if (FlappyGapAiming.TryFindGap(blockedAhead, bottomY, step, currentY, bodyRadius,
                                           out float low, out float high) == false)
            {
                //  근거가 없을 땐 떠 있는 쪽을 고른다 — 떨어지게 두면 바닥에 부딪히고,
                //  이 검사는 "통과할 수 있는가"를 묻지 "얼마나 잘 나는가"를 묻지 않는다.
                return new BotDecision(flap: true, gapFound: false, aimY: currentY);
            }

            float aim = FlappyGapAiming.AimHeight(low, high, flapArc);
            //  한 틱 뒤를 본다. 날갯짓은 세로 속도를 덮어쓰므로, 안 누르면 내려갈 참일 때 지금 눌러야
            //  늦지 않는다. "지금 목표보다 낮은가"로 보면 이미 지나간 뒤다.
            float nextY = currentY + verticalSpeed * TickForLookahead;
            return new BotDecision(flap: nextY < aim, gapFound: true, aimY: aim);
        }

        //  한 틱 앞을 본다. 커널의 틱과 같은 값이지만, 이 클래스는 물리를 모르므로 상수로 둔다 —
        //  값이 갈리면 봇이 늦거나 이르게 누를 뿐 결과가 조용히 틀리지는 않는다.
        const float TickForLookahead = 0.02f;
    }
}
```

- [ ] **Step 5: 컴파일하고 테스트를 돌린다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --async_tests true --filter "BotPilotTests"
unity cmd test_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

Expected: 6 passed, 0 failed.

- [ ] **Step 6: 테스트가 실제로 실패할 수 있는지 확인한다**

`Decide`의 `nextY < aim`을 `currentY < aim`으로 바꾼다(한 틱 앞을 안 보게) → recompile → 재실행.

Expected: `목표보다_아래로_떨어질_참이면_누른다`가 **빨강이어야 한다** — currentY 0.5는 aim 0보다 위라 안 누르게 된다. 안 깨지면 그 테스트가 아무것도 안 지키는 것이니 세로 속도를 더 크게 잡아 고친다. 확인 후 되돌리고 초록을 다시 확인한다.

- [ ] **Step 7: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/MapTools/BotPilot.cs Assets/Scripts/MapTools/BotPilot.cs.meta Assets/Scripts/MapTools/LOP.MapTools.asmdef Assets/Tests/EditMode/MapTools/LOP.MapTools.Tests.EditMode.asmdef Assets/Tests/EditMode/MapTools/BotPilotTests.cs Assets/Tests/EditMode/MapTools/BotPilotTests.cs.meta
git status --short
git commit -m "$(cat <<'MSG'
feat(maptools): 앞을 보고 날갯짓을 정하는 봇의 판단

규칙은 한 줄이다 — 안 누르면 겨냥 높이 아래로 내려갈 참인가. 날갯짓이 세로 속도를
덮어쓰므로 한 틱 앞을 봐야 늦지 않는다.

지연도 조준 오차도 넣지 않는다. 이 봇이 답하려는 것이 "사람이 아주 잘하면 통과하는가"라서다.
반응 지연은 난이도를 재는 다른 질문이고 다른 도구 몫이다.

틈 찾기와 겨냥 높이는 프로토타입에서 이미 옮겨 온 FlappyGapAiming을 쓴다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Y7nzicQU3Pxb8ng5XntfD6
MSG
)"
```

---

## Task 2: 봇을 진짜 커널로 날린다

**Files:**
- Modify: `Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs`

**Interfaces:**
- Consumes: `BotPilot.Decide` / `BotPilot.FlapArc` (Task 1); 같은 파일의 기존 `Step(BirdState, bool flap, in FlappyShape, int mapMask, HitWatcher)`, `HitWatcher`, `FlappyShape`, `FreeSpaceGrid`
- Produces (같은 파일 내부):
  - `private readonly struct BotFlight { public readonly bool Reached; public readonly bool Touched; public readonly float FarthestX; public readonly int FlapCount; public readonly int Ticks; }`
  - `private static BotFlight FlyBot(Vector3 start, float finishX, in FlappyShape shape, int mapMask, GameFramework.Physics.ICollisionQuery inner, System.Func<float, float, bool> isBlocked)`

**설계 메모.** 봇은 매 틱 **앞을 세로로 한 줄 훑는다.** 훑는 x는 `현재 x + 전진 × 0.14초`(프로토타입이 쓰던 앞보기 거리와 같은 취지), 훑는 y 범위와 간격은 탐색이 쓰는 대역·눈금을 그대로 쓴다. 막힘 여부는 `FreeSpaceGrid`가 이미 캐시하므로 그것을 뒤집어 쓴다(`IsFree == false`).

**무충돌의 정의는 ①과 같아야 한다.** `Step`은 닿으면 스턴을 건다. 그러므로 **한 번이라도 `state.Stun > 0`이 되면 그 비행은 무충돌이 아니다** — 즉시 중단하고 `Touched = true`로 돌려준다. 스턴을 안고 계속 날면 "통과는 했지만 부딪혔다"가 되어 ①의 질문과 달라진다.

---

- [ ] **Step 1: 봇 비행 루프를 더한다**

`FlappyMapPlayabilityCheck.cs`에 추가한다. `VerifyByReplay` 바로 아래에 두어 "진짜 커널로 굴리는 것들"이 모이게 한다.

```csharp
        /// <summary>봇 한 마리를 진짜 커널로 날린 결과.</summary>
        private readonly struct BotFlight
        {
            public readonly bool Reached;
            public readonly bool Touched;
            public readonly float FarthestX;
            public readonly int FlapCount;
            public readonly int Ticks;

            public BotFlight(bool reached, bool touched, float farthestX, int flapCount, int ticks)
            {
                Reached = reached;
                Touched = touched;
                FarthestX = farthestX;
                FlapCount = flapCount;
                Ticks = ticks;
            }
        }

        //  앞을 이만큼 내다본다. 전진 11에서 약 1.5m — 프로토타입이 쓰던 앞보기 거리와 같은 취지다.
        private const float BotLookaheadSeconds = 0.14f;

        //  봇을 진짜 커널로 날린다. 궤적이 하나뿐이라 상태를 묶을 이유가 없고, 그래서 반올림도
        //  표류도 생기지 않는다 — 전수 탐색이 못 하는 "증명"이 여기서 나온다.
        //  한 번이라도 닿으면(스턴이 걸리면) 무충돌이 아니므로 즉시 멈춘다.
        private static BotFlight FlyBot(Vector3 start, float finishX, in FlappyShape shape, int mapMask,
                                        GameFramework.Physics.ICollisionQuery inner,
                                        System.Func<float, float, bool> isBlocked)
        {
            var query = new HitWatcher(inner);
            var state = new BirdState { Position = new Vector3(start.x, start.y, 0f) };
            float flapArc = LOP.MapTools.BotPilot.FlapArc(shape.FlapImpulse, shape.Gravity, TickSeconds);
            float lookahead = shape.ForwardSpeed * BotLookaheadSeconds;
            int buckets = Mathf.CeilToInt((SearchMaxY - SearchMinY) / HeightGrid) + 1;
            var blocked = new bool[buckets];
            float farthest = start.x;
            int flaps = 0;

            //  코스 길이보다 넉넉히 잡는다. 봇이 제자리에 갇히면 여기서 끝난다.
            int limit = Mathf.CeilToInt((finishX - start.x) / (shape.ForwardSpeed * TickSeconds)) + 600;
            for (int tick = 0; tick < limit; tick++)
            {
                float scanX = state.Position.x + lookahead;
                for (int i = 0; i < buckets; i++)
                {
                    blocked[i] = isBlocked(scanX, SearchMinY + i * HeightGrid);
                }

                var decision = LOP.MapTools.BotPilot.Decide(blocked, SearchMinY, HeightGrid,
                                                            state.Position.y, state.VerticalSpeed,
                                                            shape.Radius, flapArc);
                if (decision.Flap)
                {
                    flaps++;
                }

                state = Step(state, decision.Flap, shape, mapMask, query);
                if (state.Position.x > farthest)
                {
                    farthest = state.Position.x;
                }
                if (state.Stun > 0f)
                {
                    return new BotFlight(false, true, farthest, flaps, tick + 1);
                }
                if (state.Position.x + shape.Radius >= finishX)
                {
                    return new BotFlight(true, false, farthest, flaps, tick + 1);
                }
            }
            return new BotFlight(false, false, farthest, flaps, limit);
        }
```

`SearchMinY` / `SearchMaxY`는 아직 없다. `Check()`가 `bounds`에서 계산해 쓰던 값을 **필드로 올려** 봇과 탐색이 같은 대역을 보게 한다 — 두 답이 다른 대역에서 나오면 비교가 성립하지 않는다. `Check()` 안에서 `bounds`를 읽은 직후에 대입하고, 두 곳(`CleanRunOptions`의 `minY`/`maxY`, 리포트의 대역 표기)이 그 필드를 쓰게 바꾼다.

- [ ] **Step 2: 컴파일만 확인한다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

Expected: `failed:false`. 이 단계는 아직 아무도 `FlyBot`을 부르지 않으므로 테스트가 늘지 않는다.

- [ ] **Step 3: 전체 스위트로 회귀가 없는지 본다**

```bash
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --async_tests true --timeout 600
unity cmd test_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

Expected: Task 1 이후 개수 그대로, 0 failed. `SearchMinY`/`SearchMaxY` 승격이 기존 동작을 바꾸지 않았는지가 여기서 걸린다.

- [ ] **Step 4: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/MapTools/BotPilot.cs Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs
git status --short
git commit -m "$(cat <<'MSG'
feat(editor): 봇을 진짜 커널로 날리는 루프

궤적이 하나뿐이라 상태를 묶을 이유가 없고, 그래서 반올림도 표류도 생기지 않는다 —
전수 탐색이 끝내 못 한 "증명"이 여기서 나온다.

한 번이라도 닿으면 즉시 멈춘다. 스턴을 안고 계속 날면 "통과는 했지만 부딪혔다"가 되어
①이 묻는 질문과 달라진다.

탐색 대역을 필드로 올려 봇과 탐색이 같은 범위를 보게 했다 — 대역이 다르면 두 답을 비교할 수 없다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Y7nzicQU3Pxb8ng5XntfD6
MSG
)"
```

---

## Task 3: 형상 규칙 — 앞으로 기운 덮개

**Files:**
- Create: `Assets/Scripts/MapTools/TrapShapeRules.cs`
- Test: `Assets/Tests/EditMode/MapTools/TrapShapeRulesTests.cs`

**Interfaces:**
- Produces:
  - `LOP.MapTools.SurfaceSample` — `readonly struct { Vector3 Point; Vector3 Normal; }` (`UnityEngine.Vector3`)
  - `LOP.MapTools.ITrapShapeRule` — `string Name { get; }`, `bool IsSuspect(in SurfaceSample sample)`
  - `LOP.MapTools.ForwardOverhangRule : ITrapShapeRule` — 생성자 `ForwardOverhangRule(float minDownward = 0.3f, float minBackward = 0.1f)`
  - `LOP.MapTools.TrapShapeRules.Default` → `IReadOnlyList<ITrapShapeRule>`

**설계 메모 — 왜 이 규칙인가.** 이 프로젝트가 실측으로 알아낸 유일한 덫 모양이다. `KinematicMover`는 수평으로 미끄러진 뒤 **세로를 따로 한 번 쓸어 올린다.** 머리 위가 덮여 있으면 그 sweep이 몇 cm에서 막혀 세로 속도가 0이 되고, **아무리 눌러도 한 뼘도 못 오른다.** 전진은 상수라 계속 밀어붙이고 뒤로 갈 수단은 없다.

기하 조건은 **면의 법선**으로 표현된다. 새를 덮는 면의 법선은 아래를 향하고(`normal.y < 0`), 그 면이 *앞으로 기울어* 있으면 법선이 뒤쪽 성분을 갖는다(`normal.x < 0`, 전진이 +x이므로). 뒤로 기운 끝면은 법선이 앞쪽(`normal.x > 0`)이라 위로 열려 있고 넘어갈 수 있다 — 실측에서 1.68m 계단은 멀쩡했고 0.55m 계단이 덫이었다.

**규칙은 목록이다.** 맵이 아직 완성 형태가 아니므로 나중에 규칙을 더할 수 있어야 한다(결정 5). 새 규칙은 `ITrapShapeRule`을 구현해 `Default`에 더하면 되고, 훑기·씨앗 합치기·판정은 하나도 안 바뀐다.

---

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/EditMode/MapTools/TrapShapeRulesTests.cs`:

```csharp
using LOP.MapTools;
using NUnit.Framework;
using UnityEngine;

namespace LOP.MapTools.Tests
{
    public class TrapShapeRulesTests
    {
        static SurfaceSample Sample(Vector3 normal) => new SurfaceSample(Vector3.zero, normal.normalized);

        [Test]
        public void 앞으로_기울어_덮은_면은_의심한다()
        {
            //  아래를 향하면서(y<0) 뒤쪽 성분이 있는(x<0) 법선 = 앞으로 기운 덮개.
            Assert.IsTrue(new ForwardOverhangRule().IsSuspect(Sample(new Vector3(-0.5f, -0.8f, 0f))));
        }

        [Test]
        public void 뒤로_기운_끝면은_의심하지_않는다()
        {
            //  실측: 같은 크기라도 뒤로 기울면 위로 열려 있어 넘어간다(1.68m 계단은 멀쩡했다).
            Assert.IsFalse(new ForwardOverhangRule().IsSuspect(Sample(new Vector3(0.5f, -0.8f, 0f))));
        }

        [Test]
        public void 평평한_천장은_의심하지_않는다()
        {
            //  똑바로 아래를 보는 면은 앞으로 기운 게 아니다 — 부딪혀도 옆으로 미끄러져 빠진다.
            Assert.IsFalse(new ForwardOverhangRule().IsSuspect(Sample(new Vector3(0f, -1f, 0f))));
        }

        [Test]
        public void 바닥은_의심하지_않는다()
        {
            Assert.IsFalse(new ForwardOverhangRule().IsSuspect(Sample(new Vector3(-0.3f, 1f, 0f))));
        }

        [Test]
        public void 거의_수직인_벽은_의심하지_않는다()
        {
            //  아래 성분이 거의 없으면 덮개가 아니라 벽이다.
            Assert.IsFalse(new ForwardOverhangRule().IsSuspect(Sample(new Vector3(-1f, -0.05f, 0f))));
        }

        [Test]
        public void 기본_규칙_목록에_덮개_규칙이_들어_있다()
        {
            //  규칙을 더할 수 있는 구조라는 것 자체를 못박는다 — 목록이 비면 씨앗이 하나도 안 는다.
            Assert.IsNotEmpty(TrapShapeRules.Default);
            CollectionAssert.Contains(System.Linq.Enumerable.ToList(
                System.Linq.Enumerable.Select(TrapShapeRules.Default, r => r.Name)), "앞으로 기운 덮개");
        }
    }
}
```

- [ ] **Step 2: 컴파일해 실패를 확인한다**

Expected: `failed:true` — `SurfaceSample` / `ForwardOverhangRule` / `TrapShapeRules` 타입이 없다.

- [ ] **Step 3: 구현한다**

`Assets/Scripts/MapTools/TrapShapeRules.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace LOP.MapTools
{
    /// <summary>지형 표면의 한 점과 그 자리 법선. 콜라이더 종류를 모른다.</summary>
    public readonly struct SurfaceSample
    {
        public readonly Vector3 Point;
        public readonly Vector3 Normal;

        public SurfaceSample(Vector3 point, Vector3 normal)
        {
            Point = point;
            Normal = normal;
        }
    }

    /// <summary>
    /// "이 모양은 덫이 될 만한가"를 표면 하나로 판단한다. <b>확정하지 않는다</b> — 여기서 걸린
    /// 자리는 굴려 보기의 씨앗이 될 뿐이고, 낌인지 아닌지는 기존 판정이 정한다.
    /// 규칙을 더하려면 이 인터페이스를 구현해 <see cref="TrapShapeRules.Default"/>에 넣는다.
    /// </summary>
    public interface ITrapShapeRule
    {
        string Name { get; }
        bool IsSuspect(in SurfaceSample sample);
    }

    /// <summary>
    /// 머리 위를 <b>앞으로 기울어</b> 덮은 면. 이 프로젝트가 실측으로 찾아낸 덫 모양이다.
    ///
    /// <para>이동 커널은 수평으로 미끄러진 뒤 세로를 따로 한 번 쓸어 올린다. 위가 덮여 있으면 그
    /// sweep이 몇 cm에서 막혀 세로 속도가 0이 되고, 아무리 눌러도 오르지 못한다. 전진은 상수라
    /// 계속 밀어붙이고 뒤로 갈 수단은 없다. 뒤로 기운 끝면은 위로 열려 있어 안전하다 —
    /// 실측에서 1.68m 계단은 멀쩡했고 0.55m 계단이 덫이었다. 크기가 아니라 기울기다.</para>
    /// </summary>
    public sealed class ForwardOverhangRule : ITrapShapeRule
    {
        private readonly float minDownward;
        private readonly float minBackward;

        /// <param name="minDownward">법선이 이만큼은 아래를 봐야 덮개로 친다. 벽을 걸러낸다.</param>
        /// <param name="minBackward">법선이 이만큼은 뒤를 봐야 "앞으로 기운" 것이다. 평평한 천장을 걸러낸다.</param>
        public ForwardOverhangRule(float minDownward = 0.3f, float minBackward = 0.1f)
        {
            this.minDownward = minDownward;
            this.minBackward = minBackward;
        }

        public string Name => "앞으로 기운 덮개";

        //  전진이 +x이므로, 뒤를 향하는 법선은 x가 음수다.
        public bool IsSuspect(in SurfaceSample sample)
            => sample.Normal.y <= -minDownward && sample.Normal.x <= -minBackward;
    }

    public static class TrapShapeRules
    {
        /// <summary>오늘 쓰는 규칙. 새 모양이 드러나면 여기에 더한다.</summary>
        public static readonly IReadOnlyList<ITrapShapeRule> Default =
            new ITrapShapeRule[] { new ForwardOverhangRule() };
    }
}
```

- [ ] **Step 4: 컴파일하고 테스트를 돌린다**

Expected: 6 passed, 0 failed.

- [ ] **Step 5: 테스트가 실제로 실패할 수 있는지 확인한다**

`IsSuspect`의 `sample.Normal.x <= -minBackward`를 `sample.Normal.x >= minBackward`로 뒤집는다(앞/뒤를 반대로) → recompile → 재실행.

Expected: `앞으로_기울어_덮은_면은_의심한다`와 `뒤로_기운_끝면은_의심하지_않는다`가 **둘 다 빨강이어야 한다.** 이 두 방향을 구분하는 것이 이 규칙의 전부이므로, 하나만 깨지면 나머지 하나는 아무것도 안 지키는 것이다. 확인 후 되돌리고 초록을 다시 확인한다.

- [ ] **Step 6: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/MapTools/TrapShapeRules.cs Assets/Scripts/MapTools/TrapShapeRules.cs.meta Assets/Tests/EditMode/MapTools/TrapShapeRulesTests.cs Assets/Tests/EditMode/MapTools/TrapShapeRulesTests.cs.meta
git status --short
git commit -m "$(cat <<'MSG'
feat(maptools): 덫이 될 만한 지형 모양을 법선으로 가린다

이 프로젝트가 실측으로 찾아낸 덫은 하나다 — 머리 위를 앞으로 기울어 덮은 면. 커널이
세로를 따로 쓸어 올리는 탓에 날갯짓이 통째로 무력해진다. 뒤로 기운 끝면은 위로 열려
있어 안전하다: 1.68m 계단은 멀쩡했고 0.55m 계단이 덫이었다. 크기가 아니라 기울기다.

규칙을 목록으로 둔다. 맵이 아직 완성 형태가 아니라 새 모양이 나올 수 있고, 그때
인터페이스 하나 구현해 목록에 넣으면 훑기도 판정도 안 바뀐다.

확정하지 않는다 — 여기서 걸린 자리는 굴려 보기의 씨앗이 될 뿐이다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Y7nzicQU3Pxb8ng5XntfD6
MSG
)"
```

---

## Task 4: 표면을 훑어 씨앗을 더한다

**Files:**
- Modify: `Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs`

**Interfaces:**
- Consumes: `SurfaceSample`, `ITrapShapeRule`, `TrapShapeRules.Default` (Task 3)
- Produces (같은 파일 내부): `private static List<(float X, float Y)> ShapeSeeds(in Bounds bounds, in FlappyShape shape, int mapMask, out int sampleCount, out int suspectCount)`

**설계 메모 — 왜 레이캐스트인가.** 지금 맵은 `BoxCollider` 119개지만 나중에 메시가 올 수 있다. `Physics.Raycast`는 **콜라이더 종류를 가리지 않고 표면의 법선을 준다.** 박스 면을 직접 읽으면 오늘은 정확하지만 메시에서 다시 짜야 한다 — 결정 5가 막으려는 것이 그것이다.

게임이 x-y 평면 한 장이므로(코스의 z는 0) 훑기도 그 평면에서 한다. 격자의 각 점에서 **네 방향(위·아래·앞·뒤)으로 짧은 레이**를 쏴 맞은 면의 법선을 모은다. 규칙에 걸린 표면점은 **그 면에서 살짝 떨어진 자리**를 씨앗으로 낸다 — 표면 안쪽은 새가 있을 수 없는 자리다.

씨앗은 기존 1단계 후보에 **더해질** 뿐이다. 판정은 그대로다(결정 4).

---

- [ ] **Step 1: 훑기를 더한다**

```csharp
        //  표면을 훑는 격자. 낌 스캔의 격자(0.2m)보다 촘촘히 본다 — 격자에 안 걸리는 자리를
        //  찾는 것이 이 훑기의 목적이라, 같은 간격으로 보면 아무것도 더 못 찾는다.
        private const float ShapeScanStep = 0.1f;
        //  표면을 찾으려고 쏘는 레이의 길이. 격자 한 칸보다 조금 길게 잡아 사이가 비지 않게 한다.
        private const float ShapeRayLength = 0.15f;
        //  규칙에 걸린 면에서 이만큼 떨어진 자리를 씨앗으로 낸다. 표면 안쪽은 새가 못 있는 자리다.
        private const float ShapeSeedOffset = 0.5f;

        //  콜라이더 종류를 가리지 않고 표면 법선을 모은다 — 지금은 BoxCollider뿐이지만 메시가
        //  와도 같은 코드가 돈다. 규칙에 걸린 자리를 낌 스캔의 씨앗으로 낸다(확정하지 않는다).
        private static List<(float X, float Y)> ShapeSeeds(in Bounds bounds, in FlappyShape shape,
                                                           int mapMask, out int sampleCount,
                                                           out int suspectCount)
        {
            var seeds = new List<(float, float)>();
            var directions = new[] { Vector3.up, Vector3.down, Vector3.right, Vector3.left };
            var rules = LOP.MapTools.TrapShapeRules.Default;
            sampleCount = 0;
            suspectCount = 0;

            for (float x = bounds.min.x; x <= bounds.max.x; x += ShapeScanStep)
            {
                for (float y = bounds.min.y; y <= bounds.max.y; y += ShapeScanStep)
                {
                    var origin = new Vector3(x, y, 0f);
                    for (int d = 0; d < directions.Length; d++)
                    {
                        if (Physics.Raycast(origin, directions[d], out RaycastHit hit, ShapeRayLength,
                                            mapMask, QueryTriggerInteraction.Ignore) == false)
                        {
                            continue;
                        }
                        sampleCount++;
                        var sample = new LOP.MapTools.SurfaceSample(hit.point, hit.normal);
                        for (int r = 0; r < rules.Count; r++)
                        {
                            if (rules[r].IsSuspect(sample) == false)
                            {
                                continue;
                            }
                            suspectCount++;
                            //  덮개 아래쪽 — 새가 실제로 갇히는 자리다.
                            var seed = hit.point + hit.normal * ShapeSeedOffset;
                            seeds.Add((seed.x, seed.y));
                            break;
                        }
                    }
                }
            }
            return seeds;
        }
```

- [ ] **Step 2: 낌 스캔이 씨앗을 받게 한다**

`ScanTraps`의 1단계 격자 루프가 끝난 **직후**, 2단계가 시작되기 **전**에 아래를 넣는다. 2단계(날갯짓 탐색)는 한 줄도 손대지 않는다 — 씨앗이 늘어난 만큼 자동으로 더 검사된다.

```csharp
            //  격자에 안 걸린 자리를 형상으로 찾아 씨앗에 더한다. 판정은 아래 2단계가 그대로 한다 —
            //  여기서 하는 일은 "어디서부터 굴려 볼까"를 늘리는 것뿐이다.
            var shapeSeeds = ShapeSeeds(bounds, shape, mapMask, out int shapeSamples, out int shapeSuspects);
            int shapeAdded = 0;
            for (int i = 0; i < shapeSeeds.Count; i++)
            {
                if (AlreadyNear(candidates, shapeSeeds[i], GridStep))
                {
                    continue;
                }
                if (Escapes(new Vector3(shapeSeeds[i].X, shapeSeeds[i].Y, 0f), shape, mapMask, query))
                {
                    continue;   // 무입력으로 빠져나가면 후보가 아니다 — 격자 씨앗과 같은 기준이다
                }
                candidates.Add(shapeSeeds[i]);
                shapeAdded++;
            }
```

같은 파일에 헬퍼를 더한다:

```csharp
        //  이미 잡힌 후보 옆에 또 씨앗을 뿌리면 같은 주머니를 여러 번 굴리게 된다.
        private static bool AlreadyNear(List<(float X, float Y)> taken, (float X, float Y) seed, float within)
        {
            for (int i = 0; i < taken.Count; i++)
            {
                if (Mathf.Abs(taken[i].X - seed.X) <= within && Mathf.Abs(taken[i].Y - seed.Y) <= within)
                {
                    return true;
                }
            }
            return false;
        }
```

`BuildTrapSection`에 훑기 결과를 실어 요약 줄에 찍는다 — 인자 셋(`shapeSamples`, `shapeSuspects`, `shapeAdded`)을 더하고 한 줄을 추가한다:

```csharp
            text.AppendLine($"  형상 훑기: 표면 {shapeSamples}곳 중 {shapeSuspects}곳이 규칙에 걸렸고"
                          + $" {shapeAdded}곳을 씨앗으로 더했다");
```

**이 줄이 필요한 이유:** 규칙이 아무것도 못 잡고 있어도(0곳) 리포트는 여전히 "낀 자리 없음"으로 끝난다. 숫자를 찍어야 규칙이 죽어 있는 것과 맵이 깨끗한 것을 구분할 수 있다.

- [ ] **Step 3: 컴파일하고 전체 스위트를 돌린다**

```bash
export PATH="$PATH:$HOME/.unity/bin"
unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd recompile_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --async_tests true --timeout 600
unity cmd test_status --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
```

Expected: Task 3 이후 개수 그대로, 0 failed. **기존 낌 테스트가 하나라도 빨강이면 판정 로직을 건드린 것이다.**

- [ ] **Step 4: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs
git status --short
git commit -m "$(cat <<'MSG'
feat(editor): 지형 모양으로 찾은 자리를 낌 스캔 씨앗에 더한다

기존 스캔은 0.2m 격자에 씨앗을 뿌리는데, 그보다 작은 주머니엔 씨앗이 안 떨어진다.
굴려 보는 판정은 정확하지만 어디서부터 굴릴지를 격자가 정한다 — 그 구멍을 메운다.

레이캐스트로 표면 법선을 모은다. 콜라이더 종류를 가리지 않으므로 지금의 BoxCollider든
나중의 메시든 같은 코드가 돈다.

판정 로직은 한 줄도 안 고쳤다. 씨앗만 늘어난다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Y7nzicQU3Pxb8ng5XntfD6
MSG
)"
```

---

## Task 5: ①을 봇 먼저로 바꾸고 리포트에 싣는다

**Files:**
- Modify: `Assets/Scripts/MapTools/PlayabilityReport.cs`
- Modify: `Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs`
- Test: `Assets/Tests/EditMode/MapTools/PlayabilityReportTests.cs`

**Interfaces:**
- Consumes: `BotFlight`/`FlyBot` (Task 2), 기존 `CleanRunSearch.Run`, `VerifyByReplay`
- Produces: `SpawnCleanRun`에 필드 둘 추가 — `public readonly bool BotReached;`, `public readonly int BotFlaps;`. 생성자는 `SpawnCleanRun(string name, float y, CleanRunResult result, bool verifiedByReplay, bool botReached, int botFlaps)`

**설계 메모 — 자리마다 순서.** 각 스폰에 대해 **봇을 먼저** 날린다. 통과하면 그 자리는 끝이다(증명됨). 실패한 자리에만 전수 탐색을 돌린다. 그래서 정상적인 맵에서는 탐색이 아예 안 돌고 검사가 몇 분에서 몇 초가 된다.

세 결과를 글리프로 가른다:

```
✅  봇이 통과했다                      → 증명됨
❌  봇 실패 + 탐색도 경로를 못 찾음     → 이 자리는 불가능
🟡  봇 실패 + 탐색은 찾음(증명 못 함)   → 모름 (봇 한계일 수 있다)
```

---

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`PlayabilityReportTests.cs`에 추가한다(기존 픽스처는 새 생성자에 맞춰 `botReached: false, botFlaps: 0`을 더한다):

```csharp
        [Test]
        public void 봇이_통과한_자리는_증명된_것으로_찍는다()
        {
            string report = Build(new SpawnCleanRun("PlayerSpawn_1", -6f,
                new CleanRunResult(true, new bool[0], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: true, botFlaps: 176));

            Assert.IsTrue(report.Contains("✅", System.StringComparison.Ordinal));
            Assert.IsFalse(report.Contains("🟡", System.StringComparison.Ordinal));
            //  봇이 통과했으면 그 자리엔 탐색을 돌리지 않았다는 사실이 읽혀야 한다.
            StringAssert.Contains("봇 통과", report);
            StringAssert.Contains("176", report);
        }

        [Test]
        public void 봇도_탐색도_실패하면_불가능으로_찍는다()
        {
            string report = Build(new SpawnCleanRun("PlayerSpawn_4", 9f,
                new CleanRunResult(false, new bool[0], 38.2f, 31f, 34, 0.6f),
                verifiedByReplay: false, botReached: false, botFlaps: 51));

            Assert.IsTrue(report.Contains("❌", System.StringComparison.Ordinal));
            Assert.IsFalse(report.Contains("✅", System.StringComparison.Ordinal));
        }

        [Test]
        public void 봇은_실패했는데_탐색이_찾으면_모름으로_찍는다()
        {
            string report = Build(new SpawnCleanRun("PlayerSpawn_2", -1f,
                new CleanRunResult(true, new bool[191], 0f, 0f, 0, 0f),
                verifiedByReplay: false, botReached: false, botFlaps: 51));

            Assert.IsTrue(report.Contains("🟡", System.StringComparison.Ordinal));
            Assert.IsFalse(report.Contains("✅", System.StringComparison.Ordinal));
            //  이 상태의 뜻이 "봇 한계일 수 있다"라는 것이 글로 남아야 한다.
            StringAssert.Contains("봇이 못 간 것", report);
        }
```

- [ ] **Step 2: 컴파일해 실패를 확인한다**

Expected: `failed:true` — `SpawnCleanRun`에 `botReached`/`botFlaps` 인자가 없다.

- [ ] **Step 3: `SpawnCleanRun`과 리포트를 고친다**

`PlayabilityReport.cs`의 `SpawnCleanRun`을 이렇게 넓힌다:

```csharp
    public readonly struct SpawnCleanRun
    {
        public readonly string Name;
        public readonly float Y;
        public readonly CleanRunResult Result;
        public readonly bool VerifiedByReplay;
        /// <summary>봇이 진짜 커널로 끝까지 갔는가. true면 이 자리는 증명된 것이다.</summary>
        public readonly bool BotReached;
        public readonly int BotFlaps;

        public SpawnCleanRun(string name, float y, CleanRunResult result, bool verifiedByReplay,
                             bool botReached, int botFlaps)
        {
            Name = name;
            Y = y;
            Result = result;
            VerifiedByReplay = verifiedByReplay;
            BotReached = botReached;
            BotFlaps = botFlaps;
        }
    }
```

그리고 자리 줄을 세 갈래로 나눈다:

```csharp
                if (run.BotReached)
                {
                    text.AppendLine($"  {run.Name} (y={run.Y:F0})   ✅  봇 통과 · 날갯짓 {run.BotFlaps}회");
                }
                else if (run.Result.Reachable == false)
                {
                    // 기존 ❌ 줄 (막힌 x, 최협 회랑)
                }
                else
                {
                    text.AppendLine($"  {run.Name} (y={run.Y:F0})   🟡  봇이 못 감 · 탐색은 경로를 찾음"
                                  + $" (날갯짓 {CountFlaps(run.Result)}회) — 증명 못 함");
                }
```

캐비어트 줄의 🟡 설명도 새 뜻에 맞춘다: 봇이 못 갔다는 것이 맵이 불가능하다는 뜻은 아니고 **봇이 못 간 것**일 수 있으며, 탐색은 경로를 찾았으나 반올림 때문에 증명은 못 한다는 것.

- [ ] **Step 4: `Check()`를 봇 먼저로 바꾼다**

`Check()`의 ① 루프를 이렇게 바꾼다. `grid.IsFree`를 뒤집어 봇의 막힘 판정으로 넘긴다 — 봇과 탐색이 **같은 자유공간**을 봐야 두 답을 비교할 수 있다.

```csharp
                for (int i = 0; i < spawns.Count; i++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar("Flappy 맵 검사 (1/3 클린런)",
                            $"{spawns[i].Name} — 봇 비행", i / (float)spawns.Count))
                    {
                        cleanRunCancelNote = $"클린런 — 스폰 {i}/{spawns.Count}개만 검사됨";
                        break;
                    }

                    //  봇이 통과하면 진짜 물리로 끝까지 간 궤적이 있으므로 증명이다 — 탐색을 안 돌린다.
                    BotFlight flight = FlyBot(spawns[i].Position, finishX, shape, mapMask, query,
                                              (x, y) => grid.IsFree(x, y) == false);
                    if (flight.Reached)
                    {
                        cleanRuns.Add(new LOP.MapTools.SpawnCleanRun(
                            spawns[i].Name, spawns[i].Position.y,
                            new LOP.MapTools.CleanRunResult(true, System.Array.Empty<bool>(), 0f, 0f, 0, 0f),
                            verifiedByReplay: true, botReached: true, botFlaps: flight.FlapCount));
                        continue;
                    }

                    //  봇이 못 갔다. 맵이 불가능한 건지 봇이 못 한 건지는 전수 탐색만 가른다.
                    EditorUtility.DisplayProgressBar("Flappy 맵 검사 (1/3 클린런)",
                        $"{spawns[i].Name} — 봇 실패, 전수 탐색", i / (float)spawns.Count);
                    var options = new LOP.MapTools.CleanRunOptions(
                        startX: spawns[i].Position.x, startY: spawns[i].Position.y, finishX: finishX,
                        minY: SearchMinY, maxY: SearchMaxY,
                        forwardSpeed: shape.ForwardSpeed, flapImpulse: shape.FlapImpulse,
                        gravity: shape.Gravity, maxFallSpeed: shape.MaxFallSpeed,
                        tickSeconds: TickSeconds, heightGrid: HeightGrid);
                    var result = LOP.MapTools.CleanRunSearch.Run(options, grid.IsFree);
                    bool verified = result.Reachable
                        && VerifyByReplay(spawns[i].Position, result.Flaps, shape, mapMask, query);
                    cleanRuns.Add(new LOP.MapTools.SpawnCleanRun(
                        spawns[i].Name, spawns[i].Position.y, result, verified,
                        botReached: false, botFlaps: flight.FlapCount));
                }
```

**봇이 통과한 자리에 빈 `CleanRunResult`를 넣는 이유:** 탐색을 안 돌렸으므로 채울 값이 없다. 최협 회랑 세 자리가 0인 것은 이미 "측정 안 됨"의 신호이고(앞 슬라이스의 R11), 리포트는 `BotReached`가 참이면 그 필드를 아예 안 본다.

- [ ] **Step 5: 컴파일하고 전체 스위트를 돌린다**

Expected: Task 4 이후 개수 + 3, 0 failed.

- [ ] **Step 6: 테스트가 실제로 실패할 수 있는지 확인한다**

`run.BotReached` 분기의 조건을 `false`로 고정한다 → recompile → 재실행.

Expected: `봇이_통과한_자리는_증명된_것으로_찍는다`가 **빨강이어야 한다**(✅ 대신 🟡가 찍힌다). 되돌리고 초록을 확인한다.

- [ ] **Step 7: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add Assets/Scripts/MapTools/PlayabilityReport.cs Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs Assets/Tests/EditMode/MapTools/PlayabilityReportTests.cs
git status --short
git commit -m "$(cat <<'MSG'
feat: ①을 봇 먼저로 바꾼다 — 통과하면 그 자리는 증명 끝

봇이 통과하면 실제 물리로 끝까지 간 궤적이 있으므로 증명이다. 실패한 자리에만 전수
탐색을 돌려 "맵이 불가능"인지 "봇이 못 간 것"인지 가른다.

탐색을 지우지 않는다 — ❌를 보일 수 있는 유일한 수단이다. 봇은 "가능"만 증명한다.
정상적인 맵에서는 탐색이 아예 안 돌아 검사가 몇 분에서 몇 초가 된다.

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Y7nzicQU3Pxb8ng5XntfD6
MSG
)"
```

---

## Task 6: 실제 맵 실행 결과를 문서에 반영

**Files:**
- Modify: `docs/ROADMAP.md`
- Modify: `docs/superpowers/specs/2026-09-07-flappy-map-playability-check-design.md`

컨트롤러가 실제 맵에서 도구를 돌려 결과를 준다. **대괄호 값은 그 출력에서만 채운다 — 추측으로 쓰지 않는다.**

- [ ] **Step 1: 로드맵에 결과를 적는다**

`## ✅ Flappy 맵 플레이 가능성 검사 (2026-09-07)` 절 아래에 후속을 더한다: 봇이 자리마다 어떤 결과를 냈는지, 형상 훑기가 씨앗을 몇 개 더했고 그중 낌으로 확정된 것이 있는지, 그리고 ①이 이제 증명된 답을 내는지.

앞 절이 "①은 정직하게 미해결"로 끝나 있으므로, **그 문장이 더 이상 현재가 아니라면 그렇게 적는다** — 낡은 "미해결"을 남겨 두면 다음 사람이 이미 끝난 일을 다시 한다.

- [ ] **Step 2: spec의 정정 블록을 갱신한다**

`2026-09-07-…-design.md` §8의 정정 블록에 한 줄 더한다 — 재생 검증이 못 낸 증명을 봇이 어떻게 냈는지, 그리고 §12의 "눈금이 적절한가"가 이제 ①의 주된 질문이 아니라는 것.

- [ ] **Step 3: 커밋**

```bash
cd /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client
git status --short
git add docs/ROADMAP.md docs/superpowers/specs/2026-09-07-flappy-map-playability-check-design.md
git status --short
git commit -m "$(cat <<'MSG'
docs: 봇이 낸 증명과 형상 씨앗의 실측을 기록한다

Co-Authored-By: Claude Opus 5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Y7nzicQU3Pxb8ng5XntfD6
MSG
)"
```

---

## 마무리

모든 태스크가 끝나고 전체 EditMode가 초록이면 컨트롤러가 실제 맵에서 한 번 돌린다(Task 6의 입력). 그 뒤 푸시는 `CLAUDE.md`의 푸시 규약을 한 줄씩 밟는다 — `fetch` → `rebase --autostash origin/main` → `checkout main` → `merge --ff-only origin/main` → `merge --no-ff <feature>` → `push`. **`--force` 금지.** 클라 레포 하나만 올라간다.
