using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryFlagPoseTests
    {
        [Test]
        public void 바람이_없으면_처진다()
        {
            var p = ArcheryFlagPose.At(0f, time: 1f, phase: 0f);
            Assert.AreEqual(0, p.Side);
            Assert.AreEqual(0f, p.Extend, 1e-6f);
            Assert.AreEqual(0f, p.FlapDegrees, 1e-6f);
        }

        [Test]
        public void 약한_바람도_처진다()
        {
            Assert.AreEqual(0, ArcheryFlagPose.At(0.2f, 1f, 0f).Side);
        }

        [Test]
        public void 양수_바람은_오른쪽으로_다_펴진다()
        {
            var p = ArcheryFlagPose.At(5f, 1f, 0f);
            Assert.AreEqual(1, p.Side);
            Assert.AreEqual(1f, p.Extend, 1e-6f);
        }

        [Test]
        public void 음수_바람은_왼쪽()
        {
            var p = ArcheryFlagPose.At(-2.5f, 1f, 0f);
            Assert.AreEqual(-1, p.Side);
            Assert.AreEqual(0.5f, p.Extend, 1e-6f);
        }

        [Test]
        public void 센_바람이_더_빨리_펄럭인다()
        {
            //  주파수 = 6 + |바람| — 약한 바람(1)은 7, 센 바람(5)은 11 rad/s. 한 주기의 시각을 비교한다.
            float weakPeriod = 2f * UnityEngine.Mathf.PI / 7f;
            var weak = ArcheryFlagPose.At(1f, weakPeriod * 0.25f, 0f);
            Assert.Greater(weak.FlapDegrees, 0f);
            var strong = ArcheryFlagPose.At(5f, 2f * UnityEngine.Mathf.PI / 11f * 0.25f, 0f);
            Assert.Greater(strong.FlapDegrees, weak.FlapDegrees);   // 같은 위상(꼭대기)에서 펴짐이 커 진폭도 크다
        }
    }
}
