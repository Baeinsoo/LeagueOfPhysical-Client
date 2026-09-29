using NUnit.Framework;
using UnityEditor;
using UnityEngine.TextCore.Text;

namespace LOP.Tests
{
    public class CutInFontTests
    {
        [Test]
        public void 컷인_글자는_모두_글꼴에_있다()
        {
            var font = AssetDatabase.LoadAssetAtPath<FontAsset>("Assets/UI/Theme/Fonts/BlackHanSans CutIn.asset");
            Assert.IsNotNull(font);
            Assert.AreEqual(AtlasPopulationMode.Static, font.atlasPopulationMode);
            foreach (char c in ArcheryGrandTitles.AllGlyphs())
            {
                if (char.IsWhiteSpace(c)) { continue; }
                Assert.IsTrue(font.characterLookupTable.ContainsKey(c), $"'{c}'(U+{(int)c:X4})가 컷인 글꼴에 없다 — LOP/UI/Build CutIn Font");
            }
        }
    }
}
