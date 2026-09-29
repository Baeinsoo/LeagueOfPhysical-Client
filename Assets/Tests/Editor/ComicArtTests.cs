using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ComicArtTests
    {
        [TestCase("Comic/FxAtlas", 1024, 512)]
        [TestCase("Comic/CutInLines", 512, 512)]
        [TestCase("Comic/CutInSkin", 512, 512)]
        [TestCase("Comic/CutInHair", 512, 512)]
        public void 그림은_리소스에_있고_크기가_맞다(string path, int w, int h)
        {
            var tex = Resources.Load<Texture2D>(path);
            Assert.IsNotNull(tex, path);
            Assert.AreEqual(w, tex.width);
            Assert.AreEqual(h, tex.height);
        }
    }
}
