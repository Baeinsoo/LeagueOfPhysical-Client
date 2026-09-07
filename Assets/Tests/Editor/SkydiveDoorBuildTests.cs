using System.Collections.Generic;
using LOP.EditorTools;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 선반마다 구멍이 둘(빠른 구멍·안전한 구멍)이 되면서 굽기 전 검사도 하나에서 셋으로 늘었다
/// (2026-09-06-skydive-doors-and-branching-design §3.2). 통과 케이스는 실제 <c>Shelves</c> 표를
/// 그대로 재고, 거절 케이스는 각 검사의 인젝터블 오버로드에 손으로 지은 작은 코스를 넣어
/// 확인한다 — 실제 표를 바꾸지 않고도 "이 검사가 이걸 잡아내는가"를 직접 겨눌 수 있다.
///
/// 거절 테스트는 절제(ablation)로도 확인한다 — 해당 가드를 잠깐 꺼서 이 테스트가 거꾸로
/// 통과하는지 보고 되돌린다. (레이저 슬라이스에서 굽기 검사기가 격자를 엉뚱한 높이에서 뽑아
/// 아무것도 못 보고 있었는데 통과·거절 테스트가 둘 다 초록이었던 사고의 재발 방지.)
/// </summary>
public class SkydiveDoorBuildTests
{
    // 코스만 겨누는 테스트는 바람을 빼고 잰다 — 그래야 실패가 코스 탓임이 분명해진다.
    private static readonly SkydiveCourseBuilder.WindSpec[] NoWind = { };

    private static SkydiveCourseBuilder.Hole Hole(float x, float z, float side, bool hasDoor)
        => new SkydiveCourseBuilder.Hole(x, z, side, hasDoor);

    private static SkydiveCourseBuilder.Shelf Shelf(float y, params SkydiveCourseBuilder.Hole[] holes)
        => new SkydiveCourseBuilder.Shelf(y, holes);

    // ── 통과 — 실제 표 ───────────────────────────────────────────────────

    [Test]
    public void 표의_코스는_전체_경로로_완주할_수_있다()
    {
        bool ok = SkydiveCourseBuilder.ReachableChain(safeOnly: false, out string report);

        Assert.IsTrue(ok, report);
    }

    [Test]
    public void 표의_코스는_안전_경로만으로도_완주할_수_있다()
    {
        // 문을 하나도 못 뚫는 사람도 느리게나마 끝낼 수 있어야 한다 — 못 끝내면
        // 그건 난이도가 아니라 고장이다(스펙 §3.2).
        bool ok = SkydiveCourseBuilder.ReachableChain(safeOnly: true, out string report);

        Assert.IsTrue(ok, report);
    }

    [Test]
    public void 표의_빠른_구멍과_안전한_구멍은_요구하는_자세가_갈린다()
    {
        // 바람까지 넣어서 잰다 — 순풍이 미는 자리에 안전한 구멍이 있으면 다이브가 공짜로
        // 실려 가 도달해 버려서, 무풍으로만 재면 갈림길이 무너진 표가 통과한다.
        string failure = SkydiveCourseBuilder.FindRouteNotSplit();

        Assert.IsNull(failure, failure);
    }

    [Test]
    public void 표의_안전한_구멍만으로도_모든_구간의_바람을_뚫는다()
    {
        // "네 조합 중 아무거나 하나"로는 스펙 §3.2 ②를 증명하지 못한다 — 빠른 구멍만
        // 뚫려 있어도 통과로 보이기 때문이다.
        string failure = SkydiveCourseBuilder.FindImpassableSection(
            SkydiveCourseBuilder.Winds, safeOnly: true);

        Assert.IsNull(failure, failure);
    }

    [Test]
    public void 표의_부활_지점은_어느_구멍과도_안_겹친다()
    {
        string failure = SkydiveCourseBuilder.FindInvalidRespawn();

        Assert.IsNull(failure, failure);
    }

    [Test]
    public void 표의_구멍은_기둥과_겹치지_않는다()
    {
        string failure = SkydiveCourseBuilder.FindHoleOnPillar();

        Assert.IsNull(failure, failure);
    }

    // ── 거절 ① — 앞 칸에서 실제로 갈 수 있었던 자리에서만 닿아야 한다 ─────────

    [Test]
    public void 어느_구멍에도_안_닿으면_전체_경로가_거절된다()
    {
        var shelves = new[]
        {
            Shelf(2600f, Hole(0f, 0f, 30f, hasDoor: true)),
            //  400m 낙하 동안 대자로도 76.7m밖에 못 간다(SkydiveReach.MaxHorizontal) —
            //  200m는 어떤 자세로도 안 닿는다.
            Shelf(2200f, Hole(200f, 0f, 10f, hasDoor: false)),
        };

        bool ok = SkydiveCourseBuilder.ReachableChain(safeOnly: false, shelves, out string report);

        Assert.IsFalse(ok, report);
        StringAssert.Contains("닿는 구멍이 없다", report);
    }

    [Test]
    public void 앞_구멍_한쪽에서만_닿으면_전체_경로가_거절된다()
    {
        //  스펙 §3.2 ①은 "각 구멍에서 적어도 하나의 다음 구멍에 닿는가"다. 아래 코스는
        //  (0,0)에서는 다음 선반에 닿지만 (70,0)에서는 90m라 못 닿는다 — (70,0)으로
        //  내려간 사람은 갇힌다. "도달 가능한 구멍들 중 누군가에게서 닿으면 됨"으로
        //  재는 구현은 이걸 초록으로 통과시킨다.
        var shelves = new[]
        {
            Shelf(2600f,
                Hole(0f, 0f, 20f, hasDoor: true),
                Hole(70f, 0f, 20f, hasDoor: false)),
            Shelf(2200f, Hole(-20f, 0f, 20f, hasDoor: false)),
        };

        bool ok = SkydiveCourseBuilder.ReachableChain(safeOnly: false, shelves, out string report);

        Assert.IsFalse(ok, report);
        StringAssert.Contains("막다른 길", report);
    }

    // ── 거절 ② — 안전한 구멍만으로는 완주가 안 될 수 있다 ────────────────────

    [Test]
    public void 안전_경로만으로는_못_끝내면_거절된다()
    {
        //  전체 경로(①)로는 끝까지 가지만(빠른 구멍을 거쳐 가면 닿는다), 안전한 구멍만
        //  골라 가면 두 번째 칸에서 끊긴다 — ①과 ②가 서로 다른 것을 재고 있음을 보여준다.
        var shelves = new[]
        {
            Shelf(2600f,
                Hole(50f, 0f, 10f, hasDoor: true),    // 빠른 구멍 — 스폰에서 닿는다
                Hole(0f, 0f, 10f, hasDoor: false)),   // 안전한 구멍 — 역시 스폰에서 닿는다
            //  전체 경로: 두 구멍 다 다음 칸을 갖는다 — (0,0)은 빠른 구멍(0,0)으로, (50,0)은
            //  둘 다로 갈 수 있다 → ①은 통과.
            //  안전 경로: 위 안전한 구멍(0,0)에서 안전한 구멍(120,0)까지 120m라 대자로도
            //  못 닿는다 → ②는 거절.
            Shelf(2200f,
                Hole(0f, 0f, 10f, hasDoor: true),
                Hole(120f, 0f, 10f, hasDoor: false)),
        };

        bool generalOk = SkydiveCourseBuilder.ReachableChain(safeOnly: false, shelves, out string generalReport);
        Assert.IsTrue(generalOk, "이 테스트 전제가 깨졌다 — 전체 경로부터 안 닿는다: " + generalReport);

        bool safeOk = SkydiveCourseBuilder.ReachableChain(safeOnly: true, shelves, out string safeReport);

        Assert.IsFalse(safeOk, safeReport);
        StringAssert.Contains("안전 경로", safeReport);
    }

    // ── 거절 ③ — 빠른/안전 구멍이 요구하는 자세가 갈려야 한다 ────────────────

    [Test]
    public void 빠른_구멍이_다이브로_안_닿으면_거절된다()
    {
        var shelves = new[]
        {
            Shelf(2600f, Hole(0f, 0f, 30f, hasDoor: true)),
            //  400m 낙하 동안 다이브 도달은 33.25m뿐이다 — 60m짜리 "빠른 구멍"은 다이브로도 못 닿는다.
            Shelf(2200f, Hole(60f, 0f, 10f, hasDoor: true)),
        };

        string failure = SkydiveCourseBuilder.FindRouteNotSplit(shelves, NoWind);

        Assert.IsNotNull(failure);
        StringAssert.Contains("다이브로 안 닿는다", failure);
    }

    [Test]
    public void 안전한_구멍이_다이브로도_닿으면_거절된다()
    {
        var shelves = new[]
        {
            Shelf(2600f, Hole(0f, 0f, 30f, hasDoor: true)),
            //  10m짜리 "안전한 구멍"은 다이브 도달(33.25m) 안이라 문이 있으나 없으나 똑같이 간다.
            Shelf(2200f, Hole(10f, 0f, 10f, hasDoor: false)),
        };

        string failure = SkydiveCourseBuilder.FindRouteNotSplit(shelves, NoWind);

        Assert.IsNotNull(failure);
        StringAssert.Contains("문이 무의미해진다", failure);
    }

    [Test]
    public void 순풍이_안전한_구멍을_다이브_사거리에_넣으면_거절된다()
    {
        //  무풍이면 안전한 구멍(0,-50)이 다이브 도달(33.25+5=38.25m) 밖이라 성질이 산다.
        //  그런데 구간 전체를 덮는 -Z 순풍은 다이브를 57.9m 실어 주므로, 실려 내려가면
        //  구멍이 7.9m 앞에 놓인다 — 문이 무의미해진다. 이 코스가 바로 실제 표에서
        //  1400→1000·600→200 구간이 무너져 있던 모양이다.
        var shelves = new[]
        {
            Shelf(2600f,
                Hole(25f, -29f, 20f, hasDoor: true),
                Hole(0f, -50f, 20f, hasDoor: false)),
        };
        var tailwind = new[]
        {
            new SkydiveCourseBuilder.WindSpec("Test_Tail",
                new Vector3(0f, 2800f, 0f), 150f, 400f, new Vector3(0f, 0f, -20f)),
        };

        Assert.IsNull(SkydiveCourseBuilder.FindRouteNotSplit(shelves, NoWind),
                      "이 테스트 전제가 깨졌다 — 무풍에서는 통과해야 한다");

        string failure = SkydiveCourseBuilder.FindRouteNotSplit(shelves, tailwind);

        Assert.IsNotNull(failure, "바람을 안 보는 구현은 여기서 null을 준다");
        StringAssert.Contains("다이브로도 닿는다", failure);
    }

    [Test]
    public void 도달할_수_없는_앞_구멍에서는_자세를_재지_않는다()
    {
        //  2600의 안전한 구멍(90,0)은 스폰에서 대자로도 못 닿는다(90 > 76.7+10). 거기서
        //  내려온 사람은 없으므로 2200의 안전한 구멍(70,0)까지 20m라는 사실은 의미가 없다.
        //  도달 불가능한 구멍까지 다음 칸 출발점으로 삼는 구현은 "문이 무의미해진다"로
        //  잘못 거절한다.
        var shelves = new[]
        {
            Shelf(2600f,
                Hole(0f, 0f, 20f, hasDoor: true),
                Hole(90f, 0f, 20f, hasDoor: false)),
            Shelf(2200f, Hole(70f, 0f, 20f, hasDoor: false)),
        };

        string failure = SkydiveCourseBuilder.FindRouteNotSplit(shelves, NoWind);

        Assert.IsNull(failure, failure);
    }

    [Test]
    public void 구멍_반폭은_다이브_사거리에_더해진다()
    {
        //  다이브 도달은 33.25m. 40m 떨어진 구멍은 반폭 10m를 더해야(43.25m) 닿고,
        //  반폭이 6m면(39.25m) 못 닿는다. 문턱 양쪽을 다 재야 반폭 항이 살아 있음이 증명된다.
        var wide = new[] { Shelf(2600f, Hole(40f, 0f, 20f, hasDoor: true)) };
        var narrow = new[] { Shelf(2600f, Hole(40f, 0f, 12f, hasDoor: true)) };

        Assert.IsNull(SkydiveCourseBuilder.FindRouteNotSplit(wide, NoWind),
                      "반폭 10m를 더하면 40m는 다이브 사거리 안이다");

        string failure = SkydiveCourseBuilder.FindRouteNotSplit(narrow, NoWind);

        Assert.IsNotNull(failure, "반폭 6m로는 40m가 다이브 사거리 밖이다");
        StringAssert.Contains("다이브로 안 닿는다", failure);
    }

    // ── 거절 — 바람이 안전한 레인만 막는 경우 (스펙 §3.2 ②) ──────────────────

    [Test]
    public void 안전한_레인만_막는_바람은_거절된다()
    {
        //  2600→2200 구간에 +Z 순풍 10m/s. 빠른 구멍 쌍((0,0)→(30,0))은 +Z 이동이 필요
        //  없어 밀려도 대자로 닿지만, 안전한 구멍 쌍((0,60)→(55,30))은 -30m를 가야 하는데
        //  바람이 +56m를 밀어 대자로도 못 닿는다. "네 조합 중 하나만 통과하면 됨"으로 재면
        //  안전한 레인이 완전히 막혀도 초록이다.
        var winds = new[]
        {
            new SkydiveCourseBuilder.WindSpec("Test_SafeLaneBlock",
                new Vector3(0f, 2400f, 0f), 150f, 400f, new Vector3(0f, 0f, 10f)),
        };

        Assert.IsNull(SkydiveCourseBuilder.FindImpassableSection(winds),
                      "이 테스트 전제가 깨졌다 — 빠른 구멍 쌍으로는 지나갈 수 있어야 한다");

        string failure = SkydiveCourseBuilder.FindImpassableSection(winds, safeOnly: true);

        Assert.IsNotNull(failure);
        StringAssert.Contains("안전한 구멍만으로는", failure);
        StringAssert.Contains("2600", failure);
        StringAssert.Contains("2200", failure);
    }

    // ── 거절 ④ — 부활 지점이 구멍·기둥과 겹치면 안 된다 (리뷰 Ruling 10) ──────

    [Test]
    public void 부활_지점이_구멍_안이면_거절된다()
    {
        //  실제 SkydiveCourseLayout.RespawnPoints[2600] = (0, 2600, 40). 이 좌표를 그대로
        //  삼키는 구멍을 손으로 지어 검사가 실제 그 값을 보는지 확인한다.
        var shelves = new[]
        {
            Shelf(2600f, Hole(0f, 40f, 20f, hasDoor: true)),
        };

        string failure = SkydiveCourseBuilder.FindInvalidRespawn(shelves);

        Assert.IsNotNull(failure);
        StringAssert.Contains("부활 지점이 구멍", failure);
    }

    [Test]
    public void 부활_지점이_두_번째_구멍_안이어도_거절된다()
    {
        //  이번 슬라이스의 핵심 변경은 "구멍이 둘"이다. 첫 구멍만 보는 구현은 이 코스를
        //  통과시킨다 — 부활 지점이 두 번째 구멍 한복판에 놓여 세우자마자 빠지는데도.
        var shelves = new[]
        {
            Shelf(2600f,
                Hole(0f, -40f, 10f, hasDoor: true),    // 부활 지점(0,40)과 안 겹친다
                Hole(0f, 40f, 20f, hasDoor: false)),   // 여기가 부활 지점을 삼킨다
        };

        string failure = SkydiveCourseBuilder.FindInvalidRespawn(shelves);

        Assert.IsNotNull(failure);
        StringAssert.Contains("구멍(0,40)", failure);
    }

    [Test]
    public void 구멍이_기둥_위에_있으면_거절된다()
    {
        var shelves = new[]
        {
            Shelf(2600f, Hole(60f, 60f, 20f, hasDoor: true)),
        };

        string failure = SkydiveCourseBuilder.FindHoleOnPillar(shelves);

        Assert.IsNotNull(failure);
        StringAssert.Contains("기둥", failure);
    }
}

/// <summary>
/// 판에서 구멍을 도려내 사각형 조각으로 쪼개는 산수(<c>SkydiveCourseBuilder.Carve</c>).
/// GameObject를 안 만드는 순수 함수라 Unity 없이 잰다 — 구멍이 둘이 되면서 새로 짜인,
/// 가장 실수하기 쉬운 부분이다.
/// </summary>
public class SkydiveShelfCarveTests
{
    private const float SlabHalf = 100f;

    private static SkydiveCourseBuilder.Hole Hole(float x, float z, float side)
        => new SkydiveCourseBuilder.Hole(x, z, side, hasDoor: false);

    private static SkydiveCourseBuilder.Plate Slab() => SkydiveCourseBuilder.FullSlab();

    [Test]
    public void 구멍_하나면_옛_4분할과_같은_값이_나온다()
    {
        //  구멍이 하나였을 때 빌더가 쓰던 공식: 북/남은 판 폭 전체, 동/서는 구멍 높이만큼.
        const float x = 30f, z = 30f, half = 10f;
        List<SkydiveCourseBuilder.Plate> plates = SkydiveCourseBuilder.Carve(Slab(), new[] { Hole(x, z, half * 2f) });

        Assert.AreEqual(4, plates.Count);
        AssertHas(plates, "N", -SlabHalf, SlabHalf, z + half, SlabHalf);
        AssertHas(plates, "S", -SlabHalf, SlabHalf, -SlabHalf, z - half);
        AssertHas(plates, "E", x + half, SlabHalf, z - half, z + half);
        AssertHas(plates, "W", -SlabHalf, x - half, z - half, z + half);
    }

    [Test]
    public void 구멍_둘이면_조각_면적_합이_판에서_구멍_둘을_뺀_값이다()
    {
        //  면적이 맞으면 조각들이 구멍을 덮지도, 서로 겹치지도 않았다는 뜻이다.
        var holes = new[] { Hole(-20f, -10f, 16f), Hole(15f, 25f, 16f) };

        List<SkydiveCourseBuilder.Plate> plates = SkydiveCourseBuilder.Carve(Slab(), holes);

        float sum = 0f;
        foreach (SkydiveCourseBuilder.Plate p in plates)
        {
            sum += p.Area;
        }

        Assert.AreEqual(200f * 200f - 16f * 16f - 16f * 16f, sum, 0.01f);
    }

    [Test]
    public void 조각끼리_겹치지_않는다()
    {
        var holes = new[] { Hole(-20f, -10f, 16f), Hole(15f, 25f, 16f) };

        List<SkydiveCourseBuilder.Plate> plates = SkydiveCourseBuilder.Carve(Slab(), holes);

        Assert.Greater(plates.Count, 4, "구멍 둘을 깎으면 조각이 넷보다 많아야 한다");
        for (int i = 0; i < plates.Count; i++)
        {
            for (int j = i + 1; j < plates.Count; j++)
            {
                SkydiveCourseBuilder.Plate a = plates[i];
                SkydiveCourseBuilder.Plate b = plates[j];
                bool overlap = a.XMin < b.XMax && b.XMin < a.XMax
                            && a.ZMin < b.ZMax && b.ZMin < a.ZMax;
                Assert.IsFalse(overlap, $"{a.Name}와 {b.Name}가 겹친다");
            }
        }
    }

    [Test]
    public void 모든_조각이_판_안에_있다()
    {
        var holes = new[] { Hole(-20f, -10f, 16f), Hole(15f, 25f, 16f) };

        foreach (SkydiveCourseBuilder.Plate p in SkydiveCourseBuilder.Carve(Slab(), holes))
        {
            Assert.GreaterOrEqual(p.XMin, -SlabHalf, p.Name);
            Assert.LessOrEqual(p.XMax, SlabHalf, p.Name);
            Assert.GreaterOrEqual(p.ZMin, -SlabHalf, p.Name);
            Assert.LessOrEqual(p.ZMax, SlabHalf, p.Name);
        }
    }

    [Test]
    public void 판_가장자리에_붙은_구멍은_음수나_0_크기_조각을_만들지_않는다()
    {
        //  구멍이 판 서쪽 끝에 딱 붙으면 서쪽 조각은 폭 0이라 아예 안 나와야 한다.
        var holes = new[] { Hole(-SlabHalf + 10f, 0f, 20f) };

        List<SkydiveCourseBuilder.Plate> plates = SkydiveCourseBuilder.Carve(Slab(), holes);

        Assert.AreEqual(3, plates.Count, "가장자리에 붙으면 서쪽 조각이 없어 셋이다");
        foreach (SkydiveCourseBuilder.Plate p in plates)
        {
            Assert.Greater(p.Width, 0f, p.Name);
            Assert.Greater(p.Depth, 0f, p.Name);
        }
    }

    private static void AssertHas(List<SkydiveCourseBuilder.Plate> plates, string name,
                                  float xMin, float xMax, float zMin, float zMax)
    {
        foreach (SkydiveCourseBuilder.Plate p in plates)
        {
            if (p.Name != name)
            {
                continue;
            }
            Assert.AreEqual(xMin, p.XMin, 0.001f, name);
            Assert.AreEqual(xMax, p.XMax, 0.001f, name);
            Assert.AreEqual(zMin, p.ZMin, 0.001f, name);
            Assert.AreEqual(zMax, p.ZMax, 0.001f, name);
            return;
        }
        Assert.Fail($"{name} 조각이 없다");
    }
}
