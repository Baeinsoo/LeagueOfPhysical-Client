using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LOP.Tests
{
    public class ArcheryAtmosphereTests
    {
        [Test]
        public void 과녁_90m는_안개가_반도_안_된다()
        {
            Assert.Less(ArcheryAtmosphere.FogAmount(90f), 0.15f);
            Assert.AreEqual(0f, ArcheryAtmosphere.FogAmount(30f));
        }

        [Test]
        public void 되돌리면_원래_값()
        {
            bool fog = RenderSettings.fog;
            var mode = RenderSettings.fogMode;
            var color = RenderSettings.fogColor;
            var sky = RenderSettings.skybox;
            var ambient = RenderSettings.ambientMode;
            var sun = RenderSettings.sun;

            var atmosphere = new ArcheryAtmosphere();
            atmosphere.Apply();
            Assert.IsTrue(RenderSettings.fog);
            Assert.AreEqual("LOP/WatercolorSky", RenderSettings.skybox.shader.name);
            atmosphere.Dispose();

            Assert.AreEqual(fog, RenderSettings.fog);
            Assert.AreEqual(mode, RenderSettings.fogMode);
            Assert.AreEqual(color, RenderSettings.fogColor);
            Assert.AreSame(sky, RenderSettings.skybox);
            Assert.AreEqual(ambient, RenderSettings.ambientMode);
            Assert.AreSame(sun, RenderSettings.sun);
        }

        [Test]
        public void 늦게_뜬_맵의_해도_끄고_카메라_하늘_상자는_잠시_끈다()
        {
            //  맵(과 그 흰 해)은 스코프 시작 뒤에 뜬다. 카메라의 Skybox 컴포넌트는 RenderSettings.skybox를 덮어 수채화 하늘을 가린다.
            //  시험 러너의 기본 씬에는 이미 "Directional Light"가 있다 — 섞이지 않게 잠시 꺼 둔다.
            var existing = new System.Collections.Generic.List<Light>();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l.enabled) { l.enabled = false; existing.Add(l); }
            }
            var warm = new GameObject("Warm").AddComponent<Light>();
            warm.type = LightType.Directional;
            var cam = new GameObject("Cam").AddComponent<Camera>();
            var box = cam.gameObject.AddComponent<Skybox>();
            Light late = null;
            var atmosphere = new ArcheryAtmosphere(warm.gameObject.scene.name);
            try
            {
                atmosphere.Apply();
                Assert.AreSame(warm, RenderSettings.sun);
                Assert.IsFalse(box.enabled);

                late = new GameObject("MapSun").AddComponent<Light>();
                late.type = LightType.Directional;
                atmosphere.OnSceneLoaded(late.gameObject.scene, LoadSceneMode.Additive);
                Assert.IsFalse(late.enabled);
                Assert.IsTrue(warm.enabled);

                atmosphere.Dispose();
                Assert.IsTrue(late.enabled);
                Assert.IsTrue(box.enabled);
            }
            finally
            {
                Object.DestroyImmediate(warm.gameObject);
                Object.DestroyImmediate(cam.gameObject);
                if (late != null) { Object.DestroyImmediate(late.gameObject); }
                foreach (var l in existing) { if (l != null) { l.enabled = true; } }
            }
        }
    }
}
