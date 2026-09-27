using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class ArcheryShootOffLineupTests
    {
        [Test]
        public void 남은_오른쪽_왼쪽을_번갈아_바깥으로()
        {
            float s = ArcheryShootOffLineup.SpacingMeters;
            Assert.AreEqual(s, ArcheryShootOffLineup.SlotOffset(0), 1e-5f);
            Assert.AreEqual(-s, ArcheryShootOffLineup.SlotOffset(1), 1e-5f);
            Assert.AreEqual(2f * s, ArcheryShootOffLineup.SlotOffset(2), 1e-5f);
            Assert.AreEqual(-2f * s, ArcheryShootOffLineup.SlotOffset(3), 1e-5f);
        }

        [Test]
        public void 바깥_자리일수록_앞으로도_나와_선다()
        {
            float f = ArcheryShootOffLineup.ForwardPerStepMeters;
            Assert.AreEqual(f, ArcheryShootOffLineup.ForwardOffset(0), 1e-5f);
            Assert.AreEqual(f, ArcheryShootOffLineup.ForwardOffset(1), 1e-5f);
            Assert.AreEqual(2f * f, ArcheryShootOffLineup.ForwardOffset(2), 1e-5f);
            Assert.AreEqual(2f * f, ArcheryShootOffLineup.ForwardOffset(3), 1e-5f);
        }

        [Test]
        public void 과녁을_보고_있어도_남이_화면_안에_든다()
        {
            //  카메라는 눈에서 0.4m 앞, 화면 좌우 약 45도(세로 60도·16:9). 여유를 두고 35도 안.
            for (int i = 0; i < 4; i++)
            {
                float side = Mathf.Abs(ArcheryShootOffLineup.SlotOffset(i));
                float ahead = ArcheryShootOffLineup.ForwardOffset(i) - 0.4f;
                Assert.Less(Mathf.Atan2(side, ahead) * Mathf.Rad2Deg, 35f, $"자리 {i}");
            }
        }

        [Test]
        public void 화살_간격은_출발에서_1_도착에서_0()
        {
            Assert.AreEqual(1f, ArcheryShootOffLineup.ArrowBlend(0f, 0.4f), 1e-5f);
            Assert.AreEqual(0.5f, ArcheryShootOffLineup.ArrowBlend(0.2f, 0.4f), 1e-5f);
            Assert.AreEqual(0f, ArcheryShootOffLineup.ArrowBlend(0.4f, 0.4f), 1e-5f);
            Assert.AreEqual(0f, ArcheryShootOffLineup.ArrowBlend(0.9f, 0.4f), 1e-5f);
            Assert.AreEqual(0f, ArcheryShootOffLineup.ArrowBlend(0.1f, 0f), 1e-5f);
        }
    }
}
