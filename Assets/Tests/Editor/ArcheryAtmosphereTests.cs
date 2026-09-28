using NUnit.Framework;
using UnityEngine;

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
    }
}
