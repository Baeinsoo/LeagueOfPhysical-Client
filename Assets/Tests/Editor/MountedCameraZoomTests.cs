using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>말 활쏘기 카메라 — 당긴 정도(0~1)에 따라 화각과 거리가 선형으로 좁아진다.</summary>
    public class MountedCameraZoomTests
    {
        [Test]
        public void 안_당기면_넓고_멀다()
        {
            var (fov, distance) = MountedCameraZoom.For(0f);
            Assert.AreEqual(62f, fov, 1e-4f);
            Assert.AreEqual(5.6f, distance, 1e-4f);
        }

        [Test]
        public void 다_당기면_좁고_가깝다()
        {
            var (fov, distance) = MountedCameraZoom.For(1f);
            Assert.AreEqual(42f, fov, 1e-4f);
            Assert.AreEqual(3.4f, distance, 1e-4f);
        }

        [Test]
        public void 중간은_선형()
        {
            var (fov, distance) = MountedCameraZoom.For(0.5f);
            Assert.AreEqual(52f, fov, 1e-4f);
            Assert.AreEqual(4.5f, distance, 1e-4f);
        }

        [Test]
        public void 범위_밖은_자른다()
        {
            var over = MountedCameraZoom.For(1.7f);
            Assert.AreEqual(42f, over.fov, 1e-4f);
            Assert.AreEqual(3.4f, over.distance, 1e-4f);

            var under = MountedCameraZoom.For(-1f);
            Assert.AreEqual(62f, under.fov, 1e-4f);
            Assert.AreEqual(5.6f, under.distance, 1e-4f);
        }
    }
}
