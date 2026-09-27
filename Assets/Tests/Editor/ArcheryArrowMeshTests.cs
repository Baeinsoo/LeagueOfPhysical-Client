using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryArrowMeshTests
    {
        private Mesh mesh;

        [SetUp]
        public void SetUp()
        {
            mesh = ArcheryArrowMesh.Build();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void 촉은_앞쪽_끝에_있고_살짝_박힌다()
        {
            //  화살은 날아가는 쪽(+Z)을 본다. 기준점은 촉 끝보다 조금 뒤라 꽂히면 촉이 묻힌다.
            Assert.AreEqual(ArcheryArrowMesh.TipEmbed, mesh.bounds.max.z, 1e-4f);
        }

        [Test]
        public void 몸통은_기준점_뒤로_화살_길이만큼_뻗는다()
        {
            Assert.AreEqual(ArcheryArrowMesh.TipEmbed - ArcheryArrowMesh.Length, mesh.bounds.min.z, 1e-4f);
        }

        [Test]
        public void 대_촉_깃은_재질을_따로_쓴다()
        {
            Assert.AreEqual(3, mesh.subMeshCount);
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                Assert.Greater(mesh.GetTriangles(i).Length, 0, $"부분 {i}가 비어 있다");
            }
        }

        [Test]
        public void 옆으로는_깃_폭_안에_든다()
        {
            float half = ArcheryArrowMesh.FletchHeight + ArcheryArrowMesh.ShaftRadius;
            Assert.LessOrEqual(mesh.bounds.max.x, half + 1e-4f);
            Assert.LessOrEqual(mesh.bounds.max.y, half + 1e-4f);
        }
    }
}
