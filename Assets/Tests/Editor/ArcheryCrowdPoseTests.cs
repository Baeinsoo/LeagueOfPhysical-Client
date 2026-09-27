using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryCrowdPoseTests
    {
        [Test]
        public void 환호는_팔을_들고_튄다()
        {
            var p = ArcheryCrowdPose.At(ArcheryCrowdMood.Cheer, time: 0.1f, phase: 0f);
            Assert.Greater(p.LeftArmDegrees, 120f);
            Assert.Greater(p.RightArmDegrees, 120f);
            Assert.Greater(p.Lift, 0f);
            Assert.IsFalse(p.Gray);
        }

        [Test]
        public void 정적은_가라앉아_멈춘다()
        {
            var a = ArcheryCrowdPose.At(ArcheryCrowdMood.Hush, time: 0.3f, phase: 1f);
            var b = ArcheryCrowdPose.At(ArcheryCrowdMood.Hush, time: 1.7f, phase: 2f);
            Assert.Less(a.Lift, 0f);
            Assert.AreEqual(a.Lift, b.Lift, 1e-6f);
            Assert.AreEqual(0f, a.LeftArmDegrees, 1e-6f);
        }

        [Test]
        public void 야유는_회색이다()
        {
            Assert.IsTrue(ArcheryCrowdPose.At(ArcheryCrowdMood.Boo, 0.5f, 0f).Gray);
            Assert.IsFalse(ArcheryCrowdPose.At(ArcheryCrowdMood.Idle, 0.5f, 0f).Gray);
        }

        [Test]
        public void 연호는_위상과_상관없이_모두_같은_박자()
        {
            var a = ArcheryCrowdPose.At(ArcheryCrowdMood.Chant, time: 0.2f, phase: 0f);
            var b = ArcheryCrowdPose.At(ArcheryCrowdMood.Chant, time: 0.2f, phase: 2.5f);
            Assert.AreEqual(a.Lift, b.Lift, 1e-6f);
        }

        [Test]
        public void 웃음은_한_팔만_들고_좌우로_떤다()
        {
            var p = ArcheryCrowdPose.At(ArcheryCrowdMood.Laugh, time: 0.05f, phase: 0f);
            Assert.Less(p.LeftArmDegrees, 60f);
            Assert.Greater(p.RightArmDegrees, 120f);
            Assert.AreNotEqual(0f, p.Sway);
        }

        [Test]
        public void 탄성은_뛰지_않고_손을_머리로()
        {
            var p = ArcheryCrowdPose.At(ArcheryCrowdMood.Gasp, time: 0.4f, phase: 0f);
            Assert.AreEqual(0f, p.Lift, 1e-6f);
            Assert.That(p.LeftArmDegrees, Is.InRange(100f, 140f));
        }
    }
}
