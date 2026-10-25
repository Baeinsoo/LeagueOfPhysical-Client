using LOP;
using NUnit.Framework;

namespace LOP.Tests
{
    public class MountedCameraRecenterTests
    {
        const float Dt = 0.1f;

        [Test]
        public void 손을_떼고_1초_뒤_말_뒤로_돌아온다()
        {
            var r = new MountedCameraRecenter();
            float cam = 0f;
            for (int i = 0; i < 9; i++) { cam += r.Step(cam, 90f, false, Dt); }
            Assert.AreEqual(0f, cam, 1e-4f, "1초가 되기 전엔 가만히 있다");

            for (int i = 0; i < 20; i++) { cam += r.Step(cam, 90f, false, Dt); }
            Assert.Greater(cam, 30f, "1초 뒤부터 돌아오기 시작한다");
            Assert.LessOrEqual(cam, 90f);

            for (int i = 0; i < 100; i++) { cam += r.Step(cam, 90f, false, Dt); }
            Assert.AreEqual(90f, cam, 1e-3f);
        }

        [Test]
        public void 조준_중엔_돌아오지_않는다()
        {
            var r = new MountedCameraRecenter();
            float cam = 0f;
            for (int i = 0; i < 50; i++) { cam += r.Step(cam, 90f, true, Dt); }
            Assert.AreEqual(0f, cam, 1e-6f);

            //  양성 대조 — 손을 놓고 1초 뒤엔 돌아온다. 그리고 다시 만지면 즉시 멈추고 시계도 처음부터.
            for (int i = 0; i < 15; i++) { cam += r.Step(cam, 90f, false, Dt); }
            Assert.Greater(cam, 0f);
            float stopped = cam;
            cam += r.Step(cam, 90f, true, Dt);
            Assert.AreEqual(stopped, cam, 1e-6f);
            for (int i = 0; i < 9; i++) { cam += r.Step(cam, 90f, false, Dt); }
            Assert.AreEqual(stopped, cam, 1e-6f);
        }
    }
}
