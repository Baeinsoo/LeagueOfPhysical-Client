using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryTargetFlagClothTests
    {
        [Test]
        public void 깃발_천의_법선은_모두_길이_1이다()
        {
            //  앞뒷면이 점을 같이 쓰면 법선이 서로 지워져 0이 된다 — LOP/Toon이 0을 정규화해 NaN이 나고 블룸이 빛 덩어리로 번졌다.
            var mesh = ArcheryTargetFlagView.BuildClothMesh();
            try
            {
                foreach (var n in mesh.normals)
                {
                    Assert.AreEqual(1f, n.magnitude, 1e-3f, n.ToString());
                }
                Assert.AreEqual(2, mesh.triangles.Length / 3);   // 여전히 양면
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
