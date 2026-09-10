using NUnit.Framework;

namespace LOP.Tests
{
    public class ReconciliationStatsTests
    {
        [Test]
        public void 근접과_비근접을_따로_센다()
        {
            var stats = new ReconciliationStats();
            stats.Record(0.05f, nearOther: false);
            stats.Record(0.40f, nearOther: true);
            stats.Record(0.02f, nearOther: false);

            Assert.AreEqual(0.40f, stats.NearMax, 1e-4f);
            Assert.AreEqual(0.05f, stats.FarMax, 1e-4f);
            Assert.AreEqual(0.40f, stats.Max, 1e-4f);   // 전체 Max는 그대로 둘을 합친 값
        }

        [Test]
        public void 옛_한_인자_호출은_비근접으로_센다()
        {
            var stats = new ReconciliationStats();
            stats.Record(0.30f);

            Assert.AreEqual(0.30f, stats.FarMax, 1e-4f);
            Assert.AreEqual(0f, stats.NearMax, 1e-4f);
        }

        [Test]
        public void 리셋하면_둘_다_지워진다()
        {
            var stats = new ReconciliationStats();
            stats.Record(0.40f, nearOther: true);
            stats.Record(0.10f, nearOther: false);
            stats.Reset();

            Assert.AreEqual(0f, stats.NearMax, 1e-4f);
            Assert.AreEqual(0f, stats.FarMax, 1e-4f);
        }
    }
}
