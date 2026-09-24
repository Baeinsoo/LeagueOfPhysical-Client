using NUnit.Framework;

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
