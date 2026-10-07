using System.Collections.Generic;
using LOP.EditorTools;
using LOP.MapTools;
using NUnit.Framework;
using UnityEngine;

//  광산 굽기의 다각형 셈 — 씬 없이. 다각형은 전통 굽기의 바닥·천장 조각과 같은 반시계 볼록이어야
//  PrismMesh가 앞면을 카메라 쪽으로 낸다(감기가 뒤집히면 면이 안 보이고 볼록 콜라이더도 엉뚱해진다).
public class FlappyMineGeometryTests
{
    const float Eps = 1e-4f;

    //  MineCourseRuleTests와 같은 물리 — 숫자는 배치가 실제로 나오기만 하면 된다.
    static readonly MinePhysics P = new MinePhysics(4.5f, 10.125f, 33.75f, 11.25f, 0.02f);

    [Test]
    public void 바닥_조각은_윗변이_바닥선이고_두께만큼_내려간_반시계_볼록_사각형()
    {
        Vector2[] q = FlappyMineGeometry.FloorQuad(2f, -10f, 5f, -8f, 20f);

        Assert.That(q, Is.Not.Null);
        Assert.That(q.Length, Is.EqualTo(4));
        AssertConvexCcw(q);
        Assert.That(TopAt(q, 2f), Is.EqualTo(-10f).Within(Eps));
        Assert.That(TopAt(q, 5f), Is.EqualTo(-8f).Within(Eps));
        Assert.That(BottomAt(q, 2f), Is.EqualTo(-30f).Within(Eps));
        Assert.That(BottomAt(q, 5f), Is.EqualTo(-28f).Within(Eps));
    }

    [Test]
    public void 천장_조각은_아랫변이_천장선이고_두께만큼_올라간_반시계_볼록_사각형()
    {
        Vector2[] q = FlappyMineGeometry.CeilingQuad(2f, 10f, 5f, 7f, 20f);

        Assert.That(q, Is.Not.Null);
        AssertConvexCcw(q);
        Assert.That(BottomAt(q, 2f), Is.EqualTo(10f).Within(Eps));
        Assert.That(BottomAt(q, 5f), Is.EqualTo(7f).Within(Eps));
        Assert.That(TopAt(q, 2f), Is.EqualTo(30f).Within(Eps));
        Assert.That(TopAt(q, 5f), Is.EqualTo(27f).Within(Eps));
    }

    [Test]
    public void 폭이_없는_바닥_천장_조각은_null()
    {
        Assert.That(FlappyMineGeometry.FloorQuad(3f, 0f, 3f, 0f, 20f), Is.Null);
        Assert.That(FlappyMineGeometry.CeilingQuad(3f, 0f, 3f, 0f, 20f), Is.Null);
    }

    [Test]
    public void 굴_지붕_조각의_아랫변은_중심선_더하기_틈_절반에_정확히_놓인다()
    {
        var tube = new MineTube { X0 = 0f, X1 = 10f, Gap = 3f, Center = x => 0.5f * x };
        Vector2[] q = FlappyMineGeometry.TubeRoofStrip(tube, 2f, 2.25f, x => 10f);

        Assert.That(q, Is.Not.Null);
        AssertConvexCcw(q);
        Assert.That(BottomAt(q, 2f), Is.EqualTo(1f + 1.5f).Within(Eps));
        Assert.That(BottomAt(q, 2.25f), Is.EqualTo(1.125f + 1.5f).Within(Eps));
        //  High가 NaN이면 통로 천장선까지 막는다.
        Assert.That(TopAt(q, 2f), Is.EqualTo(10f).Within(Eps));
        Assert.That(TopAt(q, 2.25f), Is.EqualTo(10f).Within(Eps));
    }

    [Test]
    public void 굴_바닥_조각의_윗변은_중심선_빼기_틈_절반에_정확히_놓인다()
    {
        var tube = new MineTube { X0 = 0f, X1 = 10f, Gap = 3f, Center = x => 0.5f * x };
        Vector2[] q = FlappyMineGeometry.TubeFloorStrip(tube, 2f, 2.25f, x => -10f);

        Assert.That(q, Is.Not.Null);
        AssertConvexCcw(q);
        Assert.That(TopAt(q, 2f), Is.EqualTo(1f - 1.5f).Within(Eps));
        Assert.That(TopAt(q, 2.25f), Is.EqualTo(1.125f - 1.5f).Within(Eps));
        Assert.That(BottomAt(q, 2f), Is.EqualTo(-10f).Within(Eps));
    }

    //  갈림길 굴은 칸막이·천장 사이로 좁혀진다 — Low/High가 있으면 통로 선 대신 그 높이까지만.
    [Test]
    public void 굴_조각은_Low_High가_있으면_통로_선_대신_그_높이까지만_막는다()
    {
        var tube = new MineTube { X0 = 0f, X1 = 10f, Gap = 2f, Low = 0.6f, High = 12f, Center = x => 5f };

        Vector2[] roof = FlappyMineGeometry.TubeRoofStrip(tube, 1f, 1.25f, x => 99f);
        Vector2[] floor = FlappyMineGeometry.TubeFloorStrip(tube, 1f, 1.25f, x => -99f);

        Assert.That(TopAt(roof, 1f), Is.EqualTo(12f).Within(Eps));
        Assert.That(BottomAt(floor, 1.25f), Is.EqualTo(0.6f).Within(Eps));
    }

    [Test]
    public void 높이가_0인_굴_조각은_null()
    {
        //  굴 위 가장자리가 천장선에 딱 붙었다(중심 8.5 + 틈 절반 1.5 = 10) — 지붕 조각이 납작하다.
        var tube = new MineTube { X0 = 0f, X1 = 10f, Gap = 3f, Center = x => 8.5f };
        Assert.That(FlappyMineGeometry.TubeRoofStrip(tube, 0f, 0.25f, x => 10f), Is.Null);
        //  천장선을 넘어간 것도 막을 것이 없다.
        Assert.That(FlappyMineGeometry.TubeRoofStrip(tube, 0f, 0.25f, x => 9f), Is.Null);

        var low = new MineTube { X0 = 0f, X1 = 10f, Gap = 3f, Center = x => -8.5f };
        Assert.That(FlappyMineGeometry.TubeFloorStrip(low, 0f, 0.25f, x => -10f), Is.Null);
    }

    //  실제 광산 코스로 굽는 굴 조각 전부 — 하나라도 감기가 뒤집히거나 오목하면 콜라이더가 엉뚱해진다.
    [Test]
    public void 광산_코스의_굴_조각은_전부_반시계_볼록이고_아랫변_윗변이_틈선에_놓인다()
    {
        MineCourse course = MineCourseRule.Layout(P);
        System.Func<float, float> ceiling = x => course.CenterAt(x) + course.HalfAt(x);
        System.Func<float, float> floor = x => course.CenterAt(x) - course.HalfAt(x);

        int strips = 0;
        foreach (MineTube t in course.Tubes)
        {
            foreach ((float x0, float x1) in FlappyMineGeometry.Slices(t.X0, t.X1, 0.25f))
            {
                Vector2[] roof = FlappyMineGeometry.TubeRoofStrip(t, x0, x1, ceiling);
                if (roof != null)
                {
                    AssertConvexCcw(roof);
                    Assert.That(BottomAt(roof, x0), Is.EqualTo(t.Center(x0) + t.Gap * 0.5f).Within(Eps));
                    strips++;
                }
                Vector2[] bottom = FlappyMineGeometry.TubeFloorStrip(t, x0, x1, floor);
                if (bottom != null)
                {
                    AssertConvexCcw(bottom);
                    Assert.That(TopAt(bottom, x1), Is.EqualTo(t.Center(x1) - t.Gap * 0.5f).Within(Eps));
                    strips++;
                }
            }
        }
        Assert.That(strips, Is.GreaterThan(100), "조각이 거의 없으면 이 검사는 아무것도 지키지 않는다");
    }

    [Test]
    public void 조각_나누기는_끝을_정확히_덮고_마지막_조각만_짧다()
    {
        var slices = new List<(float, float)>(FlappyMineGeometry.Slices(1f, 2.1f, 0.25f));

        Assert.That(slices.Count, Is.EqualTo(5));
        Assert.That(slices[0].Item1, Is.EqualTo(1f).Within(Eps));
        Assert.That(slices[4].Item2, Is.EqualTo(2.1f).Within(Eps));
        for (int i = 1; i < slices.Count; i++) { Assert.That(slices[i].Item1, Is.EqualTo(slices[i - 1].Item2)); }
    }

    //  같은 x에 꼭짓점이 둘(세로 변)인 사각형·삼각형에서 x의 윗/아래 높이를 읽는다.
    static float TopAt(Vector2[] q, float x)
    {
        float best = float.NegativeInfinity;
        foreach (Vector2 v in q) { if (Mathf.Abs(v.x - x) < Eps) { best = Mathf.Max(best, v.y); } }
        Assert.That(best, Is.Not.EqualTo(float.NegativeInfinity), $"x={x}에 꼭짓점이 없다");
        return best;
    }

    static float BottomAt(Vector2[] q, float x)
    {
        float best = float.PositiveInfinity;
        foreach (Vector2 v in q) { if (Mathf.Abs(v.x - x) < Eps) { best = Mathf.Min(best, v.y); } }
        Assert.That(best, Is.Not.EqualTo(float.PositiveInfinity), $"x={x}에 꼭짓점이 없다");
        return best;
    }

    //  모든 연속 세 꼭짓점의 외적이 양(반시계로 꺾임) — 볼록이면서 감기가 한 방향이다.
    static void AssertConvexCcw(Vector2[] q)
    {
        Assert.That(q.Length, Is.GreaterThanOrEqualTo(3));
        for (int i = 0; i < q.Length; i++)
        {
            Vector2 a = q[i], b = q[(i + 1) % q.Length], c = q[(i + 2) % q.Length];
            float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
            Assert.That(cross, Is.GreaterThan(0f), $"꼭짓점 {i + 1}에서 반시계 볼록이 아니다: {string.Join(" ", q)}");
        }
    }
}
