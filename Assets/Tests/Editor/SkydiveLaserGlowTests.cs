using NUnit.Framework;

namespace LOP.Tests
{
    public class SkydiveLaserGlowTests
    {
        [Test]
        public void 켜진_빔은_블룸_임계를_넘는다()
        {
            Assert.Greater(SkydiveLaserView.LitColor.r, 1f);
        }

        [Test]
        public void 빛_번짐_굵기는_판정_굵기와_같다()
        {
            //  보이는 범위 = 맞는 범위. 더 가늘게 그리면 "틈이 있어 보이는데 죽는다".
            foreach (float r in new[] { 0.15f, 0.6f, 0.9f })
            {
                Assert.AreEqual(r * 2f, SkydiveLaserView.GlowThickness(r), 0.0001f);
            }
        }

        [Test]
        public void 심지는_빛_번짐보다_가늘다()
        {
            //  젤다·미션 임파서블 레이저 — 하얗게 빛나는 가는 심지 + 둘레 번짐.
            foreach (float r in new[] { 0.15f, 0.6f, 0.9f })
            {
                Assert.Less(SkydiveLaserView.CoreThickness(r), SkydiveLaserView.GlowThickness(r) * 0.5f);
                Assert.Greater(SkydiveLaserView.CoreThickness(r), 0f);
            }
        }

        [Test]
        public void 빛_번짐_재질이_있다()
        {
            //  Resources에 있어야 빌드에 셰이더가 따라간다(이름으로 찾으면 빌드에서 빠질 수 있다).
            var m = UnityEngine.Resources.Load<UnityEngine.Material>(SkydiveLaserView.GlowMaterialResource);
            Assert.IsNotNull(m);
            Assert.AreEqual("LOP/LaserGlow", m.shader.name);
        }
    }
}
