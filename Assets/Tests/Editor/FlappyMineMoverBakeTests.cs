using System.Linq;
using LOP.EditorTools;
using LOP.MapTools;
using NUnit.Framework;
using UnityEngine;

//  광산 굽기의 움직이는 장애물 — 씬 없이 빈 루트에 구워 마커 수·값·자세를 본다.
public class FlappyMineMoverBakeTests
{
    static readonly MinePhysics P = new MinePhysics(4.5f, 10.125f, 33.75f, 11.25f, 0.02f);
    GameObject root;
    MineCourse course;

    [SetUp]
    public void SetUp()
    {
        course = MineCourseRule.Layout(P);
        root = new GameObject("ComposedMapTest");
        FlappyMineMoverBake.Build(root.transform, course, null,
            x => course.CenterAt(x) - course.HalfAt(x), x => course.CenterAt(x) + course.HalfAt(x));
        Physics.SyncTransforms();
    }

    [TearDown]
    public void TearDown() { if (root != null) { Object.DestroyImmediate(root); } }

    [Test]
    public void 마커_수가_코스와_같다()
    {
        Assert.AreEqual(course.Lifts.Count, root.GetComponentsInChildren<LOP.FlappyLift>().Length);       // 10
        Assert.AreEqual(course.Pendulums.Count, root.GetComponentsInChildren<LOP.FlappyPendulum>().Length); // 12
        Assert.AreEqual(course.Rockfalls.Count, root.GetComponentsInChildren<LOP.FlappyRockfall>().Length); // 4
        Assert.AreEqual(course.Props.Count, root.transform.Cast<Transform>().Count(t => t.name.StartsWith("CollapsedProp_")));
    }

    [Test]
    public void 승강기_마커_값과_쉬는_틈()
    {
        MineLift l = course.Lifts[0];
        var lift = root.GetComponentsInChildren<LOP.FlappyLift>().OrderBy(q => q.transform.position.x).First();
        Assert.AreEqual(l.Amplitude, lift.Amplitude, 1e-5f);
        Assert.AreEqual(l.Period, lift.Period, 1e-5f);
        Assert.AreEqual(l.Phase, lift.Phase, 1e-5f);
        Assert.IsNotNull(lift.Cage);
        Bounds low = lift.Cage.Find($"LiftPipeLow_{l.Gate.X:F1}").GetComponent<Collider>().bounds;
        Bounds high = lift.Cage.Find($"LiftPipeHigh_{l.Gate.X:F1}").GetComponent<Collider>().bounds;
        Assert.AreEqual(l.Gate.GapCenter - l.Gate.Gap / 2f, low.max.y, 1e-3f, "아래 파이프 윗끝 = 틈 아래");
        Assert.AreEqual(l.Gate.GapCenter + l.Gate.Gap / 2f, high.min.y, 1e-3f, "위 파이프 아랫끝 = 틈 위");
        Assert.AreEqual(0f, low.center.z, 1e-3f, "판정은 게임 평면 z 0");
    }

    [Test]
    public void 승강기를_세우면_틈이_진폭만큼_움직인다()
    {
        MineLift l = course.Lifts[0];
        var lift = root.GetComponentsInChildren<LOP.FlappyLift>().OrderBy(q => q.transform.position.x).First();
        var field = new LOP.FlappyLiftField();
        field.Add(lift);
        field.PoseForTick((long)Mathf.Round(l.Period / 4f / 0.02f), 0.02f);   // 위상 0 → 사분 주기(38틱)에 거의 꼭대기
        Bounds low = lift.Cage.Find($"LiftPipeLow_{l.Gate.X:F1}").GetComponent<Collider>().bounds;
        Assert.AreEqual(l.Gate.GapCenter - l.Gate.Gap / 2f + l.Amplitude, low.max.y, 1e-2f);
    }

    [Test]
    public void 승강기_파이프는_진폭만큼_움직여도_끝이_통로에_안_드러난다()
    {
        foreach (MineLift l in course.Lifts)
        {
            var lift = root.GetComponentsInChildren<LOP.FlappyLift>().First(q => q.name == $"Lift_{l.Gate.X:F1}");
            Bounds low = lift.Cage.Find($"LiftPipeLow_{l.Gate.X:F1}").GetComponent<Collider>().bounds;
            Bounds high = lift.Cage.Find($"LiftPipeHigh_{l.Gate.X:F1}").GetComponent<Collider>().bounds;
            float half = l.Gate.Width / 2f;
            foreach (float x in new[] { l.Gate.X - half, l.Gate.X, l.Gate.X + half })
            {
                //  케이지가 진폭만큼 올라가도 아래 파이프 끝은 바닥선 아래, 내려가도 위 파이프 끝은 천장선 위.
                Assert.Less(low.min.y + l.Amplitude, course.CenterAt(x) - course.HalfAt(x), $"Lift_{l.Gate.X:F1} 아래 끝");
                Assert.Greater(high.max.y - l.Amplitude, course.CenterAt(x) + course.HalfAt(x), $"Lift_{l.Gate.X:F1} 위 끝");
            }
        }
    }

    [Test]
    public void 광차는_축에서_줄_길이만큼_아래()
    {
        MinePendulum p = course.Pendulums[0];
        var marker = root.GetComponentsInChildren<LOP.FlappyPendulum>().First(q => q.name == $"Cart_{p.PivotX:F1}_0");
        Assert.AreEqual(p.PivotY, marker.transform.position.y, 1e-3f);
        Assert.AreEqual(p.Amplitude, marker.Amplitude, 1e-4f);
        Assert.AreEqual(p.Period, marker.Period, 1e-4f);
        Assert.AreEqual(p.Phase, marker.Phase, 1e-4f);
        Assert.IsNotNull(marker.Upright, "광차 상자는 Upright 자식 아래 — 진자가 돌아도 수평");
        Assert.AreEqual(p.PivotY - p.Length, marker.Upright.position.y, 1e-3f, "Upright = 줄 끝");
        Bounds box = marker.Upright.Find("CartBox").GetComponent<Collider>().bounds;
        Assert.AreEqual(p.PivotY - p.Length, box.center.y, 1e-3f);
        Assert.AreEqual(p.CartWidth, box.size.x, 1e-3f);
        Assert.AreEqual(p.CartHeight, box.size.y, 1e-3f);
    }

    [Test]
    public void 진자가_기울어도_광차는_수평으로_원_위에_있다()
    {
        MinePendulum p = course.Pendulums[0];
        var marker = root.GetComponentsInChildren<LOP.FlappyPendulum>().First(q => q.name == $"Cart_{p.PivotX:F1}_0");
        var field = new LOP.FlappyPendulumField();
        field.Add(marker);
        //  각도가 0이 아닌 틱을 고른다(위상에 따라 0틱이 수직일 수 있다).
        long tick = Enumerable.Range(0, 400).Select(t => (long)t)
            .First(t => Mathf.Abs(LOP.FlappyPendulumCurve.AngleAt(p.Amplitude, p.Period, p.Phase, t, 0.02f)) > 10f);
        field.PoseForTick(tick, 0.02f);
        float angle = LOP.FlappyPendulumCurve.AngleAt(p.Amplitude, p.Period, p.Phase, tick, 0.02f) * Mathf.Deg2Rad;

        Bounds box = marker.Upright.Find("CartBox").GetComponent<Collider>().bounds;
        //  축 기준 (0, -L)을 angle만큼 돌린 자리.
        Assert.AreEqual(p.PivotX + p.Length * Mathf.Sin(angle), box.center.x, 1e-3f);
        Assert.AreEqual(p.PivotY - p.Length * Mathf.Cos(angle), box.center.y, 1e-3f);
        //  기울었다면 축 정렬 경계가 커진다 — 크기가 그대로면 수평이다.
        Assert.AreEqual(p.CartWidth, box.size.x, 1e-3f);
        Assert.AreEqual(p.CartHeight, box.size.y, 1e-3f);
    }

    [Test]
    public void 낙석은_천장에_서고_바위는_숨은_자리에_굽는다()
    {
        MineRockfall r = course.Rockfalls[0];
        var marker = root.GetComponentsInChildren<LOP.FlappyRockfall>().OrderBy(q => q.transform.position.x).First();
        Assert.AreEqual(r.X, marker.transform.position.x, 1e-3f);
        Assert.AreEqual(r.Ceiling, marker.transform.position.y, 1e-3f);
        Assert.AreEqual(r.Drop, marker.Drop, 1e-4f);
        Assert.AreEqual(r.Speed, marker.Speed, 1e-4f);
        Assert.AreEqual(r.Period, marker.Period, 1e-4f);
        Assert.AreEqual(r.Phase, marker.Phase, 1e-4f);
        Assert.AreEqual(marker.HiddenRise, marker.Rock.localPosition.y, 1e-4f);
        Bounds box = marker.Rock.Find("RockBox").GetComponent<Collider>().bounds;
        Assert.AreEqual(r.Size, box.size.x, 1e-3f);
        Assert.Greater(box.min.y, r.Ceiling, "숨은 바위는 통로 밖");
    }
}
