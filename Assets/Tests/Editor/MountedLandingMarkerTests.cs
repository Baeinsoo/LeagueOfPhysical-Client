using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>화살은 떨어지는데 조준선은 직선이다 — 점은 화살이 실제로 닿을 자리에 찍혀야 한다.</summary>
    public class MountedLandingMarkerTests
    {
        // 높이 0의 평평한 땅.
        private static Vector3? Ground(Vector3 from, Vector3 to)
        {
            if (from.y < 0f || to.y >= 0f) return null;
            float t = from.y / (from.y - to.y);
            return Vector3.Lerp(from, to, t);
        }

        [Test]
        public void 평평한_땅이면_떨어진_만큼_앞에_찍힌다()
        {
            var origin = new Vector3(0f, 2.5f, 0f);
            Vector3? point = MountedLandingMarker.LandingPoint(origin, new Vector3(0f, 0f, 60f), 3f, 1f / 60f, Ground);

            //  2.5 m를 떨어지는 시간 √(2·2.5/9.81) ≈ 0.714초 → 60 m/s로 약 42.8 m 앞.
            Assert.IsTrue(point.HasValue);
            Assert.AreEqual(0f, point.Value.y, 1e-3f);
            Assert.AreEqual(42.8f, point.Value.z, 0.3f);
        }

        [Test]
        public void 앞에_벽이_있으면_벽에_찍힌다()
        {
            Vector3? Wall(Vector3 from, Vector3 to)
                => from.z < 20f && to.z >= 20f ? Vector3.Lerp(from, to, (20f - from.z) / (to.z - from.z)) : (Vector3?)null;

            Vector3? point = MountedLandingMarker.LandingPoint(new Vector3(0f, 2.5f, 0f), new Vector3(0f, 0f, 60f), 3f, 1f / 60f, Wall);

            Assert.IsTrue(point.HasValue);
            Assert.AreEqual(20f, point.Value.z, 1e-3f);
            Assert.Less(point.Value.y, 2.5f, "20 m 가는 동안 조금 떨어진다");
        }

        [Test]
        public void 아무것도_안_닿으면_없다()
        {
            Assert.IsFalse(MountedLandingMarker.LandingPoint(
                new Vector3(0f, 2.5f, 0f), new Vector3(0f, 30f, 60f), 1f, 1f / 60f, (a, b) => null).HasValue);
        }
    }
}
