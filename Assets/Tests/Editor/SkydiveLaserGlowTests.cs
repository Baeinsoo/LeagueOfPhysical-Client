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

        [Test]
        public void 심지와_창_테두리도_깊이에_따라_옅어진다()
        {
            //  그물이 여러 장 겹치면 원근만으로는 앞뒤를 못 가른다 — 밝기가 깊이를 말해야 한다.
            //  심지·번짐·창 테두리가 모두 같은 셰이더(내 캐릭터 높이 기준 옅어짐)를 써야 한 장씩 같이 옅어진다.
            var core = UnityEngine.Resources.Load<UnityEngine.Material>(SkydiveLaserView.CoreMaterialResource);
            Assert.IsNotNull(core);
            var frame = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/Art/Materials/Pyramid/ChimneyWindowFrame.mat");
            foreach (var m in new[] { core, frame, UnityEngine.Resources.Load<UnityEngine.Material>(SkydiveLaserView.GlowMaterialResource) })
            {
                Assert.AreEqual("LOP/LaserGlow", m.shader.name, m.name);
                Assert.IsTrue(m.HasProperty("_FadeFar"), m.name);
            }
            //  재질 에셋 값은 셰이더 기본값을 덮는다 — 실제로 쓰는 값은 런타임·굽기에서 ApplyFade로 덮어쓴 것.
            Assert.LessOrEqual(SkydiveLaserView.FadeFar, 25f, "계단 10m — 두 장 아래면 충분히 옅어야 앞이 읽힌다");
            Assert.Less(SkydiveLaserView.FadeMin, 0.3f);
            Assert.AreEqual(SkydiveLaserView.FadeFar, frame.GetFloat("_FadeFar"), 0.001f, "창 테두리도 같은 값(굽기에서 덮어씀)");
        }
    }
}
