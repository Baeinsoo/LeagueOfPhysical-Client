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
            //  400m 낙하 동안 대자로도 76.7m밖에 못 간다(SkydiveWindReach.SelfReach) —
            //  200m는 어떤 자세로도 안 닿는다.
            Shelf(2200f, Hole(200f, 0f, 10f, hasDoor: false)),
        };

        bool ok = SkydiveCourseBuilder.ReachableChain(safeOnly: false, shelves, NoWind, out string report);

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

        bool ok = SkydiveCourseBuilder.ReachableChain(safeOnly: false, shelves, NoWind, out string report);

        Assert.IsFalse(ok, report);
        StringAssert.Contains("막다른 길", report);
    }

    [Test]
    public void 역풍이_다음_구멍을_사거리_밖으로_밀면_전체_경로가_거절된다()
    {
        //  검사 ①도 바람을 본다. 무풍이면 스폰에서 70m 구멍은 대자 도달(76.7m)+반폭 10m 안이라
        //  닿지만, 구간 전체를 덮는 -X 역풍은 대자를 56m 뒤로 밀어 126m를 가게 만든다(사거리 86.7m).
        //  다이브는 29m 밀리는데 사거리가 43m뿐이라 더 못 간다 — 어느 자세로도 못 닿는다.
        //  ①이 무풍으로만 재면 이런 코스를 초록으로 통과시킨다.
        var shelves = new[]
        {
            Shelf(2600f, Hole(70f, 0f, 20f, hasDoor: true)),
        };
        var headwind = new[]
        {
            new SkydiveCourseBuilder.WindSpec("Test_Head",
                new Vector3(0f, 2800f, 0f), 150f, 400f, new Vector3(-10f, 0f, 0f)),
        };

        Assert.IsTrue(SkydiveCourseBuilder.ReachableChain(safeOnly: false, shelves, NoWind, out _),
                      "이 테스트 전제가 깨졌다 — 무풍에서는 닿아야 한다");

        bool ok = SkydiveCourseBuilder.ReachableChain(safeOnly: false, shelves, headwind, out string report);

        Assert.IsFalse(ok, "바람을 안 보는 구현은 여기서 통과한다");
        StringAssert.Contains("닿는 구멍이 없다", report);
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

        bool generalOk = SkydiveCourseBuilder.ReachableChain(safeOnly: false, shelves, NoWind, out string generalReport);
        Assert.IsTrue(generalOk, "이 테스트 전제가 깨졌다 — 전체 경로부터 안 닿는다: " + generalReport);

        bool safeOk = SkydiveCourseBuilder.ReachableChain(safeOnly: true, shelves, NoWind, out string safeReport);

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

    [Test]
    public void 두_번째_구멍이_기둥_위여도_거절된다()
    {
        //  구멍이 하나짜리인 코스로만 재면 첫 구멍만 보는 구현도 통과한다 — 이번 슬라이스의
        //  핵심 변경(구멍이 둘)을 실제로 겨눈다.
        var shelves = new[]
        {
            Shelf(2600f,
                Hole(0f, 0f, 20f, hasDoor: true),      // 기둥(±60,±60)과 안 겹친다
                Hole(-60f, 60f, 20f, hasDoor: false)), // 여기가 기둥 위다
        };

        string failure = SkydiveCourseBuilder.FindHoleOnPillar(shelves);

        Assert.IsNotNull(failure);
        StringAssert.Contains("구멍(-60,60)", failure);
    }
}

/// <summary>
/// 문 표(<c>SkydiveCourseBuilder.Doors</c>)와 구멍 표가 서로 맞는지 재는 굽기 전 검사들.
/// 표 둘이 조용히 어긋나면 에러 하나 없이 <b>게임만</b> 달라진다 — 빠른 구멍인데 문이 없거나,
/// 안전한 구멍에 문이 붙거나, 닫아도 모서리가 뚫린 문이 서거나, 부활 지점이 벽 속이거나.
/// 그래서 주석이 아니라 검사로 지킨다.
///
/// 거절 케이스는 각 검사의 인젝터블 오버로드에 손으로 지은 표를 넣어 확인하고, 절제(ablation)로
/// 그 가드를 잠깐 껐을 때 거꾸로 통과하는지도 본다.
/// </summary>
public class SkydiveDoorSpecTests
{
    private static SkydiveCourseBuilder.Hole Hole(float x, float z, float side, bool hasDoor)
        => new SkydiveCourseBuilder.Hole(x, z, side, hasDoor);

    private static SkydiveCourseBuilder.Shelf Shelf(float y, params SkydiveCourseBuilder.Hole[] holes)
        => new SkydiveCourseBuilder.Shelf(y, holes);

    private static SkydiveCourseBuilder.DoorSpec Door(string name, Vector3 center, float half,
                                                      float axisAngleDegrees = 0f, float halfDepth = -1f)
        => new SkydiveCourseBuilder.DoorSpec(name, center, half, halfDepth < 0f ? half : halfDepth,
                                            axisAngleDegrees, period: 120, openTicks: 40,
                                            moveTicks: 20, phase: 0);

    // ── 통과 — 실제 표 ───────────────────────────────────────────────────

    [Test]
    public void 표의_문은_빠른_구멍마다_정확히_하나씩_있다()
    {
        string failure = SkydiveCourseBuilder.FindDoorHoleMismatch();

        Assert.IsNull(failure, failure);
    }

    [Test]
    public void 표의_문은_자기_구멍_치수와_맞는다()
    {
        string failure = SkydiveCourseBuilder.FindDoorSizeMismatch();

        Assert.IsNull(failure, failure);
    }

    [Test]
    public void 표의_부활_지점은_어느_문_패널과도_안_겹친다()
    {
        string failure = SkydiveCourseBuilder.FindRespawnInDoorPanel();

        Assert.IsNull(failure, failure);
    }

    [Test]
    public void 표의_문은_모두_완전히_닫히는_구간을_갖는다()
    {
        //  ClosedTicks가 0 이하면 openness가 0에 닿지 않아 크러시 판정이 영영 안 걸린다 —
        //  문이 장식이 되고 갈림길이 사라지는데 아무 에러도 안 난다.
        foreach (SkydiveCourseBuilder.DoorSpec spec in SkydiveCourseBuilder.Doors)
        {
            Assert.Greater(spec.ClosedTicks, 0, spec.Name);
        }
    }

    // ── 거절 C1 — 문과 구멍이 1:1 ────────────────────────────────────────

    [Test]
    public void 빠른_구멍에_문이_없으면_거절된다()
    {
        var shelves = new[] { Shelf(2600f, Hole(0f, 0f, 30f, hasDoor: true)) };
        var doors = new SkydiveCourseBuilder.DoorSpec[0];

        string failure = SkydiveCourseBuilder.FindDoorHoleMismatch(shelves, doors);

        Assert.IsNotNull(failure);
        StringAssert.Contains("문이 0개다", failure);
    }

    [Test]
    public void 같은_구멍에_문이_둘이면_거절된다()
    {
        var shelves = new[] { Shelf(2600f, Hole(0f, 0f, 30f, hasDoor: true)) };
        var doors = new[]
        {
            Door("Door_A", new Vector3(0f, 2600f, 0f), 15f),
            Door("Door_B", new Vector3(0f, 2600f, 0f), 15f),
        };

        string failure = SkydiveCourseBuilder.FindDoorHoleMismatch(shelves, doors);

        Assert.IsNotNull(failure);
        StringAssert.Contains("문이 2개다", failure);
    }

    [Test]
    public void 안전한_구멍에_문이_있으면_거절된다()
    {
        //  안전한 길의 존재 이유는 "문 타이밍을 못 맞춰도 끝낼 수 있다"(스펙 §3.2 ②)다.
        var shelves = new[]
        {
            Shelf(2600f,
                Hole(0f, 0f, 30f, hasDoor: true),
                Hole(0f, 60f, 20f, hasDoor: false)),
        };
        var doors = new[]
        {
            Door("Door_Fast", new Vector3(0f, 2600f, 0f), 15f),
            Door("Door_Safe", new Vector3(0f, 2600f, 60f), 10f),
        };

        string failure = SkydiveCourseBuilder.FindDoorHoleMismatch(shelves, doors);

        Assert.IsNotNull(failure);
        StringAssert.Contains("안전한 구멍(0,60)에 문이 있다", failure);
    }

    [Test]
    public void 구멍이_없는_자리의_문은_거절된다()
    {
        //  판 한복판에 벽만 서는 것을 막는다. "구멍마다 문이 있나"만 보는 구현은 이걸 놓친다.
        var shelves = new[] { Shelf(2600f, Hole(0f, 0f, 30f, hasDoor: true)) };
        var doors = new[]
        {
            Door("Door_Fast", new Vector3(0f, 2600f, 0f), 15f),
            Door("Door_Nowhere", new Vector3(70f, 2600f, 70f), 10f),
        };

        string failure = SkydiveCourseBuilder.FindDoorHoleMismatch(shelves, doors);

        Assert.IsNotNull(failure);
        StringAssert.Contains("Door_Nowhere", failure);
    }

    // ── 거절 C3 — 문 치수·축 ─────────────────────────────────────────────

    [Test]
    public void 문이_구멍보다_넓으면_거절된다()
    {
        var shelves = new[] { Shelf(2600f, Hole(0f, 0f, 30f, hasDoor: true)) };
        var doors = new[] { Door("Door_Wide", new Vector3(0f, 2600f, 0f), 16f) };

        string failure = SkydiveCourseBuilder.FindDoorSizeMismatch(shelves, doors);

        Assert.IsNotNull(failure);
        StringAssert.Contains("HalfWidth", failure);
    }

    [Test]
    public void 문_깊이가_구멍과_다르면_거절된다()
    {
        //  폭만 재는 구현은 이걸 통과시킨다 — 닫혀 있는데 z 방향으로 빠져나갈 수 있다.
        var shelves = new[] { Shelf(2600f, Hole(0f, 0f, 30f, hasDoor: true)) };
        var doors = new[] { Door("Door_Shallow", new Vector3(0f, 2600f, 0f), 15f, halfDepth: 10f) };

        string failure = SkydiveCourseBuilder.FindDoorSizeMismatch(shelves, doors);

        Assert.IsNotNull(failure);
        StringAssert.Contains("HalfDepth", failure);
    }

    [Test]
    public void 미끄러지는_축이_구십도의_배수가_아니면_거절된다()
    {
        //  정사각형은 90° 회전에만 자기 자신이 된다. 45°짜리 문은 닫아도 구멍 네 모서리가
        //  뚫린 채로 남는다 — 치수만 재는 구현은 이걸 통과시킨다.
        var shelves = new[] { Shelf(2600f, Hole(0f, 0f, 30f, hasDoor: true)) };
        var straight = new[] { Door("Door_Ok", new Vector3(0f, 2600f, 0f), 15f, axisAngleDegrees: 180f) };
        var tilted = new[] { Door("Door_Tilted", new Vector3(0f, 2600f, 0f), 15f, axisAngleDegrees: 45f) };

        Assert.IsNull(SkydiveCourseBuilder.FindDoorSizeMismatch(shelves, straight),
                      "180도는 90의 배수라 통과해야 한다");

        string failure = SkydiveCourseBuilder.FindDoorSizeMismatch(shelves, tilted);

        Assert.IsNotNull(failure);
        StringAssert.Contains("모서리가 뚫린다", failure);
    }

    // ── 거절 C2 — 부활 지점이 패널 부피 안 ───────────────────────────────

    [Test]
    public void 물러난_패널이_부활_지점을_덮으면_거절된다()
    {
        //  실제 200 선반이 그 자리다. 부활 지점(0,−15)과 빠른 구멍(0,0) 가장자리(z=−8) 사이가
        //  7m뿐이라, 문이 ±Z로 물러나면(90°) 물러난 띠(로컬 x 8~16)가 그 자리를 정통으로 덮는다.
        //  ±X로 물러나면(0°) 10.6m 떨어져 안전하다 — 축 하나가 결과를 가른다.
        var shelves = new[] { Shelf(200f, Hole(0f, 0f, 16f, hasDoor: true)) };
        var alongX = new[] { Door("Door_200", new Vector3(0f, 200f, 0f), 8f, axisAngleDegrees: 0f) };
        var alongZ = new[] { Door("Door_200", new Vector3(0f, 200f, 0f), 8f, axisAngleDegrees: 90f) };

        Assert.IsNull(SkydiveCourseBuilder.FindRespawnInDoorPanel(shelves, alongX),
                      "이 테스트 전제가 깨졌다 — ±X로 물러나면 부활 지점과 안 겹친다");

        string failure = SkydiveCourseBuilder.FindRespawnInDoorPanel(shelves, alongZ);

        Assert.IsNotNull(failure, "닫힌 자세만 보는 구현은 여기서 null을 준다");
        StringAssert.Contains("물러난", failure);
    }

    [Test]
    public void 닫힌_패널이_부활_지점을_덮으면_거절된다()
    {
        //  부활 지점(0,200,−15) 한복판에 문을 놓는다. 닫힌 패널은 거기를 채우고, 물러난 패널은
        //  12m 옆이라 안 닿는다 — 물러난 자세만 보는 구현은 이걸 놓친다.
        var shelves = new[] { Shelf(200f, Hole(0f, -15f, 16f, hasDoor: true)) };
        var doors = new[] { Door("Door_OnRespawn", new Vector3(0f, 200f, -15f), 8f) };

        string failure = SkydiveCourseBuilder.FindRespawnInDoorPanel(shelves, doors);

        Assert.IsNotNull(failure);
        StringAssert.Contains("닫힌", failure);
    }
}

/// <summary>
/// 구운 문 패널이 <b>판정이 보는 상자</b>와 같은지 되읽어 대조한다
/// (<c>SkydiveCourseBuilder.FindDoorPanelMismatch</c>).
///
/// 이 프로젝트는 이미 "그린 도형과 판정 도형이 달라" 한 번 당했다
/// (<c>[[hitbox-vs-drawn-shape-must-be-compared]]</c>). 문 패널은 상자라 계산이 쉬운 만큼,
/// 어긋나도 눈으로는 안 보인다 — 크기·회전·자리를 숫자로 대조하는 것만이 증거다.
/// </summary>
public class SkydiveDoorBakeTests
{
    private static SkydiveCourseBuilder.DoorSpec Spec(float axisAngleDegrees)
        => new SkydiveCourseBuilder.DoorSpec("Door_Test", new Vector3(0f, 200f, 0f),
                                             halfWidth: 8f, halfDepth: 8f,
                                             axisAngleDegrees: axisAngleDegrees,
                                             period: 120, openTicks: 40, moveTicks: 20, phase: 0);

    private static GameObject Bake(float axisAngleDegrees)
        => SkydiveCourseBuilder.CreateDoorVolume(null, Spec(axisAngleDegrees), null);

    [Test]
    public void 구운_패널의_콜라이더가_판정_상자와_같다()
    {
        GameObject go = Bake(0f);
        try
        {
            var volume = go.GetComponent<LOP.DoorVolume>();

            //  판정(DoorGeometry)의 반치수는 (HalfWidth/2, Thickness/2, HalfDepth)다.
            var expected = new Vector3(8f, 2.8f, 16f);
            foreach (Transform panel in new[] { volume.PanelA, volume.PanelB })
            {
                Vector3 size = Vector3.Scale(panel.GetComponent<BoxCollider>().size, panel.lossyScale);
                Assert.AreEqual(expected.x, size.x, 0.001f, panel.name + " x");
                Assert.AreEqual(expected.y, size.y, 0.001f, panel.name + " y");
                Assert.AreEqual(expected.z, size.z, 0.001f, panel.name + " z");
            }

            Assert.IsNull(SkydiveCourseBuilder.FindDoorPanelMismatch(volume));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void 각진_문은_패널까지_같은_각으로_돌아_있다()
    {
        //  판정 상자는 AxisAngle만큼 돌아간 상자다. 패널을 안 돌리고 크기만 맞추면
        //  90도 문에서 보이는 상자와 죽이는 상자가 직각으로 갈린다.
        GameObject go = Bake(90f);
        try
        {
            var volume = go.GetComponent<LOP.DoorVolume>();

            Assert.AreEqual(0f, Quaternion.Angle(volume.transform.rotation, Quaternion.identity), 0.01f,
                            "허브는 절대 안 돈다");
            foreach (Transform panel in new[] { volume.PanelA, volume.PanelB })
            {
                Assert.AreEqual(0f, Quaternion.Angle(panel.rotation, Quaternion.Euler(0f, 90f, 0f)), 0.01f,
                                panel.name);
            }

            Assert.IsNull(SkydiveCourseBuilder.FindDoorPanelMismatch(volume));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void 허브가_돌아가_있으면_걸린다()
    {
        GameObject go = Bake(0f);
        try
        {
            go.transform.rotation = Quaternion.Euler(0f, 30f, 0f);

            string failure = SkydiveCourseBuilder.FindDoorPanelMismatch(go.GetComponent<LOP.DoorVolume>());

            Assert.IsNotNull(failure);
            StringAssert.Contains("허브가 돌아가 있다", failure);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void 패널_크기가_판정_상자와_다르면_걸린다()
    {
        GameObject go = Bake(0f);
        try
        {
            var volume = go.GetComponent<LOP.DoorVolume>();
            volume.PanelA.localScale = new Vector3(8f, 2.8f, 8f);   // HalfDepth를 두 배 안 한 실수

            string failure = SkydiveCourseBuilder.FindDoorPanelMismatch(volume);

            Assert.IsNotNull(failure);
            StringAssert.Contains("콜라이더 크기", failure);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void 패널_회전이_미끄러지는_축과_다르면_걸린다()
    {
        GameObject go = Bake(90f);
        try
        {
            var volume = go.GetComponent<LOP.DoorVolume>();
            volume.PanelA.localRotation = Quaternion.identity;   // 축을 안 반영한 실수

            string failure = SkydiveCourseBuilder.FindDoorPanelMismatch(volume);

            Assert.IsNotNull(failure);
            StringAssert.Contains("회전이 미끄러지는 축", failure);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void 콜라이더를_지우면_걸린다()
    {
        //  레이저 마커는 콜라이더를 지운다. 그 습관대로 문에도 지우면 벽이 아니게 되어
        //  닫히는 문이 사람을 밀어내지 못하고 그냥 통과시킨다.
        GameObject go = Bake(0f);
        try
        {
            var volume = go.GetComponent<LOP.DoorVolume>();
            Object.DestroyImmediate(volume.PanelA.GetComponent<BoxCollider>());

            string failure = SkydiveCourseBuilder.FindDoorPanelMismatch(volume);

            Assert.IsNotNull(failure);
            StringAssert.Contains("콜라이더가 없다", failure);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void 콜라이더가_트리거면_걸린다()
    {
        //  트리거는 통과시킨다 — 모양은 그대로인데 벽이 아니게 되어 문이 사람을 안 민다.
        GameObject go = Bake(0f);
        try
        {
            var volume = go.GetComponent<LOP.DoorVolume>();
            volume.PanelB.GetComponent<BoxCollider>().isTrigger = true;

            string failure = SkydiveCourseBuilder.FindDoorPanelMismatch(volume);

            Assert.IsNotNull(failure);
            StringAssert.Contains("트리거", failure);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void 두_패널이_같은_오브젝트면_걸린다()
    {
        //  PanelB를 PanelA로 복사해 두는 실수. 크기·회전 검사는 둘 다 통과시키지만, 실제로는
        //  한쪽만 움직여 구멍 절반이 영영 열린 채로 남는다 — 문이 문이 아니게 된다.
        GameObject go = Bake(0f);
        try
        {
            var volume = go.GetComponent<LOP.DoorVolume>();
            volume.PanelB = volume.PanelA;

            string failure = SkydiveCourseBuilder.FindDoorPanelMismatch(volume);

            Assert.IsNotNull(failure);
            StringAssert.Contains("같은 오브젝트", failure);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
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
    public void 한_축이_겹치는_구멍_둘은_일곱_조각이_된다()
    {
        //  실제 표의 2600 선반 모양: x가 둘 다 0이라 두 구멍이 같은 세로줄에 선다. 앞선 두-구멍
        //  테스트의 픽스처는 x·z가 둘 다 서로소라 "구멍끼리 축이 겹치는" 갈래를 안 지났는데,
        //  실제 표의 2600/1800/600/200이 전부 이 모양이다.
        //  첫 구멍이 판을 N/S/E/W 넷으로 가르고, 둘째 구멍은 그중 N 조각 하나만 다시 넷으로
        //  가르므로 3 + 4 = 7이다.
        var holes = new[] { Hole(0f, 0f, 30f), Hole(0f, 60f, 20f) };

        List<SkydiveCourseBuilder.Plate> plates = SkydiveCourseBuilder.Carve(Slab(), holes);

        Assert.AreEqual(7, plates.Count);

        float sum = 0f;
        foreach (SkydiveCourseBuilder.Plate p in plates)
        {
            sum += p.Area;
        }
        Assert.AreEqual(38700f, sum, 0.01f, "200×200 판에서 30×30과 20×20을 뺀 값");

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
