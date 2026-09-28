using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class LOPToonMaterialsTests
    {
        [Test]
        public void 툰_셰이더에_색을_넣고_외곽선은_끈다()
        {
            var m = LOPToonMaterials.Create(Color.red);
            Assert.AreEqual("LOP/Toon", m.shader.name);
            Assert.AreEqual(Color.red, m.GetColor("_BaseColor"));
            Assert.AreEqual(Color.red, m.color);   // [MainColor] — 기존 코드의 material.color가 그대로 먹는다
            Assert.IsFalse(m.GetShaderPassEnabled("SRPDefaultUnlit"));
            Object.DestroyImmediate(m);
        }
    }
}
