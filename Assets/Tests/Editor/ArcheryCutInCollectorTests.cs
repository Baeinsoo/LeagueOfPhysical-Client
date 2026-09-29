using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryCutInCollectorTests
    {
        [Test]
        public void 정중앙은_가장_좋은_순위_한_명()
        {
            var c = new ArcheryCutInCollector();
            c.OnBull("b", 2);
            c.OnBull("a", 0);
            c.OnBull("c", 1);
            Assert.AreEqual("a", c.Take(null, null).BullId);
        }

        [Test]
        public void 고르면_후보는_비운다()
        {
            var c = new ArcheryCutInCollector();
            c.OnRobinHood("r");
            var first = c.Take("x", "y");
            Assert.AreEqual("r", first.RobinHoodId);
            Assert.AreEqual("x", first.ComebackId);
            Assert.AreEqual("y", first.LastPlaceId);
            var second = c.Take(null, null);
            Assert.IsNull(second.RobinHoodId);
            Assert.IsNull(second.BullId);
        }
    }
}
