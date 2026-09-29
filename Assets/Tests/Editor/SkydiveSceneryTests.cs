using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LOP.Tests
{
    public class SkydiveSceneryTests
    {
        private static GameObject Plate(string name, string materialName)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = materialName };
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        private static int Roots() => Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t => t.parent == null && t.name == "SkydiveScenery");

        [Test]
        public void 맵이_늦게_떠도_입힌다()
        {
            var scenery = new SkydiveScenery();
            scenery.Start();
            Assert.AreEqual(0, Roots());
            var stone = Plate("Shelf", "SkydiveStone");
            var floor = Plate("Floor", "Lit");
            try
            {
                scenery.OnSceneLoaded(stone.scene, LoadSceneMode.Additive);
                scenery.OnSceneLoaded(stone.scene, LoadSceneMode.Additive);
                Assert.AreEqual(1, Roots());
                Assert.AreEqual("LOP/Toon", stone.GetComponent<Renderer>().sharedMaterial.shader.name);
                Assert.IsFalse(floor.GetComponent<Renderer>().enabled);
            }
            finally
            {
                scenery.Dispose();
                Object.DestroyImmediate(stone);
                Object.DestroyImmediate(floor);
            }
        }

        [Test]
        public void 되돌리면_원래_재질()
        {
            var stone = Plate("Shelf", "SkydiveStone");
            var cloud = Plate("CloudLayer", "SkydiveCloud");
            var original = stone.GetComponent<Renderer>().sharedMaterial;
            var scenery = new SkydiveScenery();
            try
            {
                scenery.Start();
                Assert.IsFalse(cloud.GetComponent<Renderer>().enabled);
                scenery.Dispose();
                Assert.AreSame(original, stone.GetComponent<Renderer>().sharedMaterial);
                Assert.IsTrue(cloud.GetComponent<Renderer>().enabled);
                Assert.AreEqual(0, Roots());
            }
            finally
            {
                Object.DestroyImmediate(stone);
                Object.DestroyImmediate(cloud);
            }
        }
    }
}
