using LOP.UI;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>티어마다 도형이 달라 색을 못 가리는 사람도 티어를 구분한다(1부 §3.7).</summary>
    public class TierShapeGeometryTests
    {
        private static readonly Rect Box = new Rect(0, 0, 48, 48);

        [TestCase("iron", TierShape.Circle)]
        [TestCase("bronze", TierShape.Triangle)]
        [TestCase("silver", TierShape.RoundedSquare)]
        [TestCase("gold", TierShape.Star)]
        [TestCase("platinum", TierShape.Hexagon)]
        [TestCase("emerald", TierShape.Pentagon)]
        [TestCase("diamond", TierShape.Diamond)]
        [TestCase("master", TierShape.Crown)]
        [TestCase("unknown", TierShape.Circle)]
        [TestCase(null, TierShape.Circle)]
        public void 티어_코드로_도형을_고른다(string code, TierShape expected) =>
            Assert.AreEqual(expected, TierShapeGeometry.ForTierCode(code));

        [TestCase(TierShape.Triangle, 3)]
        [TestCase(TierShape.RoundedSquare, 4)]
        [TestCase(TierShape.Star, 10)]
        [TestCase(TierShape.Hexagon, 6)]
        [TestCase(TierShape.Pentagon, 5)]
        [TestCase(TierShape.Diamond, 4)]
        [TestCase(TierShape.Crown, 7)]
        public void 꼭짓점_수(TierShape s, int count) =>
            Assert.AreEqual(count, TierShapeGeometry.Points(s, Box).Length);

        [Test]
        public void 원은_점이_없다() => Assert.IsEmpty(TierShapeGeometry.Points(TierShape.Circle, Box));

        [Test]
        public void 모든_점이_상자_안()
        {
            foreach (TierShape s in System.Enum.GetValues(typeof(TierShape)))
            foreach (var p in TierShapeGeometry.Points(s, Box))
                Assert.IsTrue(p.x >= -0.01f && p.x <= 48.01f && p.y >= -0.01f && p.y <= 48.01f, $"{s} {p}");
        }

        [Test]
        public void 크기_0이면_빈_결과() => Assert.IsEmpty(TierShapeGeometry.Points(TierShape.Star, new Rect(0, 0, 0, 0)));
    }
}
