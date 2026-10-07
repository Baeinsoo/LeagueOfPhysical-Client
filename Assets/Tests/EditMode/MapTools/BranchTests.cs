using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.MapTools.Tests
{
    public class BranchTests
    {
        const float Half = 10.92f;
        static readonly FlapArc Arc = new FlapArc(18.6f, 59f, 6.8f, 0.02f);
        static CourseProfile Compose()
            => CourseProfileRule.Compose(0f, 748f, 11.4f, Half, 20260919UL, 11.4f * 4f, 11.4f * 8f, Arc);

        [Test]
        public void 코스의_갈림길은_계곡_둘_빌딩_위층_하나_언덕_굴_하나다()
        {
            var p = Compose();
            List<Branch> all = CourseProfileRule.Branches(p, Half);
            Assert.AreEqual(4, all.Count);
            Assert.AreEqual(2, all.FindAll(b => b.Kind == BranchKind.Valley && b.Other == BranchSide.Below).Count);
            Branch building = all.Find(b => b.Kind == BranchKind.Building);
            Box2 upper = BuildingLayout.UpperLaneBox(p.Buildings[0], Half);
            Assert.AreEqual(BranchSide.Below, building.Other);
            Assert.AreEqual(upper.X0, building.Rect.X0, 1e-3f);
            Assert.AreEqual(upper.Y0, building.Rect.Y0, 1e-3f);
            Assert.AreEqual(upper.Y1, building.Rect.Y1, 1e-3f);
            Branch hill = all.Find(b => b.Kind == BranchKind.Hill);
            HillTunnelPiece t = p.HillTunnels[0];
            Assert.AreEqual(BranchSide.Above, hill.Other);
            Assert.AreEqual(t.Mouth, hill.Rect.X0, 1e-3f);
            Assert.AreEqual(t.Exit, hill.Rect.X1, 1e-3f);
            Assert.AreEqual(t.FloorY, hill.Rect.Y0, 1e-3f);
            Assert.AreEqual(t.TopY, hill.Rect.Y1, 1e-3f);
            for (int i = 1; i < all.Count; i++) { Assert.Less(all[i - 1].Rect.X0, all[i].Rect.X0, "x 순"); }
        }

        [Test]
        public void 표시_이름으로_갈림길을_되살린다()
        {
            var b = new Branch(ShortcutRect.FromCenterSize(120f, 3f, 40f, 5f), BranchSide.Below, BranchKind.Building);
            Assert.AreEqual("Branch_100_Below_Building", b.MarkerName);
            Assert.IsTrue(Branch.TryParse(b.MarkerName, 120f, 3f, 40f, 5f, out Branch back));
            Assert.AreEqual(BranchSide.Below, back.Other);
            Assert.AreEqual(BranchKind.Building, back.Kind);
            Assert.AreEqual(100f, back.Rect.X0, 1e-4f);
            Assert.IsFalse(Branch.TryParse("Shortcut_100", 0f, 0f, 1f, 1f, out _));
            Assert.IsFalse(Branch.TryParse("Branch_100_Sideways_Building", 0f, 0f, 1f, 1f, out _));
        }

        [Test]
        public void 이름표는_사람이_읽는_말이다()
        {
            Assert.AreEqual("계곡 지름길", new Branch(default, BranchSide.Below, BranchKind.Valley).Label);
            Assert.AreEqual("빌딩 위층", new Branch(default, BranchSide.Below, BranchKind.Building).Label);
            Assert.AreEqual("언덕 굴", new Branch(default, BranchSide.Above, BranchKind.Hill).Label);
            Assert.AreEqual("광산 굴", new Branch(default, BranchSide.Below, BranchKind.Mine).Label);
        }
    }
}
