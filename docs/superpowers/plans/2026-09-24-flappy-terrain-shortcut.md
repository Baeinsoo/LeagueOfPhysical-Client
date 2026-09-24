# Flappy 코스 — 뾰족한 지형 조각과 곧은 지름길 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 코스의 완만한 사인 물결을 뾰족한 U자·A자·계단 조각으로 바꾸고, 지름길이 있는 U자에서는 깊이 절반 높이를 수평으로 곧게 가로지르는 지름길(관문 0, 패드 1개)을 굽는다. 검사기는 "지름길 없이 완주"와 "지름길로 들어가서 완주"를 따로 증명한다.

**Architecture:** 순수 계층(`LOP.MapTools`, 에디터 전용 어셈블리)에 `CourseProfile`(꺾은선 높이 함수 + 평지 목록 + 지름길 사각형)과 `ShortcutRule`(금지 영역·패드 자리·리포트 절)을 둔다. 빌더는 그 결과대로 바닥·천장 경사 조각을 굽는데, 지름길 구간의 천장은 위쪽 상자 하나 + 아래쪽 "혀"(돌출 다각형 메시)로 바꾼다. 검사기는 탐색의 한 틱 전진 판정을 감싸 금지 영역을 막힌 것으로 취급한다 — 탐색 코드 자체는 안 바꾼다(단, 넓어진 높이 범위 때문에 살아 있는 상태만 훑게 최적화한다).

**Tech Stack:** Unity 6000.3 (C#), NUnit EditMode 테스트, `unity` CLI(`~/.unity/bin/unity`), GameFramework `DeterministicRandom`.

**Spec:** `docs/superpowers/specs/2026-09-24-flappy-terrain-shortcut-design.md`

## Global Constraints

- 내리막 기울기 **2.5**(≈68°), 오르막 기울기 구간별 **1.0 / 1.3 / 1.5**. 오르막 2.0은 초당 6탭 넘게 필요해 금지.
- U 바닥 길이 **16m**(spec의 12m에서 변경 — 아래 "판단" 참고), A 꼭대기 **12m**, 조각 사이 평지 ≥ **11.4m**(한 칸).
- 구간 표: 1 = U 15 / A 12 / 오르막 1.0 / 계단 없음 / 지름길 없음 · 2 = U 30 / A 15 / 1.3 / 계단 10 / 지름길 · 3 = U 40 / A 20 / 1.5 / 계단 10 / 지름길.
- 지름길 = 깊이 절반 높이, 세로 폭 = 창 **4.37m**, 안은 관문 0, 패드는 부스트(8.16m)가 **출구 1.5m 전**에 끝나는 자리.
- 벽(경사)에는 관문을 두지 않는다. 관문은 평지·U 바닥·A 꼭대기에서 끝으로부터 **2.5m** 안쪽에만.
- 런타임·서버·Shared는 **변경 없음**. 바뀌는 곳은 클라 `Assets/Scripts/MapTools/`, `Assets/Scripts/Editor/`, `Assets/Tests/EditMode/MapTools/`, 맵 씬(Art 서브모듈).
- `LOP.MapTools`는 **에디터 전용 asmdef** — 런타임 코드가 참조하면 플레이어 빌드만 깨진다. 이번 작업은 런타임에서 안 쓴다.
- 맵 씬에 **클라 전용 컴포넌트를 붙이지 않는다**(서버에서 missing script → 씬 주입이 끊긴다). 지름길 표시는 Transform만 있는 빈 GameObject로 한다.
- `.meta`는 유니티가 만든 것만 커밋. 파일 삭제는 `.cs`와 `.meta`를 함께 `git rm`.
- `git add -A` / `git commit -a` 금지. 커밋 전 `git status --short`로 스테이지 확인. 로컬 픽스처(`Assets/Art` 포인터 — 의도적으로 올릴 때 제외, `Jua-Regular SDF.asset`, `PackageManagerSettings.asset`, `ProjectSettings.asset`)는 스테이지 금지.
- main 직접 커밋 금지. 작업 브랜치: `feature/flappy-terrain-shortcut`(이미 있음). 푸시는 CLAUDE.md "푸시 규약" 순서로 한 줄씩.
- 플레이 모드를 임의로 끄지 않는다. 플레이 중에는 테스트를 보내지 않는다(큐가 막힌다) — `unity cmd editor_status`로 먼저 확인.
- 코스 굽기 뒤에는 **곧바로 `unity cmd save_scene`** — 안 하면 다음 도메인 리로드가 저장 대화상자를 띄워 에디터를 막는다.

### 명령 모음 (모든 태스크 공통)

```bash
export PATH="$PATH:$HOME/.unity/bin"
# 컴파일
unity cmd recompile; until timeout 50 unity cmd recompile_status 2>&1 | grep -qE '"completed"|"up_to_date"'; do sleep 6; done; timeout 50 unity cmd recompile_status
# 전체 EditMode 테스트 (결과는 Editor.log와 TestResults.xml로 읽는다 — test_status는 비어 올 때가 있다)
before=$(grep -c "Run finished" ~/Library/Logs/Unity/Editor.log); unity cmd run_tests --detach; until [ "$(grep -c 'Run finished' ~/Library/Logs/Unity/Editor.log)" -gt "$before" ]; do sleep 10; done; grep "Run finished" ~/Library/Logs/Unity/Editor.log | tail -1
python3 -c "
import re; s=open('/Users/insoobae/Library/Application Support/BSGames/LeagueOfPhysical-Client/TestResults.xml',encoding='utf-8',errors='replace').read()
[print('RED:', re.search(r'fullname=\"([^\"]+)\"', m.group(1)).group(1)) for m in re.finditer(r'<test-case([^>]*result=\"Failed\"[^>]*)>', s)]"
# 맵 씬 열기 · 굽기 · 저장
unity cmd open_scene Assets/Art/Scenes/FlappyRaceMap.unity
unity cmd menu "LOP/Debug/Flappy 전통 코스 굽기"; unity cmd save_scene
# 빠른 검사 (30분 이상 걸린다 — 백그라운드로 돌리고 Logs/FlappyMapCheck.txt의 수정 시각이 바뀌길 기다린다)
unity cmd menu "LOP/Debug/Flappy 맵 검사 (빠름 — 위상 훑기 건너뜀)"
```

### 이 플랜이 spec과 다르게 정한 것 (판단)

| 판단 | 이유 | 틀렸을 때 비용 |
|---|---|---|
| U 바닥 12m → **16m** | 관문을 끝에서 2.5m 안쪽에만 두면 12m 바닥엔 7m만 남아 관문이 거의 안 선다. 16m면 보통 1개 선다 | 상수 하나 |
| spec §7 "스폰 4자리 전부 지름길 없이" → 4자리 탐색은 지름길을 금지한 채 돌리고, 봇 ✅는 그대로 둔 뒤 **스폰 1의 지름길-없는 증명을 따로** 찍는다 | 봇은 지름길을 모르고 날아서, 봇 ✅가 지름길을 지났는지 가를 수 없다 | 절 하나 |
| 금지 영역 = 지름길 길이의 **가운데 절반** | 입구·출구 근처는 지름길 띠가 회랑 천장과 겹친다. 가운데 절반은 완전히 막힌 굴이라 두 길이 깨끗이 갈린다(테스트로 못박는다) | 판정 함수 하나 |

---

## File Structure

| 파일 | 할 일 |
|---|---|
| `Assets/Scripts/MapTools/CourseProfile.cs` (새) | `TerrainKind`, `SectionTerrain`, `FlatSpan`, `ShortcutRect`, `RampPiece`, `CourseProfile`, `CourseProfileRule` — 조각 조립·꺾은선·지름길 기하·경사 조각 자르기 |
| `Assets/Scripts/MapTools/ShortcutRule.cs` (새) | 금지 영역 판정, 지름길 패드 자리, 🔀 리포트 절 |
| `Assets/Scripts/MapTools/ClassicCourse.cs` (수정) | `Layout`에 `gateAllowed` 인자, `Validate`가 건너뛴 칸을 허용 |
| `Assets/Scripts/MapTools/CleanRunSearch.cs` (수정) | 살아 있는 상태만 훑기(성능) |
| `Assets/Scripts/MapTools/PlayabilityReport.cs` (수정) | `shortcutSection` 인자 |
| `Assets/Scripts/MapTools/CourseElevation.cs` (삭제) | 사인 물결 — `CourseProfile`로 대체 |
| `Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs` (수정) | 프로필로 굽기, 돌출 메시, 지름길·패드·표시, 배경 높이 따라가기 |
| `Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs` (수정) | 지름길 읽기, 금지 영역 감싸기, 두 길 증명 |
| `Assets/Tests/EditMode/MapTools/CourseProfileTests.cs` (새) | |
| `Assets/Tests/EditMode/MapTools/ShortcutRuleTests.cs` (새) | |
| `Assets/Tests/EditMode/MapTools/ClassicCourseTests.cs` (수정) | 건너뛰기 테스트 추가 |
| `Assets/Tests/EditMode/MapTools/CourseElevationTests.cs` (삭제) | |
| `docs/ROADMAP.md` (수정) | 09-24 정정 + 이번 슬라이스 기록 |

---

### Task 1: 로드맵 정정 — 09-24 패드 자리 변경과 탐색 x 누적 수정

**Files:**
- Modify: `docs/ROADMAP.md` — `## ✅ 부스트 패드 — 위험한 쪽을 고른 값을 거리로 돌려준다 (2026-09-23)` 절의 맨 끝(그 다음 `## 상태` 제목 바로 위)

- [ ] **Step 1: 절 끝에 정정 소절을 붙인다**

`## 상태` 바로 위(부스트 패드 절의 `### 열린 것` 목록 뒤)에 아래를 넣는다:

```markdown
### 정정 (2026-09-24) — 위 "무엇을 놓았나"는 이제 틀리다

**패드는 구간 뒤가 아니라 구간 안에 있다.** 구간 뒤에 놓았더니 6개 전부 함정이었다(사용자 발견):
부스트가 8.2m를 데려가는데 다음 파이프가 4.4m 앞이었다. 대시는 중력·날갯짓이 없는 수평 직선이라
그 동안 높이를 못 바꾸는데, 구간 뒤에 오는 것은 차선이 다른 평범한 관문이었다.

- 지금 자리: **마지막 두 도전 관문 사이, 다음 도전 관문의 창 높이.** 부스트가 그 창을 통과시켜 준다.
- 폭 3.5 → 1.5m(부스트 시작점이 덜 흔들리게), 회랑을 곡선이 아니라 **현**으로 재서 맞춘다(`RampLift`).
- 검사 ⚡ 절에 **"부스트 앞이 뚫렸나"** 를 더했다. 이 질문이 없어서 함정인 채로 배포됐다.

**검사기 회귀도 고쳤다.** 4자리 중 1자리만 ✅였다(09-14엔 4/4). 탐색이 x를 곱셈(`StartX + stepX × 틱`)으로,
재생(커널)이 더하기로 쌓아 4267틱에서 3cm 벌어졌다 — 커널의 벽 여유 0.02m를 넘어 스치는 판정이 뒤집혔다.
방아쇠는 경기 60 → 90초(3000틱 1.2cm → 4500틱 3.0cm). 09-14에 높이(y)를 고친 것과 같은 결함의 나머지
절반이다. `FlappyTickMath.ColumnXTable`이 더하기로 채우고, "곱셈과 더하기가 실제로 벌어진다"를 테스트가
못박는다. 리포트의 "탐색은 점을 격자에 붙여 찍는다"는 낡은 설명도 지웠다.
```

- [ ] **Step 2: 커밋**

```bash
git add docs/ROADMAP.md
git status --short
git commit -m "docs(roadmap): 09-24 패드 자리 변경과 탐색 x 누적 수정을 기록한다"
```

---

### Task 2: `CourseProfile` — 조각을 이어 붙인 꺾은선 코스

**Files:**
- Create: `Assets/Scripts/MapTools/CourseProfile.cs`
- Test: `Assets/Tests/EditMode/MapTools/CourseProfileTests.cs`

**Interfaces:**
- Produces:
  - `enum TerrainKind { Flat, Valley, Hill, StepDown, StepUp }`
  - `readonly struct SectionTerrain(float valleyDepth, float hillHeight, float riseSlope, float stepHeight, bool valleyShortcut)`
  - `readonly struct FlatSpan { float From, To; float Length }`
  - `readonly struct ShortcutRect { float X0, X1, Y0, Y1; float[] Tongue; float CenterY; float Length; static ShortcutRect FromCenterSize(float cx, float cy, float w, float h) }` — `Tongue`는 (x,y) 쌍 4개(반시계). `FromCenterSize`는 `Tongue = null`.
  - `readonly struct RampPiece { float X0, Lift0, X1, Lift1 }`
  - `sealed class CourseProfile { int VertexCount; float X(int); float Y(int); IReadOnlyList<FlatSpan> Flats; IReadOnlyList<ShortcutRect> Shortcuts; float CenterAt(float x); bool GateAllowedAt(float x, float margin); float MinY; float MaxY; }`
  - `static class CourseProfileRule { const float DropSlope=2.5f, ValleyBottom=16f, HillTop=12f, GateMargin=2.5f, MinTongue=1f; static readonly SectionTerrain[] Sections; static CourseProfile Compose(float startX, float length, float spacing, float corridorHalf, float window, ulong seed, float leadIn, float tail); static ShortcutRect ValleyShortcut(float x0, float baseY, float depth, float riseSlope, float corridorHalf, float window); static List<RampPiece> FloorPieces(CourseProfile p, IReadOnlyList<float> splitXs); static List<RampPiece> CeilingPieces(CourseProfile p, IReadOnlyList<float> splitXs); }`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/EditMode/MapTools/CourseProfileTests.cs`:

```csharp
using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 조각을 이어 붙인 코스. <b>새가 실제로 따라갈 수 있는 모양인가</b>가 이 테스트의 전부다 —
    /// 오르막은 탭 빈도가, 내리막은 최대 낙하 속도가 한계를 정한다(spec §2).
    /// </summary>
    public class CourseProfileTests
    {
        const float StartX = 0f;
        const float Length = 612f;          // 90초 × 6.8m/s
        const float Spacing = 11.4f;
        const float Half = 10.92f;          // 카메라 30m · FOV 40의 화면 세로 절반
        const float Window = 4.37f;
        const float LeadIn = Spacing * 4f;
        const float Tail = Spacing * 8f;

        static CourseProfile Compose(ulong seed = 20260919UL)
            => CourseProfileRule.Compose(StartX, Length, Spacing, Half, Window, seed, LeadIn, Tail);

        [Test]
        public void 출발점은_높이_0이고_앞뒤_여유까지_덮는다()
        {
            var p = Compose();
            Assert.AreEqual(0f, p.CenterAt(StartX), 1e-4f);
            Assert.AreEqual(StartX - LeadIn, p.X(0), 1e-3f);
            Assert.AreEqual(StartX + Length + Tail, p.X(p.VertexCount - 1), 1e-2f);
        }

        [Test]
        public void 경사는_물리_한계_안이다()
        {
            //  내리막 2.5 = 최대 낙하 30m/s로 따라갈 수 있는 4.4보다 한참 안쪽.
            //  오르막은 구간 한계(1.0/1.3/1.5) — 2.0이면 초당 6탭이 넘는다.
            var p = Compose();
            float sectionLen = Length / CourseProfileRule.Sections.Length;
            for (int i = 1; i < p.VertexCount; i++)
            {
                float dx = p.X(i) - p.X(i - 1);
                float dy = p.Y(i) - p.Y(i - 1);
                Assert.Greater(dx, 0f, $"{i}번째 꼭짓점이 뒤로 갔다");
                float slope = dy / dx;
                if (slope < 0f)
                {
                    Assert.LessOrEqual(-slope, CourseProfileRule.DropSlope + 1e-3f, $"x={p.X(i):F1} 내리막");
                }
                else if (slope > 0f)
                {
                    int s = System.Math.Min(CourseProfileRule.Sections.Length - 1,
                                            (int)((p.X(i - 1) - StartX) / sectionLen));
                    Assert.LessOrEqual(slope, CourseProfileRule.Sections[s].RiseSlope + 1e-3f,
                                       $"x={p.X(i):F1} 오르막(구간 {s + 1})");
                }
            }
        }

        [Test]
        public void 평지는_모두_한_칸_이상이다()
        {
            //  벽 뒤에 바로 관문이 오지 않게 조각 사이 평지를 남긴다. A 꼭대기(12m)도 한 칸보다 길다.
            foreach (FlatSpan f in Compose().Flats)
            {
                Assert.GreaterOrEqual(f.Length, Spacing - 1e-3f, $"평지 {f.From:F1}~{f.To:F1}");
            }
        }

        [Test]
        public void 높낮이가_실제로_크게_바뀐다()
        {
            var p = Compose();
            Assert.GreaterOrEqual(p.MaxY - p.MinY, 40f, "U 40m가 들어가야 한다");
        }

        [Test]
        public void 지름길은_구간_2와_3에_하나씩이다()
        {
            var shortcuts = Compose().Shortcuts;
            Assert.AreEqual(2, shortcuts.Count);
            float sectionLen = Length / 3f;
            Assert.That(shortcuts[0].X0, Is.GreaterThan(StartX + sectionLen).And.LessThan(StartX + 2 * sectionLen));
            Assert.That(shortcuts[1].X0, Is.GreaterThan(StartX + 2 * sectionLen));
        }

        [Test]
        public void 지름길_입구와_출구에서_천장이_지름길_윗면과_만난다()
        {
            //  입구·출구는 "천장선이 지름길 윗면을 지나는 곳"으로 정의된다. 여기가 어긋나면
            //  위쪽 상자와 경사 조각 사이에 틈이 생기거나 겹친다.
            var p = Compose();
            foreach (ShortcutRect r in p.Shortcuts)
            {
                Assert.AreEqual(r.Y1, p.CenterAt(r.X0) + Half, 1e-3f, "입구");
                Assert.AreEqual(r.Y1, p.CenterAt(r.X1) + Half, 1e-3f, "출구");
                Assert.AreEqual(Window, r.Y1 - r.Y0, 1e-4f, "세로 폭 = 평범한 틈");
            }
        }

        [Test]
        public void 혀는_지름길_아래에_있고_두께가_최소_이상이다()
        {
            foreach (ShortcutRect r in Compose().Shortcuts)
            {
                float[] t = r.Tongue;
                Assert.AreEqual(8, t.Length);
                Assert.AreEqual(r.Y0, t[1], 1e-4f);                       // 윗변 = 지름길 바닥
                Assert.AreEqual(r.Y0, t[7], 1e-4f);
                Assert.GreaterOrEqual(r.Y0 - t[3], CourseProfileRule.MinTongue - 1e-4f);
                Assert.Greater(t[0], r.X0);                                // 혀는 지름길 안쪽
                Assert.Less(t[6], r.X1);
            }
        }

        [Test]
        public void 얕은_U에는_지름길을_못_낸다()
        {
            //  깊이 절반에 창을 뚫으면 혀가 남아야 한다. 20m면 혀가 없다.
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => CourseProfileRule.ValleyShortcut(0f, 0f, 20f, 1.3f, Half, Window));
        }

        [Test]
        public void 관문은_평지에서만_끝으로부터_여유를_두고_선다()
        {
            var p = Compose();
            foreach (FlatSpan f in p.Flats)
            {
                Assert.IsTrue(p.GateAllowedAt((f.From + f.To) * 0.5f, CourseProfileRule.GateMargin));
                Assert.IsFalse(p.GateAllowedAt(f.From + 1f, CourseProfileRule.GateMargin),
                               $"평지 {f.From:F1} 시작 1m 안쪽에 관문이 선다");
            }
            //  U 벽 한가운데에는 서면 안 된다.
            ShortcutRect r = p.Shortcuts[0];
            Assert.IsFalse(p.GateAllowedAt((r.X0 + r.Tongue[2]) * 0.5f, CourseProfileRule.GateMargin));
        }

        [Test]
        public void 같은_씨앗은_같은_코스_다른_씨앗은_다른_코스()
        {
            var a = Compose(1UL); var b = Compose(1UL); var c = Compose(2UL);
            Assert.AreEqual(a.VertexCount, b.VertexCount);
            for (int i = 0; i < a.VertexCount; i++) { Assert.AreEqual(a.X(i), b.X(i)); Assert.AreEqual(a.Y(i), b.Y(i)); }
            bool differs = a.VertexCount != c.VertexCount;
            for (int i = 0; differs == false && i < a.VertexCount; i++) { differs = a.X(i) != c.X(i); }
            Assert.IsTrue(differs);
        }

        [Test]
        public void 바닥_조각은_끊김_없이_꺾은선을_그대로_덮는다()
        {
            var p = Compose();
            var pieces = CourseProfileRule.FloorPieces(p, new[] { 204f, 408f });
            Assert.AreEqual(p.X(0), pieces[0].X0, 1e-3f);
            for (int i = 0; i < pieces.Count; i++)
            {
                Assert.AreEqual(p.CenterAt(pieces[i].X0), pieces[i].Lift0, 1e-3f);
                Assert.AreEqual(p.CenterAt(pieces[i].X1), pieces[i].Lift1, 1e-3f);
                if (i > 0) { Assert.AreEqual(pieces[i - 1].X1, pieces[i].X0, 1e-4f, "끊김"); }
            }
            Assert.AreEqual(p.X(p.VertexCount - 1), pieces[pieces.Count - 1].X1, 1e-3f);
            //  구간 경계에서 끊겨야 색이 바뀐다.
            Assert.IsTrue(pieces.Exists(q => System.Math.Abs(q.X0 - 204f) < 1e-3f));
        }

        [Test]
        public void 천장_조각은_지름길_구간을_도려낸다()
        {
            var p = Compose();
            var pieces = CourseProfileRule.CeilingPieces(p, new float[0]);
            foreach (ShortcutRect r in p.Shortcuts)
            {
                foreach (RampPiece q in pieces)
                {
                    float mid = (q.X0 + q.X1) * 0.5f;
                    Assert.IsFalse(mid > r.X0 && mid < r.X1, $"x={mid:F1} 천장 조각이 지름길을 막는다");
                }
                Assert.IsTrue(pieces.Exists(q => System.Math.Abs(q.X1 - r.X0) < 1e-3f), "입구에서 끝나는 조각");
                Assert.IsTrue(pieces.Exists(q => System.Math.Abs(q.X0 - r.X1) < 1e-3f), "출구에서 시작하는 조각");
            }
        }
    }
}
```

- [ ] **Step 2: 컴파일해서 실패를 본다** — `CourseProfile`이 없어 컴파일 에러(`CS0246`). (명령 모음의 recompile)

- [ ] **Step 3: 구현한다**

`Assets/Scripts/MapTools/CourseProfile.cs`:

```csharp
using System;
using System.Collections.Generic;
using GameFramework.Rng;

namespace LOP.MapTools
{
    public enum TerrainKind { Flat, Valley, Hill, StepDown, StepUp }

    /// <summary>한 구간의 지형 손잡이. 구간이 갈수록 깊고 가팔라진다(spec §3).</summary>
    public readonly struct SectionTerrain
    {
        public readonly float ValleyDepth;
        public readonly float HillHeight;
        /// <summary>오르막 기울기. 탭 빈도가 한계를 정한다 — 1.5면 초당 약 3.5탭.</summary>
        public readonly float RiseSlope;
        /// <summary>계단 높이. 0이면 이 구간엔 계단이 없다.</summary>
        public readonly float StepHeight;
        public readonly bool ValleyShortcut;

        public SectionTerrain(float valleyDepth, float hillHeight, float riseSlope, float stepHeight,
                              bool valleyShortcut)
        {
            ValleyDepth = valleyDepth;
            HillHeight = hillHeight;
            RiseSlope = riseSlope;
            StepHeight = stepHeight;
            ValleyShortcut = valleyShortcut;
        }
    }

    /// <summary>회랑 중심이 평평한 x 범위. 관문은 여기에만 선다.</summary>
    public readonly struct FlatSpan
    {
        public readonly float From, To;
        public FlatSpan(float from, float to) { From = from; To = to; }
        public float Length => To - From;
    }

    /// <summary>
    /// U자를 수평으로 가로지르는 지름길. <see cref="X0"/>·<see cref="X1"/>은 천장선이 지름길 윗면을
    /// 지나는 곳(입구·출구)이고, <see cref="Tongue"/>은 지름길과 계곡 사이에 남는 덩어리(혀)다.
    /// </summary>
    public readonly struct ShortcutRect
    {
        public readonly float X0, X1, Y0, Y1;
        /// <summary>(x, y) 네 쌍, 반시계: 윗변 왼쪽 → 바닥 왼쪽 → 바닥 오른쪽 → 윗변 오른쪽.</summary>
        public readonly float[] Tongue;

        public ShortcutRect(float x0, float x1, float y0, float y1, float[] tongue)
        {
            X0 = x0; X1 = x1; Y0 = y0; Y1 = y1; Tongue = tongue;
        }

        public float CenterY => (Y0 + Y1) * 0.5f;
        public float Length => X1 - X0;

        /// <summary>씬 표시(빈 GameObject의 위치·크기)에서 되살린다. 혀는 검사에 필요 없어 비운다.</summary>
        public static ShortcutRect FromCenterSize(float cx, float cy, float w, float h)
            => new ShortcutRect(cx - w * 0.5f, cx + w * 0.5f, cy - h * 0.5f, cy + h * 0.5f, null);
    }

    /// <summary>경사 조각 하나 — 두 x 사이를 곧은 선으로 잇는다. Lift는 회랑 중심 높이.</summary>
    public readonly struct RampPiece
    {
        public readonly float X0, Lift0, X1, Lift1;
        public RampPiece(float x0, float lift0, float x1, float lift1)
        {
            X0 = x0; Lift0 = lift0; X1 = x1; Lift1 = lift1;
        }
    }

    /// <summary>
    /// 코스의 회랑 중심 높이 = <b>꺾은선</b>(도로 설계의 종단면, vertical profile). 조각(평지·계곡·언덕·
    /// 계단)을 이어 붙여 만든다. 꺾은선이라 바닥·천장을 조각 하나 = 곧은 경사 하나로 정확히 덮을 수
    /// 있다 — 곡선을 곧은 조각으로 덮을 때 생기던 수 cm 오차가 없다.
    /// </summary>
    public sealed class CourseProfile
    {
        private readonly float[] xs;
        private readonly float[] ys;
        private readonly List<FlatSpan> flats;
        private readonly List<ShortcutRect> shortcuts;

        internal CourseProfile(float[] xs, float[] ys, List<FlatSpan> flats, List<ShortcutRect> shortcuts)
        {
            this.xs = xs;
            this.ys = ys;
            this.flats = flats;
            this.shortcuts = shortcuts;
            MinY = float.MaxValue;
            MaxY = float.MinValue;
            foreach (float y in ys)
            {
                MinY = Math.Min(MinY, y);
                MaxY = Math.Max(MaxY, y);
            }
        }

        public int VertexCount => xs.Length;
        public float X(int i) => xs[i];
        public float Y(int i) => ys[i];
        public IReadOnlyList<FlatSpan> Flats => flats;
        public IReadOnlyList<ShortcutRect> Shortcuts => shortcuts;
        public float MinY { get; }
        public float MaxY { get; }

        public float CenterAt(float x)
        {
            if (x <= xs[0]) { return ys[0]; }
            int last = xs.Length - 1;
            if (x >= xs[last]) { return ys[last]; }
            int lo = 0, hi = last;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (xs[mid] <= x) { lo = mid; } else { hi = mid; }
            }
            float t = (x - xs[lo]) / (xs[hi] - xs[lo]);
            return ys[lo] + (ys[hi] - ys[lo]) * t;
        }

        /// <summary>이 x에 관문을 세워도 되나 — 평지 안쪽으로 <paramref name="margin"/>만큼 들어와 있어야 한다.</summary>
        public bool GateAllowedAt(float x, float margin)
        {
            foreach (FlatSpan f in flats)
            {
                if (x >= f.From + margin && x <= f.To - margin) { return true; }
            }
            return false;
        }
    }

    public static class CourseProfileRule
    {
        /// <summary>내리막 기울기(≈68°). 최대 낙하 30m/s로 따라갈 수 있는 한계 4.4보다 한참 안쪽.</summary>
        public const float DropSlope = 2.5f;
        /// <summary>U 바닥 길이. 관문을 끝에서 <see cref="GateMargin"/> 안쪽에 두면 보통 하나가 선다.</summary>
        public const float ValleyBottom = 16f;
        public const float HillTop = 12f;
        /// <summary>관문을 평지 끝에서 이만큼 안쪽에만 둔다 — 벽을 내려오자마자 창이 있으면 못 멈춘다.</summary>
        public const float GateMargin = 2.5f;
        /// <summary>지름길과 계곡 사이 혀의 최소 두께. 이보다 얇으면 두 길이 사실상 붙는다.</summary>
        public const float MinTongue = 1f;

        /// <summary>구간 1(배우기) · 2 · 3. spec §3 표 그대로.</summary>
        public static readonly SectionTerrain[] Sections =
        {
            new SectionTerrain(valleyDepth: 15f, hillHeight: 12f, riseSlope: 1.0f, stepHeight: 0f, valleyShortcut: false),
            new SectionTerrain(valleyDepth: 30f, hillHeight: 15f, riseSlope: 1.3f, stepHeight: 10f, valleyShortcut: true),
            new SectionTerrain(valleyDepth: 40f, hillHeight: 20f, riseSlope: 1.5f, stepHeight: 10f, valleyShortcut: true),
        };

        public static float ValleyLength(float depth, float rise) => depth / DropSlope + ValleyBottom + depth / rise;
        public static float HillLength(float height, float rise) => height / rise + HillTop + height / DropSlope;
        public static float StepLength(float height, bool down, float rise) => down ? height / DropSlope : height / rise;

        /// <param name="leadIn">출발선 앞 평지(스폰 뒤를 덮는 여유).</param>
        /// <param name="tail">결승선 뒤 평지.</param>
        public static CourseProfile Compose(float startX, float length, float spacing, float corridorHalf,
                                            float window, ulong seed, float leadIn, float tail)
        {
            var rng = new DeterministicRandom(seed);
            var xs = new List<float> { startX - leadIn };
            var ys = new List<float> { 0f };
            var flats = new List<FlatSpan>();
            var shortcuts = new List<ShortcutRect>();

            float x = startX;
            float y = 0f;
            float flatStart = startX - leadIn;
            float sectionLen = length / Sections.Length;

            for (int s = 0; s < Sections.Length; s++)
            {
                SectionTerrain t = Sections[s];
                var kinds = new List<TerrainKind> { TerrainKind.Valley, TerrainKind.Hill };
                //  계단 방향은 구간 시작에 정한다 — 계곡·언덕은 제자리로 돌아오므로 구간 안에서
                //  기준 높이가 바뀌는 것은 계단뿐이다. 0에서 멀어지지 않게 되돌리는 쪽으로 간다.
                bool stepDown = y > 1e-3f || (Math.Abs(y) <= 1e-3f && rng.Range(0, 2) == 0);
                if (t.StepHeight > 0f)
                {
                    kinds.Add(stepDown ? TerrainKind.StepDown : TerrainKind.StepUp);
                }
                for (int i = kinds.Count - 1; i > 0; i--)
                {
                    int j = rng.Range(0, i + 1);
                    (kinds[i], kinds[j]) = (kinds[j], kinds[i]);
                }

                float piecesLen = 0f;
                foreach (TerrainKind k in kinds) { piecesLen += PieceLength(k, t); }

                int gaps = kinds.Count + 1;
                var mins = new float[gaps];
                for (int g = 0; g < gaps; g++) { mins[g] = spacing; }
                if (s == 0) { mins[0] = spacing * 3f; }                    // 스폰 앞 평지
                if (s == Sections.Length - 1) { mins[gaps - 1] = spacing * 2f; }  // 결승선 앞 평지
                float minSum = 0f;
                foreach (float m in mins) { minSum += m; }
                float free = sectionLen - piecesLen - minSum;
                if (free < 0f)
                {
                    throw new InvalidOperationException(
                        $"구간 {s + 1}에 조각이 안 들어간다 (조각 {piecesLen:F1}m + 최소 평지 {minSum:F1}m > {sectionLen:F1}m)");
                }
                var weights = new float[gaps];
                float weightSum = 0f;
                for (int g = 0; g < gaps; g++) { weights[g] = rng.Range(0f, 1f) + 0.1f; weightSum += weights[g]; }

                for (int g = 0; g < gaps; g++)
                {
                    x += mins[g] + free * weights[g] / weightSum;
                    if (g == gaps - 1) { break; }

                    //  평지가 여기서 끝나고 조각이 시작된다.
                    flats.Add(new FlatSpan(flatStart, x));
                    xs.Add(x); ys.Add(y);
                    TerrainKind kind = kinds[g];
                    switch (kind)
                    {
                        case TerrainKind.Valley:
                        {
                            float d = t.ValleyDepth;
                            float x0 = x;
                            float bottom0 = x0 + d / DropSlope;
                            float bottom1 = bottom0 + ValleyBottom;
                            xs.Add(bottom0); ys.Add(y - d);
                            xs.Add(bottom1); ys.Add(y - d);
                            flats.Add(new FlatSpan(bottom0, bottom1));
                            x = bottom1 + d / t.RiseSlope;
                            xs.Add(x); ys.Add(y);
                            if (t.ValleyShortcut)
                            {
                                shortcuts.Add(ValleyShortcut(x0, y, d, t.RiseSlope, corridorHalf, window));
                            }
                            break;
                        }
                        case TerrainKind.Hill:
                        {
                            float h = t.HillHeight;
                            float top0 = x + h / t.RiseSlope;
                            float top1 = top0 + HillTop;
                            xs.Add(top0); ys.Add(y + h);
                            xs.Add(top1); ys.Add(y + h);
                            flats.Add(new FlatSpan(top0, top1));
                            x = top1 + h / DropSlope;
                            xs.Add(x); ys.Add(y);
                            break;
                        }
                        default:
                        {
                            bool down = kind == TerrainKind.StepDown;
                            x += StepLength(t.StepHeight, down, t.RiseSlope);
                            y += down ? -t.StepHeight : t.StepHeight;
                            xs.Add(x); ys.Add(y);
                            break;
                        }
                    }
                    flatStart = x;
                }
            }

            float end = startX + length + tail;
            flats.Add(new FlatSpan(flatStart, end));
            xs.Add(end); ys.Add(y);
            return new CourseProfile(xs.ToArray(), ys.ToArray(), flats, shortcuts);
        }

        static float PieceLength(TerrainKind kind, SectionTerrain t)
        {
            switch (kind)
            {
                case TerrainKind.Valley: return ValleyLength(t.ValleyDepth, t.RiseSlope);
                case TerrainKind.Hill: return HillLength(t.HillHeight, t.RiseSlope);
                case TerrainKind.StepDown: return StepLength(t.StepHeight, true, t.RiseSlope);
                default: return StepLength(t.StepHeight, false, t.RiseSlope);
            }
        }

        /// <summary>
        /// 계곡의 깊이 절반 높이에 수평 지름길을 뚫는다. 입구는 내리막 벽 중간이라 먼저 절반을 급강하해야
        /// 들어갈 수 있다(spec §4). 혀(지름길과 계곡 사이)가 <see cref="MinTongue"/>보다 얇으면 던진다.
        /// </summary>
        /// <param name="x0">계곡이 시작하는 x(평지 끝).</param>
        public static ShortcutRect ValleyShortcut(float x0, float baseY, float depth, float riseSlope,
                                                  float corridorHalf, float window)
        {
            float center = baseY - depth * 0.5f;
            float y0 = center - window * 0.5f;
            float y1 = center + window * 0.5f;
            float topCeiling = baseY + corridorHalf;
            float bottomCeiling = baseY - depth + corridorHalf;
            if (y0 - bottomCeiling < MinTongue)
            {
                throw new ArgumentOutOfRangeException(nameof(depth), depth,
                    $"깊이 {depth}m면 지름길 아래 혀가 {y0 - bottomCeiling:F2}m다 (최소 {MinTongue}m)");
            }
            float bottom0 = x0 + depth / DropSlope;
            float bottom1 = bottom0 + ValleyBottom;
            //  내리막 천장선 y = topCeiling − DropSlope·(x − x0), 오르막 천장선 y = bottomCeiling + rise·(x − bottom1).
            float entry = x0 + (topCeiling - y1) / DropSlope;
            float exit = bottom1 + (y1 - bottomCeiling) / riseSlope;
            float tongueLeft = x0 + (topCeiling - y0) / DropSlope;
            float tongueRight = bottom1 + (y0 - bottomCeiling) / riseSlope;
            var tongue = new[]
            {
                tongueLeft, y0,
                bottom0, bottomCeiling,
                bottom1, bottomCeiling,
                tongueRight, y0,
            };
            return new ShortcutRect(entry, exit, y0, y1, tongue);
        }

        /// <summary>바닥 경사 조각. 꺾은선의 모든 꼭짓점과 <paramref name="splitXs"/>(구간 경계)에서 끊는다.</summary>
        public static List<RampPiece> FloorPieces(CourseProfile p, IReadOnlyList<float> splitXs)
            => Pieces(p, splitXs, cutShortcuts: false);

        /// <summary>천장 경사 조각. 바닥과 같되 지름길 입구~출구는 도려낸다 — 그 자리는 빌더가 위쪽 상자와 혀로 채운다.</summary>
        public static List<RampPiece> CeilingPieces(CourseProfile p, IReadOnlyList<float> splitXs)
            => Pieces(p, splitXs, cutShortcuts: true);

        static List<RampPiece> Pieces(CourseProfile p, IReadOnlyList<float> splitXs, bool cutShortcuts)
        {
            float first = p.X(0);
            float last = p.X(p.VertexCount - 1);
            var cuts = new List<float>();
            for (int i = 0; i < p.VertexCount; i++) { cuts.Add(p.X(i)); }
            foreach (float s in splitXs) { if (s > first && s < last) { cuts.Add(s); } }
            if (cutShortcuts)
            {
                foreach (ShortcutRect r in p.Shortcuts) { cuts.Add(r.X0); cuts.Add(r.X1); }
            }
            cuts.Sort();

            var pieces = new List<RampPiece>();
            for (int i = 1; i < cuts.Count; i++)
            {
                float a = cuts[i - 1], b = cuts[i];
                if (b - a < 1e-4f) { continue; }
                if (cutShortcuts)
                {
                    float mid = (a + b) * 0.5f;
                    bool inside = false;
                    foreach (ShortcutRect r in p.Shortcuts) { inside |= mid > r.X0 && mid < r.X1; }
                    if (inside) { continue; }
                }
                pieces.Add(new RampPiece(a, p.CenterAt(a), b, p.CenterAt(b)));
            }
            return pieces;
        }
    }
}
```

- [ ] **Step 4: 컴파일·테스트 — 새 테스트 12개 전부 PASS.** 실패하면 `Compose`의 평지 합(구간 길이)부터 본다: `평지 합 + 조각 합 = 204m`.

- [ ] **Step 5: 일부러 깨뜨린다** — `DropSlope`를 `5f`로 바꿔 `경사는_물리_한계_안이다`가, `MinTongue` 검사를 지워 `얕은_U에는_지름길을_못_낸다`가 빨강이 되는지 본다. 되돌린다.

- [ ] **Step 6: 커밋** (유니티가 만든 `.meta` 포함)

```bash
git add Assets/Scripts/MapTools/CourseProfile.cs Assets/Scripts/MapTools/CourseProfile.cs.meta \
        Assets/Tests/EditMode/MapTools/CourseProfileTests.cs Assets/Tests/EditMode/MapTools/CourseProfileTests.cs.meta
git status --short
git commit -m "feat(maptools): 코스를 뾰족한 지형 조각의 꺾은선으로 — U·A·계단, U 지름길 기하"
```

---

### Task 3: 관문 배치가 벽을 건너뛴다

**Files:**
- Modify: `Assets/Scripts/MapTools/ClassicCourse.cs` — `Layout`(인자 + 도전 구간 거르기 + 건너뛰기), `Validate`(간격을 칸의 배수로)
- Test: `Assets/Tests/EditMode/MapTools/ClassicCourseTests.cs`

**Interfaces:**
- Produces: `ClassicCourseRule.Layout(..., System.Func<float, float> centerAt = null, int challengeRuns = 0, System.Func<float, bool> gateAllowed = null)` — `gateAllowed`가 false인 x에는 관문을 안 둔다. null이면 **예전과 한 자리도 다르지 않다.**

- [ ] **Step 1: 실패하는 테스트를 쓴다** — `ClassicCourseTests.cs` 클래스 끝(마지막 `}` 두 개 위)에 추가:

```csharp
        //  ── 벽 건너뛰기 ──────────────────────────────────────────────
        //  벽(가파른 경사)에는 관문을 두지 않는다 — 벽 자체가 장애물이다.

        static bool NoGateIn100To200(float x) => x < 100f || x > 200f;

        [Test]
        public void 관문을_못_두는_자리는_건너뛴다()
        {
            var pipes = ClassicCourseRule.Layout(0f, 400f, 11.4f, -7.28f, 7.28f, 4.37f, 6f, 5UL,
                                                 gateAllowed: NoGateIn100To200);
            Assert.IsFalse(pipes.Exists(p => p.X >= 100f && p.X <= 200f));
            foreach (CoursePipe p in pipes)
            {
                float k = p.X / 11.4f;
                Assert.AreEqual(System.Math.Round(k), k, 1e-3, $"x={p.X} 칸에서 벗어났다");
            }
        }

        [Test]
        public void 건너뛴_칸을_사이에_둔_두_관문은_높이차를_안_본다()
        {
            //  벽을 건너는 높이차는 규칙이 아니라 검사기의 클린런이 판정한다.
            var pipes = new List<CoursePipe> { new CoursePipe(11.4f, -3f), new CoursePipe(34.2f, 3f) };
            Assert.IsNull(ClassicCourseRule.Validate(pipes, -7.28f, 7.28f, 4.37f, 11.4f, 1f));
        }

        [Test]
        public void 간격이_칸의_배수가_아니면_여전히_잡는다()
        {
            var pipes = new List<CoursePipe> { new CoursePipe(11.4f, 0f), new CoursePipe(28.5f, 0f) };
            Assert.That(ClassicCourseRule.Validate(pipes, -7.28f, 7.28f, 4.37f, 11.4f, 6f), Does.Contain("간격"));
        }

        [Test]
        public void 모두_허용하면_예전과_같은_코스다()
        {
            var before = ClassicCourseRule.Layout(0f, 612f, 11.4f, Floor, Ceiling, Window, 6f, 7UL, challengeRuns: 6);
            var after = ClassicCourseRule.Layout(0f, 612f, 11.4f, Floor, Ceiling, Window, 6f, 7UL, challengeRuns: 6,
                                                 gateAllowed: _ => true);
            Assert.AreEqual(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(before[i].X, after[i].X);
                Assert.AreEqual(before[i].GapCenter, after[i].GapCenter);
                Assert.AreEqual(before[i].HasChallenge, after[i].HasChallenge);
            }
        }

        [Test]
        public void 도전_구간이_벽에_걸치면_통째로_빠진다()
        {
            //  반쯤 걸친 구간은 차선이 벽을 건너 이어져 따라갈 수 없다 — 네 관문이 모두 서야 남는다.
            var pipes = ClassicCourseRule.Layout(0f, 612f, 11.4f, Floor, Ceiling, Window, 6f, 7UL,
                                                 challengeRuns: 6, gateAllowed: NoGateIn100To200);
            var run = new List<CoursePipe>();
            foreach (CoursePipe p in pipes)
            {
                if (p.HasChallenge) { run.Add(p); continue; }
                Assert.That(run.Count, Is.EqualTo(0).Or.EqualTo(ClassicCourseRule.ChallengeRunLength));
                for (int i = 1; i < run.Count; i++) { Assert.AreEqual(11.4f, run[i].X - run[i - 1].X, 1e-3f); }
                run.Clear();
            }
        }
```

(`Floor`, `Ceiling`, `Window`는 이 테스트 파일에 이미 있는 상수다. `using System.Collections.Generic;`도 이미 있다 — 없으면 추가.)

- [ ] **Step 2: 컴파일해서 실패를 본다** — `gateAllowed` 인자가 없어 `CS1739`.

- [ ] **Step 3: `Layout`을 고친다**

시그니처의 `int challengeRuns = 0)` 을 다음으로 바꾼다:

```csharp
                                              int challengeRuns = 0,
                                              System.Func<float, bool> gateAllowed = null)
```

`runStarts`를 다 채운 블록(`for (int r = 0; r < challengeRuns; r++) { ... }`가 들어 있는 `if` 블록) **바로 뒤**에 넣는다:

```csharp
            //  벽에 걸치는 도전 구간은 통째로 뺀다. 반만 남기면 차선이 벽을 건너 이어져
            //  "벽을 따라 내려가며 먼 창을 노리는" 따라갈 수 없는 자리가 된다.
            var positions = new List<float>();
            for (float px = startX + spacing; px <= startX + courseLength + 1e-4f; px += spacing)
            {
                positions.Add(px);
            }
            if (gateAllowed != null)
            {
                runStarts.RemoveWhere(start =>
                {
                    for (int k = 0; k < ChallengeRunLength; k++)
                    {
                        int g = start + k;
                        if (g < 1 || g > positions.Count || gateAllowed(positions[g - 1]) == false) { return true; }
                    }
                    return false;
                });
            }
```

그리고 관문 루프의 머리를

```csharp
            for (float x = startX + spacing; x <= startX + courseLength + 1e-4f; x += spacing)
            {
                gateIndex++;
```

에서

```csharp
            foreach (float x in positions)
            {
                gateIndex++;
                //  벽 위에는 관문을 두지 않는다. 난수도 안 뽑고 previousLift도 안 옮긴다 —
                //  벽 다음 첫 관문은 벽 전체의 고저차를 예산에서 빼므로 워크가 멈춰 선다.
                if (gateAllowed != null && gateAllowed(x) == false)
                {
                    continue;
                }
```

로 바꾼다. (`positions`는 원래 루프와 **같은 더하기**로 x를 쌓으므로 x 값이 한 자리도 안 바뀐다.)

- [ ] **Step 4: `Validate`의 간격·높이차 검사를 고친다**

```csharp
                float gap = p.X - pipes[i - 1].X;
                if (Math.Abs(gap - spacing) > 1e-3f)
                {
                    return $"x={p.X:F1}의 간격이 {gap:F2}m다 (목표 {spacing:F2}m)";
                }
```

를

```csharp
                float gap = p.X - pipes[i - 1].X;
                //  벽 위 칸은 비어 있을 수 있다 — 간격은 한 칸의 <b>배수</b>면 된다.
                int cells = (int)Math.Round(gap / spacing);
                if (cells < 1 || Math.Abs(gap - cells * spacing) > 1e-3f)
                {
                    return $"x={p.X:F1}의 간격이 {gap:F2}m다 (목표 {spacing:F2}m의 배수)";
                }
```

로, 맨 아래 높이차 검사

```csharp
                float step = Math.Abs(p.GapCenter - pipes[i - 1].GapCenter);
                if (step > maxStep + 1e-3f)
```

를

```csharp
                //  칸을 건너뛴 두 관문 사이(= 벽을 건넘)는 이 규칙이 아니라 검사기의 클린런이 판정한다.
                float step = Math.Abs(p.GapCenter - pipes[i - 1].GapCenter);
                if (cells == 1 && step > maxStep + 1e-3f)
```

로 바꾼다.

- [ ] **Step 5: 컴파일·테스트 — 새 5개 PASS, 기존 ClassicCourse 테스트 전부 그대로 PASS.**

- [ ] **Step 6: 일부러 깨뜨린다** — `Validate`의 `cells == 1 &&`를 지우면 `건너뛴_칸을_사이에_둔_두_관문은_높이차를_안_본다`가, `runStarts.RemoveWhere` 블록을 주석 처리하면 `도전_구간이_벽에_걸치면_통째로_빠진다`가 빨강이어야 한다. 되돌린다.

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/MapTools/ClassicCourse.cs Assets/Tests/EditMode/MapTools/ClassicCourseTests.cs
git status --short
git commit -m "feat(maptools): 관문은 벽을 건너뛴다 — 벽에 걸친 도전 구간은 통째로 뺀다"
```

---

### Task 4: `ShortcutRule` — 금지 영역·지름길 패드·🔀 리포트 절

**Files:**
- Create: `Assets/Scripts/MapTools/ShortcutRule.cs`
- Modify: `Assets/Scripts/MapTools/PlayabilityReport.cs` — `Build` 인자 `string shortcutSection = null` + 출력
- Test: `Assets/Tests/EditMode/MapTools/ShortcutRuleTests.cs`

**Interfaces:**
- Consumes: `ShortcutRect` (Task 2)
- Produces:
  - `static bool ShortcutRule.InCore(ShortcutRect r, float x)` — 지름길 길이의 가운데 절반
  - `static bool ShortcutRule.ForbidsShortcut(IReadOnlyList<ShortcutRect> all, float x, float y)` — 어느 지름길이든 가운데 절반 안, 지름길 띠 안
  - `static bool ShortcutRule.ForbidsValley(ShortcutRect r, float x, float y)` — 그 지름길 가운데 절반 안, 지름길 바닥(`Y0`) 아래
  - `static float? ShortcutRule.PadCenterX(ShortcutRect r, float boostSpan, float padWidth, float exitClear)` — 자리가 없으면 null
  - `readonly struct ShortcutProof(string label, float x0, float x1, bool found, bool verified, float blockedX)`
  - `static string ShortcutRule.Section(ShortcutProof safeRoute, IReadOnlyList<ShortcutProof> shortcuts)`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/EditMode/MapTools/ShortcutRuleTests.cs`:

```csharp
using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 지름길 두 길을 <b>깨끗이 가르는가</b>. 금지 영역이 회랑까지 막으면 "지름길 없이"가 거짓으로
    /// 실패하고, 덜 막으면 "지름길로"가 계곡으로 새어 거짓으로 통과한다.
    /// </summary>
    public class ShortcutRuleTests
    {
        const float Half = 10.92f;
        const float Window = 4.37f;

        //  구간 3의 U(깊이 40, 오르막 1.5)를 x=100에서 시작한다.
        static readonly ShortcutRect Deep = CourseProfileRule.ValleyShortcut(100f, 0f, 40f, 1.5f, Half, Window);

        [Test]
        public void 가운데_절반은_혀_위에_있다()
        {
            //  이게 참이어야 가운데 절반에서 지름길 띠와 계곡이 혀로 완전히 갈린다.
            float quarter = Deep.Length * 0.25f;
            Assert.GreaterOrEqual(Deep.X0 + quarter, Deep.Tongue[0]);
            Assert.LessOrEqual(Deep.X1 - quarter, Deep.Tongue[6]);
        }

        [Test]
        public void 지름길_금지는_가운데의_지름길_띠만_막는다()
        {
            var all = new List<ShortcutRect> { Deep };
            float mid = (Deep.X0 + Deep.X1) * 0.5f;
            Assert.IsTrue(ShortcutRule.ForbidsShortcut(all, mid, Deep.Y0 + 0.5f));
            Assert.IsFalse(ShortcutRule.ForbidsShortcut(all, mid, Deep.Y0 - 5f), "계곡은 열려 있어야 한다");
            Assert.IsFalse(ShortcutRule.ForbidsShortcut(all, Deep.X0 + 0.5f, Deep.Y0 + 0.5f), "입구 근처는 회랑과 겹친다");
        }

        [Test]
        public void 계곡_금지는_가운데의_지름길_아래만_막는다()
        {
            float mid = (Deep.X0 + Deep.X1) * 0.5f;
            Assert.IsTrue(ShortcutRule.ForbidsValley(Deep, mid, Deep.Y0 - 5f));
            Assert.IsFalse(ShortcutRule.ForbidsValley(Deep, mid, Deep.Y0 + 0.5f), "지름길은 열려 있어야 한다");
            Assert.IsFalse(ShortcutRule.ForbidsValley(Deep, Deep.X1 + 5f, Deep.Y0 - 5f));
        }

        [Test]
        public void 패드는_부스트가_출구_전에_끝나는_자리다()
        {
            float span = 8.16f, width = 1.5f, clear = 1.5f;
            float? x = ShortcutRule.PadCenterX(Deep, span, width, clear);
            Assert.IsTrue(x.HasValue);
            Assert.AreEqual(Deep.X1 - clear, x.Value + width * 0.5f + span, 1e-3f);
            Assert.Greater(x.Value - width * 0.5f, Deep.X0, "패드는 지름길 안에 있다");
        }

        [Test]
        public void 지름길이_짧으면_패드를_안_놓는다()
        {
            var tiny = new ShortcutRect(0f, 8f, -2f, 2f, null);
            Assert.IsFalse(ShortcutRule.PadCenterX(tiny, 8.16f, 1.5f, 1.5f).HasValue);
        }

        [Test]
        public void 리포트는_두_길을_따로_말한다()
        {
            var safe = new ShortcutProof("지름길 없이", 0f, 0f, found: true, verified: true, blockedX: 0f);
            var ok = new ShortcutProof("지름길", 300f, 324f, true, true, 0f);
            var trap = new ShortcutProof("지름길", 480f, 504f, false, false, 482.3f);
            string s = ShortcutRule.Section(safe, new List<ShortcutProof> { ok, trap });
            Assert.That(s, Does.Contain("🔀"));
            Assert.That(s, Does.Contain("지름길 없이"));
            Assert.That(s, Does.Contain("x=300~324"));
            Assert.That(s, Does.Contain("함정"));
            Assert.That(s, Does.Contain("482.3"));
        }

        [Test]
        public void 지름길이_없으면_그렇다고_말한다()
        {
            var safe = new ShortcutProof("지름길 없이", 0f, 0f, true, true, 0f);
            Assert.That(ShortcutRule.Section(safe, new List<ShortcutProof>()), Does.Contain("지름길이 없다"));
        }
    }
}
```

- [ ] **Step 2: 컴파일해서 실패를 본다** — `ShortcutRule` 없음.

- [ ] **Step 3: 구현한다**

`Assets/Scripts/MapTools/ShortcutRule.cs`:

```csharp
using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>검사기가 한 길을 증명한 결과. 순수 계층에서 절을 찍으려고 씬 타입 없이 들고 온다.</summary>
    public readonly struct ShortcutProof
    {
        public readonly string Label;
        public readonly float X0, X1;
        /// <summary>탐색이 경로를 찾았나.</summary>
        public readonly bool Found;
        /// <summary>그 경로를 진짜 커널로 재생해도 안 닿았나 — ✅의 근거.</summary>
        public readonly bool Verified;
        public readonly float BlockedX;

        public ShortcutProof(string label, float x0, float x1, bool found, bool verified, float blockedX)
        {
            Label = label; X0 = x0; X1 = x1; Found = found; Verified = verified; BlockedX = blockedX;
        }
    }

    /// <summary>
    /// 지름길 두 길을 따로 증명하기 위한 규칙. 검사기는 탐색의 한 틱 판정을 이 금지 영역으로 감싸
    /// "막힌 것으로 취급"한다 — 탐색 코드는 그대로 두고 길만 강제한다(경로 탐색의 무한대 비용 칸과 같다).
    ///
    /// <para><b>왜 가운데 절반만 막나.</b> 입구·출구 근처는 지름길 띠가 회랑 천장과 겹친다. 거기까지
    /// 막으면 계곡으로 내려가는 새가 벽 위쪽을 스칠 때 거짓으로 막힌다. 가운데 절반은 혀가 두 길을
    /// 완전히 가르는 굴이라(테스트로 못박는다), 어느 길이든 반드시 여기를 지난다.</para>
    /// </summary>
    public static class ShortcutRule
    {
        public static bool InCore(ShortcutRect r, float x)
        {
            float quarter = r.Length * 0.25f;
            return x >= r.X0 + quarter && x <= r.X1 - quarter;
        }

        public static bool ForbidsShortcut(IReadOnlyList<ShortcutRect> all, float x, float y)
        {
            for (int i = 0; i < all.Count; i++)
            {
                ShortcutRect r = all[i];
                if (InCore(r, x) && y >= r.Y0 && y <= r.Y1) { return true; }
            }
            return false;
        }

        public static bool ForbidsValley(ShortcutRect r, float x, float y) => InCore(r, x) && y < r.Y0;

        /// <summary>
        /// 지름길 안 패드 자리. 부스트(대시 = 조종 안 되는 수평 직선)가 <b>출구 <paramref name="exitClear"/>m
        /// 전에 끝나야</b> 한다 — 09-24에 앞이 막힌 패드 6개를 배포한 교훈이다. 자리가 안 나오면 null.
        /// </summary>
        public static float? PadCenterX(ShortcutRect r, float boostSpan, float padWidth, float exitClear)
        {
            float right = r.X1 - exitClear - boostSpan;
            float center = right - padWidth * 0.5f;
            if (center - padWidth * 0.5f <= r.X0) { return null; }
            return center;
        }

        public static string Section(ShortcutProof safeRoute, IReadOnlyList<ShortcutProof> shortcuts)
        {
            var text = new StringBuilder();
            text.AppendLine("── 🔀 지름길 ──────────────────────────");
            if (shortcuts == null || shortcuts.Count == 0)
            {
                text.AppendLine("  지름길이 없다.");
                return text.ToString().TrimEnd();
            }
            text.AppendLine("  " + Line(safeRoute, "지름길을 막고 완주", "안전한 길이 없다 — 맵이 불가능하다"));
            foreach (ShortcutProof p in shortcuts)
            {
                text.AppendLine($"  x={p.X0:F0}~{p.X1:F0}  "
                              + Line(p, "들어가서 완주", "지름길이 아니라 함정이다(못 들어가거나 못 나온다)"));
            }
            text.AppendLine("  (✅는 탐색이 찾은 길을 진짜 커널로 다시 날려 안 닿은 것이다. 🟡는 찾았지만 재생이 어긋났다.)");
            return text.ToString().TrimEnd();
        }

        static string Line(ShortcutProof p, string okText, string failText)
        {
            string head = string.IsNullOrEmpty(p.Label) ? "" : p.Label + "  ";
            if (p.Found == false) { return $"{head}❌ 탐색 x={p.BlockedX:F1}에서 막힘 — {failText}"; }
            if (p.Verified == false) { return $"{head}🟡 {okText} — 탐색은 찾았으나 재생이 어긋남"; }
            return $"{head}✅ {okText} (재생 확인)";
        }
    }
}
```

`PlayabilityReport.cs` — `Build` 시그니처 끝의

```csharp
                                   string boostSection = null)
```

를

```csharp
                                   string boostSection = null,
                                   string shortcutSection = null)
```

로 바꾸고, 본문의 부스트 절 출력 블록

```csharp
            if (string.IsNullOrEmpty(boostSection) == false)
            {
                text.AppendLine(boostSection);
                text.AppendLine();
            }
```

바로 뒤에 넣는다:

```csharp

            //  지름길은 클린런 바로 곁의 증명이다 — ①이 "어떻게든 완주"라면 이 절은 "두 길 각각 완주"다.
            if (string.IsNullOrEmpty(shortcutSection) == false)
            {
                text.AppendLine(shortcutSection);
                text.AppendLine();
            }
```

- [ ] **Step 4: 컴파일·테스트 — 새 7개 PASS.**

- [ ] **Step 5: 일부러 깨뜨린다** — `InCore`의 `quarter`를 `0f`로 바꿔 `지름길_금지는_가운데의_지름길_띠만_막는다`(입구 근처 줄)가, `PadCenterX`의 `exitClear`를 빼서 `패드는_부스트가_출구_전에_끝나는_자리다`가 빨강인지 본다. 되돌린다.

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/MapTools/ShortcutRule.cs Assets/Scripts/MapTools/ShortcutRule.cs.meta \
        Assets/Scripts/MapTools/PlayabilityReport.cs \
        Assets/Tests/EditMode/MapTools/ShortcutRuleTests.cs Assets/Tests/EditMode/MapTools/ShortcutRuleTests.cs.meta
git status --short
git commit -m "feat(maptools): 지름길 두 길을 가르는 금지 영역, 지름길 패드 자리, 🔀 리포트 절"
```

---

### Task 5: 빌더가 프로필로 굽는다 — 경사 조각·돌출 메시·지름길·패드

**Files:**
- Modify: `Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs`
- Delete: `Assets/Scripts/MapTools/CourseElevation.cs` (+`.meta`), `Assets/Tests/EditMode/MapTools/CourseElevationTests.cs` (+`.meta`)

**Interfaces:**
- Consumes: `CourseProfileRule.Compose/FloorPieces/CeilingPieces`, `CourseProfile.CenterAt/GateAllowedAt/Shortcuts`, `ShortcutRule.PadCenterX`, `BoostPadRule.Fit`, `ClassicCourseRule.Layout(..., gateAllowed)` (Tasks 2–4)
- Produces (씬): `ComposedMap` 아래 `Shortcut_<X0>` — **Transform만 있는** 빈 GameObject. `position = (중심 x, 중심 y, 0)`, `localScale = (X1−X0, Y1−Y0, 1)`. 검사기(Task 7)가 이것으로 지름길을 읽는다.

- [ ] **Step 1: 프로필을 만든다** — `Build()`의

```csharp
            //  회랑 중심이 코스를 따라 오르내린다 — 긴 내리막이 다이브를 만든다.
            System.Func<float, float> centerAt =
                x => LOP.MapTools.CourseElevation.CenterY(x, StartX, length);

            var pipes = LOP.MapTools.ClassicCourseRule.Layout(
                StartX, length, spacing, floorY, ceilingY, window, MaxGapStep, Seed, centerAt,
                ChallengeRuns);
```

를

```csharp
            //  코스는 뾰족한 U·A·계단 조각을 이어 붙인 꺾은선이다(spec 2026-09-24).
            //  앞뒤 여유는 스폰 뒤와 결승선 뒤를 덮는다 — 예전 경사 격자의 여유(앞 4칸·뒤 8칸)와 같다.
            var profile = LOP.MapTools.CourseProfileRule.Compose(
                StartX, length, spacing, ceilingY, window, Seed,
                leadIn: spacing * 4f, tail: spacing * 8f);
            System.Func<float, float> centerAt = profile.CenterAt;

            var pipes = LOP.MapTools.ClassicCourseRule.Layout(
                StartX, length, spacing, floorY, ceilingY, window, MaxGapStep, Seed, centerAt,
                ChallengeRuns,
                gateAllowed: x => profile.GateAllowedAt(x, LOP.MapTools.CourseProfileRule.GateMargin));
```

로 바꾼다.

- [ ] **Step 2: 바닥·천장을 프로필 조각으로 굽는다** — 고정 격자 루프

```csharp
            int rampCount = (int)System.Math.Ceiling(length / spacing) + 8;
            for (int i = 0; i < rampCount; i++)
            {
                float x0 = StartX - spacing * 4f + spacing * i;
                float x1 = x0 + spacing;
                Material rampSkin = SectionMaterial((x0 + x1) * 0.5f, length, fallback);
                Ramp(composed.transform, $"Floor_{i}", x0, x1, centerAt(x0), centerAt(x1),
                     floorY, below: true, material: rampSkin);
                Ramp(composed.transform, $"Ceiling_{i}", x0, x1, centerAt(x0), centerAt(x1),
                     ceilingY, below: false, material: rampSkin);
            }
```

를 통째로

```csharp
            //  꺾은선의 꼭짓점마다, 그리고 구간 경계마다 끊는다 — 조각 하나가 곧은 경사 하나라
            //  바닥·천장이 꺾은선에 정확히 놓인다. 구간 경계에서 끊어야 색이 바뀐다.
            float sectionLength = length / FlappyRace.CourseSectionRule.Count;
            var splits = new System.Collections.Generic.List<float>();
            for (int s = 1; s < FlappyRace.CourseSectionRule.Count; s++) { splits.Add(StartX + sectionLength * s); }

            var floorPieces = LOP.MapTools.CourseProfileRule.FloorPieces(profile, splits);
            for (int i = 0; i < floorPieces.Count; i++)
            {
                LOP.MapTools.RampPiece q = floorPieces[i];
                Ramp(composed.transform, $"Floor_{i}", q.X0, q.X1, q.Lift0, q.Lift1,
                     floorY, below: true, material: SectionMaterial((q.X0 + q.X1) * 0.5f, length, fallback));
            }
            var ceilingPieces = LOP.MapTools.CourseProfileRule.CeilingPieces(profile, splits);
            for (int i = 0; i < ceilingPieces.Count; i++)
            {
                LOP.MapTools.RampPiece q = ceilingPieces[i];
                Ramp(composed.transform, $"Ceiling_{i}", q.X0, q.X1, q.Lift0, q.Lift1,
                     ceilingY, below: false, material: SectionMaterial((q.X0 + q.X1) * 0.5f, length, fallback));
            }

            //  지름길 구간의 천장은 경사 조각이 아니라 두 덩어리다: 지름길 위의 상자, 지름길과 계곡 사이의 혀.
            int shortcutPads = Shortcuts(composed.transform, profile, config, length, fallback);
```

로 바꾼다.

- [ ] **Step 3: 지름길 굽기 함수를 더한다** — `BoostPads(` 함수 정의 바로 위에:

```csharp
        //  지름길 하나 = 위쪽 상자 + 혀(돌출 다각형) + 패드 + 검사기용 표시.
        //  표시는 <b>Transform만 있는</b> 빈 GameObject다 — 맵 씬은 서버도 읽으므로 클라 전용 컴포넌트를
        //  붙이면 서버에서 missing script가 되어 씬 주입이 끊긴다.
        private static int Shortcuts(Transform parent, LOP.MapTools.CourseProfile profile,
                                     LOP.MasterData.FlappyConfig config, float length, Material fallback)
        {
            int pads = 0;
            float span = BoostPadDuration * config.ForwardSpeed * config.DashMult;
            foreach (LOP.MapTools.ShortcutRect r in profile.Shortcuts)
            {
                float mid = (r.X0 + r.X1) * 0.5f;
                Material skin = SectionMaterial(mid, length, fallback);

                var roof = Box(parent, $"ShortcutRoof_{r.X0:F0}", skin);
                roof.transform.localScale = new Vector3(r.X1 - r.X0, WallThickness, PipeDepth);
                roof.transform.position = new Vector3(mid, r.Y1 + WallThickness * 0.5f, PipeZ);

                var tongue = new Vector2[4];
                for (int i = 0; i < 4; i++) { tongue[i] = new Vector2(r.Tongue[i * 2], r.Tongue[i * 2 + 1]); }
                Prism(parent, $"ShortcutTongue_{r.X0:F0}", tongue, skin);

                var marker = new GameObject($"Shortcut_{r.X0:F0}");
                marker.transform.SetParent(parent, worldPositionStays: false);
                marker.transform.position = new Vector3(mid, r.CenterY, 0f);
                marker.transform.localScale = new Vector3(r.X1 - r.X0, r.Y1 - r.Y0, 1f);
                Undo.RegisterCreatedObjectUndo(marker, "Build classic course");

                float? padX = LOP.MapTools.ShortcutRule.PadCenterX(r, span, BoostPadWidth, ShortcutExitClear);
                if (padX.HasValue == false)
                {
                    Debug.LogWarning($"[전통 코스] x={r.X0:F0} 지름길이 짧아 패드를 못 놓았다 ({r.Length:F1}m)");
                    continue;
                }
                float padY = r.CenterY;
                float padHeight = LOP.MapTools.BoostPadRule.Fit(
                    ref padY, r.Y1 - r.Y0, r.Y0 + BoostPadClearance, r.Y1 - BoostPadClearance);
                BoostPad(parent, $"BoostPad_{padX.Value:F0}", padX.Value, padY, padHeight, fallback);
                pads++;
            }
            return pads;
        }

        //  z로 돌출한 볼록 다각형. 그려지는 면은 z [−2.5, 0], 콜라이더는 z [−1.25, +1.25] —
        //  Box()가 지키는 판정면 정렬 규약과 같다(원근 카메라가 틈을 좁게 그리지 않게).
        private static GameObject Prism(Transform parent, string name, Vector2[] polygon, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.layer = LayerMask.NameToLayer("Default");
            go.AddComponent<MeshFilter>().sharedMesh = PrismMesh(name, polygon, PipeZ - PipeDepth * 0.5f, PipeZ + PipeDepth * 0.5f);
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.AddComponent<MeshCollider>().sharedMesh = PrismMesh(name + "_Collider", polygon, -PipeDepth * 0.5f, PipeDepth * 0.5f);
            Undo.RegisterCreatedObjectUndo(go, "Build classic course");
            return go;
        }

        //  면마다 꼭짓점을 따로 둔다 — 모서리가 각지게 빛받아야 벽으로 읽힌다(공유하면 뭉개진다).
        private static Mesh PrismMesh(string name, Vector2[] poly, float zNear, float zFar)
        {
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            int n = poly.Length;

            //  앞면(카메라 쪽, z가 작은 쪽). 다각형은 반시계라 −z에서 보면 시계 — 유니티 앞면 규칙에 맞는다.
            int front = vertices.Count;
            for (int i = 0; i < n; i++) { vertices.Add(new Vector3(poly[i].x, poly[i].y, zNear)); }
            for (int i = 1; i < n - 1; i++) { triangles.Add(front); triangles.Add(front + i + 1); triangles.Add(front + i); }

            int back = vertices.Count;
            for (int i = 0; i < n; i++) { vertices.Add(new Vector3(poly[i].x, poly[i].y, zFar)); }
            for (int i = 1; i < n - 1; i++) { triangles.Add(back); triangles.Add(back + i); triangles.Add(back + i + 1); }

            for (int i = 0; i < n; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % n];
                int s = vertices.Count;
                vertices.Add(new Vector3(a.x, a.y, zNear));
                vertices.Add(new Vector3(b.x, b.y, zNear));
                vertices.Add(new Vector3(b.x, b.y, zFar));
                vertices.Add(new Vector3(a.x, a.y, zFar));
                triangles.Add(s); triangles.Add(s + 1); triangles.Add(s + 2);
                triangles.Add(s); triangles.Add(s + 2); triangles.Add(s + 3);
            }

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
```

그리고 상수 블록(`BoostPadMinHeight` 선언 아래)에 더한다:

```csharp
        //  지름길 패드의 부스트가 출구보다 이만큼 먼저 끝나야 한다(spec §4).
        private const float ShortcutExitClear = 1.5f;
```

- [ ] **Step 4: 배경이 코스 높이를 따라가게 한다** — `Backdrop` 호출

```csharp
            Backdrop(composed.transform, "Midground",
                     LOP.MapTools.BackdropLayout.Midground(StartX, length, MidgroundSeed),
                     MidgroundZ, MidgroundDepth, FlappyCityMaterials.Midground);
```

에 인자 `centerAt`을 끝에 더하고, `Backdrop` 정의 시그니처에 `System.Func<float, float> liftAt` 를 마지막 인자로 더한 뒤 본문의

```csharp
                go.transform.position = new Vector3(b.X, b.CenterY, z);
```

를

```csharp
                //  중간층은 코스 높이를 따라간다 — 40m 계곡에 내려가면 평지 기준 건물이 화면 위로 사라진다.
                go.transform.position = new Vector3(b.X, b.CenterY + liftAt(b.X), z);
```

로 바꾼다. (배경 스카이라인은 82m 거리라 화면 세로 59.7m를 담으므로 그대로 둔다 — spec §11.)

- [ ] **Step 5: 로그 줄을 바꾼다** — `Debug.Log($"[전통 코스] ...` 의

```csharp
                    + $" · 고저차 ±{LOP.MapTools.CourseElevation.AmpStart:F0}~{LOP.MapTools.CourseElevation.AmpEnd:F0}m"
                    + $" (파장 {LOP.MapTools.CourseElevation.Wavelength:F0}m)"
```

를

```csharp
                    + $" · 높낮이 {profile.MinY:F0}~{profile.MaxY:F0}m (조각 꼭짓점 {profile.VertexCount}개)"
                    + $" · 지름길 {profile.Shortcuts.Count}개 (패드 {shortcutPads}개)"
```

로 바꾼다.

- [ ] **Step 5b: 도전 구간 패드의 `RampLift`를 걷어낸다** — `RampLift`는 "바닥이 고정 격자의 현"이라는 전제로 현을 다시 계산하는 함수였다. 이제 바닥·천장이 꺾은선 꼭짓점에서 끊기므로 **실제 면 = `centerAt` 그대로**다. 격자 전제가 남아 있으면 틀린 현을 재서 패드를 엉뚱하게 깎는다. `BoostPads` 안의

```csharp
                    float lift = RampLift(sampleX, spacing, centerAt);
```

를

```csharp
                    //  바닥·천장이 꺾은선 꼭짓점에서 끊기므로 실제 면이 곧 centerAt이다.
                    float lift = centerAt(sampleX);
```

로 바꾸고, `RampLift` 함수 정의(요약 주석 포함)를 지운다. 그 위 루프 주석 중 "곡선이 아니라 현을 재야 한다 …" 세 줄도 지운다.

- [ ] **Step 6: 사인 물결을 지운다**

```bash
git rm Assets/Scripts/MapTools/CourseElevation.cs Assets/Scripts/MapTools/CourseElevation.cs.meta \
       Assets/Tests/EditMode/MapTools/CourseElevationTests.cs Assets/Tests/EditMode/MapTools/CourseElevationTests.cs.meta
grep -rn "CourseElevation" Assets/Scripts Assets/Tests   # 아무것도 안 나와야 한다
```

`ClassicCourse.cs`의 `<see cref="CourseElevation"/>` 주석 참조는 `<see cref="CourseProfile.CenterAt"/>`로 바꾼다.

- [ ] **Step 7: 컴파일·전체 테스트 PASS.**

- [ ] **Step 8: 굽고 눈으로 본다** — 명령 모음대로 맵 씬 열기 → 굽기 → `save_scene`. 콘솔의 `[전통 코스]` 줄에 `지름길 2개 (패드 2개)`, 높낮이 범위가 40m 이상인지 본다. 다음 eval로 지름길이 뚫려 있고 혀·위쪽 상자가 제자리인지 잰다(`/tmp/ev/shortcut.cs`):

```csharp
var sb = new System.Text.StringBuilder();
int mask = UnityEngine.LayerMask.GetMask("Default");
foreach (UnityEngine.Transform t in UnityEngine.GameObject.Find("ComposedMap").transform)
{
    if (t.name.StartsWith("Shortcut_") == false) continue;
    var c = t.position; var s = t.localScale;
    // 가운데 절반을 수평으로 쓸어 본다 — 뚫려 있어야 한다
    bool blocked = UnityEngine.Physics.CapsuleCast(
        new UnityEngine.Vector3(c.x - s.x * 0.25f, c.y - 0.45f, 0f), new UnityEngine.Vector3(c.x - s.x * 0.25f, c.y + 0.45f, 0f),
        0.45f, UnityEngine.Vector3.right, out UnityEngine.RaycastHit hit, s.x * 0.5f, mask);
    // 바로 아래(혀)와 바로 위(상자)는 막혀 있어야 한다
    bool below = UnityEngine.Physics.CheckSphere(new UnityEngine.Vector3(c.x, c.y - s.y * 0.5f - 0.3f, 0f), 0.2f, mask);
    bool above = UnityEngine.Physics.CheckSphere(new UnityEngine.Vector3(c.x, c.y + s.y * 0.5f + 0.3f, 0f), 0.2f, mask);
    sb.AppendLine($"{t.name} 길이 {s.x:F1} · 가운데 {(blocked ? "막힘 " + hit.collider.name : "뚫림")} · 아래 {(below ? "벽" : "빔!")} · 위 {(above ? "벽" : "빔!")}");
}
return sb.ToString();
```

```bash
unity cmd eval_file /tmp/ev/shortcut.cs
```

기대: 두 줄 모두 `가운데 뚫림 · 아래 벽 · 위 벽`. 하나라도 어긋나면 Step 3의 위쪽 상자·혀 좌표부터 본다.

그리고 씬을 **닫았다 다시 열어** 혀 메시가 남는지 확인한다(절차적 메시가 씬에 저장되는지):

```bash
unity cmd open_scene Assets/Scenes/Entrance.unity; unity cmd open_scene Assets/Art/Scenes/FlappyRaceMap.unity
unity cmd eval_file /tmp/ev/shortcut.cs     # 다시 같은 결과여야 한다
```

- [ ] **Step 9: 커밋** (클라만 — 맵 씬은 Task 8에서 Art에 커밋한다)

```bash
git add Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs Assets/Scripts/MapTools/ClassicCourse.cs
git status --short     # CourseElevation 네 파일이 D로 스테이지돼 있어야 한다
git commit -m "feat(flappy): 코스를 지형 조각으로 굽는다 — U 지름길(위쪽 상자·혀·패드), 배경이 높이를 따라간다"
```

---

### Task 6: 탐색이 살아 있는 상태만 훑는다 (성능)

높이 범위가 ±11m → 최대 ±60m로 넓어져, 열마다 상태표 전체를 훑는 지금 방식은 칸 수에 비례해 느려진다. 살아 있는 상태만 목록으로 들고 다닌다. **결과는 한 비트도 안 바뀌어야 한다**(중복 제거가 값 기준이라 순서와 무관 — `TryAdvance` 주석).

**Files:**
- Modify: `Assets/Scripts/MapTools/CleanRunSearch.cs` — `Run`, `TryAdvance`, `Archive`

- [ ] **Step 1: 바꾸기 전 시간을 잰다** — Task 5에서 구운 맵으로 빠른 검사를 돌리고(백그라운드), 끝나면:

```bash
grep "^\[맵 검사\] 클린런" ~/Library/Logs/Unity/Editor.log | tail -1     # "전수 탐색+재생 Nms" 를 적어 둔다
```

- [ ] **Step 2: `TryAdvance`에 살아 있는 목록을 넘긴다** — 시그니처 끝 `float[] next)` 를 `float[] next, List<int> nextLive)` 로, 본문 끝의

```csharp
            if (float.IsNaN(next[index]) || ny > next[index])
            {
                next[index] = ny;
            }
            return true;
```

를

```csharp
            if (float.IsNaN(next[index]))
            {
                nextLive.Add(index);
                next[index] = ny;
            }
            else if (ny > next[index])
            {
                next[index] = ny;
            }
            return true;
```

로 바꾼다.

- [ ] **Step 3: `Archive`가 목록만 훑게 한다** — 시그니처를 `static Column Archive(float[] dense, List<int> live, SearchGrid grid, out int count, out float span)`로 바꾸고 본문을:

```csharp
        {
            //  칸 번호 오름차순이어야 한다 — 되짚기가 "가장 낮은 칸부터" 고르는 전제다.
            live.Sort();
            count = live.Count;
            int lo = int.MaxValue, hi = int.MinValue;
            var states = new int[count];
            var heights = new float[count];
            for (int n = 0; n < count; n++)
            {
                int i = live[n];
                grid.Decode(i, out int bucket, out _, out _);
                if (bucket < lo) { lo = bucket; }
                if (bucket > hi) { hi = bucket; }
                states[n] = i;
                heights[n] = dense[i];
            }
            span = count == 0 ? 0f : (hi - lo) * grid.HeightGrid;
            return new Column(states, heights);
        }
```

로 바꾼다. (위의 "열 하나를 되짚기용으로 압축한다" 주석은 그대로 둔다.)

- [ ] **Step 4: `Run`의 열 루프를 두 버퍼 교대로 바꾼다** — `var current = NewColumn(grid.StateCount);` 다음 줄에 `var currentLive = new List<int>();`를, 시드 줄

```csharp
            current[grid.StateIndex(grid.HeightBucket(options.StartY), ladder: 1, rung: 0)] = options.StartY;
```

을

```csharp
            int seed = grid.StateIndex(grid.HeightBucket(options.StartY), ladder: 1, rung: 0);
            current[seed] = options.StartY;
            currentLive.Add(seed);
            //  버퍼 둘을 번갈아 쓴다 — 열마다 상태표 전체를 새로 만들고 NaN으로 채우는 비용이 칸 수에
            //  비례해서, 높이 범위가 넓은 코스에서 탐색 시간의 대부분이 됐다. 다 쓴 칸만 비운다.
            var spare = NewColumn(grid.StateCount);
            var spareLive = new List<int>();
```

로, 그 아래 `var columns = new List<Column> { Archive(current, grid, out _, out _) };` 를 `var columns = new List<Column> { Archive(current, currentLive, grid, out _, out _) };` 로 바꾼다.

열 루프 안의

```csharp
                var next = NewColumn(grid.StateCount);
                bool any = false;

                for (int state = 0; state < grid.StateCount; state++)
                {
                    float y = current[state];
                    if (float.IsNaN(y))
                    {
                        continue;
                    }
```

를

```csharp
                var next = spare;
                var nextLive = spareLive;
                bool any = false;

                for (int li = 0; li < currentLive.Count; li++)
                {
                    int state = currentLive[li];
                    float y = current[state];
```

로, 두 `TryAdvance(...)` 호출의 끝 인자 `next)` 를 `next, nextLive)` 로 바꾼다. 그리고

```csharp
                columns.Add(Archive(next, grid, out int liveCount, out float liveSpan));
```

를

```csharp
                columns.Add(Archive(next, nextLive, grid, out int liveCount, out float liveSpan));
```

로, 루프 끝의 `current = next;` 를

```csharp
                //  다 쓴 버퍼는 쓴 칸만 NaN으로 되돌려 다음 열의 빈 버퍼로 돌린다.
                for (int li = 0; li < currentLive.Count; li++) { current[currentLive[li]] = float.NaN; }
                currentLive.Clear();
                spare = current;
                spareLive = currentLive;
                current = next;
                currentLive = nextLive;
```

로 바꾼다.

- [ ] **Step 5: 컴파일·전체 테스트 PASS.** CleanRunSearch 테스트(탐색 결과·되짚기)가 전부 그대로 초록이어야 한다 — 결과가 같다는 증거다.

- [ ] **Step 6: 다시 잰다** — Step 1과 같은 맵으로 빠른 검사를 돌려 `전수 탐색+재생` 시간과 **클린런 판정(✅/🟡/❌)이 Step 1과 같은지** 본다. 판정이 다르면 이 태스크를 되돌리고 원인을 찾는다(결과가 달라지면 안 되는 최적화다).

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/MapTools/CleanRunSearch.cs
git status --short
git commit -m "perf(maptools): 탐색이 살아 있는 상태만 훑는다 — 높이 범위가 넓어져도 생존 수에만 비례"
```

---

### Task 7: 검사기가 두 길을 따로 증명한다

**Files:**
- Modify: `Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs`

**Interfaces:**
- Consumes: `ShortcutRect.FromCenterSize`, `ShortcutRule.ForbidsShortcut/ForbidsValley/Section`, `ShortcutProof`, `PlayabilityReport.Build(..., shortcutSection)` (Tasks 2, 4), 씬 표시 `Shortcut_*` (Task 5)

- [ ] **Step 1: 지름길을 읽는다** — `ReadSpawns()` 정의 바로 위에:

```csharp
        //  빌더가 남긴 표시(Transform만 있는 빈 GameObject)에서 지름길 사각형을 되살린다.
        //  위치 = 중심, 크기 = (길이, 세로 폭).
        private static List<LOP.MapTools.ShortcutRect> ReadShortcuts()
        {
            var shortcuts = new List<LOP.MapTools.ShortcutRect>();
            var composed = GameObject.Find("ComposedMap");
            if (composed == null) { return shortcuts; }
            foreach (Transform t in composed.transform)
            {
                if (t.name.StartsWith("Shortcut_", System.StringComparison.Ordinal) == false) { continue; }
                Vector3 c = t.position, s = t.localScale;
                shortcuts.Add(LOP.MapTools.ShortcutRect.FromCenterSize(c.x, c.y, s.x, s.y));
            }
            shortcuts.Sort((a, b) => a.X0.CompareTo(b.X0));
            return shortcuts;
        }
```

- [ ] **Step 2: 기존 클린런 탐색은 지름길을 막고 돌린다** — `var searchSweep = SearchTickSweep(shape, mapMask, query);` 바로 다음 줄에:

```csharp
            //  ①의 탐색은 <b>지름길을 막고</b> 돈다 — 그래야 탐색으로 얻은 ✅가 "안전한 길이 있다"는 뜻이 된다.
            //  (봇은 지름길을 모르고 날아서 봇 ✅는 이 약속 밖이다 — 그래서 🔀 절이 따로 증명한다.)
            var shortcuts = ReadShortcuts();
            LOP.MapTools.TickSweepProbe mainSweep = shortcuts.Count == 0
                ? searchSweep
                : (x, y, vy) => LOP.MapTools.ShortcutRule.ForbidsShortcut(shortcuts, x, y) == false
                                && searchSweep(x, y, vy);
```

를 넣고, 클린런 루프 안의 `var result = LOP.MapTools.CleanRunSearch.Run(options, grid.IsFreeExact, searchSweep);` 를 `var result = LOP.MapTools.CleanRunSearch.Run(options, grid.IsFreeExact, mainSweep);` 로 바꾼다.

- [ ] **Step 3: 두 길 증명을 돌린다** — 클린런 루프가 끝나고 `cleanRunWatch.Stop();` 바로 **앞**에:

```csharp
                //  🔀 지름길 — 스폰 1에서 두 길을 따로 날린다. 지름길이 없으면 절 자체가 "없다"를 말한다.
                shortcutSection = ProveShortcuts(spawns[0].Position, finishX, shape, mapMask, query,
                                                 grid, searchSweep, mainSweep, shortcuts);
```

그리고 이 변수를 `Run` 함수의 클린런 블록 **밖**(보고서를 만드는 곳에서 보이게, `var cleanRuns = ...` 선언 근처)에 `string shortcutSection = null;` 로 선언한다. 보고서 `PlayabilityReport.Build(` 호출의 마지막 인자(`LOP.MapTools.BoostPadRule.Section(...)`) 뒤에 `, shortcutSection` 를 더한다.

증명 함수를 `ReadShortcuts()` 아래에 더한다:

```csharp
        private static string ProveShortcuts(Vector3 start, float finishX, in FlappyShape shape, int mapMask,
                                             GameFramework.Physics.ICollisionQuery query, FreeSpaceGrid grid,
                                             LOP.MapTools.TickSweepProbe searchSweep,
                                             LOP.MapTools.TickSweepProbe noShortcutSweep,
                                             List<LOP.MapTools.ShortcutRect> shortcuts)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            //  in 매개변수는 로컬 함수·람다가 잡을 수 없다(CS1628) — 복사본을 잡는다.
            FlappyShape body = shape;
            var options = new LOP.MapTools.CleanRunOptions(
                startX: start.x, startY: start.y, finishX: finishX,
                minY: SearchMinY, maxY: SearchMaxY,
                forwardSpeed: body.ForwardSpeed, flapImpulse: body.FlapImpulse,
                gravity: body.Gravity, maxFallSpeed: body.MaxFallSpeed,
                tickSeconds: TickSeconds, heightGrid: HeightGrid);

            LOP.MapTools.ShortcutProof Prove(string label, float x0, float x1, LOP.MapTools.TickSweepProbe probe)
            {
                var result = LOP.MapTools.CleanRunSearch.Run(options, grid.IsFreeExact, probe);
                if (result.Reachable == false)
                {
                    return new LOP.MapTools.ShortcutProof(label, x0, x1, false, false, result.BlockedX);
                }
                float[] heights = LOP.MapTools.CleanRunSearch.PathHeights(options, result.Flaps);
                bool verified = VerifyByReplay(start, result.Flaps, body, mapMask, query, heights, out _);
                return new LOP.MapTools.ShortcutProof(label, x0, x1, true, verified, 0f);
            }

            var safe = Prove("지름길 없이", 0f, 0f, noShortcutSweep);
            var proofs = new List<LOP.MapTools.ShortcutProof>();
            foreach (LOP.MapTools.ShortcutRect r in shortcuts)
            {
                LOP.MapTools.ShortcutRect only = r;
                proofs.Add(Prove("", r.X0, r.X1,
                    (x, y, vy) => LOP.MapTools.ShortcutRule.ForbidsValley(only, x, y) == false && searchSweep(x, y, vy)));
            }
            Debug.Log($"[맵 검사] 지름길 증명 {proofs.Count + 1}번 — {watch.ElapsedMilliseconds}ms");
            return LOP.MapTools.ShortcutRule.Section(safe, proofs);
        }
```

(`VerifyByReplay`의 `path` 인자는 기본값 null이라 생략한다. `FreeSpaceGrid`는 같은 파일 안의 private 중첩 클래스다.)

- [ ] **Step 4: 컴파일·전체 테스트 PASS.**

- [ ] **Step 5: 증명이 실제로 갈리는지 확인한다** — Task 5에서 구운 맵으로 빠른 검사(백그라운드, 30분+). 끝나면:

```bash
sed -n '/🔀 지름길/,/^$/p' Logs/FlappyMapCheck.txt
sed -n '/① 클린런/,/── /p' Logs/FlappyMapCheck.txt | head -12
grep "^\[맵 검사\] 지름길 증명" ~/Library/Logs/Unity/Editor.log | tail -1
```

기대: `지름길 없이 ✅`, 지름길 두 줄 모두 `✅ 들어가서 완주`, 클린런 4자리 ✅. ❌가 나오면 맵(지름길 높이·오르막)을 고칠 일이지 검사를 고칠 일이 아니다 — 리포트의 `탐색 x=`가 가리키는 자리를 eval로 잰다.

- [ ] **Step 6: 금지 영역이 실제로 길을 강제하는지 뮤테이션** — `ProveShortcuts`의 지름길 증명 probe에서 `ForbidsValley(only, x, y) == false &&` 를 지우고 다시 검사하면, 지름길 줄은 여전히 ✅지만 **탐색이 계곡으로 갔는지** 알 수 없다 — 그래서 대신 이렇게 본다: 지름길 입구를 임시로 막는다(빌더의 `Shortcuts`에서 `roof`의 `localScale.y`를 `WallThickness + (r.Y1 - r.Y0) * 2f`, `position.y`를 `r.Y0 + (WallThickness + (r.Y1 - r.Y0) * 2f) * 0.5f`로 바꿔 지름길을 메운다), 굽고, 검사해서 지름길 줄이 **❌ 함정**으로 나오는지 본다. 되돌리고 다시 굽는다. (검사 한 번 30분+ — 이 뮤테이션은 이 슬라이스에서 한 번만 한다.)

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs
git status --short
git commit -m "feat(flappy): 검사기가 지름길 두 길을 따로 증명한다 — 금지 영역으로 길을 강제"
```

---

### Task 8: 종합 검증 · 기록 · 푸시 · 배포

**Files:**
- Modify: `docs/ROADMAP.md`
- Art 서브모듈: `Scenes/FlappyRaceMap.unity`
- 클라: `Assets/Art` 포인터

- [ ] **Step 1: 최종 굽기·검사** — 굽기 → `save_scene` → 빠른 검사. 확인할 것:
  - ① 클린런 4자리 ✅
  - ⚡ 부스트 패드 `✅ 전부 밟을 수 있는 자리다`(도전 구간 패드 + 지름길 패드 2개 — 앞길 검사 포함)
  - 🔀 지름길 `지름길 없이 ✅` + 두 줄 `✅`
  - 🎥 시각 정직성·🧱 층 규약에 새 위반 없음(혀 메시가 처음 들어간 것이라 특히 본다 — spec §11)

- [ ] **Step 2: 로드맵에 이 슬라이스를 기록한다** — `## 상태` 바로 위에 새 절 `## ✅ 뾰족한 지형 조각과 곧은 지름길 (2026-09-24~)`을 쓴다. 담을 것: 왜(옛 터널이 화면 밖·굴뚝, 분리대 안은 억지), 물리 한계 표(내리막 4.4 / 오르막 탭 빈도 표), 구간 표, 지름길 손익 표, 두 길 증명과 "가운데 절반" 이유, 탐색 최적화 전후 시간(Task 6 Step 1·6에서 잰 값), 검사 결과, 열린 것(지름길 높이·폭 튜닝, A 지름길, 도전 구간 수 감소).

- [ ] **Step 3: 커밋(클라)**

```bash
git add docs/ROADMAP.md
git status --short
git commit -m "docs(roadmap): 뾰족한 지형 조각과 곧은 지름길 슬라이스를 기록한다"
```

- [ ] **Step 4: Art 먼저 푸시** — 서브모듈에서 브랜치를 파고 맵 씬만 커밋한 뒤 푸시 규약대로:

```bash
cd Assets/Art
git fetch origin
git checkout -b feature/flappy-terrain-shortcut
git add Scenes/FlappyRaceMap.unity
git status --short      # 씬 하나만
git commit -m "feat(flappy): 코스를 뾰족한 지형 조각과 곧은 지름길로 다시 굽는다"
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/flappy-terrain-shortcut -m "Merge feature/flappy-terrain-shortcut: 뾰족한 지형 조각과 곧은 지름길"
git push origin main
cd ../..
```

- [ ] **Step 5: 클라 포인터 커밋 후 푸시**

```bash
git add Assets/Art
git status --short      # 포인터만 (Jua·PackageManagerSettings는 스테이지 금지)
git commit -m "chore(art): 뾰족한 지형 조각과 곧은 지름길 코스 포인터"
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/flappy-terrain-shortcut -m "Merge feature/flappy-terrain-shortcut: 뾰족한 지형 조각과 곧은 지름길"
git push origin main
```

리베이스 뒤 `git submodule update` 후 recompile로 컴파일을 확인한다(형제 레포가 뒤처졌으면 먼저 당긴다).

- [ ] **Step 6: 배포** — 맵 콘텐츠만 바뀌었다(서버 코드 변경 없음 → 게임서버 재배포 불필요):

```bash
gh workflow run content-deploy.yml -f target=all -f package_ref=main -f prune=false
gh run list --workflow=content-deploy.yml --limit 1
```

끝날 때까지 기다려 잡 6개(plan·client-app·iOS·Windows·OSX·Linux)가 모두 success인지 확인한다.
