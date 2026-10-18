using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ChibiOutfitTests
    {
        [Test]
        public void 외곽선은_상의_색을_어둡게_한_것이다()
        {
            var c = new ChibiColors(Hex("#3B82F6"), Hex("#F7D2AE"), Hex("#4A3222"), Hex("#3A3450"), Color.white);
            Assert.AreEqual(c.Top.r * 0.4f, c.Outline.r, 1e-4f);
            Assert.AreEqual(c.Top.b * 0.4f, c.Outline.b, 1e-4f);
        }

        private static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
    }
}
