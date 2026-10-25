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

        [Test]
        public void 누르고_0점15초면_거의_다_확대된다()
        {
            //  시위는 0.35초에 만작(초당 1/0.35)으로 차오르고 화면은 매 프레임 그걸 따라간다.
            float draw = 0f, shown = 0f;
            const float dt = 1f / 60f;
            for (int i = 0; i < 9; i++)   // 0.15초
            {
                draw = UnityEngine.Mathf.MoveTowards(draw, 1f, dt / 0.35f);
                shown = MountedCameraZoom.Step(shown, draw, dt);
            }
            Assert.GreaterOrEqual(shown, 0.95f);
        }

        [Test]
        public void 놓으면_천천히_풀린다()
        {
            //  쏘는 순간 시위가 0으로 떨어져도 한 프레임에 화각이 튀지 않고, 0.1초 뒤에도 절반 넘게 남는다.
            const float dt = 1f / 60f;
            float shown = MountedCameraZoom.Step(1f, 0f, dt);
            Assert.Greater(shown, 0.95f);
            for (int i = 1; i < 6; i++)
            {
                shown = MountedCameraZoom.Step(shown, 0f, dt);
            }
            Assert.Greater(shown, 0.7f);
            Assert.Less(shown, 1f);
        }
    }
}
