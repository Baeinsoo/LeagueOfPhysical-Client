using LOP.EditorTools;
using NUnit.Framework;

/// <summary>
/// 선반마다 구멍이 둘(빠른 구멍·안전한 구멍)이 되면서 굽기 전 검사도 하나에서 셋으로 늘었다
/// (2026-09-06-skydive-doors-and-branching-design §3.2). 통과 케이스는 실제 <c>Shelves</c> 표를
/// 그대로 재고, 거절 케이스는 각 검사의 인젝터블 오버로드(<c>ReachableChain</c>/
/// <c>FindRouteNotSplit</c>/<c>FindInvalidRespawn</c>)에 손으로 지은 작은 코스를 넣어 확인한다 —
/// 실제 표를 바꾸지 않고도 "이 검사가 이걸 잡아내는가"를 직접 겨눌 수 있다.
///
/// 각 거절 테스트는 절제(ablation)로도 확인했다 — 해당 검사를 잠깐 꺼서 이 테스트가 거꾸로
/// 통과하는지 손으로 확인한 뒤 되돌렸다. 과정과 결과는 task-6-report.md에 남긴다. (레이저
/// 슬라이스에서 굽기 검사기가 격자를 엉뚱한 높이에서 뽑아 아무것도 못 보고 있었는데 통과·거절
/// 테스트가 둘 다 초록이었던 사고의 재발 방지.)
/// </summary>
public class SkydiveDoorBuildTests
{
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
        string failure = SkydiveCourseBuilder.FindRouteNotSplit();

        Assert.IsNull(failure, failure);
    }

    [Test]
    public void 표의_부활_지점은_어느_구멍과도_안_겹친다()
    {
        // 구멍이 하나였을 때부터 있던 검사이지만, 구멍이 둘이 된 지금은 "어느 쪽과도"가
        // 핵심이다 — 리뷰가 지적한 위험(Ruling 10): 부활 지점이 문 판정 범위 안이면
        // 세우자마자 다시 죽는다.
        string failure = SkydiveCourseBuilder.FindInvalidRespawn();

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
            //  전체 경로: 위 빠른 구멍(50,0)에서 70m라 76.7m 대자로 닿는다 → ①은 통과.
            //  안전 경로: 위 안전한 구멍(0,0)에서 120m라 대자로도 못 닿는다 → ②는 거절.
            Shelf(2200f, Hole(120f, 0f, 10f, hasDoor: false)),
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

        string failure = SkydiveCourseBuilder.FindRouteNotSplit(shelves);

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

        string failure = SkydiveCourseBuilder.FindRouteNotSplit(shelves);

        Assert.IsNotNull(failure);
        StringAssert.Contains("문이 무의미해진다", failure);
    }

    // ── 거절 ④ — 부활 지점이 구멍과 겹치면 안 된다 (리뷰 Ruling 10) ──────────

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
}
