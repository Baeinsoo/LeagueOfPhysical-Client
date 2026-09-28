using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ChibiOutfitTests
    {
        private static readonly Color[] Party =
            new[] { "#FF4F5E", "#2EC4A6", "#6C63FF", "#F59E0B", "#3B82F6", "#10B981", "#EC4899", "#F97316" }.Select(Hex).ToArray();

        [Test]
        public void 같은_엔티티는_늘_같은_색()
        {
            var a = ChibiOutfit.ColorsFor("7:3");
            var b = ChibiOutfit.ColorsFor("7:3");
            Assert.AreEqual(a.Top, b.Top);
            Assert.AreEqual(a.Skin, b.Skin);
            Assert.AreEqual(a.Hair, b.Hair);
        }

        [Test]
        public void 저지는_파티_색이고_바지_신발_외곽선은_규칙대로()
        {
            var c = ChibiOutfit.ColorsFor("abc");
            CollectionAssert.Contains(Party, c.Top);
            Assert.AreEqual(Hex("#3A3450"), c.Bottom);
            Assert.AreEqual(Color.white, c.Shoe);
            Assert.AreEqual(c.Top.r * 0.4f, c.Outline.r, 1e-4f);
        }

        [Test]
        public void 여덟_명이면_색이_넷_이상으로_갈린다()
        {
            int distinct = Enumerable.Range(1, 8).Select(i => ChibiOutfit.ColorsFor("e" + i).Top).Distinct().Count();
            Assert.GreaterOrEqual(distinct, 4);
        }

        [Test]
        public void 해시는_고정값이다()
        {
            //  string.GetHashCode는 런타임마다 다를 수 있다 — 화면마다 색이 달라지면 안 된다.
            Assert.AreEqual(0x811C9DC5u, ChibiOutfit.Fnv1a(""));
            Assert.AreEqual(0xE40C292Cu, ChibiOutfit.Fnv1a("a"));
        }

        private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    }
}
