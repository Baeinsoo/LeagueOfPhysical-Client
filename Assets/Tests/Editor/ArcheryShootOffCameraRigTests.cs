using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryShootOffCameraRigTests
    {
        [Test]
        public void 당기면_0_25초에_걸쳐_1인칭으로_들어간다()
        {
            float b = 0f;
            b = ArcheryShootOffCameraRig.StepBlend(b, drawing: true, deltaTime: 0.1f);
            Assert.AreEqual(0.4f, b, 1e-4f);
            b = ArcheryShootOffCameraRig.StepBlend(b, drawing: true, deltaTime: 0.2f);
            Assert.AreEqual(1f, b, 1e-4f);
        }

        [Test]
        public void 놓으면_다시_3인칭으로_빠진다()
        {
            Assert.AreEqual(0.6f, ArcheryShootOffCameraRig.StepBlend(1f, drawing: false, deltaTime: 0.1f), 1e-4f);
            Assert.AreEqual(0f, ArcheryShootOffCameraRig.StepBlend(0.1f, drawing: false, deltaTime: 0.1f), 1e-4f);
        }

        [Test]
        public void 끝에서는_정확히_1인칭_거리와_3인칭_거리()
        {
            Assert.AreEqual(-0.4f, ArcheryShootOffCameraRig.DistanceAt(1f, firstPersonDistance: -0.4f), 1e-5f);
            Assert.AreEqual(ArcheryShootOffCameraRig.ThirdPersonDistance,
                            ArcheryShootOffCameraRig.DistanceAt(0f, firstPersonDistance: -0.4f), 1e-5f);
        }

        [Test]
        public void 카메라는_3인칭에서_위로_올리고_1인칭에서는_눈높이_그대로()
        {
            Assert.AreEqual(ArcheryShootOffCameraRig.ThirdPersonRaise, ArcheryShootOffCameraRig.RaiseAt(0f), 1e-5f);
            Assert.AreEqual(0f, ArcheryShootOffCameraRig.RaiseAt(1f), 1e-5f);
        }
    }
}
