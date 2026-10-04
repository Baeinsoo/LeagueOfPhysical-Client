using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace LOP.MapTools.Tests
{
    public class GuardLayoutTests
    {
        //  칸: x 100~130, y 0~5 (높이 5).
        private static Branch Make(BranchKind kind, float x0 = 100f, float y0 = 0f, float y1 = 5f)
            => new Branch(ShortcutRect.FromCenterSize((x0 + x0 + 30f) * 0.5f, (y0 + y1) * 0.5f, 30f, y1 - y0),
                          kind == BranchKind.Hill ? BranchSide.Above : BranchSide.Below, kind);

        [Test]
        public void 언덕_진자는_입구_안쪽_천장에_매달고_막대는_칸_높이보다_2m_짧다()
        {
            Assert.IsTrue(GuardLayout.TryPendulum(Make(BranchKind.Hill), out var g));
            Assert.AreEqual(101.5f, g.PivotX, 1e-4f);
            Assert.AreEqual(5f, g.PivotY, 1e-4f);
            //  5 − 2.0 = 3.0 — 아래로 빠져나갈 틈을 남긴다.
            Assert.AreEqual(3.0f, g.Length, 1e-4f);
        }

        [Test]
        public void 칸이_너무_낮으면_진자를_안_놓는다()
            => Assert.IsFalse(GuardLayout.TryPendulum(Make(BranchKind.Hill, y1: 4f), out _));

        [Test]
        public void 칸이_높아도_막대는_6m까지다()
        {
            Assert.IsTrue(GuardLayout.TryPendulum(Make(BranchKind.Hill, y1: 12f), out var g));
            Assert.AreEqual(GuardLayout.MaxRod, g.Length, 1e-4f);
        }

        [Test]
        public void 광고판은_위층_입구_안쪽에서_돌고_바닥_천장과_0_3_띈다()
        {
            var g = GuardLayout.Billboard(Make(BranchKind.Building));
            //  reach = 5/2 − 0.3 = 2.2, 두께 반 0.3 — 판 길이는 모서리가 reach 안에 들게.
            Assert.AreEqual(2f * Mathf.Sqrt(2.2f * 2.2f - 0.09f), g.Length, 1e-4f);
            Assert.AreEqual(2.2f, g.Sector.Radius, 1e-4f);
            Assert.AreEqual(102.5f, g.PivotX, 1e-4f);
            Assert.AreEqual(2.5f, g.PivotY, 1e-4f);
            Assert.GreaterOrEqual(g.Sector.HalfAngleDegrees, 180f);
            Assert.AreEqual(0.3f, g.PivotY - g.Sector.Radius, 1e-4f);
            Assert.AreEqual(4.7f, g.PivotY + g.Sector.Radius, 1e-4f);
            Assert.AreEqual(100.3f, g.PivotX - g.Sector.Radius, 1e-4f, "입구 벽(X0) 안쪽 0.3부터 돈다");
        }

        [Test]
        public void 코스_배치는_빌딩_광고판_언덕_진자뿐이고_계곡엔_없다()
        {
            var spots = GuardLayout.ForCourse(new List<Branch>
            {
                Make(BranchKind.Building, x0: 34f, y1: 5f),
                Make(BranchKind.Valley, x0: 291f),
                Make(BranchKind.Hill, x0: 626f),
                Make(BranchKind.Valley, x0: 678f),
            });
            Assert.AreEqual(2, spots.Count);
            Assert.AreEqual(GuardKind.Billboard, spots[0].Kind); Assert.AreEqual(34f, spots[0].BranchX0, 1e-4f);
            Assert.AreEqual(GuardKind.Pendulum, spots[1].Kind); Assert.AreEqual(626f, spots[1].BranchX0, 1e-4f);
            Assert.AreEqual("Guard_626_Pendulum", spots[1].MarkerName);
        }

        [Test]
        public void 진자_부채꼴은_끝_철골까지_덮는다()
        {
            GuardLayout.TryPendulum(Make(BranchKind.Hill), out var g);
            Assert.AreEqual(-90f, g.Sector.AxisDegrees, 1e-4f);
            //  막대 길이 3.0, 끝 철골 반높이 0.4·반폭 0.8 — 안쪽 모서리는 축에서 (rod − 반높이)만큼 떨어져 있다.
            float extraDeg = Mathf.Atan2(0.8f, 3.0f - 0.4f) * Mathf.Rad2Deg;
            Assert.AreEqual(55f + extraDeg, g.Sector.HalfAngleDegrees, 1e-3f);
            Assert.AreEqual(Mathf.Sqrt(3.4f * 3.4f + 0.8f * 0.8f), g.Sector.Radius, 1e-4f);
            Assert.Less(g.BandX0, g.PivotX); Assert.Greater(g.BandX1, g.PivotX);
        }
    }
}
