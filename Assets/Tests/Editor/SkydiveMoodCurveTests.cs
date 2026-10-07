using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>맵별 분위기 — 높이 사이를 부드럽게 잇고, 끝 밖은 끝 값(빛을 향한 낙하: 위는 어둡고 아래 출구는 금빛).</summary>
    public class SkydiveMoodCurveTests
    {
        private static SkydiveMoodKey Key(float alt, float bloom, Color fog)
            => new SkydiveMoodKey { Altitude = alt, Bloom = bloom, Fog = fog, FogDensity = 0.001f, Ambient = fog, Sun = Color.white, SunIntensity = 1f, Exposure = 0f };

        [Test]
        public void 두_높이_사이는_섞는다()
        {
            var keys = new[] { Key(1000f, 0.4f, Color.black), Key(0f, 2.4f, Color.white) };
            var m = SkydiveMoodCurve.Evaluate(keys, 500f);
            Assert.AreEqual(1.4f, m.Bloom, 1e-4f);
            Assert.AreEqual(0.5f, m.Fog.r, 1e-4f);
        }

        [Test]
        public void 끝_밖은_끝_값이고_순서가_섞여도_된다()
        {
            var keys = new[] { Key(0f, 2.4f, Color.white), Key(1000f, 0.4f, Color.black) };
            Assert.AreEqual(0.4f, SkydiveMoodCurve.Evaluate(keys, 5000f).Bloom, 1e-4f);
            Assert.AreEqual(2.4f, SkydiveMoodCurve.Evaluate(keys, -50f).Bloom, 1e-4f);
        }

        [Test]
        public void 비어_있으면_없음()
        {
            Assert.IsFalse(SkydiveMoodCurve.TryEvaluate(null, 10f, out _));
            Assert.IsFalse(SkydiveMoodCurve.TryEvaluate(new SkydiveMoodKey[0], 10f, out _));
        }
    }
}
