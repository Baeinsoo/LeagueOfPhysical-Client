using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LOP.Tests
{
    public class ArcheryCutInFreezeTests
    {
        [Test]
        public void 가중치가_볼륨에_실리고_흑백이다()
        {
            var freeze = new ArcheryCutInFreeze();
            freeze.Start();
            try
            {
                freeze.Weight = 0.7f;
                var volume = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
                var mine = System.Array.Find(volume, v => v.name == "CutInFreeze");
                Assert.IsNotNull(mine);
                Assert.AreEqual(0.7f, mine.weight, 1e-4f);
                Assert.IsTrue(mine.sharedProfile.TryGet<ColorAdjustments>(out var ca));
                Assert.AreEqual(-100f, ca.saturation.value, 1e-3f);
            }
            finally
            {
                freeze.Dispose();
            }
        }

        [Test]
        public void 지우면_볼륨이_사라진다()
        {
            var freeze = new ArcheryCutInFreeze();
            freeze.Start();
            freeze.Dispose();
            Assert.IsFalse(System.Array.Exists(Object.FindObjectsByType<Volume>(FindObjectsSortMode.None), v => v.name == "CutInFreeze"));
        }
    }
}
