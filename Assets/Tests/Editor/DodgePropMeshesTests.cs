using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class DodgePropMeshesTests
    {
        [Test]
        public void 장독은_반지름_0점5_구_안에_든다()
        {
            // 판정은 원 — 구르는 장독을 어느 방향에서 봐도 원 밖으로 안 나가야 한다.
            var m = DodgePropMeshes.Jar();
            foreach (var v in m.vertices) Assert.LessOrEqual(v.magnitude, 0.5f + 1e-4f, v.ToString());
            Assert.Greater(m.bounds.size.y, 0.9f);   // 쪼그라든 장독이 아니다
        }

        [Test]
        public void 슬리퍼는_길이_1_폭은_그보다_작다()
        {
            var b = DodgePropMeshes.Slipper().bounds;
            Assert.AreEqual(1f, b.size.z, 1e-3f);
            Assert.Less(b.size.x, b.size.z);
            Assert.Less(b.size.y, 0.3f);
        }
    }
}
