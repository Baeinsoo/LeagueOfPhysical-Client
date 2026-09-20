# Flappy 맵 — 무너지는 고층 도시 구현 계획

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 검증이 끝난 그레이박스 코스에 세 층(게임 평면·중간층·배경)과 구간 셋(온전 → 반붕괴 → 붕괴)을 입히고, 경기 기본 길이를 90초로 올린다. 관문 기하는 바꾸지 않는다.

**Architecture:** 배치·분류 규칙은 전부 **순수 C#(`LOP.MapTools`)** 에 두고 EditMode로 잰다. 유니티 쪽은 그 규칙을 읽어 씬을 굽는 **에디터 빌더** 하나와, 진행률로 안개·하늘을 칠하는 **런타임 `ITickable` 하나**뿐이다. 새로 생기는 규약("콜라이더는 게임 평면에만")은 사람이 지키는 게 아니라 **맵 검사**가 지킨다.

**Tech Stack:** Unity 6000.3 / URP · VContainer · NUnit(EditMode) · `GameFramework.Rng.DeterministicRandom` · Luban MasterData

**Spec:** [docs/superpowers/specs/2026-09-20-flappy-collapsing-city-design.md](../specs/2026-09-20-flappy-collapsing-city-design.md)

## Global Constraints

모든 태스크의 요구사항에 아래가 **암묵적으로 포함된다.**

- **LOP-Shared와 서버는 손대지 않는다.** 건드려야 할 일이 생기면 설계가 틀렸다는 신호다.
- **관문 기하 불변.** 창은 `GateRhythmRule.TargetWindow`, 간격은 `TargetSpacing(ForwardSpeed)`에서 계속 나온다. 미터를 새로 박지 않는다.
- **게임 평면의 밝기·대비는 구간이 진행돼도 낮추지 않는다.** 그을음은 색조로만. 구체 규칙: **게임 평면 재질의 알베도 휘도(`0.2126R+0.7152G+0.0722B`)를 0.60 아래로 내리지 않는다.** 어두워지는 것은 하늘·중간층·배경뿐이다.
- **콜라이더는 게임 평면에만.** 중간층·배경 오브젝트는 `BoxCollider`를 지운다.
- **구간 경계는 코스 길이의 ⅓·⅔로 계산한다.** 미터 상수로 박지 않는다.
- **안개는 런타임이 소유한다.** 맵이 additive로 로드돼 씬의 `RenderSettings`는 무시되므로 `FlappyAtmosphere`가 매 틱 쓴다. 시작값을 기억해 `Dispose`에서 되돌리고, **스카이박스는 복제본만** 칠한다(원본은 서브모듈 파일이다).
- **`Physics.SyncTransforms()`** — 빌더가 콜라이더를 옮긴 뒤 반드시 부른다. 이 프로젝트는 `autoSyncTransforms`를 끄고 산다.
- **git**: `main` 직접 커밋 금지 · `git add -A` / `git commit -a` 금지(경로 지정) · `--force` push 금지. 로컬 픽스처(`Assets/Art` 포인터, `Jua-Regular SDF.asset`, `PackageManagerSettings.asset`)를 절대 스테이지하지 않는다.
- **`.meta` 파일은 유니티가 만든 것을 함께 커밋한다.** 손으로 만들지 않는다.
- **Art 서브모듈**(`Assets/Art`)의 변경은 Art 레포에서 따로 커밋·푸시하고, 클라의 포인터가 그 뒤를 따른다. **콘텐츠 빌드는 그 push 뒤에** 돌린다.
- **씬을 유니티에서 열어 둔 채 씬 파일을 git으로 되돌리지 않는다.** 외부 변경 대화상자가 메인 스레드를 막는다.

---

## File Structure

**새로 만드는 것 (클라)**

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/MapTools/CourseSection.cs` | 진행률 → 구간(순수). 경계 계산이 여기 한 곳 |
| `Assets/Scripts/MapTools/LayerContract.cs` | 층 규약 판정 + 리포트 절(순수) |
| `Assets/Scripts/MapTools/BackdropLayout.cs` | 중간층·배경 실루엣 배치(순수) |
| `Assets/Scripts/Game/FlappySkyGradient.cs` | 진행률 → 안개색·밀도·하늘틴트(순수) |
| `Assets/Scripts/Game/FlappyAtmosphere.cs` | 위 곡선을 매 틱 `RenderSettings`에 쓴다 |
| `Assets/Scripts/Editor/FlappyCityMaterials.cs` | 재질 6종을 없을 때만 만든다 |
| `Assets/Tests/EditMode/MapTools/CourseSectionTests.cs` | |
| `Assets/Tests/EditMode/MapTools/LayerContractTests.cs` | |
| `Assets/Tests/EditMode/MapTools/BackdropLayoutTests.cs` | |
| `Assets/Tests/Editor/FlappySkyGradientTests.cs` | |
| `Assets/Tests/Editor/FlappyAtmosphereTests.cs` | |

**고치는 것 (클라)**

| 파일 | 무엇 |
|---|---|
| `Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs` | 90초 · 구간별 재질 · 중간층/배경 굽기 |
| `Assets/Scripts/MapTools/PlayabilityReport.cs` | 층 규약 절 자리 |
| `Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs` | 층 규약 수집·전달 |
| `Assets/Scripts/Game/FlappyChaserView.cs` | 벽 두께·재질·먼지 |
| `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs` | `FlappyAtmosphere` 등록 |

**새로 만드는 것 (Art 서브모듈)**

`Assets/Art/Environment/FlappyRace/` 아래 재질 5개 — `CityIntact.mat` · `CityExposed.mat` · `CityCharred.mat` · `Midground.mat` · `Skyline.mat`. (추격자 벽 재질은 Art에 두지 않는다 — 런타임이 코드로 만들므로 에셋을 두면 아무도 안 읽는 채 값만 갈라진다.) 그리고 다시 구운 `Assets/Art/Scenes/FlappyRaceMap.unity`.

---

### Task 1: 구간 규칙

경계를 한 곳에 못박는다. 이후 모든 태스크가 여기서 구간을 묻는다.

**Files:**
- Create: `Assets/Scripts/MapTools/CourseSection.cs`
- Test: `Assets/Tests/EditMode/MapTools/CourseSectionTests.cs`

**Interfaces:**
- Produces: `enum CourseSection { Intact, Exposed, Charred }` · `CourseSectionRule.Progress(float x, float startX, float courseLength) → float` (0~1 클램프) · `CourseSectionRule.Of(float x, float startX, float courseLength) → CourseSection` · `CourseSectionRule.Count = 3`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/EditMode/MapTools/CourseSectionTests.cs`:

```csharp
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 코스를 셋으로 가르는 규칙. <b>미터가 아니라 비율</b>이라는 것이 이 테스트의 요점이다 —
    /// 길이가 바뀌면 경계가 따라 움직여야 한다.
    /// </summary>
    public class CourseSectionTests
    {
        [Test]
        public void 진행률은_시작에서_0_끝에서_1이다()
        {
            Assert.AreEqual(0f, CourseSectionRule.Progress(0f, 0f, 612f), 1e-4f);
            Assert.AreEqual(1f, CourseSectionRule.Progress(612f, 0f, 612f), 1e-4f);
            Assert.AreEqual(0.5f, CourseSectionRule.Progress(306f, 0f, 612f), 1e-4f);
        }

        [Test]
        public void 코스_밖은_0과_1로_잘린다()
        {
            //  스폰은 시작선보다 뒤, 결승 연출은 끝보다 앞일 수 있다 — 거기서 값이 튀면 안 된다.
            Assert.AreEqual(0f, CourseSectionRule.Progress(-50f, 0f, 612f), 1e-4f);
            Assert.AreEqual(1f, CourseSectionRule.Progress(900f, 0f, 612f), 1e-4f);
        }

        [Test]
        public void 길이가_0이면_전부_시작으로_본다()
        {
            //  나누기가 터지는 자리. 코스가 아직 안 구워진 상태에서도 불릴 수 있다.
            Assert.AreEqual(0f, CourseSectionRule.Progress(100f, 0f, 0f), 1e-4f);
            Assert.AreEqual(CourseSection.Intact, CourseSectionRule.Of(100f, 0f, 0f));
        }

        [Test]
        public void 세_구간은_삼등분이다()
        {
            Assert.AreEqual(CourseSection.Intact, CourseSectionRule.Of(10f, 0f, 612f));
            Assert.AreEqual(CourseSection.Exposed, CourseSectionRule.Of(300f, 0f, 612f));
            Assert.AreEqual(CourseSection.Charred, CourseSectionRule.Of(600f, 0f, 612f));
        }

        [Test]
        public void 경계는_뒤_구간에_속한다()
        {
            //  경계가 어느 쪽인지 안 정해 두면 빌더와 검사기가 서로 다른 답을 낸다.
            Assert.AreEqual(CourseSection.Exposed, CourseSectionRule.Of(204f, 0f, 612f));
            Assert.AreEqual(CourseSection.Charred, CourseSectionRule.Of(408f, 0f, 612f));
        }

        [Test]
        public void 길이가_바뀌면_경계도_따라_움직인다()
        {
            //  미터를 박았다면 이 테스트가 깨진다. 419m 코스에서 204m는 이미 2구간이다.
            Assert.AreEqual(CourseSection.Exposed, CourseSectionRule.Of(204f, 0f, 419f));
            Assert.AreEqual(CourseSection.Intact, CourseSectionRule.Of(130f, 0f, 419f));
        }

        [Test]
        public void 시작선이_0이_아니어도_된다()
        {
            Assert.AreEqual(CourseSection.Intact, CourseSectionRule.Of(60f, 50f, 300f));
            Assert.AreEqual(CourseSection.Charred, CourseSectionRule.Of(330f, 50f, 300f));
        }
    }
}
```

- [ ] **Step 2: 돌려서 실패를 본다**

Unity 에디터에서 **Window ▸ General ▸ Test Runner ▸ EditMode**, 또는 CLI:
`unity run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --filter CourseSectionTests`

기대: 컴파일 실패 — `CourseSectionRule`이 없다.

- [ ] **Step 3: 최소 구현**

`Assets/Scripts/MapTools/CourseSection.cs`:

```csharp
namespace LOP.MapTools
{
    /// <summary>코스가 얼마나 무너졌는가. 순서가 곧 진행 방향이다.</summary>
    public enum CourseSection
    {
        /// <summary>아직 서 있는 사무실.</summary>
        Intact = 0,
        /// <summary>반쯤 무너진 중층 — 유리가 깨지고 철골이 드러났다.</summary>
        Exposed = 1,
        /// <summary>붕괴 한가운데 — 그을린 철골과 잔불.</summary>
        Charred = 2,
    }

    /// <summary>
    /// 코스의 어디쯤인가를 구간으로 바꾼다.
    ///
    /// <para><b>왜 비율인가</b>: 경계를 미터로 박으면 코스 길이를 바꿨을 때 조용히 틀려진다
    /// (회랑 하한 4.912가 물리 변경 뒤에도 문서에 남아 있던 사고와 같은 종류다). 길이가
    /// 60초에서 90초로 늘어도 "앞 3분의 1은 멀쩡하다"는 뜻은 그대로여야 한다.</para>
    /// </summary>
    public static class CourseSectionRule
    {
        /// <summary>구간 개수. 삼등분이라는 사실이 여기 한 곳에만 있다.</summary>
        public const int Count = 3;

        /// <summary>코스를 0(시작)~1(끝)로 잰다. 코스 밖은 잘린다.</summary>
        public static float Progress(float x, float startX, float courseLength)
        {
            if (courseLength <= 0f)
            {
                return 0f;
            }
            float t = (x - startX) / courseLength;
            return t < 0f ? 0f : (t > 1f ? 1f : t);
        }

        /// <summary>경계값은 <b>뒤 구간</b>에 속한다(204m는 2구간의 첫 미터다).</summary>
        public static CourseSection Of(float x, float startX, float courseLength)
        {
            float t = Progress(x, startX, courseLength);
            int index = (int)(t * Count);
            if (index >= Count)
            {
                index = Count - 1;   // t == 1(결승선)은 마지막 구간이다
            }
            return (CourseSection)index;
        }
    }
}
```

- [ ] **Step 4: 다시 돌려 통과를 본다**

같은 명령. 기대: 7개 전부 PASS.

- [ ] **Step 5: 일부러 깨뜨려 빨강을 확인한다**

`index = (int)(t * Count)` 를 `index = 0`으로 바꿔 돌린다. **최소 4개가 실패해야 한다.** 확인했으면 되돌린다. (이 프로젝트에서 "초록인데 아무것도 안 지키는 테스트"가 한 슬라이스에 여섯 개 나온 적이 있다.)

- [ ] **Step 6: 커밋**

```bash
git add Assets/Scripts/MapTools/CourseSection.cs Assets/Scripts/MapTools/CourseSection.cs.meta \
        Assets/Tests/EditMode/MapTools/CourseSectionTests.cs Assets/Tests/EditMode/MapTools/CourseSectionTests.cs.meta
git status --short
git commit -m "feat(maptools): 코스를 비율로 삼등분하는 구간 규칙"
```

---

### Task 2: 층 규약 검사

**이 태스크가 이번 작업의 안전장치다.** 씬을 바꾸기 *전에* 넣어서, 이후 모든 굽기가 처음부터 검사를 받게 한다.

**Files:**
- Create: `Assets/Scripts/MapTools/LayerContract.cs`
- Test: `Assets/Tests/EditMode/MapTools/LayerContractTests.cs`
- Modify: `Assets/Scripts/MapTools/PlayabilityReport.cs` (`Build`에 인자 하나 + 절 출력)
- Modify: `Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs` (씬에서 수집해 전달)

**Interfaces:**
- Consumes: 없음
- Produces: `readonly struct LayerBlock(string name, float x, bool isGameplay, bool hasCollider, string materialName)` · `readonly struct LayerViolation(string name, float x, string reason)` · `LayerContract.Check(IReadOnlyList<LayerBlock>, IReadOnlyCollection<string> gameplayMaterials) → List<LayerViolation>` · `LayerContract.Section(IReadOnlyList<LayerBlock>, IReadOnlyCollection<string>) → string`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/EditMode/MapTools/LayerContractTests.cs`:

```csharp
using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 세 층을 가르고 나면 눈으로 못 잡는 버그가 둘 생긴다 —
    /// <b>배경인데 부딪힌다</b>, <b>장애물인데 배경처럼 보인다</b>.
    /// 그 둘을 기계가 잡는다.
    /// </summary>
    public class LayerContractTests
    {
        static readonly string[] Gameplay = { "CityIntact", "CityExposed", "CityCharred" };

        static LayerBlock Block(string name, bool isGameplay, bool hasCollider, string material)
            => new LayerBlock(name, x: 10f, isGameplay, hasCollider, material);

        [Test]
        public void 제대로_된_맵은_위반이_없다()
        {
            var blocks = new List<LayerBlock>
            {
                Block("PipeLow_11", isGameplay: true, hasCollider: true, "CityIntact"),
                Block("Midground_40", isGameplay: false, hasCollider: false, "Midground"),
                Block("Skyline_80", isGameplay: false, hasCollider: false, "Skyline"),
            };

            Assert.AreEqual(0, LayerContract.Check(blocks, Gameplay).Count);
        }

        [Test]
        public void 배경에_콜라이더가_있으면_위반이다()
        {
            //  "안 닿을 줄 알았는데 닿는다" — 플레이어가 원인을 짚을 수 없는 종류다.
            var blocks = new List<LayerBlock>
            {
                Block("Midground_40", isGameplay: false, hasCollider: true, "Midground"),
            };

            var bad = LayerContract.Check(blocks, Gameplay);

            Assert.AreEqual(1, bad.Count);
            Assert.That(bad[0].Reason.IndexOf("콜라이더", System.StringComparison.Ordinal),
                        Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void 게임_평면에_콜라이더가_없으면_위반이다()
        {
            //  반대 방향 — 장애물처럼 선명하게 그려 놓고 통과된다.
            var blocks = new List<LayerBlock>
            {
                Block("PipeHigh_22", isGameplay: true, hasCollider: false, "CityIntact"),
            };

            Assert.AreEqual(1, LayerContract.Check(blocks, Gameplay).Count);
        }

        [Test]
        public void 게임_평면이_배경_재질을_쓰면_위반이다()
        {
            //  읽는 규칙("선명하고 테두리가 밝으면 닿는 것")이 깨지는 자리다.
            var blocks = new List<LayerBlock>
            {
                Block("PipeLow_33", isGameplay: true, hasCollider: true, "Skyline"),
            };

            var bad = LayerContract.Check(blocks, Gameplay);

            Assert.AreEqual(1, bad.Count);
            Assert.That(bad[0].Reason.IndexOf("재질", System.StringComparison.Ordinal),
                        Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void 재질이_없는_게임_평면도_위반이다()
        {
            var blocks = new List<LayerBlock>
            {
                Block("PipeLow_44", isGameplay: true, hasCollider: true, null),
            };

            Assert.AreEqual(1, LayerContract.Check(blocks, Gameplay).Count);
        }

        [Test]
        public void 배경은_아무_재질이나_써도_된다()
        {
            //  배경 재질 목록까지 강제하면 아트가 손을 못 댄다. 규약은 "닿는 것"에만 건다.
            var blocks = new List<LayerBlock>
            {
                Block("Skyline_80", isGameplay: false, hasCollider: false, "무엇이든"),
            };

            Assert.AreEqual(0, LayerContract.Check(blocks, Gameplay).Count);
        }

        [Test]
        public void 훑은_것이_없으면_그렇게_적는다()
        {
            //  빈 절을 찍으면 "재 봤더니 괜찮다"로 잘못 읽힌다.
            string text = LayerContract.Section(new List<LayerBlock>(), Gameplay);
            Assert.That(text.IndexOf("훑은 블록이 없다", System.StringComparison.Ordinal),
                        Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void 위반이_없으면_층별_개수를_적는다()
        {
            var blocks = new List<LayerBlock>
            {
                Block("PipeLow_11", true, true, "CityIntact"),
                Block("Midground_40", false, false, "Midground"),
            };

            string text = LayerContract.Section(blocks, Gameplay);

            Assert.That(text.IndexOf("✅", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(text.IndexOf("게임 평면 1", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(text.IndexOf("배경 1", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void 위반은_이름과_자리를_적는다()
        {
            var blocks = new List<LayerBlock> { Block("Midground_40", false, true, "Midground") };

            string text = LayerContract.Section(blocks, Gameplay);

            Assert.That(text.IndexOf("❌", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
            Assert.That(text.IndexOf("Midground_40", System.StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
        }
    }
}
```

> **주의:** 문자열 검사는 반드시 `IndexOf(..., StringComparison.Ordinal)`로 한다. `StringAssert.Contains`는 문화권 비교라 이모지가 든 문자열에서 **없는 것도 통과**한다(이 프로젝트에서 실제로 겪었다).

- [ ] **Step 2: 돌려서 실패를 본다**

`unity run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --filter LayerContractTests`
기대: 컴파일 실패.

- [ ] **Step 3: 최소 구현**

`Assets/Scripts/MapTools/LayerContract.cs`:

```csharp
using System.Collections.Generic;
using System.Text;

namespace LOP.MapTools
{
    /// <summary>검사에 넘기는 블록 하나. 씬 타입을 안 들고 와야 순수 계층에서 잴 수 있다.</summary>
    public readonly struct LayerBlock
    {
        public readonly string Name;
        public readonly float X;
        /// <summary>새의 z대역과 겹치는가 — 즉 닿을 수 있는 자리인가.</summary>
        public readonly bool IsGameplay;
        public readonly bool HasCollider;
        public readonly string MaterialName;

        public LayerBlock(string name, float x, bool isGameplay, bool hasCollider, string materialName)
        {
            Name = name;
            X = x;
            IsGameplay = isGameplay;
            HasCollider = hasCollider;
            MaterialName = materialName;
        }
    }

    public readonly struct LayerViolation
    {
        public readonly string Name;
        public readonly float X;
        public readonly string Reason;

        public LayerViolation(string name, float x, string reason)
        {
            Name = name;
            X = x;
            Reason = reason;
        }
    }

    /// <summary>
    /// <b>층 규약</b> — 닿는 것과 안 닿는 것이 보이는 대로여야 한다는 약속을 기계가 지킨다.
    ///
    /// <para>맵을 세 층(게임 평면 · 중간층 · 배경)으로 가르고 나면 눈으로 못 잡는 버그가 둘
    /// 생긴다: <b>배경인데 부딪힌다</b>(배경에 콜라이더가 남음), <b>장애물인데 배경처럼
    /// 보인다</b>(게임 평면이 배경 재질을 씀). 둘 다 "다음 사람이 블록 하나를 잘못 놓으면"
    /// 조용히 돌아오는 종류라 검사로 막는다.</para>
    ///
    /// <para>배경 <i>재질</i>은 목록으로 강제하지 않는다 — 규약은 "닿는 것"에만 건다.
    /// 아트가 배경을 자유롭게 손대도 이 검사가 방해하지 않아야 한다.</para>
    /// </summary>
    public static class LayerContract
    {
        public static List<LayerViolation> Check(IReadOnlyList<LayerBlock> blocks,
                                                 IReadOnlyCollection<string> gameplayMaterials)
        {
            var bad = new List<LayerViolation>();
            if (blocks == null)
            {
                return bad;
            }
            foreach (LayerBlock b in blocks)
            {
                if (b.IsGameplay == false)
                {
                    if (b.HasCollider)
                    {
                        bad.Add(new LayerViolation(b.Name, b.X, "배경인데 콜라이더가 있다 — 안 보이는 벽이 된다"));
                    }
                    continue;
                }
                if (b.HasCollider == false)
                {
                    bad.Add(new LayerViolation(b.Name, b.X, "게임 평면인데 콜라이더가 없다 — 장애물처럼 보이고 통과된다"));
                }
                if (IsGameplayMaterial(b.MaterialName, gameplayMaterials) == false)
                {
                    bad.Add(new LayerViolation(b.Name, b.X,
                        $"게임 평면인데 재질이 '{b.MaterialName ?? "없음"}' — 배경처럼 읽힌다"));
                }
            }
            return bad;
        }

        private static bool IsGameplayMaterial(string name, IReadOnlyCollection<string> allowed)
        {
            if (string.IsNullOrEmpty(name) || allowed == null)
            {
                return false;
            }
            foreach (string a in allowed)
            {
                if (string.Equals(a, name, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public static string Section(IReadOnlyList<LayerBlock> blocks,
                                     IReadOnlyCollection<string> gameplayMaterials)
        {
            var text = new StringBuilder();
            text.AppendLine("── 🧱 층 규약 ─────────────────────────");
            text.AppendLine("  (닿는 것은 게임 평면에만 있다. 배경에 콜라이더가 남거나 게임 평면이");
            text.AppendLine("   배경 재질을 쓰면 '보이는 대로 부딪힌다'가 깨진다.)");

            if (blocks == null || blocks.Count == 0)
            {
                text.Append("  훑은 블록이 없다 — 검사가 아무것도 못 봤다");
                return text.ToString();
            }

            int gameplay = 0;
            foreach (LayerBlock b in blocks)
            {
                if (b.IsGameplay) { gameplay++; }
            }
            text.AppendLine($"  게임 평면 {gameplay}개 · 배경 {blocks.Count - gameplay}개");

            List<LayerViolation> bad = Check(blocks, gameplayMaterials);
            if (bad.Count == 0)
            {
                text.Append("  ✅ 위반 없음");
                return text.ToString();
            }
            text.AppendLine($"  ❌ 위반 {bad.Count}개");
            for (int i = 0; i < bad.Count && i < 12; i++)
            {
                text.AppendLine($"     {bad[i].Name} (x={bad[i].X:F1}) — {bad[i].Reason}");
            }
            if (bad.Count > 12)
            {
                text.AppendLine($"     … 그리고 {bad.Count - 12}개 더");
            }
            return text.ToString().TrimEnd();
        }
    }
}
```

- [ ] **Step 4: 통과를 본다**

같은 명령. 기대: 9개 PASS.

- [ ] **Step 5: 일부러 깨뜨린다**

`Check`의 배경 분기에서 `if (b.HasCollider)` 를 `if (false)`로 바꾸고 돌린다. **`배경에_콜라이더가_있으면_위반이다`와 `위반은_이름과_자리를_적는다`가 실패해야 한다.** 되돌린다.

- [ ] **Step 6: 리포트에 절 자리를 낸다**

`Assets/Scripts/MapTools/PlayabilityReport.cs` — `Build(...)` 시그니처 맨 끝에 인자를 하나 더한다(기본값이 있어 기존 호출부는 안 깨진다):

```csharp
                                   float targetSpacing = 0f,
                                   string layerSection = null)
```

그리고 시각 정직성 절 **바로 뒤**에 출력한다(둘 다 "읽기 쉬움"에 대한 절이라 붙여 둔다):

```csharp
            if (layerSection != null)
            {
                text.AppendLine();
                text.AppendLine(layerSection);
            }
```

- [ ] **Step 7: 검사기가 씬에서 모아 넘긴다**

`Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs` — `BlockDepthScan.IsGameplayBlock`을 쓰는 기존 루프(약 1542행) 곁에서 같은 렌더러 순회로 `LayerBlock`을 모은다:

```csharp
            var layerBlocks = new System.Collections.Generic.List<LOP.MapTools.LayerBlock>();
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Bounds bounds = renderer.bounds;
                bool isGameplay = LOP.MapTools.BlockDepthScan.IsGameplayBlock(bounds, bodyRadius);
                var collider = renderer.GetComponent<Collider>();
                //  머티리얼 이름은 인스턴스가 아니라 에셋 이름이어야 한다 — 런타임 복제본은
                //  "Foo (Instance)"가 되어 목록과 안 맞는다. 에디터라 sharedMaterial이 맞다.
                string material = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : null;
                layerBlocks.Add(new LOP.MapTools.LayerBlock(
                    renderer.gameObject.name, bounds.center.x, isGameplay,
                    collider != null && collider.enabled, material));
            }
            string layerSection = LOP.MapTools.LayerContract.Section(layerBlocks, GameplayMaterials);
```

그리고 같은 파일 위쪽에 허용 목록을 둔다:

```csharp
        //  게임 평면이 쓸 수 있는 재질. 구간 셋 + 아직 안 갈아 끼운 그레이박스 재질.
        private static readonly string[] GameplayMaterials =
            { "CityIntact", "CityExposed", "CityCharred", "FloorNeutral" };
```

마지막으로 `PlayabilityReport.Build(...)` 호출에 `layerSection: layerSection`을 더한다.

- [ ] **Step 8: 검사를 돌려 절이 찍히는지 본다**

맵 씬을 열고 메뉴 `LOP/Debug/Flappy 맵 검사`. 지금은 파이프가 `FloorNeutral`이라 **✅ 위반 없음**이 나와야 한다(허용 목록에 넣어 뒀다).

- [ ] **Step 9: 커밋**

```bash
git add Assets/Scripts/MapTools/LayerContract.cs Assets/Scripts/MapTools/LayerContract.cs.meta \
        Assets/Tests/EditMode/MapTools/LayerContractTests.cs Assets/Tests/EditMode/MapTools/LayerContractTests.cs.meta \
        Assets/Scripts/MapTools/PlayabilityReport.cs Assets/Scripts/Editor/FlappyMapPlayabilityCheck.cs
git status --short
git commit -m "feat(maptools): 닿는 것과 안 닿는 것을 검사가 지킨다"
```

---

### Task 3: 재질 다섯

**Files:**
- Create: `Assets/Scripts/Editor/FlappyCityMaterials.cs`
- Create (Art): `Assets/Art/Environment/FlappyRace/{CityIntact,CityExposed,CityCharred,Midground,Skyline}.mat`

**Interfaces:**
- Produces: `FlappyCityMaterials.Of(CourseSection) → Material` · `FlappyCityMaterials.Midground/Skyline → Material` · `FlappyCityMaterials.EnsureAll()`

- [ ] **Step 1: 만드는 스크립트를 쓴다**

`Assets/Scripts/Editor/FlappyCityMaterials.cs`:

```csharp
using UnityEditor;
using UnityEngine;

namespace LOP.EditorTools
{
    /// <summary>
    /// 무너지는 도시의 재질 다섯. <b>없을 때만 만든다</b> — 한 번 만든 뒤 인스펙터에서 손으로
    /// 고친 값이 다시 구울 때마다 날아가면 아트를 만질 수가 없다.
    ///
    /// <para><b>게임 평면은 어두워지지 않는다.</b> 구간이 진행돼도 알베도 휘도를 0.60 아래로
    /// 내리지 않는다 — 제일 어려운 구간에서 관문이 제일 안 보이면 난이도를 아트로 몰래 올린
    /// 셈이다. 그을음은 <i>색조</i>로만 표현하고, 잔불 발광이 테두리를 오히려 밝게 만든다.</para>
    /// </summary>
    public static class FlappyCityMaterials
    {
        private const string Folder = "Assets/Art/Environment/FlappyRace";

        public static Material Of(LOP.MapTools.CourseSection section)
        {
            switch (section)
            {
                case LOP.MapTools.CourseSection.Exposed: return Load("CityExposed");
                case LOP.MapTools.CourseSection.Charred: return Load("CityCharred");
                default: return Load("CityIntact");
            }
        }

        public static Material Midground => Load("Midground");
        public static Material Skyline => Load("Skyline");

        [MenuItem("LOP/Debug/Flappy 도시 재질 만들기")]
        public static void EnsureAll()
        {
            //  게임 평면 셋 — 휘도 0.74 / 0.68 / 0.62. 색조만 차갑다→따뜻하다→붉다로 간다.
            Ensure("CityIntact", new Color(0.76f, 0.76f, 0.74f), smoothness: 0.10f, emission: Color.black);
            Ensure("CityExposed", new Color(0.76f, 0.66f, 0.54f), smoothness: 0.14f, emission: Color.black);
            Ensure("CityCharred", new Color(0.74f, 0.58f, 0.48f), smoothness: 0.18f,
                   emission: new Color(0.50f, 0.16f, 0.05f));

            //  중간층·배경 — 여기만 어두워진다. 안개가 거리로 더 씻긴다.
            Ensure("Midground", new Color(0.33f, 0.34f, 0.38f), smoothness: 0f, emission: Color.black);
            Ensure("Skyline", new Color(0.28f, 0.34f, 0.46f), smoothness: 0f, emission: Color.black);

            AssetDatabase.SaveAssets();
            Debug.Log("[도시 재질] 없던 것만 만들었다 — 이미 있던 것은 손대지 않았다.");
        }

        private static Material Load(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/{name}.mat");
        }

        private static void Ensure(string name, Color baseColor, float smoothness, Color emission)
        {
            if (Load(name) != null)
            {
                return;
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[도시 재질] URP Lit 셰이더를 못 찾았다.");
                return;
            }
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            if (emission != Color.black)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetColor("_EmissionColor", emission);
            }
            AssetDatabase.CreateAsset(material, $"{Folder}/{name}.mat");
        }
    }
}
```

- [ ] **Step 2: 돌려서 다섯이 생겼는지 본다**

메뉴 `LOP/Debug/Flappy 도시 재질 만들기`. 기대 로그: `[도시 재질] 없던 것만 만들었다`.
확인: `ls Assets/Art/Environment/FlappyRace/*.mat | grep -E 'City|Midground|Skyline'` → 5개.

- [ ] **Step 3: 두 번 돌려도 안 덮어쓰는지 본다**

`CityIntact.mat`의 `_BaseColor`를 인스펙터에서 눈에 띄게 바꾸고 메뉴를 다시 누른다. **색이 그대로여야 한다.** (덮어쓴다면 `Ensure`의 early-return이 깨진 것이다.)

- [ ] **Step 4: 커밋 — Art가 먼저다**

```bash
cd Assets/Art
git status --short
git add Environment/FlappyRace/CityIntact.mat Environment/FlappyRace/CityIntact.mat.meta \
        Environment/FlappyRace/CityExposed.mat Environment/FlappyRace/CityExposed.mat.meta \
        Environment/FlappyRace/CityCharred.mat Environment/FlappyRace/CityCharred.mat.meta \
        Environment/FlappyRace/Midground.mat Environment/FlappyRace/Midground.mat.meta \
        Environment/FlappyRace/Skyline.mat Environment/FlappyRace/Skyline.mat.meta
git commit -m "feat(flappy): 무너지는 도시 재질 다섯"
cd ../..
git add Assets/Scripts/Editor/FlappyCityMaterials.cs Assets/Scripts/Editor/FlappyCityMaterials.cs.meta
git commit -m "feat(flappy): 도시 재질을 없을 때만 만드는 메뉴"
```

> Art 브랜치·푸시는 Task 9에서 한꺼번에 한다. **`Assets/Art` 포인터는 아직 스테이지하지 않는다.**

---

### Task 4: 빌더 — 90초와 구간별 재질

**Files:**
- Modify: `Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs`

**Interfaces:**
- Consumes: `CourseSectionRule.Of` (Task 1) · `FlappyCityMaterials.Of` (Task 3)

- [ ] **Step 1: 길이를 90초로 올린다**

`RaceSeconds` 상수와 그 주석을 바꾼다:

```csharp
        //  한 판을 90초로 잡는다. 전진 6.8 m/s면 612m이고 관문 약 53개다.
        //  <b>맵마다 다를 수 있는 값</b>이다 — 경기 길이는 씬의 결승선 x로 표현되고, 런타임에
        //  60초든 90초든 가정하는 곳은 없다(Archery의 MatchDurationTicks 같은 제한이 없다).
        private const float RaceSeconds = 90f;
```

- [ ] **Step 2: 파이프가 자기 구간의 재질을 입게 한다**

`Pipe`와 `Slab`의 `Material material` 인자는 그대로 두고, **부르는 쪽**이 구간을 물어 고른다. `Build()`의 파이프 루프를 고친다:

```csharp
            foreach (LOP.MapTools.CoursePipe p in pipes)
            {
                Material skin = SectionMaterial(p.X, length, fallback);
                float lowTop = p.GapCenter - window * 0.5f;
                float highBottom = p.GapCenter + window * 0.5f;
                Pipe(composed.transform, $"PipeLow_{p.X:F0}", p.X, floorY, lowTop, skin);
                Pipe(composed.transform, $"PipeHigh_{p.X:F0}", p.X, highBottom, ceilingY, skin);
            }
```

`var material = FindCourseMaterial();` 를 `var fallback = FindCourseMaterial();` 로 바꾸고, 아래 헬퍼를 더한다:

```csharp
        //  구간 재질이 아직 없으면(Task 3을 안 돌렸으면) 그레이박스 재질로 계속 간다 —
        //  색이 다를 뿐 구조 검증에는 지장이 없다.
        private static Material SectionMaterial(float x, float length, Material fallback)
        {
            Material m = FlappyCityMaterials.Of(LOP.MapTools.CourseSectionRule.Of(x, StartX, length));
            return m != null ? m : fallback;
        }
```

- [ ] **Step 3: 바닥·천장도 구간마다 끊는다**

한 덩어리면 구간이 진행돼도 바닥 색이 안 바뀌어 **구간 경계가 바닥에서만 안 보인다.** 슬래브 하나를 `CourseSectionRule.Count` 조각으로 나눈다. `Build()`의 두 `Slab(...)` 호출을 아래로 바꾼다:

```csharp
            //  바닥·천장은 구간마다 끊는다 — 한 덩어리면 색이 안 바뀌어 경계가 바닥에서만 끊긴다.
            //  앞뒤로는 코스 밖(스폰·결승선)까지 덮도록 여유를 준다.
            float slabSpan = length / LOP.MapTools.CourseSectionRule.Count;
            for (int i = 0; i < LOP.MapTools.CourseSectionRule.Count; i++)
            {
                bool first = i == 0;
                bool last = i == LOP.MapTools.CourseSectionRule.Count - 1;
                float from = StartX + slabSpan * i - (first ? spacing * 4f : 0f);
                float to = StartX + slabSpan * (i + 1) + (last ? spacing * 4f : 0f);
                Material skin = SectionMaterial((from + to) * 0.5f, length, fallback);
                Slab(composed.transform, $"Floor_{i}", from, to - from,
                     floorY - WallThickness * 0.5f, to - from, WallThickness, skin);
                Slab(composed.transform, $"Ceiling_{i}", from, to - from,
                     ceilingY + WallThickness * 0.5f, to - from, WallThickness, skin);
            }
```

- [ ] **Step 4: 로그에 구간을 적는다**

```csharp
            Debug.Log($"[전통 코스] 파이프 {pipes.Count}쌍 · 창 {window:F2}m · 간격 {spacing:F1}m"
                    + $" · 회랑 {corridor:F1}m · 길이 {length:F0}m ({RaceSeconds:F0}초)"
                    + $" · 구간 {LOP.MapTools.CourseSectionRule.Count}개 × {length / LOP.MapTools.CourseSectionRule.Count:F0}m");
```

- [ ] **Step 5: 굽고 눈으로 확인한다**

맵 씬(`Assets/Art/Scenes/FlappyRaceMap.unity`)을 열고 메뉴 `LOP/Debug/Flappy 전통 코스 굽기`.

기대 로그: `파이프 53쌍 · 창 4.02m · 간격 11.4m · 회랑 14.6m · 길이 612m (90초) · 구간 3개 × 204m`
기대 화면: 앞 ⅓ 밝은 회백, 가운데 ⅓ 베이지, 뒤 ⅓ 붉은 갈색. 바닥·천장도 같이 바뀐다.

- [ ] **Step 6: 검사를 돌린다**

메뉴 `LOP/Debug/Flappy 맵 검사`. 확인할 것 넷:
- `🧱 층 규약` **✅ 위반 없음**
- `🎥 시각 정직성` ✅ (뒤쪽 두께 0)
- `②-d 관문 박자` — 관문 53개, 평균 간격 목표의 1.0배
- ① 클린런 — 네 스폰 전부 ✅

- [ ] **Step 7: 커밋**

```bash
git add Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs
git status --short
git commit -m "feat(flappy): 코스를 90초로 늘리고 구간마다 다른 재질을 입힌다"
```

> 씬 파일(`Assets/Art/Scenes/FlappyRaceMap.unity`)은 Task 6까지 다 구운 뒤 Task 9에서 한 번에 커밋한다.

---

### Task 5: 중간층 — 비어 있던 깊이를 채운다

**Files:**
- Create: `Assets/Scripts/MapTools/BackdropLayout.cs`
- Test: `Assets/Tests/EditMode/MapTools/BackdropLayoutTests.cs`
- Modify: `Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs`

**Interfaces:**
- Consumes: `CourseSectionRule` (Task 1) · `FlappyCityMaterials.Midground/Skyline` (Task 3)
- Produces: `readonly struct BackdropBox(float x, float centerY, float width, float height, float tiltDegrees)` · `BackdropLayout.Midground(float startX, float length, ulong seed) → List<BackdropBox>` · `BackdropLayout.Skyline(float startX, float length, ulong seed) → List<BackdropBox>`

- [ ] **Step 1: 실패하는 테스트를 쓴다**

`Assets/Tests/EditMode/MapTools/BackdropLayoutTests.cs`:

```csharp
using System.Collections.Generic;
using LOP.MapTools;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    /// <summary>
    /// 중간층·배경 실루엣의 배치. 게임에 안 닿는 것들이라 규칙이 느슨하지만, <b>코스 전체를
    /// 덮는가</b>와 <b>같은 씨앗이면 같은 그림인가</b>는 못박는다 — 배경이 중간에 끊기는 사고가
    /// 실제로 있었다(도시가 557m에서 끝나는데 코스는 612m였다).
    /// </summary>
    public class BackdropLayoutTests
    {
        const float StartX = 0f;
        const float Length = 612f;

        [Test]
        public void 중간층이_코스_전체를_덮는다()
        {
            List<BackdropBox> boxes = BackdropLayout.Midground(StartX, Length, seed: 1UL);

            Assert.Greater(boxes.Count, 0);
            Assert.LessOrEqual(boxes[0].X, StartX, "코스 시작보다 앞에서 시작해야 한다");
            Assert.GreaterOrEqual(boxes[boxes.Count - 1].X, StartX + Length, "코스 끝을 넘겨야 한다");
        }

        [Test]
        public void 배경도_코스_전체를_덮는다()
        {
            List<BackdropBox> boxes = BackdropLayout.Skyline(StartX, Length, seed: 1UL);

            Assert.LessOrEqual(boxes[0].X, StartX);
            Assert.GreaterOrEqual(boxes[boxes.Count - 1].X, StartX + Length);
        }

        [Test]
        public void 같은_씨앗이면_같은_그림이다()
        {
            List<BackdropBox> a = BackdropLayout.Midground(StartX, Length, seed: 7UL);
            List<BackdropBox> b = BackdropLayout.Midground(StartX, Length, seed: 7UL);

            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].X, b[i].X, 1e-4f);
                Assert.AreEqual(a[i].Height, b[i].Height, 1e-4f);
            }
        }

        [Test]
        public void 다른_씨앗이면_다른_그림이다()
        {
            List<BackdropBox> a = BackdropLayout.Midground(StartX, Length, seed: 7UL);
            List<BackdropBox> b = BackdropLayout.Midground(StartX, Length, seed: 8UL);

            bool same = a.Count == b.Count;
            if (same)
            {
                for (int i = 0; i < a.Count; i++)
                {
                    if (System.Math.Abs(a[i].Height - b[i].Height) > 1e-4f) { same = false; break; }
                }
            }
            Assert.IsFalse(same, "씨앗이 안 먹고 있다");
        }

        [Test]
        public void 배경이_중간층보다_크다()
        {
            //  82m 거리에서 화면 세로가 59.7m다 — 6m짜리 건물은 자갈로 보인다(실제로 그랬다).
            float mid = Tallest(BackdropLayout.Midground(StartX, Length, 3UL));
            float sky = Tallest(BackdropLayout.Skyline(StartX, Length, 3UL));

            Assert.Greater(sky, mid);
            Assert.Greater(sky, 40f, "배경은 화면(59.7m)의 절반은 넘어야 스카이라인으로 읽힌다");
        }

        [Test]
        public void 뒤로_갈수록_중간층이_낮고_기울어진다()
        {
            //  구간 1은 서 있고 구간 3은 무너져 있다 — 그게 보여야 진행이 읽힌다.
            List<BackdropBox> boxes = BackdropLayout.Midground(StartX, Length, seed: 5UL);

            float firstThird = MeanHeight(boxes, StartX, StartX + Length / 3f);
            float lastThird = MeanHeight(boxes, StartX + Length * 2f / 3f, StartX + Length);
            Assert.Less(lastThird, firstThird, "뒤 구간이 더 낮아야 한다");

            float firstTilt = MeanTilt(boxes, StartX, StartX + Length / 3f);
            float lastTilt = MeanTilt(boxes, StartX + Length * 2f / 3f, StartX + Length);
            Assert.Greater(lastTilt, firstTilt, "뒤 구간이 더 기울어야 한다");
        }

        [Test]
        public void 길이가_0이면_비어_있다()
        {
            Assert.AreEqual(0, BackdropLayout.Midground(StartX, 0f, 1UL).Count);
            Assert.AreEqual(0, BackdropLayout.Skyline(StartX, -5f, 1UL).Count);
        }

        static float Tallest(List<BackdropBox> boxes)
        {
            float h = 0f;
            foreach (BackdropBox b in boxes) { if (b.Height > h) { h = b.Height; } }
            return h;
        }

        static float MeanHeight(List<BackdropBox> boxes, float from, float to)
        {
            float sum = 0f; int n = 0;
            foreach (BackdropBox b in boxes)
            {
                if (b.X >= from && b.X < to) { sum += b.Height; n++; }
            }
            return n == 0 ? 0f : sum / n;
        }

        static float MeanTilt(List<BackdropBox> boxes, float from, float to)
        {
            float sum = 0f; int n = 0;
            foreach (BackdropBox b in boxes)
            {
                if (b.X >= from && b.X < to) { sum += System.Math.Abs(b.TiltDegrees); n++; }
            }
            return n == 0 ? 0f : sum / n;
        }
    }
}
```

- [ ] **Step 2: 돌려서 실패를 본다**

`unity run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --filter BackdropLayoutTests`

- [ ] **Step 3: 최소 구현**

`Assets/Scripts/MapTools/BackdropLayout.cs`:

```csharp
using System.Collections.Generic;
using GameFramework.Rng;

namespace LOP.MapTools
{
    /// <summary>배경 실루엣 한 덩어리. 판정과 무관하므로 값만 있다.</summary>
    public readonly struct BackdropBox
    {
        public readonly float X;
        public readonly float CenterY;
        public readonly float Width;
        public readonly float Height;
        /// <summary>z축 둘레로 기울인 각도. z 범위가 안 변해 층이 흐트러지지 않는다.</summary>
        public readonly float TiltDegrees;

        public BackdropBox(float x, float centerY, float width, float height, float tiltDegrees)
        {
            X = x;
            CenterY = centerY;
            Width = width;
            Height = height;
            TiltDegrees = tiltDegrees;
        }
    }

    /// <summary>
    /// 게임 평면 <b>뒤</b>에 깔리는 두 층의 배치.
    ///
    /// <para><b>왜 순수 계층인가</b>: "코스 전체를 덮는가"는 씬 없이 잴 수 있고, 안 재면
    /// 조용히 틀린다 — 실제로 배경 도시가 557m에서 끝나는데 코스가 612m였다.</para>
    ///
    /// <para><b>기울기는 z축 둘레로만</b> 준다. 다른 축으로 돌리면 블록의 z 범위가 변해
    /// 층이 섞이고, 층 규약 검사의 분류(새의 z대역과 겹치는가)가 흔들린다.</para>
    /// </summary>
    public static class BackdropLayout
    {
        /// <summary>중간층 — 34m 거리. 화면 세로 24.8m를 채워야 하므로 그보다 크게 잡는다.</summary>
        public static List<BackdropBox> Midground(float startX, float length, ulong seed)
        {
            return Layout(startX, length, seed,
                          stepMin: 9f, stepMax: 19f,
                          widthMin: 6f, widthMax: 14f,
                          heightMin: 18f, heightMax: 38f,
                          baseY: -12f,
                          //  뒤로 갈수록 낮아지고(무너진다) 기운다.
                          heightDecay: 0.45f, tiltMax: 14f);
        }

        /// <summary>배경 — 82m 거리. 화면 세로가 59.7m라 40m는 넘겨야 스카이라인으로 읽힌다.</summary>
        public static List<BackdropBox> Skyline(float startX, float length, ulong seed)
        {
            return Layout(startX, length, seed,
                          stepMin: 14f, stepMax: 32f,
                          widthMin: 8f, widthMax: 26f,
                          heightMin: 30f, heightMax: 58f,
                          baseY: -30f,
                          heightDecay: 0.25f, tiltMax: 6f);
        }

        private static List<BackdropBox> Layout(float startX, float length, ulong seed,
                                                float stepMin, float stepMax,
                                                float widthMin, float widthMax,
                                                float heightMin, float heightMax,
                                                float baseY, float heightDecay, float tiltMax)
        {
            var boxes = new List<BackdropBox>();
            if (length <= 0f)
            {
                return boxes;
            }
            var rng = new DeterministicRandom(seed);

            //  코스보다 한 발씩 앞뒤로 넘겨 깐다 — 시작·결승 연출에서 배경이 끊기면 안 된다.
            float margin = stepMax * 2f;
            for (float x = startX - margin; x <= startX + length + margin; x += rng.Range(stepMin, stepMax))
            {
                float t = CourseSectionRule.Progress(x, startX, length);
                float shrink = 1f - heightDecay * t;
                float height = rng.Range(heightMin, heightMax) * shrink;
                float width = rng.Range(widthMin, widthMax);
                float tilt = rng.Range(-tiltMax, tiltMax) * t;   // 앞은 곧고 뒤는 기운다
                boxes.Add(new BackdropBox(x, baseY + height * 0.5f, width, height, tilt));
            }
            return boxes;
        }
    }
}
```

- [ ] **Step 4: 통과를 본다** — 7개 PASS.

- [ ] **Step 5: 일부러 깨뜨린다**

`float tilt = ... * t;` 에서 `* t`를 지우고 돌린다. **`뒤로_갈수록_중간층이_낮고_기울어진다`가 실패해야 한다.** 되돌린다.

- [ ] **Step 6: 빌더가 중간층을 굽는다**

`FlappyClassicCourseBuilder`에 상수와 메서드를 더한다:

```csharp
        //  중간층 깊이. 34m 거리가 되어 화면 세로 24.8m를 담는다. 게임 평면(z=0)과 배경(z=62)
        //  사이가 통째로 비어 있던 자리다 — 2.5D가 안 읽히던 이유.
        private const float MidgroundZ = 14f;
        private const float MidgroundDepth = 6f;
        private const ulong MidgroundSeed = 20260920UL;
```

`Build()`에서 파이프를 놓은 **뒤에** 부른다:

```csharp
            Backdrop(composed.transform, "Midground",
                     LOP.MapTools.BackdropLayout.Midground(StartX, length, MidgroundSeed),
                     MidgroundZ, MidgroundDepth, FlappyCityMaterials.Midground);
```

메서드:

```csharp
        //  게임 평면 뒤에 까는 실루엣. <b>콜라이더를 지운다</b> — 남으면 "안 보이는 벽"이 되고,
        //  그건 플레이어가 원인을 짚을 수 없는 종류의 버그다(층 규약 검사가 잡는 바로 그것).
        private static void Backdrop(Transform parent, string groupName,
                                     System.Collections.Generic.IReadOnlyList<LOP.MapTools.BackdropBox> boxes,
                                     float z, float depth, Material material)
        {
            var group = new GameObject(groupName);
            group.transform.SetParent(parent, worldPositionStays: false);
            Undo.RegisterCreatedObjectUndo(group, "Build classic course");

            foreach (LOP.MapTools.BackdropBox b in boxes)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"{groupName}_{b.X:F0}";
                go.transform.SetParent(group.transform, worldPositionStays: false);
                go.transform.localScale = new Vector3(b.Width, b.Height, depth);
                go.transform.position = new Vector3(b.X, b.CenterY, z);
                go.transform.rotation = Quaternion.Euler(0f, 0f, b.TiltDegrees);
                Object.DestroyImmediate(go.GetComponent<BoxCollider>());
                if (material != null)
                {
                    go.GetComponent<MeshRenderer>().sharedMaterial = material;
                }
                Undo.RegisterCreatedObjectUndo(go, "Build classic course");
            }
        }
```

- [ ] **Step 7: 굽고 검사한다**

메뉴 `LOP/Debug/Flappy 전통 코스 굽기` → `LOP/Debug/Flappy 맵 검사`.

- `🧱 층 규약` **✅ 위반 없음** (중간층에 콜라이더가 남았다면 여기서 잡힌다)
- ① 클린런 네 스폰 ✅ (중간층이 판정에 안 끼어드는지의 증거)
- 씬 뷰에서 위에서 내려다보면 z=14에 한 줄이 더 생겨 있다

- [ ] **Step 8: 커밋**

```bash
git add Assets/Scripts/MapTools/BackdropLayout.cs Assets/Scripts/MapTools/BackdropLayout.cs.meta \
        Assets/Tests/EditMode/MapTools/BackdropLayoutTests.cs Assets/Tests/EditMode/MapTools/BackdropLayoutTests.cs.meta \
        Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs
git status --short
git commit -m "feat(flappy): 비어 있던 깊이에 중간층을 깐다"
```

---

### Task 6: 배경 도시를 키우고 612m까지 늘린다

**Files:**
- Modify: `Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs`

**Interfaces:**
- Consumes: `BackdropLayout.Skyline` (Task 5)

- [ ] **Step 1: 빌더가 `CitySilhouette`만 다시 굽게 한다**

`---Environment---` 아래에는 `Clouds`·`Decorations`도 있다. **`CitySilhouette`만** 갈아 끼운다.

상수:

```csharp
        //  배경 도시. 카메라에서 82m라 화면 세로가 59.7m다 — 기존 건물이 1~6.4m뿐이라
        //  화면의 10%만 채우고 있었다(스카이라인이 아니라 자갈이었다).
        private const float SkylineZ = 62f;
        private const float SkylineDepth = 8f;
        private const ulong SkylineSeed = 20260921UL;
```

`Build()`에서 중간층 다음에:

```csharp
            RebuildSkyline(length);
```

메서드:

```csharp
        //  <c>---Environment---</c>의 <c>CitySilhouette</c>만 다시 굽는다. 구름·장식은 손대지 않는다.
        private static void RebuildSkyline(float length)
        {
            var env = GameObject.Find("---Environment---");
            if (env == null)
            {
                Debug.LogWarning("[전통 코스] ---Environment---가 없다 — 배경을 못 구웠다.");
                return;
            }
            Transform city = env.transform.Find("CitySilhouette");
            if (city == null)
            {
                var made = new GameObject("CitySilhouette");
                made.transform.SetParent(env.transform, worldPositionStays: false);
                Undo.RegisterCreatedObjectUndo(made, "Build classic course");
                city = made.transform;
            }
            Undo.RegisterFullObjectHierarchyUndo(city.gameObject, "Build classic course");
            for (int i = city.childCount - 1; i >= 0; i--)
            {
                Undo.DestroyObjectImmediate(city.GetChild(i).gameObject);
            }
            Backdrop(city, "Skyline",
                     LOP.MapTools.BackdropLayout.Skyline(StartX, length, SkylineSeed),
                     SkylineZ, SkylineDepth, FlappyCityMaterials.Skyline);
        }
```

> `Backdrop`은 자식 그룹을 하나 더 만든다(`CitySilhouette/Skyline/Skyline_12` …). 그룹이 하나 더 껴도 상관없고, 다음에 다시 구울 때 통째로 지워진다.

- [ ] **Step 2: 굽고 눈으로 확인한다**

메뉴 `LOP/Debug/Flappy 전통 코스 굽기`.

- 게임 뷰에서 배경 건물이 **화면 세로의 절반 이상**을 차지한다(전에는 10%)
- 코스 끝(x≈612)까지 배경이 이어진다 — 전에는 557m에서 끊겼다
- 씬 뷰 위에서 보면 z가 0 / 14 / 62 세 줄이다

- [ ] **Step 3: 검사한다** — `🧱 층 규약` ✅ · ① 클린런 ✅.

- [ ] **Step 4: 커밋**

```bash
git add Assets/Scripts/Editor/FlappyClassicCourseBuilder.cs
git commit -m "feat(flappy): 배경 도시를 키우고 코스 끝까지 늘린다"
```

---

### Task 7: 진행에 따라 하늘이 바뀐다

**Files:**
- Create: `Assets/Scripts/Game/FlappySkyGradient.cs`
- Create: `Assets/Scripts/Game/FlappyAtmosphere.cs`
- Test: `Assets/Tests/Editor/FlappySkyGradientTests.cs`, `Assets/Tests/Editor/FlappyAtmosphereTests.cs`
- Modify: `Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`

**Interfaces:**
- Consumes: `CourseSectionRule.Progress` (Task 1)
- Produces: `FlappySkyGradient.Evaluate(float progress) → (Color fog, Color skyTint, float density)` · `FlappyAtmosphere : ITickable, IDisposable` with `public void Apply(float progress)`

- [ ] **Step 1: 곡선 테스트를 쓴다**

`Assets/Tests/Editor/FlappySkyGradientTests.cs`:

```csharp
using LOP;
using NUnit.Framework;
using UnityEngine;

public class FlappySkyGradientTests
{
    [Test]
    public void 뒤로_갈수록_안개가_짙어진다()
    {
        //  §3.4의 표 — 0.009에서 0.016으로. 배경이 42%에서 82% 씻긴다.
        Assert.AreEqual(0.009f, FlappySkyGradient.Evaluate(0f).density, 1e-4f);
        Assert.AreEqual(0.016f, FlappySkyGradient.Evaluate(1f).density, 1e-4f);
        Assert.Greater(FlappySkyGradient.Evaluate(0.8f).density, FlappySkyGradient.Evaluate(0.2f).density);
    }

    [Test]
    public void 뒤로_갈수록_안개가_따뜻해진다()
    {
        //  서늘한 아침빛 → 주황 먼지빛. 파랑이 줄고 빨강이 는다.
        Color start = FlappySkyGradient.Evaluate(0f).fog;
        Color end = FlappySkyGradient.Evaluate(1f).fog;

        Assert.Less(end.b, start.b);
        Assert.GreaterOrEqual(end.r, start.r);
    }

    [Test]
    public void 코스_밖_진행률도_잘린다()
    {
        //  스폰이 시작선보다 뒤라 음수 진행률이 실제로 들어온다.
        Assert.AreEqual(FlappySkyGradient.Evaluate(0f).density,
                        FlappySkyGradient.Evaluate(-2f).density, 1e-5f);
        Assert.AreEqual(FlappySkyGradient.Evaluate(1f).density,
                        FlappySkyGradient.Evaluate(5f).density, 1e-5f);
    }

    [Test]
    public void 중간은_양끝_사이에_있다()
    {
        float mid = FlappySkyGradient.Evaluate(0.5f).density;
        Assert.Greater(mid, 0.009f);
        Assert.Less(mid, 0.016f);
    }
}
```

- [ ] **Step 2: 돌려서 실패를 본다**

`unity run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --filter FlappySkyGradientTests`

- [ ] **Step 3: 곡선을 구현한다**

`Assets/Scripts/Game/FlappySkyGradient.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 코스 진행률(0~1) 하나로 하늘이 정해진다. <see cref="SkydiveSkyGradient"/>의 짝이며,
    /// 축만 고도(y)가 아니라 진행(x)이다.
    ///
    /// <para><b>안개 하나가 세 층을 가른다</b>: ExponentialSquared는 거리의 제곱으로 먹으므로
    /// (씻김 = 1 − exp(−(밀도·거리)²)), 층이 20 / 34 / 82m로 떨어져 있으면 한 값이 세 층을
    /// 다르게 씻긴다 — 0.009에서 3% / 9% / 42%, 0.016에서 10% / 26% / 82%.
    /// 그래서 층마다 재질 채도를 따로 만들지 않는다.</para>
    /// </summary>
    public static class FlappySkyGradient
    {
        //  구간 1 — 서늘한 아침빛. 배경이 42% 씻긴다.
        private static readonly Color StartFog = new Color(0.62f, 0.68f, 0.76f);
        private static readonly Color StartSky = new Color(0.66f, 0.74f, 0.86f);
        private const float StartDensity = 0.009f;

        //  구간 3 — 주황 먼지빛. 배경이 82% 씻겨 거의 사라진다.
        private static readonly Color EndFog = new Color(0.58f, 0.40f, 0.28f);
        private static readonly Color EndSky = new Color(0.62f, 0.42f, 0.28f);
        private const float EndDensity = 0.016f;

        public static (Color fog, Color skyTint, float density) Evaluate(float progress)
        {
            float t = progress < 0f ? 0f : (progress > 1f ? 1f : progress);
            return (Color.Lerp(StartFog, EndFog, t),
                    Color.Lerp(StartSky, EndSky, t),
                    Mathf.Lerp(StartDensity, EndDensity, t));
        }
    }
}
```

- [ ] **Step 4: 통과를 본다** — 4개 PASS. 그리고 `Color.Lerp(StartFog, EndFog, t)`를 `StartFog`로 고정해 **`뒤로_갈수록_안개가_따뜻해진다`가 실패하는지** 확인하고 되돌린다.

- [ ] **Step 5: 대기 테스트를 쓴다**

`Assets/Tests/Editor/FlappyAtmosphereTests.cs`:

```csharp
using GameFramework.World;
using LOP;
using NUnit.Framework;
using UnityEngine;

public class FlappyAtmosphereTests
{
    private bool fog;
    private Color fogColor;
    private float density;
    private Material skybox;

    [SetUp]
    public void SetUp()
    {
        fog = RenderSettings.fog;
        fogColor = RenderSettings.fogColor;
        density = RenderSettings.fogDensity;
        skybox = RenderSettings.skybox;
    }

    [TearDown]
    public void TearDown()
    {
        RenderSettings.fog = fog;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = density;
        RenderSettings.skybox = skybox;
    }

    private sealed class FakeContext : IPlayerContext
    {
        public GameFramework.ISession session { get; set; }
        public string entityId { get; set; }
        public LOPActor actor { get; set; }
    }

    private static (FlappyAtmosphere sut, Entity e) Make(float x)
    {
        var registry = new EntityRegistry();
        var e = new Entity("me");
        e.Add(new GameFramework.World.Transform
        {
            Position = new System.Numerics.Vector3(x, 0f, 0f)
        });
        registry.Add(e);
        return (new FlappyAtmosphere(new FakeContext { entityId = "me" }, registry), e);
    }

    [Test]
    public void 안개를_켜고_지수제곱으로_둔다()
    {
        //  씬에 저장해 봐야 소용없다 — 맵이 additive라 활성 씬의 설정이 이긴다.
        RenderSettings.fog = false;

        Make(0f).sut.Apply(0f);

        Assert.IsTrue(RenderSettings.fog);
        Assert.AreEqual(FogMode.ExponentialSquared, RenderSettings.fogMode);
    }

    [Test]
    public void 진행률이_안개_밀도를_정한다()
    {
        var sut = Make(0f).sut;

        sut.Apply(0f);
        float atStart = RenderSettings.fogDensity;
        sut.Apply(1f);

        Assert.Greater(RenderSettings.fogDensity, atStart);
    }

    [Test]
    public void 참가_전에는_손대지_않는다()
    {
        //  entityId가 비어 있는 동안 칠하면 로비 화면 색이 바뀐다.
        var registry = new EntityRegistry();
        var sut = new FlappyAtmosphere(new FakeContext { entityId = null }, registry);
        RenderSettings.fogDensity = 0.5f;

        sut.Tick();

        Assert.AreEqual(0.5f, RenderSettings.fogDensity, 1e-5f);
    }

    [Test]
    public void 끝나면_시작값으로_되돌린다()
    {
        //  안개는 전역이라 안 되돌리면 다음 게임모드로 샌다.
        RenderSettings.fog = false;
        RenderSettings.fogDensity = 0.5f;
        var sut = Make(0f).sut;

        sut.Apply(1f);
        sut.Dispose();

        Assert.IsFalse(RenderSettings.fog);
        Assert.AreEqual(0.5f, RenderSettings.fogDensity, 1e-5f);
    }

    [Test]
    public void 스카이박스_원본을_칠하지_않는다()
    {
        //  원본은 서브모듈 파일이다 — 그대로 칠하면 플레이할 때마다 .mat이 더러워진다.
        var source = new Material(Shader.Find("Skybox/Procedural"));
        RenderSettings.skybox = source;
        var sut = Make(0f).sut;

        sut.Apply(1f);

        Assert.AreNotSame(source, RenderSettings.skybox);
        Object.DestroyImmediate(source);
    }
}
```

- [ ] **Step 6: 돌려서 실패를 본다** — `FlappyAtmosphere`가 없어 컴파일 실패.

- [ ] **Step 7: 대기를 구현한다**

`Assets/Scripts/Game/FlappyAtmosphere.cs`:

```csharp
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 안개·하늘을 코스 진행률에 맞춰 매 프레임 갱신한다. <see cref="SkydiveAtmosphere"/>의 짝이고
    /// 축만 고도(y)가 아니라 진행(x)이다.
    ///
    /// <para>월드에서 <b>읽기만</b> 한다 — 시뮬은 자신이 관찰되는 것을 모른다. 연속 상태라
    /// 이벤트가 아니라 pull이다(world-core-connection-architecture.md).</para>
    ///
    /// <para><b>씬에 맡기지 않는 이유</b>: 맵 씬은 additive로 로드되고 유니티는 <i>활성 씬</i>의
    /// RenderSettings만 적용한다. 맵 씬에 안개를 켜 저장해도 활성 씬(꺼짐)이 이긴다.</para>
    /// </summary>
    public class FlappyAtmosphere : VContainer.Unity.ITickable, System.IDisposable
    {
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        //  결승선은 씬에서 찾는다. 못 찾으면 진행률이 0에 머물러 하늘이 안 변할 뿐 터지지 않는다.
        private float courseStartX;
        private float courseLength;
        private bool measured;

        //  원본 스카이박스는 서브모듈 에셋(공유)이라 그대로 칠하면 플레이할 때마다 .mat이
        //  더러워진다. 처음 한 번만 복사본을 만들어 그것만 칠한다.
        private Material skyboxInstance;

        private readonly bool originalFog;
        private readonly FogMode originalFogMode;
        private readonly Color originalFogColor;
        private readonly float originalFogDensity;
        private readonly Material originalSkybox;
        private bool disposed;

        public FlappyAtmosphere(IPlayerContext playerContext,
                                GameFramework.World.EntityRegistry entityRegistry)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;

            originalFog = RenderSettings.fog;
            originalFogMode = RenderSettings.fogMode;
            originalFogColor = RenderSettings.fogColor;
            originalFogDensity = RenderSettings.fogDensity;
            originalSkybox = RenderSettings.skybox;
        }

        public void Tick()
        {
            if (string.IsNullOrEmpty(playerContext.entityId))
            {
                return;   // 아직 참가 전 — 손대지 않는다
            }

            var entity = entityRegistry.Get(playerContext.entityId);
            var transform = entity?.Get<GameFramework.World.Transform>();
            if (transform == null)
            {
                return;
            }

            Measure();
            Apply(LOP.MapTools.CourseSectionRule.Progress(transform.Position.X, courseStartX, courseLength));
        }

        /// <summary>진행률 하나로 대기 전체가 정해진다. 테스트가 이 문으로 들어온다.</summary>
        public void Apply(float progress)
        {
            var sky = FlappySkyGradient.Evaluate(progress);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = sky.fog;
            RenderSettings.fogDensity = sky.density;

            TintSky(sky.skyTint);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;

            RenderSettings.fog = originalFog;
            RenderSettings.fogMode = originalFogMode;
            RenderSettings.fogColor = originalFogColor;
            RenderSettings.fogDensity = originalFogDensity;
            RenderSettings.skybox = originalSkybox;

            if (skyboxInstance != null)
            {
                Object.Destroy(skyboxInstance);
                skyboxInstance = null;
            }
        }

        //  결승선 x가 곧 코스 길이다 — 맵마다 다른 값이라 상수로 박지 않는다.
        private void Measure()
        {
            if (measured)
            {
                return;
            }
            var finish = Object.FindFirstObjectByType<FinishLine>(FindObjectsInactive.Include);
            if (finish == null)
            {
                return;   // 맵이 아직 안 올라왔다 — 다음 틱에 다시 본다
            }
            courseStartX = 0f;
            courseLength = finish.transform.position.x - courseStartX;
            measured = true;
        }

        private void TintSky(Color skyTint)
        {
            if (skyboxInstance == null)
            {
                Material source = RenderSettings.skybox;
                if (source == null)
                {
                    return;
                }
                skyboxInstance = new Material(source);
                RenderSettings.skybox = skyboxInstance;
            }

            if (skyboxInstance.HasProperty("_SkyTint"))
            {
                skyboxInstance.SetColor("_SkyTint", skyTint);
            }
            else if (skyboxInstance.HasProperty("_Tint"))
            {
                skyboxInstance.SetColor("_Tint", skyTint);
            }
        }
    }
}
```

> **`Object.Destroy`가 EditMode 테스트에서 안 지워진다.** `Dispose` 테스트는 `RenderSettings` 복원만 보므로 문제없다. 만약 테스트가 누수로 실패하면 `Application.isPlaying ? Object.Destroy : Object.DestroyImmediate`로 가른다.

- [ ] **Step 8: 통과를 본다** — 5개 PASS.

- [ ] **Step 9: 등록한다**

`Assets/Scripts/Game/FlappyRaceLifetimeScope.cs`의 `FlappyChaserView` 등록 근처에:

```csharp
            builder.RegisterEntryPoint<FlappyAtmosphere>();
```

- [ ] **Step 10: 플레이로 확인한다**

로컬 리그로 한 판. 출발할 때 서늘한 푸른빛이고 결승 쪽에서 주황 먼지빛이 된다. 배경 도시가 뒤로 갈수록 안개에 잠긴다. **게임 평면(파이프)은 끝까지 또렷해야 한다** — 흐려지면 §3.5 위반이다.

- [ ] **Step 11: 커밋**

```bash
git add Assets/Scripts/Game/FlappySkyGradient.cs Assets/Scripts/Game/FlappySkyGradient.cs.meta \
        Assets/Scripts/Game/FlappyAtmosphere.cs Assets/Scripts/Game/FlappyAtmosphere.cs.meta \
        Assets/Tests/Editor/FlappySkyGradientTests.cs Assets/Tests/Editor/FlappySkyGradientTests.cs.meta \
        Assets/Tests/Editor/FlappyAtmosphereTests.cs Assets/Tests/Editor/FlappyAtmosphereTests.cs.meta \
        Assets/Scripts/Game/FlappyRaceLifetimeScope.cs
git status --short
git commit -m "feat(flappy): 진행에 따라 하늘이 무너져 간다"
```

---

### Task 8: 추격자를 붕괴 전선으로

**Files:**
- Modify: `Assets/Scripts/Game/FlappyChaserView.cs`

**Interfaces:**
- Consumes: 없음. **벽 재질은 코드가 셰이더에서 직접 만든다** — 벽 자체가 런타임 생성물(`CreatePrimitive`)이라 Art 에셋을 물릴 길이 없고, 에셋을 만들어 두면 아무도 안 읽는 채 값만 갈라진다.

- [ ] **Step 1: 앞으로 두껍게 만든다**

`WallThickness`(2m)를 x와 z에 함께 쓰던 것을 가른다:

```csharp
        private const float WallHeight = 300f;
        private const float WallThickness = 2f;
        //  카메라 쪽으로 뻗는 두께. 앞으로 늘리는 것은 <b>공짜다</b> — 판정면보다 앞이라
        //  틈을 좁아 보이게 만들지 않는다(09-15 판정면 정렬 §③). 그래야 "다가오는 면"이 보인다.
        private const float WallDepth = 9f;
```

`EnsureWall`의 스케일과 z를 고친다:

```csharp
            wall.transform.localScale = new Vector3(WallThickness, WallHeight, WallDepth);
```

그리고 `LateTick`에서 z를 세운다 — 벽의 뒷면이 게임 평면에 걸리고 앞쪽으로만 뻗게:

```csharp
            //  뒷면을 판정면(z=0)에 걸고 앞으로만 뻗는다. 뒤로 뻗으면 원근이 틈을 좁아 보이게 만든다.
            position.z = -WallDepth * 0.5f;
```

- [ ] **Step 2: 먼지벽 색과 잔불**

`EnsureWall`의 재질 부분을 바꾼다:

```csharp
            //  어두운 먼지벽 + 앞 가장자리 잔불. 벽은 런타임 생성물이라 Art 에셋을 물릴 수 없어
            //  색을 여기 둔다 — 이 값이 추격자 색의 유일한 출처다.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var material = new Material(shader);
                material.SetColor("_BaseColor", new Color(0.16f, 0.13f, 0.12f));
                material.SetFloat("_Smoothness", 0f);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(0.60f, 0.12f, 0.03f));
                wall.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
```

- [ ] **Step 3: 먼지를 흘린다**

`EnsureWall` 끝에서 파티클을 붙인다(프리팹 없이 코드로 — Art 의존을 안 만든다):

```csharp
            //  벽 앞으로 흘러나오는 먼지. 벽이 화면에 들어오는 것은 <b>실패했을 때뿐</b>이라
            //  (잘 날면 5개 화면 뒤에 있다) 값이 비싸지 않게 유지한다.
            var dustHost = new GameObject("Dust");
            dustHost.transform.SetParent(wall.transform, worldPositionStays: false);
            var dust = dustHost.AddComponent<ParticleSystem>();
            var main = dust.main;
            main.startLifetime = 1.4f;
            main.startSpeed = 3.5f;
            main.startSize = 6f;
            main.startColor = new Color(0.35f, 0.30f, 0.27f, 0.35f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            var emission = dust.emission;
            emission.rateOverTime = 45f;
            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            //  벽이 세로로 300m라 로컬 스케일을 그대로 쓰면 파티클이 화면 밖까지 퍼진다.
            shape.scale = new Vector3(0.5f, 0.12f, 1f);
            var renderer = dustHost.GetComponent<ParticleSystemRenderer>();
            renderer.material = wall.GetComponent<MeshRenderer>().sharedMaterial;
```

- [ ] **Step 4: 플레이로 확인한다**

로컬 리그에서 **일부러 멈춰** 벽이 따라붙게 한다. 확인:
- 벽이 다가오는 **면**으로 보인다(선이 아니라)
- 앞 가장자리가 잔불로 빛난다
- 먼지가 벽 앞으로 흘러나온다
- **벽에 부딪혀도 물리적으로 밀리지 않는다**(콜라이더 없음 — 판정은 서버 x 비교뿐)

- [ ] **Step 5: 커밋**

```bash
git add Assets/Scripts/Game/FlappyChaserView.cs
git commit -m "feat(flappy): 추격자를 붕괴 전선으로 — 다가오는 먼지벽"
```

---

### Task 9: 종합 검증과 배포

**Files:**
- Modify: `Assets/Art/Scenes/FlappyRaceMap.unity` (Art 서브모듈 — 지금까지 구운 결과)
- Modify: `Assets/Art` 포인터 (클라)

- [ ] **Step 1: 마지막으로 한 번 굽는다**

맵 씬을 열고 `LOP/Debug/Flappy 전통 코스 굽기`. 기대 로그:
`파이프 53쌍 · 창 4.02m · 간격 11.4m · 회랑 14.6m · 길이 612m (90초) · 구간 3개 × 204m`

- [ ] **Step 2: 맵 검사를 돌려 네 절을 전부 본다**

`LOP/Debug/Flappy 맵 검사`:

| 절 | 기대 |
|---|---|
| ① 클린런 | 네 스폰 전부 ✅ 봇 통과 |
| ②-d 관문 박자 | 관문 53개, 평균 간격 목표의 1.0배, 창 넓음 경고 없음 |
| 🎥 시각 정직성 | ✅ 뒤쪽 두께 0 |
| 🧱 층 규약 | ✅ 위반 없음 |

**하나라도 어긋나면 여기서 멈춘다.** 특히 ①이 깨지면 코스가 길어지며 통과 못 하는 자리가 생겼다는 뜻이다.

- [ ] **Step 3: EditMode 전체를 돌린다**

`unity run_tests --project-path /Users/insoobae/workspace/LOP/LeagueOfPhysical-Client --mode EditMode --detach`

> 긴 작업이라 `--detach`가 필요하다. 붙여 두면 파이프라인이 메인 스레드를 5초만 기다리고 실패로 보고하면서도 계속 돌아 에디터를 점유한다.

기대: 전부 통과 + 이번에 더한 테스트 **32개**(구간 7 · 층 규약 9 · 배경 배치 7 · 하늘 곡선 4 · 대기 5)가 목록에 있다.

- [ ] **Step 4: Art를 먼저 올린다**

```bash
cd Assets/Art
git status --short
git checkout -b feature/collapsing-city
git add Scenes/FlappyRaceMap.unity
git commit -m "feat(flappy): 무너지는 도시 — 세 층과 구간 셋으로 다시 구운 코스"
git fetch origin
git rebase --autostash origin/main
git checkout main
git merge --ff-only origin/main
git merge --no-ff feature/collapsing-city
git push origin main
cd ../..
```

> **한 줄씩 결과를 보고 넘어간다.** `&&`로 잇지 않는다 — 실패한 단계를 지나쳐도 뒤가 성공해 "푸시는 됐는데 절차는 안 밟은" 상태가 된다.

- [ ] **Step 5: 클라 포인터를 올린다**

```bash
git add Assets/Art
git status --short
git commit -m "chore(art): 무너지는 도시 맵 포인터"
```

그리고 위와 같은 순서로 클라 `main`에 머지·푸시한다.

- [ ] **Step 6: 콘텐츠를 굽는다**

`gh workflow run content-deploy -f target=standalone` — 맵 씬과 재질이 번들에 들어가야 로컬 k8s에서 보인다. **Art push 뒤에** 돌린다(어기면 마커가 missing script로 구워져 방이 안 뜬다).

빌드 로그에서 Art 커밋 해시가 방금 것인지 확인한다:
`gh run view <id> --log | grep -c <art-sha>`

- [ ] **Step 7: 눈으로 본다**

로컬 리그에서 한 판. 확인할 것:

| | 기대 |
|---|---|
| 깊이 | 게임 평면 / 중간층 / 배경 세 줄이 구분돼 보인다 |
| 진행 | 앞은 밝은 회백, 뒤는 붉은 갈색 + 주황 먼지빛 |
| 읽기 | **끝까지 파이프가 제일 선명하다** — 흐려지면 §3.5 위반 |
| 혼동 | 중간층을 장애물로 착각하지 않는다 |
| 추격자 | 멈추면 먼지벽이 면으로 다가온다 |
| 길이 | 한 판이 약 90초다 |

- [ ] **Step 8: ROADMAP에 적는다**

`docs/ROADMAP.md`에 절을 하나 더한다 — 무엇을 왜 했고, 무엇을 안 했는지(추격자 압박 튜닝 · 중간층 z 값 · 새 겉모습 축소). 열린 결정 넷(spec §9)의 답이 나왔으면 그 답도 같이 적는다.

- [ ] **Step 9: 커밋**

```bash
git add docs/ROADMAP.md
git commit -m "docs(roadmap): 무너지는 도시를 기록한다"
```
