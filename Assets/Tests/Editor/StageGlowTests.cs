using System.IO;
using LOP;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>빛나는 반투명 셰이더가 있고 폰 빌드에 포함되며, 원뿔·삼각 메시가 맞는 모양이다.</summary>
    public class StageGlowTests
    {
        [Test]
        public void 무대_빛_셰이더가_있고_항상_포함된다()
        {
            Assert.IsNotNull(Shader.Find("LOP/StageGlow"));
            string meta = File.ReadAllText("Assets/Shaders/LOP/LOPStageGlow.shader.meta");
            string guid = System.Text.RegularExpressions.Regex.Match(meta, @"guid: (\w+)").Groups[1].Value;
            StringAssert.Contains(guid, File.ReadAllText("ProjectSettings/GraphicsSettings.asset"));
        }

        [Test]
        public void 원뿔은_옆면만_있고_높이만큼_선다()
        {
            var mesh = GlowMeshes.Cone(0.15f, 2.4f, 26f, 24);
            Assert.AreEqual((24 + 1) * 2, mesh.vertexCount);
            Assert.AreEqual(24 * 6, mesh.triangles.Length);
            Assert.AreEqual(26f, mesh.bounds.size.y, 1e-3);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void 삼각_깃발은_앞뒤_두_장()
        {
            var mesh = GlowMeshes.Triangle(0.26f, 0.3f);
            Assert.AreEqual(6, mesh.triangles.Length);
            Assert.AreEqual(6, mesh.vertexCount, "앞뒤 면이 꼭짓점을 같이 쓰면 법선이 지워져 검게 나온다");
            Object.DestroyImmediate(mesh);
        }
    }
}
