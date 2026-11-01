using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>기둥은 칸이 무너진 뒤 0.5초 동안 땅속으로 가라앉는다.</summary>
    public class KnockbackPillarViewTests
    {
        const float Height = 3f;
        const long Collapse = 500;

        [Test]
        public void 무너지기_전에는_안_가라앉는다()
        {
            Assert.AreEqual(0f, KnockbackPillarView.SinkDepth(Collapse, Collapse - 1, Height), 1e-4f);
            Assert.AreEqual(0f, KnockbackPillarView.SinkDepth(Collapse, Collapse, Height), 1e-4f);
        }

        [Test]
        public void 반_초에_걸쳐_선형으로_가라앉고_그_뒤는_높이만큼이다()
        {
            Assert.AreEqual(Height / 2f, KnockbackPillarView.SinkDepth(Collapse, Collapse + 12.5, Height), 1e-4f);
            Assert.AreEqual(Height, KnockbackPillarView.SinkDepth(Collapse, Collapse + 25, Height), 1e-4f);
            Assert.AreEqual(Height, KnockbackPillarView.SinkDepth(Collapse, Collapse + 400, Height), 1e-4f);
        }

        [Test]
        public void 안_무너지는_기둥은_안_가라앉는다()
        {
            Assert.AreEqual(0f, KnockbackPillarView.SinkDepth(long.MaxValue, 1000, Height), 1e-4f);
        }
    }
}
