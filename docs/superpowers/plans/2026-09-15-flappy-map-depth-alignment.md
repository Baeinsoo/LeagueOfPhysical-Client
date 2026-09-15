# Flappy 맵 판정면 정렬 + 시각 정직성 검사 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 장애물이 판정면 뒤로 뻗어 화면이 실제보다 26~54cm 좁게 보이던 것을 없애고, 다시는 조용히 돌아오지 않도록 맵 검사가 그것을 재게 한다.

**Architecture:** 순수 커널(`VisualHonesty`)이 왜곡을 계산하고, 맵 검사가 씬에서 각 블록의 *판정면 뒤쪽 두께*를 재어 리포트에 찍는다. 그다음 에디터 도구가 블록을 제 뒤쪽 두께만큼 카메라 쪽으로 당겨 뒷면을 판정면에 맞춘다. 그 변경이 게임플레이 중립인지는 **검사를 다시 돌려 기대값과 대조해 증명**한다.

**Tech Stack:** Unity 2022 EditMode(NUnit), C#, 기존 `Assets/Scripts/MapTools/` 순수 커널 패턴

**Spec:** `docs/superpowers/specs/2026-09-15-flappy-map-25d-depth-alignment-design.md`

## Global Constraints

- 카메라 거리 **C = 30m** (`FlappyCameraFollow.fixedZ = -30`, 게임 평면 z = 0), 세로 FOV **40도**
- 화면 세로 반높이 = `30·tan(20°)` = **10.919106m**
- 새 몸 **r = 0.45**, h = 0.90. 새의 z대역 = **[−0.45, +0.45]**
- **판정에 관여하는 블록의 정의**: Default 레이어 콜라이더 중 **z범위가 새의 z대역과 겹치는 것.** 배경(z 60~64)은 안 겹치므로 자동 제외된다
- **뒤쪽 두께 d** = `collider.bounds.max.z − 0` (판정면보다 뒤로 뻗은 양)
- 왜곡 식: `파고드는 양 = h · d/(C+d)`
- 맵 씬은 **`Assets/Art/Scenes/FlappyRaceMap.unity`** (Art 서브모듈). 검사 스크립트는 **매번 이 씬을 직접 연다** — 게임 씬에서 돌리면 모달이 떠 에디터가 멎는다
- **배경(z=62)은 절대 건드리지 않는다**
- **테스트를 위해 어셈블리를 옮기지 않는다.** 순수 계산은 `Assets/Scripts/MapTools/`의 static 커널로 둔다(`StaticPinch`·`WindmillPhase`·`FreeSpaceGridMath`가 그 본보기)
- **통과만으로 검증됐다고 하지 않는다 — 일부러 깨뜨려 빨강을 확인한다**
- `StringAssert`를 이모지에 쓰지 않는다(문화권 비교라 없는 문자열도 통과) — `Ordinal` 비교를 쓴다
- 주석은 한국어·일상어로, **비자명한 의도(왜)**만
- `git add -A` / `git commit -a` 금지. 바꾼 파일만 경로로 지정하고 `git status --short`로 확인
- 로컬 픽스처 절대 스테이지 금지: `Assets/Art`, `Assets/UI/Theme/Fonts/Jua-Regular SDF.asset`, `ProjectSettings/PackageManagerSettings.asset`, `ProjectSettings/ProjectSettings.asset`
- `.meta` 파일은 함께 커밋한다. 손으로 만들지 않는다

### 긴 에디터 작업 (이걸 모르면 몇 시간 날린다)

- `unity` CLI를 쓰고 **항상 `--project-path`를 준다**
- **긴 작업은 `--detach`로 던지고 잡 ID로 회수한다.** `--timeout`은 CLI 쪽 대기일 뿐, 파이프라인은 메인 스레드를 **5초**만 기다린다
- 결과는 **`Logs/` 아래 파일로 쓴다.** 콘솔은 도메인 리로드에 지워지고, 클립보드는 detach에서 안 채워지고, `Temp/`는 리로드가 비운다
- `run_tests`는 **재컴파일하지 않는다.** 코드를 고쳤으면 `recompile --focus true` 먼저
- `eval_file`은 코드를 메서드 본문에 감싸므로 `using`이 안 된다 — **풀 네임스페이스로 쓴다**
- 에디터가 먹통이면 **모달이 떠 있는지 사람에게 물어본다.** 진행 표시줄 버튼은 **취소**다

---

## File Structure

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/MapTools/VisualHonesty.cs` (신규) | 순수 계산 — 뒤쪽 두께가 만드는 왜곡 |
| `Assets/Tests/EditMode/MapTools/VisualHonestyTests.cs` (신규) | 위 커널 테스트 |
| `Assets/Scripts/MapTools/BlockDepthScan.cs` (신규) | 씬에서 잰 블록 목록을 리포트용 자료로 정리(순수) |
| `Assets/Tests/EditMode/MapTools/BlockDepthScanTests.cs` (신규) | 위 정리 로직 테스트 |
| `Assets/Scripts/MapTools/PlayabilityReport.cs` (수정) | 🎥 시각 정직성 절 추가 |
| `Assets/Tests/EditMode/MapTools/PlayabilityReportTests.cs` (수정) | 그 절의 문구 테스트 |
| `Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs` (수정) | 씬에서 콜라이더를 훑어 `BlockDepthScan`에 넘김 |
| `Assets/Scripts/Editor/FlappyDepthAlignTool.cs` (신규) | 블록을 당겨 뒷면을 판정면에 맞추는 에디터 도구 |

---

## Task 1: 시각 정직성 커널

**Files:**
- Create: `Assets/Scripts/MapTools/VisualHonesty.cs`
- Test: `Assets/Tests/EditMode/MapTools/VisualHonestyTests.cs`

**Interfaces:**
- Consumes: (없음)
- Produces:
  - `float VisualHonesty.ScreenHalfHeight(float cameraDistance, float verticalFovDegrees)`
  - `float VisualHonesty.Intrusion(float trueHeight, float cameraDistance, float backDepth)`
  - `float VisualHonesty.ApparentHeight(float trueHeight, float cameraDistance, float backDepth)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/EditMode/MapTools/VisualHonestyTests.cs`:

```csharp
using NUnit.Framework;
using LOP.MapTools;

namespace LOP.Tests.MapTools
{
    public class VisualHonestyTests
    {
        //  지금 맵의 실제 값. 1.25/31.25가 정확히 0.04라 손으로 검산된다.
        [Test]
        public void 뒤쪽_두께가_만드는_파고듦은_높이에_비례한다()
        {
            Assert.AreEqual(0.26f, VisualHonesty.Intrusion(6.5f, 30f, 1.25f), 1e-4f);
            Assert.AreEqual(0.52f, VisualHonesty.Intrusion(13.0f, 30f, 1.25f), 1e-4f);
        }

        //  이 값이 틀리면 "h·d/C" 같은 흔한 오식과 구별이 안 된다(그 식이면 0.27083).
        [Test]
        public void 분모는_C가_아니라_C_더하기_d다()
        {
            float wrong = 6.5f * 1.25f / 30f;
            Assert.AreNotEqual(wrong, VisualHonesty.Intrusion(6.5f, 30f, 1.25f), 1e-4f);
        }

        [Test]
        public void 뒤쪽_두께가_0이면_파고듦도_0이다()
        {
            Assert.AreEqual(0f, VisualHonesty.Intrusion(10.9f, 30f, 0f), 1e-6f);
        }

        [Test]
        public void 화면_중앙에서는_파고듦이_없다()
        {
            Assert.AreEqual(0f, VisualHonesty.Intrusion(0f, 30f, 1.25f), 1e-6f);
        }

        //  z=1.4에 있던 4개(범위 0.10~2.70)가 가장 심하다 — 두께가 크면 더 파고든다.
        [Test]
        public void 두꺼울수록_더_파고든다()
        {
            float thin = VisualHonesty.Intrusion(6.5f, 30f, 1.25f);
            float thick = VisualHonesty.Intrusion(6.5f, 30f, 2.70f);
            Assert.Greater(thick, thin);
            Assert.AreEqual(0.5367f, thick, 1e-3f);
        }

        [Test]
        public void 보이는_높이는_판정면보다_중앙에_가깝다()
        {
            float apparent = VisualHonesty.ApparentHeight(6.5f, 30f, 1.25f);
            Assert.AreEqual(6.24f, apparent, 1e-4f);
            Assert.Less(apparent, 6.5f);
        }

        [Test]
        public void 화면_반높이는_거리와_시야각에서_나온다()
        {
            Assert.AreEqual(10.919106f, VisualHonesty.ScreenHalfHeight(30f, 40f), 1e-4f);
        }
    }
}
```

- [ ] **Step 2: 빨강을 확인한다**

Run: `unity cmd run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --filter VisualHonestyTests`
Expected: FAIL — `VisualHonesty`가 없다

- [ ] **Step 3: 커널을 쓴다**

`Assets/Scripts/MapTools/VisualHonesty.cs`:

```csharp
using System;

namespace LOP.MapTools
{
    /// <summary>
    /// 장애물이 판정면보다 <b>뒤로</b> 뻗어 있을 때 화면에서 틈이 얼마나 좁아 보이는지.
    ///
    /// <para>원근 카메라에서 멀리 있는 면은 소실점 쪽으로 당겨져 보인다. 장애물이 화면 위쪽에
    /// 있으면 그 당겨짐이 아래로 — 즉 틈 안으로 — 향하므로, 플레이어는 실제보다 좁은 틈을 본다.
    /// 통과 여유가 3~5cm인 맵에서는 이 오차가 여유보다 크다.</para>
    ///
    /// <para>상태 없는 순수 계산이라 <c>*System</c>이 아니라 static 커널이다
    /// (<see cref="StaticPinch"/>·<see cref="WindmillPhase"/>와 같은 짝).</para>
    /// </summary>
    public static class VisualHonesty
    {
        /// <summary>화면이 담는 세로 반높이(판정면 기준). 카메라가 보는 범위를 잰다.</summary>
        public static float ScreenHalfHeight(float cameraDistance, float verticalFovDegrees)
        {
            double halfAngle = verticalFovDegrees * 0.5 * Math.PI / 180.0;
            return (float)(cameraDistance * Math.Tan(halfAngle));
        }

        /// <summary>
        /// 뒷면이 화면에서 나타나는 높이. 소실점 쪽으로 <c>C/(C+d)</c>만큼 당겨진다.
        /// </summary>
        /// <param name="trueHeight">판정면에서의 진짜 높이(화면 중앙 기준).</param>
        /// <param name="backDepth">판정면보다 뒤로 뻗은 두께. 0이면 왜곡이 없다.</param>
        public static float ApparentHeight(float trueHeight, float cameraDistance, float backDepth)
        {
            return trueHeight * cameraDistance / (cameraDistance + backDepth);
        }

        /// <summary>
        /// 틈 안으로 파고들어 보이는 양. 이만큼 틈이 좁아 보인다.
        /// </summary>
        public static float Intrusion(float trueHeight, float cameraDistance, float backDepth)
        {
            return trueHeight * backDepth / (cameraDistance + backDepth);
        }
    }
}
```

- [ ] **Step 4: 초록을 확인한다**

Run: `unity cmd recompile --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --focus true`, 그다음 `run_tests --filter VisualHonestyTests`
Expected: PASS 7건

- [ ] **Step 5: 일부러 깨뜨려 빨강을 확인한다**

`Intrusion`의 분모를 `cameraDistance`로 바꿔 본다 → `분모는_C가_아니라_C_더하기_d다`와 `뒤쪽_두께가_만드는_파고듦은_높이에_비례한다`가 빨강이어야 한다. 확인 후 되돌린다.
`ApparentHeight`의 곱을 나눗셈으로 바꿔 본다 → `보이는_높이는_판정면보다_중앙에_가깝다`가 빨강이어야 한다. 되돌린다.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/MapTools/VisualHonesty.cs Assets/Scripts/MapTools/VisualHonesty.cs.meta Assets/Tests/EditMode/MapTools/VisualHonestyTests.cs Assets/Tests/EditMode/MapTools/VisualHonestyTests.cs.meta
git status --short
git commit -m "feat(flappy-map): 뒤쪽 두께가 틈을 얼마나 좁아 보이게 하는지 잰다"
```

---

## Task 2: 검사가 씬에서 재고 리포트에 찍는다

**Files:**
- Create: `Assets/Scripts/MapTools/BlockDepthScan.cs`
- Test: `Assets/Tests/EditMode/MapTools/BlockDepthScanTests.cs`
- Modify: `Assets/Scripts/MapTools/PlayabilityReport.cs`
- Modify: `Assets/Tests/EditMode/MapTools/PlayabilityReportTests.cs`
- Modify: `Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs`

**Interfaces:**
- Consumes: `VisualHonesty.Intrusion`, `VisualHonesty.ScreenHalfHeight` (Task 1)
- Produces:
  - `readonly struct BlockDepth { string Name; float X; float BackDepth; }` — 생성자 `BlockDepth(string name, float x, float backDepth)`
  - `readonly struct DepthVerdict { bool Honest; int Count; float WorstBackDepth; string WorstName; float WorstX; }`
  - `DepthVerdict BlockDepthScan.Judge(IReadOnlyList<BlockDepth> blocks, float tolerance)`
  - `PlayabilityReport.Build(...)`에 `IReadOnlyList<BlockDepth> blockDepths = null` 선택 인자 추가

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/EditMode/MapTools/BlockDepthScanTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using LOP.MapTools;

namespace LOP.Tests.MapTools
{
    public class BlockDepthScanTests
    {
        static List<BlockDepth> Blocks(params float[] depths)
        {
            var list = new List<BlockDepth>();
            for (int i = 0; i < depths.Length; i++)
            {
                list.Add(new BlockDepth($"Block{i}", i * 10f, depths[i]));
            }
            return list;
        }

        [Test]
        public void 전부_판정면에_맞으면_정직하다()
        {
            var v = BlockDepthScan.Judge(Blocks(0f, 0f, 0f), tolerance: 0.01f);
            Assert.IsTrue(v.Honest);
            Assert.AreEqual(0, v.Count);
        }

        //  허용오차 안쪽은 정직으로 본다 — 부동소수점 찌꺼기까지 경고하면 신호가 죽는다.
        [Test]
        public void 허용오차_안쪽은_세지_않는다()
        {
            var v = BlockDepthScan.Judge(Blocks(0.005f), tolerance: 0.01f);
            Assert.IsTrue(v.Honest);
            Assert.AreEqual(0, v.Count);
        }

        [Test]
        public void 뒤로_뻗은_것만_센다()
        {
            var v = BlockDepthScan.Judge(Blocks(0f, 1.25f, 0f, 2.70f), tolerance: 0.01f);
            Assert.IsFalse(v.Honest);
            Assert.AreEqual(2, v.Count);
        }

        //  가장 심한 것을 집어내야 고칠 자리를 안다. 개수만으로는 어디가 문제인지 모른다.
        [Test]
        public void 가장_두꺼운_것을_이름과_자리까지_집어낸다()
        {
            var v = BlockDepthScan.Judge(Blocks(1.25f, 2.70f, 0.50f), tolerance: 0.01f);
            Assert.AreEqual(2.70f, v.WorstBackDepth, 1e-4f);
            Assert.AreEqual("Block1", v.WorstName);
            Assert.AreEqual(10f, v.WorstX, 1e-4f);
        }

        [Test]
        public void 블록이_없으면_정직하다()
        {
            var v = BlockDepthScan.Judge(new List<BlockDepth>(), tolerance: 0.01f);
            Assert.IsTrue(v.Honest);
        }
    }
}
```

`Assets/Tests/EditMode/MapTools/PlayabilityReportTests.cs`에 덧붙인다:

```csharp
        //  ⚠️와 ✅는 뜻이 다르므로 글자도 달라야 한다. 이모지 검색은 Ordinal로 — 문화권
        //  비교(StringAssert)는 이모지가 없는 문자열도 통과시킨다.
        [Test]
        public void 뒤로_뻗은_블록이_있으면_리포트가_경고한다()
        {
            var blocks = new List<BlockDepth> { new BlockDepth("FillPinchTop", 370f, 1.25f) };
            string text = PlayabilityReport.Build(/* 기존 인자는 이 파일의 다른 테스트가 쓰는 헬퍼를 그대로 쓴다 */
                                                 blockDepths: blocks);
            Assert.That(text.IndexOf("시각 정직성", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(text.IndexOf("FillPinchTop", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(text.IndexOf("⚠️", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void 전부_맞았으면_경고하지_않는다()
        {
            var blocks = new List<BlockDepth> { new BlockDepth("FillPinchTop", 370f, 0f) };
            string text = PlayabilityReport.Build(blockDepths: blocks);
            Assert.That(text.IndexOf("시각 정직성", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(text.IndexOf("보이는 대로 부딪힌다", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
        }

        //  안 쟀을 때 절을 찍으면 "쟀는데 괜찮았다"로 잘못 읽힌다 — AppendPhaseSweep이 이미
        //  같은 이유로 그렇게 한다.
        [Test]
        public void 안_쟀으면_절_자체를_안_찍는다()
        {
            string text = PlayabilityReport.Build(blockDepths: null);
            Assert.That(text.IndexOf("시각 정직성", System.StringComparison.Ordinal), Is.LessThan(0));
        }
```

- [ ] **Step 2: 빨강을 확인한다**

Run: `unity cmd run_tests --project-path ... --mode EditMode --filter "BlockDepthScanTests|PlayabilityReportTests"`
Expected: FAIL — `BlockDepth`/`BlockDepthScan`이 없고 `Build`에 `blockDepths` 인자가 없다

- [ ] **Step 3: 순수 판정을 쓴다**

`Assets/Scripts/MapTools/BlockDepthScan.cs`:

```csharp
using System.Collections.Generic;

namespace LOP.MapTools
{
    /// <summary>판정에 관여하는 블록 하나가 판정면보다 뒤로 얼마나 뻗어 있나.</summary>
    public readonly struct BlockDepth
    {
        public readonly string Name;
        public readonly float X;
        /// <summary>판정면(z=0)보다 뒤로 뻗은 두께. 0이면 뒷면이 판정면에 맞아 있다.</summary>
        public readonly float BackDepth;

        public BlockDepth(string name, float x, float backDepth)
        {
            Name = name; X = x; BackDepth = backDepth;
        }
    }

    /// <summary>한 맵의 시각 정직성 판정.</summary>
    public readonly struct DepthVerdict
    {
        public readonly bool Honest;
        /// <summary>허용오차를 넘겨 뒤로 뻗은 블록 수.</summary>
        public readonly int Count;
        public readonly float WorstBackDepth;
        public readonly string WorstName;
        public readonly float WorstX;

        public DepthVerdict(bool honest, int count, float worstBackDepth, string worstName, float worstX)
        {
            Honest = honest; Count = count;
            WorstBackDepth = worstBackDepth; WorstName = worstName; WorstX = worstX;
        }
    }

    public static class BlockDepthScan
    {
        /// <param name="tolerance">이만큼까지는 맞은 것으로 본다. 부동소수점 찌꺼기로 경고가
        /// 뜨면 진짜 신호가 묻힌다.</param>
        public static DepthVerdict Judge(IReadOnlyList<BlockDepth> blocks, float tolerance)
        {
            int count = 0;
            float worst = 0f;
            string worstName = null;
            float worstX = 0f;

            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i].BackDepth <= tolerance)
                {
                    continue;
                }
                count++;
                if (blocks[i].BackDepth > worst)
                {
                    worst = blocks[i].BackDepth;
                    worstName = blocks[i].Name;
                    worstX = blocks[i].X;
                }
            }
            return new DepthVerdict(count == 0, count, worst, worstName, worstX);
        }
    }
}
```

- [ ] **Step 4: 리포트에 절을 붙인다**

`PlayabilityReport.Build`에 선택 인자 `IReadOnlyList<BlockDepth> blockDepths = null`을 추가하고, `AppendPhaseSweep` 호출부 근처에 `AppendVisualHonesty(text, blockDepths)`를 부른다. 절은 이렇게 찍는다:

```csharp
        //  ── 🎥 시각 정직성 ──────────────────────────────────────
        //  판정(①의 ✅/❌)과 섞지 않는다 — 이건 통과 가능성이 아니라 <읽기 쉬움>의 문제다.
        //  안 쟀으면 절 자체를 안 찍는다: 빈 절은 "쟀는데 괜찮았다"로 읽힌다.
        const float CameraDistance = 30f;      // FlappyCameraFollow.fixedZ = -30, 게임 평면 z=0
        const float VerticalFov = 40f;         // 씬 카메라 field of view
        const float DepthTolerance = 0.01f;

        static void AppendVisualHonesty(StringBuilder text, IReadOnlyList<BlockDepth> blocks)
        {
            if (blocks == null)
            {
                return;
            }

            float halfHeight = VisualHonesty.ScreenHalfHeight(CameraDistance, VerticalFov);
            text.AppendLine($"── 🎥 시각 정직성 (카메라 {CameraDistance:F0}m · FOV {VerticalFov:F0} → 화면 세로 ±{halfHeight:F2}m) ──");

            DepthVerdict v = BlockDepthScan.Judge(blocks, DepthTolerance);
            if (v.Honest)
            {
                text.AppendLine("  ✅ 판정면 뒤로 뻗은 블록 없음 — 보이는 대로 부딪힌다");
                text.AppendLine();
                return;
            }

            float mid = VisualHonesty.Intrusion(6.5f, CameraDistance, v.WorstBackDepth);
            float edge = VisualHonesty.Intrusion(halfHeight, CameraDistance, v.WorstBackDepth);
            text.AppendLine($"  ⚠️ 판정면 뒤로 뻗은 블록 {v.Count}개 — 가장 두꺼운 것 {v.WorstBackDepth:F2}m");
            text.AppendLine($"     {v.WorstName} (x={v.WorstX:F1})");
            text.AppendLine($"     그 블록 때문에 화면 중상단(h=6.5m)에서 {mid * 100f:F0}cm, 화면 끝에서 {edge * 100f:F0}cm 좁게 보인다");
            text.AppendLine("     → 블록을 제 뒤쪽 두께만큼 카메라 쪽으로 당기면 0이 된다 (LOP/Debug/Flappy 판정면 정렬)");
            text.AppendLine();
        }
```

- [ ] **Step 5: 검사가 씬에서 재도록 잇는다**

`FlappyMapPlayabilityCheck.cs`에서 맵 씬을 연 뒤, Default 레이어 콜라이더를 훑어 `BlockDepth` 목록을 만든다:

```csharp
        /// <summary>
        /// 판정에 관여하는 블록만 고른다 — 새의 z대역과 겹치는 콜라이더. 배경(z 60~64)은
        /// 안 겹치므로 자동으로 빠진다.
        /// </summary>
        private static List<LOP.MapTools.BlockDepth> ScanBlockDepths(float bodyRadius)
        {
            var result = new List<LOP.MapTools.BlockDepth>();
            foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (collider.gameObject.layer != LayerMask.NameToLayer("Default"))
                {
                    continue;
                }
                Bounds b = collider.bounds;
                if (b.max.z < -bodyRadius || b.min.z > bodyRadius)
                {
                    continue;   // 새가 지나는 두께를 안 건드린다 = 배경
                }
                float backDepth = Mathf.Max(0f, b.max.z);
                result.Add(new LOP.MapTools.BlockDepth(collider.name, b.center.x, backDepth));
            }
            return result;
        }
```

그 결과를 `PlayabilityReport.Build(..., blockDepths: ScanBlockDepths(config.BodyRadius))`로 넘긴다. **빠름 모드에서도 찍는다** — 훑기가 싸다(콜라이더 순회 한 번).

- [ ] **Step 6: 초록을 확인한다**

Run: `unity cmd recompile --focus true`, 그다음 `run_tests --mode EditMode`
Expected: 기존 1644 + 새 8 = **1652 전부 PASS**

- [ ] **Step 7: 일부러 깨뜨려 빨강을 확인한다**

- `Judge`의 `<=` 를 `<` 로 바꾼다 → `허용오차_안쪽은_세지_않는다`가 빨강
- `worst` 갱신 조건 `>` 를 `<` 로 바꾼다 → `가장_두꺼운_것을_이름과_자리까지_집어낸다`가 빨강
- `AppendVisualHonesty`의 `if (blocks == null) return;`을 지운다 → `안_쟀으면_절_자체를_안_찍는다`가 빨강
- 전부 확인 후 되돌린다

- [ ] **Step 8: 실제 맵에서 돌려 현재 상태를 기록한다**

빠름 모드로 검사를 돌린다(`--detach`, 결과는 `Logs/FlappyMapCheck.txt`). **⚠️가 떠야 하고**, 가장 두꺼운 것이 **2.70m**(z=1.4에 있던 4개)로 나와야 한다. 이 리포트를 보고에 첨부한다.

- [ ] **Step 9: 커밋**

```bash
git add Assets/Scripts/MapTools/BlockDepthScan.cs Assets/Scripts/MapTools/BlockDepthScan.cs.meta Assets/Scripts/MapTools/PlayabilityReport.cs Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs Assets/Tests/EditMode/MapTools/BlockDepthScanTests.cs Assets/Tests/EditMode/MapTools/BlockDepthScanTests.cs.meta Assets/Tests/EditMode/MapTools/PlayabilityReportTests.cs
git status --short
git commit -m "feat(flappy-map): 장애물이 판정면 뒤로 얼마나 뻗었는지를 검사가 잰다"
```

---

## Task 3: 판정면 정렬 도구 + 실제 정렬

**Files:**
- Create: `Assets/Scripts/Editor/FlappyDepthAlignTool.cs`
- Modify: `Assets/Art/Scenes/FlappyRaceMap.unity` (Art 서브모듈 — **따로 커밋한다**)

**Interfaces:**
- Consumes: Task 2의 블록 선별 규칙(새의 z대역과 겹치는 Default 콜라이더)
- Produces: 메뉴 `LOP/Debug/Flappy 판정면 정렬`

- [ ] **Step 1: 도구를 쓴다**

`Assets/Scripts/Editor/FlappyDepthAlignTool.cs`:

```csharp
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 장애물의 <b>뒷면</b>을 판정면(z=0)에 맞춘다. 두께는 그대로 두고 방향만 바꾼다 —
    /// 뒤로 뻗어 있으면 화면에서 틈이 좁아 보이는데, 앞으로 뻗으면 그 왜곡이 사라진다.
    ///
    /// <para>게임플레이는 안 바뀐다: 박스는 z방향으로 단면이 일정하고, 옮긴 뒤에도 새의
    /// z대역과 겹치므로 x/y 스윕이 재는 거리가 같다. 그래도 <b>믿지 말고 검사로 확인한다.</b></para>
    /// </summary>
    public static class FlappyDepthAlignTool
    {
        private const string MapScenePath = "Assets/Art/Scenes/FlappyRaceMap.unity";
        private const float BodyRadius = 0.45f;
        private const float Tolerance = 0.01f;

        [MenuItem("LOP/Debug/Flappy 판정면 정렬")]
        private static void Align()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                MapScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

            var moved = new List<string>();
            var skipped = new List<string>();

            foreach (var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (collider.gameObject.layer != LayerMask.NameToLayer("Default"))
                {
                    continue;
                }
                Bounds b = collider.bounds;
                if (b.max.z < -BodyRadius || b.min.z > BodyRadius)
                {
                    continue;   // 배경 — 새가 지나는 두께를 안 건드린다
                }
                if (b.max.z <= Tolerance)
                {
                    continue;   // 이미 맞아 있다
                }

                //  x나 y로 돌아간 조상이 있으면 z를 옮기는 것이 뜻대로 안 된다. 임의 판단하지
                //  말고 건너뛰고 보고한다.
                if (HasNonZRotation(collider.transform))
                {
                    skipped.Add($"{collider.name} (x/y 회전)");
                    continue;
                }

                Transform t = collider.transform;
                Undo.RecordObject(t, "판정면 정렬");
                Vector3 p = t.position;
                p.z -= b.max.z;
                t.position = p;
                EditorUtility.SetDirty(t);
                moved.Add($"{collider.name}  Δz={-b.max.z:F3}");
            }

            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();

            var log = new StringBuilder();
            log.AppendLine($"옮긴 블록 {moved.Count}개");
            foreach (string m in moved) { log.AppendLine("  " + m); }
            log.AppendLine($"건너뛴 블록 {skipped.Count}개");
            foreach (string s in skipped) { log.AppendLine("  " + s); }
            System.IO.File.WriteAllText("Logs/FlappyDepthAlign.txt", log.ToString());
            Debug.Log($"[판정면 정렬] 옮김 {moved.Count} · 건너뜀 {skipped.Count} — Logs/FlappyDepthAlign.txt");
        }

        /// <summary>z축 둘레 회전은 z 범위를 안 바꾸므로 괜찮다. x나 y로 돌면 얘기가 다르다.</summary>
        private static bool HasNonZRotation(Transform t)
        {
            for (Transform cur = t; cur != null; cur = cur.parent)
            {
                Vector3 e = cur.localEulerAngles;
                if (Mathf.Abs(Mathf.DeltaAngle(e.x, 0f)) > 0.01f) { return true; }
                if (Mathf.Abs(Mathf.DeltaAngle(e.y, 0f)) > 0.01f) { return true; }
            }
            return false;
        }
    }
}
```

- [ ] **Step 2: 컴파일을 확인한다**

Run: `unity cmd recompile --project-path ... --focus true`, 그다음 `recompile_status`
Expected: 에러 0. 기존 테스트 **1652 전부 PASS** (이 도구는 기존 로직을 안 건드린다)

- [ ] **Step 3: 정렬을 실행한다**

메뉴 `LOP/Debug/Flappy 판정면 정렬`을 `eval_file`로 부른다(`--detach`). `Logs/FlappyDepthAlign.txt`를 읽어 **옮긴 개수가 149 + 4 = 153 근처**인지, **건너뛴 것이 0인지** 확인한다.

**건너뛴 것이 있으면 멈추고 보고한다** — x/y로 돌아간 블록은 사람이 봐야 한다.

- [ ] **Step 4: 배경이 안 움직였는지 확인한다**

```bash
git -C Assets/Art diff --stat Scenes/FlappyRaceMap.unity
git -C Assets/Art diff Scenes/FlappyRaceMap.unity | grep -c "z: 62"
```
Expected: `z: 62`가 **변경 줄에 안 나타나야 한다**(배경은 그대로).

- [ ] **Step 5: Art 레포에 커밋한다**

```bash
git -C Assets/Art add Scenes/FlappyRaceMap.unity
git -C Assets/Art status --short
git -C Assets/Art commit -m "fix(flappy-map): 장애물 뒷면을 판정면에 맞춘다

판정면보다 뒤로 뻗은 두께가 화면에서 틈을 좁아 보이게 하고 있었다.
두께는 그대로 두고 방향만 카메라 쪽으로 돌린다."
```

**푸시는 하지 않는다** — Task 4의 증명이 통과한 뒤에 한다.

---

## Task 4: 중립성 증명 + 기록

**Files:**
- Modify: `docs/ROADMAP.md`

**Interfaces:**
- Consumes: Task 3의 정렬된 맵, Task 2의 🎥 절

- [ ] **Step 1: 전체 검사를 돌린다**

빠름 모드가 아니라 **전체 모드**로 돌린다(`--detach`, 결과 `Logs/FlappyMapCheck.txt`).

- [ ] **Step 2: 기대값과 대조한다**

**아래가 전부 같아야 한다. 하나라도 다르면 중립이 아니므로 거기서 멈추고 보고한다.**

| 확인 | 기대값 |
|---|---|
| 네 자리 판정 | ✅ ✅ ✅ ✅ |
| 날갯짓 수 | 105 / 110 / 102 / 102 |
| 회전 여유 | 0.15 / 0.10 / 0.10 / 0.21m |
| 최소 여유 | 0.03 / 0.03 / 0.05 / 0.04m |
| 🎥 시각 정직성 | **✅ 판정면 뒤로 뻗은 블록 없음** |

**날갯짓 수가 달라지면 경로 자체가 바뀐 것이다 — 되돌린다.**

> 미세한 차이가 나오면 **뒷면을 z=+0.05로 두는 것**을 먼저 시도한다(`FlappyDepthAlignTool`의
> `p.z -= b.max.z` 를 `p.z -= (b.max.z - 0.05f)` 로). 그때 생기는 왜곡은 h=6.5m에서 약 1cm로
> 통과 여유(3cm)보다 작다.

- [ ] **Step 3: 로드맵에 적는다**

`docs/ROADMAP.md`의 `## 📋 Flappy — spec들이 열어 둔 것 전수` 절 **바로 앞**에 새 절을 넣는다:

```markdown
### 화면이 실제보다 좁게 보여 주고 있었다 — 판정면 정렬 (2026-09-15)

맵에 입체감을 주려다 재 보니 **깊이는 이미 있었다.** 문제는 방향이었다 — 장애물이 판정면보다
**뒤로** 1.25m(일부 2.7m) 뻗어 있었고, 원근 카메라에서 그 뒷면이 소실점 쪽으로 당겨져
**틈 안으로 파고들어** 보였다.

```
파고드는 양 = h · d/(C+d)        C=30m(카메라), d=판정면 뒤쪽 두께
  h=6.5m  → 26cm (최악 54cm)
  화면 끝 → 44cm
```

**통과 여유가 3~5cm인 맵에서 오차가 여유의 5~10배였다.**

고친 방법은 블록을 **제 뒤쪽 두께만큼 카메라 쪽으로 당기는 것**이다. 두께는 하나도 안 줄고
방향만 바뀐다. 박스는 z방향으로 단면이 일정하고 새의 z대역과 여전히 겹치므로 x/y 스윕 결과가
같다 — **그 중립성은 논증이 아니라 검사 재실행으로 증명했다**(네 자리 ✅ 유지, 날갯짓
105/110/102/102 그대로, 여유도 동일).

그리고 **새 블록이 z=0에 놓이면 조용히 돌아오므로** 검사에 `🎥 시각 정직성` 절을 뒀다.
판정(①의 ✅/❌)과는 섞지 않는다 — 이건 통과 가능성이 아니라 읽기 쉬움의 문제다.

> **다음 (아직 안 함)** — 층 가르기. z=62에 배경 블록 64개가 이미 있다. 게임 평면은 선명·고대비·
> 밝은 테두리, 배경은 채도↓·안개·아웃포커스로 갈라 *"선명하면 닿는 것"*을 눈이 배우게 한다.
> 거의 모든 2.5D가 투영 방식과 무관하게 함께 쓰는 조각이다. spec `2026-09-15-flappy-map-25d-depth-alignment-design.md` §3②.
```

- [ ] **Step 4: 커밋한다**

```bash
git add docs/ROADMAP.md
git status --short
git commit -m "docs(roadmap): 화면이 실제보다 좁게 보여 주던 것을 고친 경위를 적는다"
```

- [ ] **Step 5: 두 레포를 푸시한다 — Art 먼저**

**Art 서브모듈이 먼저다.** 클라 포인터가 아직 없는 Art 커밋을 가리키면 다른 기계에서 맵이 깨진다.

```bash
git -C Assets/Art fetch origin
git -C Assets/Art rebase --autostash origin/main
git -C Assets/Art checkout main
git -C Assets/Art merge --ff-only origin/main
git -C Assets/Art merge --no-ff <feature>
git -C Assets/Art push origin main
```

그다음 클라를 `CLAUDE.md`의 푸시 규약대로 올린다(**한 줄씩 결과를 확인하고 넘어간다. `&&`로 잇지 않는다**). 클라 커밋에는 **Art 포인터 변경이 포함되어야 한다** — `Assets/Art`를 경로로 지정해 스테이지한다(이때만 예외이고, 다른 로컬 픽스처 셋은 여전히 금지).

---

## Self-Review

**1. Spec 커버리지**

| spec 절 | 어느 Task |
|---|---|
| §3① 판정면 정렬 | Task 3 |
| §3② 층 가르기 | **범위 밖 — 별도 슬라이스**(Task 4 Step 3의 "다음"에 기록) |
| §3③ 앞으로 더 두껍게 | **범위 밖** — ①② 후 화면을 보고 정한다(spec §8) |
| §3④ 시각 정직성 검사 | Task 1 · Task 2 |
| §4 중립성 증명 | Task 4 Step 2 |
| §7 z=1.4의 4개 | **해소** — "새의 z대역과 겹치는 콜라이더" 규칙이 이들을 장애물로 올바르게 분류한다(가장 두꺼운 2.7m) |
| §7 x/y 회전 | Task 3의 `HasNonZRotation` — 건너뛰고 보고 |
| §7 배경 보호 | Task 3 Step 4에서 확인 |

**2. 플레이스홀더**: 없음. 모든 코드 단계에 실제 코드가 있다.

**3. 타입 일관성**: `BlockDepth(string, float, float)` / `DepthVerdict` / `BlockDepthScan.Judge` / `VisualHonesty.Intrusion`이 Task 1→2→3에서 같은 이름·인자로 쓰인다. `BodyRadius 0.45`·`Tolerance 0.01f`·`CameraDistance 30f`가 Task 2·3에서 같은 값이다.
