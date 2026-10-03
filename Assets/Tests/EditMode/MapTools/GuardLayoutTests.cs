using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class GuardLayoutTests
    {
        //  칸: x 100~130, y 0~5 (높이 5).
        private static Branch Make(BranchKind kind, float x0 = 100f, float y0 = 0f, float y1 = 5f)
            => new Branch(ShortcutRect.FromCenterSize((x0 + x0 + 30f) * 0.5f, (y0 + y1) * 0.5f, 30f, y1 - y0),
                          kind == BranchKind.Hill ? BranchSide.Above : BranchSide.Below, kind);

        [Test]
        public void 진자는_입구_안쪽_천장에_매달고_맨_아래가_바닥_위_0_6이다()
        {
            Assert.IsTrue(GuardLayout.TryPendulum(Make(BranchKind.Valley), out var g));
            Assert.AreEqual(101.5f, g.PivotX, 1e-4f);
            Assert.AreEqual(5f, g.PivotY, 1e-4f);
            //  5 − 0.6 − 0.4(끝 철골 반 높이) = 4.0
            Assert.AreEqual(4.0f, g.Length, 1e-4f);
            Assert.AreEqual(0.6f, g.PivotY - g.Length - GuardLayout.TipHeight * 0.5f, 1e-4f);
        }

        [Test]
        public void 칸이_너무_낮으면_진자를_안_놓는다()
            => Assert.IsFalse(GuardLayout.TryPendulum(Make(BranchKind.Valley, y1: 3f), out _));

        [Test]
        public void 칸이_높아도_막대는_6m까지다()
        {
            Assert.IsTrue(GuardLayout.TryPendulum(Make(BranchKind.Hill, y1: 12f), out var g));
            Assert.AreEqual(GuardLayout.MaxRod, g.Length, 1e-4f);
        }

        [Test]
        public void 광고판은_입구_앞에_서고_아래_끝이_바닥_위_0_3이다()
        {
            var g = GuardLayout.Billboard(Make(BranchKind.Building));
            Assert.AreEqual(100f - 0.3f - 3.5f, g.PivotX, 1e-4f);
            Assert.AreEqual(0.3f + 3.5f, g.PivotY, 1e-4f);
            Assert.GreaterOrEqual(g.Sector.HalfAngleDegrees, 180f);
            Assert.LessOrEqual(g.PivotX + g.Sector.Radius, 100f, "판이 돌아도 건물 안으로 들어가지 않는다");
        }

        [Test]
        public void 코스_배치는_빌딩_광고판_언덕_진자_첫_계곡만_진자다()
        {
            var spots = GuardLayout.ForCourse(new List<Branch>
            {
                Make(BranchKind.Building, x0: 34f, y1: 5f),
                Make(BranchKind.Valley, x0: 291f),
                Make(BranchKind.Hill, x0: 626f),
                Make(BranchKind.Valley, x0: 678f),
            });
            Assert.AreEqual(3, spots.Count);
            Assert.AreEqual(GuardKind.Billboard, spots[0].Kind);
            Assert.AreEqual(GuardKind.Pendulum, spots[1].Kind); Assert.AreEqual(291f, spots[1].BranchX0, 1e-4f);
            Assert.AreEqual(GuardKind.Pendulum, spots[2].Kind); Assert.AreEqual(626f, spots[2].BranchX0, 1e-4f);
            Assert.AreEqual("Guard_291_Pendulum", spots[1].MarkerName);
        }

        [Test]
        public void 진자_부채꼴은_끝_철골까지_덮는다()
        {
            GuardLayout.TryPendulum(Make(BranchKind.Valley), out var g);
            Assert.AreEqual(-90f, g.Sector.AxisDegrees, 1e-4f);
            Assert.Greater(g.Sector.HalfAngleDegrees, GuardLayout.PendulumAmplitude);
            Assert.GreaterOrEqual(g.Sector.Radius, g.Length + GuardLayout.TipHeight * 0.5f);
            Assert.Less(g.BandX0, g.PivotX); Assert.Greater(g.BandX1, g.PivotX);
        }
    }
}
