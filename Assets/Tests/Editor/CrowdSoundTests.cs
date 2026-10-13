using LOP;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>합성 함성 — 소리 값이 정상이고, 일반에선 조용하며, 랭크 전환 순간이 가장 크다.</summary>
    public class CrowdSoundTests
    {
        [Test]
        public void 합성_소리는_길이가_맞고_범위_안이며_같은_씨앗이면_같다()
        {
            var a = CrowdNoise.Generate(8000, 1f, 3);
            var b = CrowdNoise.Generate(8000, 1f, 3);
            Assert.AreEqual(8000, a.Length);
            CollectionAssert.AreEqual(a, b);
            float peak = 0f;
            foreach (var s in a)
            {
                Assert.IsFalse(float.IsNaN(s));
                peak = System.Math.Max(peak, System.Math.Abs(s));
            }
            Assert.LessOrEqual(peak, 1f);
            Assert.Greater(peak, 0.3f, "소리가 있다");
        }

        [Test]
        public void 일반에선_조용하다()
        {
            Assert.AreEqual(0f, CrowdMix.Target(0f, 1f));
        }

        [Test]
        public void 랭크는_전환_순간이_가장_크고_상한을_넘지_않는다()
        {
            Assert.AreEqual(CrowdMix.Base, CrowdMix.Target(1f, 0f), 1e-4);
            Assert.Greater(CrowdMix.Target(1f, 1f), CrowdMix.Target(1f, 0f));
            Assert.LessOrEqual(CrowdMix.Target(1f, 5f), CrowdMix.Max);
        }
    }
}
