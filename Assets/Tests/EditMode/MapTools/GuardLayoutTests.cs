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
        public void 셔터는_칸_높이에_0_3을_더한_만큼_올라간다()
        {
            var g = GuardLayout.Shutter(Make(BranchKind.Building, y0: 2f, y1: 7f), 101f);
            Assert.AreEqual(GuardKind.Shutter, g.Kind);
            Assert.AreEqual(5.3f, g.Travel, 1e-4f);
            Assert.AreEqual(2f, g.Y0, 1e-4f);
            Assert.AreEqual(7f, g.Y1, 1e-4f);
        }

        [Test]
        public void 닫힌_문은_바닥_0_05_아래부터_천장_0_45_위까지다()
        {
            var g = GuardLayout.Shutter(Make(BranchKind.Building, y0: 2f, y1: 7f), 101f);
            Assert.AreEqual(1.95f, g.DoorBottom, 1e-4f);
            Assert.AreEqual(7.45f, g.DoorTop, 1e-4f);
            //  다 열리면 문 바닥이 천장보다 위 — 칸이 통째로 열린다.
            Assert.Greater(g.DoorBottom + g.Travel, g.Y1);
        }

        [Test]
        public void 띠와_쓸린_사각형은_문_폭과_올라간_자리까지다()
        {
            var g = GuardLayout.Shutter(Make(BranchKind.Hill, y0: 2f, y1: 7f), 101f);
            Assert.AreEqual(101f, g.DoorX, 1e-4f);
            Assert.AreEqual(101f, g.PivotX, 1e-4f);
            Assert.AreEqual(100.6f, g.BandX0, 1e-4f);
            Assert.AreEqual(101.4f, g.BandX1, 1e-4f);
            Assert.AreEqual(100.6f, g.SweepRect.X0, 1e-4f);
            Assert.AreEqual(101.4f, g.SweepRect.X1, 1e-4f);
            Assert.AreEqual(1.95f, g.SweepRect.Y0, 1e-4f);
            Assert.AreEqual(7.45f + 5.3f, g.SweepRect.Y1, 1e-4f);
        }

        [Test]
        public void 코스_배치는_빌딩_언덕마다_셔터_하나_계곡엔_없다()
        {
            var spots = GuardLayout.ForCourse(new List<Branch>
            {
                Make(BranchKind.Hill, x0: 626f),
                Make(BranchKind.Valley, x0: 291f),
                Make(BranchKind.Building, x0: 34f),
                Make(BranchKind.Valley, x0: 678f),
            });
            Assert.AreEqual(2, spots.Count);
            Assert.AreEqual(34f, spots[0].BranchX0, 1e-4f, "x0 순");
            Assert.AreEqual(626f, spots[1].BranchX0, 1e-4f);
            foreach (GuardSpot s in spots) { Assert.AreEqual(GuardKind.Shutter, s.Kind); }
            //  기본 자리: 입구 안쪽 0.3 + 반폭 0.4.
            Assert.AreEqual(34.7f, spots[0].DoorX, 1e-4f);
            Assert.AreEqual("Guard_626_Shutter", spots[1].MarkerName);
            Assert.AreEqual("셔터", spots[1].Label);
        }

        [Test]
        public void 문_자리만_옮기면_나머지는_그대로다()
        {
            var g = GuardLayout.Shutter(Make(BranchKind.Hill, y0: 2f, y1: 7f), 101f);
            var moved = g.AtDoorX(103.5f);
            Assert.AreEqual(103.5f, moved.DoorX, 1e-4f);
            Assert.AreEqual(103.1f, moved.BandX0, 1e-4f);
            Assert.AreEqual(g.Travel, moved.Travel, 1e-6f);
            Assert.AreEqual(g.Y0, moved.Y0, 1e-6f);
            Assert.AreEqual(g.Y1, moved.Y1, 1e-6f);
            Assert.AreEqual(g.MarkerName, moved.MarkerName, "이름은 갈림길 x0로 짓는다 — 검사기가 이 이름으로 찾는다");
        }

        [Test]
        public void 빈_목록이면_문지기도_없다()
        {
            Assert.AreEqual(0, GuardLayout.ForCourse(null).Count);
            Assert.AreEqual(0, GuardLayout.ForCourse(new List<Branch>()).Count);
        }
    }
}
