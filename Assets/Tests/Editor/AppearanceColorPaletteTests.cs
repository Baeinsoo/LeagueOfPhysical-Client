using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>외형 색표 — 번호를 색으로. 0(모름)·없는 번호·다른 부위 번호는 그 부위 기본색.</summary>
    public class AppearanceColorPaletteTests
    {
        private static AppearanceColorPalette Palette() => new AppearanceColorPalette(TestEconomyTables.AppearanceColors);

        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        [Test]
        public void 번호의_색을_돌려준다()
        {
            Assert.AreEqual(Hex("#E3B089"), Palette().Skin(104));
            Assert.AreEqual(Hex("#8A5A3B"), Palette().Hair(203));
        }

        [Test]
        public void 영이나_없는_번호는_기본색()
        {
            Assert.AreEqual(Hex("#F7D2AE"), Palette().Skin(0));
            Assert.AreEqual(Hex("#F7D2AE"), Palette().Skin(999));
            Assert.AreEqual(Hex("#4A3222"), Palette().Hair(0));
        }

        [Test]
        public void 다른_부위의_번호는_기본색()
        {
            Assert.AreEqual(Hex("#F7D2AE"), Palette().Skin(203));
            Assert.AreEqual(Hex("#4A3222"), Palette().Hair(104));
        }

        [Test]
        public void 부위별_선택지는_표시_순서대로()
        {
            var skins = Palette().Options(AppearanceColorPalette.SkinPart);
            CollectionAssert.AreEqual(new[] { 101, 102, 103, 104, 105 }, skins.Select(o => o.Id).ToArray());
            Assert.AreEqual("밝은 살구", skins[0].Name);
            Assert.AreEqual(4, Palette().Options(AppearanceColorPalette.HairPart).Count);
        }

        [Test]
        public void 기본값은_부위마다_하나()
        {
            Assert.AreEqual(101, Palette().DefaultId(AppearanceColorPalette.SkinPart));
            Assert.AreEqual(201, Palette().DefaultId(AppearanceColorPalette.HairPart));
            foreach (var part in new[] { "skin", "hair" })
            {
                Assert.AreEqual(1, TestEconomyTables.AppearanceColors.DataList.Count(r => r.Part == part && r.IsDefault), part);
            }
        }

        [Test]
        public void 이름은_번호로_찾는다()
        {
            Assert.AreEqual("금발", Palette().NameOf(204));
            Assert.AreEqual("", Palette().NameOf(999));
        }
    }
}
