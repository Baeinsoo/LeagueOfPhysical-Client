using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class SkydiveAtmosphereSkyTests
    {
        [Test]
        public void 수채화_하늘의_지평선을_안개색으로()
        {
            var sky = new Material(Shader.Find("LOP/WatercolorSky"));
            try
            {
                SkydiveAtmosphere.PaintSky(sky, new Color(0.9f, 0.85f, 0.7f));
                Assert.AreEqual(new Color(0.9f, 0.85f, 0.7f), sky.GetColor("_HorizonColor"));
                Assert.Greater(sky.GetColor("_BottomColor").r, 0.9f - 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(sky);
            }
        }

        [Test]
        public void 하늘을_되돌린다()
        {
            var before = RenderSettings.skybox;
            var atmosphere = new SkydiveAtmosphere(null, null);
            atmosphere.UseWatercolorSky();
            Assert.AreEqual("LOP/WatercolorSky", RenderSettings.skybox.shader.name);
            atmosphere.Dispose();
            Assert.AreSame(before, RenderSettings.skybox);
        }

        [Test]
        public void 카메라_하늘_상자는_잠시_끈다()
        {
            //  Skydive.unity 카메라에 Skybox 컴포넌트가 있어 RenderSettings.skybox를 덮는다 — 그래서 예전 고도 틴트가 안 보였다.
            var cam = new GameObject("Cam").AddComponent<Camera>();
            var box = cam.gameObject.AddComponent<Skybox>();
            var before = RenderSettings.skybox;
            var atmosphere = new SkydiveAtmosphere(null, null);
            try
            {
                atmosphere.Apply(1500f);
                Assert.IsFalse(box.enabled);
                atmosphere.Dispose();
                Assert.IsTrue(box.enabled);
            }
            finally
            {
                Object.DestroyImmediate(cam.gameObject);
                RenderSettings.skybox = before;
            }
        }
    }
}
