using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>선반 윗면 타일 무늬 — 단색 선반은 다가가도 변하는 게 없어 거리가 안 느껴진다.</summary>
    public class SkydiveShelfGridTests
    {
        [Test]
        public void 선반_재질은_윗면_무늬를_켠다()
        {
            var shelf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shelf.name = "Shelf";
            shelf.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "SkydiveStone" };
            var scenery = new SkydiveScenery();
            try
            {
                scenery.Start();
                var m = shelf.GetComponent<Renderer>().sharedMaterial;
                Assert.IsTrue(m.HasProperty("_TopGrid"));
                Assert.Greater(m.GetFloat("_TopGrid"), 0f);
            }
            finally
            {
                scenery.Dispose();
                Object.DestroyImmediate(shelf);
            }
        }

        [Test]
        public void 툰_기본값은_무늬_꺼짐()
        {
            var m = LOPToonMaterials.Create(Color.white);
            Assert.AreEqual(0f, m.GetFloat("_TopGrid"));
        }
    }
}
