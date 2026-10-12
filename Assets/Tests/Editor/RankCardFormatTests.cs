using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>로비 랭크 모드의 티어 카드 — 두 줄 문구와 도형용 티어 코드.</summary>
    public class RankCardFormatTests
    {
        private static readonly LOP.MasterData.TbRankDivision D = TestRankTables.Divisions;

        private static RankDto Rank(int division, int lp, int played = 5, int peak = 15) => new RankDto
        {
            divisionIndex = division, lp = lp, placementPlayed = played, placementGames = 5, peakDivisionIndex = peak,
        };

        [Test]
        public void 배치_끝_두_줄()
        {
            var c = RankCardFormat.From(Rank(14, 47), D);
            Assert.IsTrue(c.Loaded);
            Assert.AreEqual("골드 II · 47 LP", c.Headline);
            Assert.AreEqual("승급까지 53 LP · 시즌 최고 골드 I", c.Subline);
            Assert.AreEqual("gold", c.TierCode);
        }

        [Test]
        public void 마스터는_승급까지가_없다()
        {
            var c = RankCardFormat.From(Rank(28, 120, peak: 28), D);
            Assert.AreEqual("마스터 · 120 LP", c.Headline);
            Assert.AreEqual("시즌 최고 마스터", c.Subline);
        }

        [Test]
        public void 배치_중()
        {
            var c = RankCardFormat.From(Rank(9, 40, played: 2), D);
            Assert.AreEqual("배치 2/5", c.Subline);
            StringAssert.Contains("40 LP", c.Headline);
        }

        [Test]
        public void 배치_전()
        {
            var c = RankCardFormat.From(Rank(-1, 0, played: 0, peak: -1), D);
            Assert.AreEqual("배치 전", c.Headline);
            Assert.AreEqual("배치 0/5", c.Subline);
            Assert.IsNull(c.TierCode);
        }

        [Test]
        public void 시즌_최고를_모르면_뺀다()
        {
            var c = RankCardFormat.From(Rank(14, 47, peak: -1), D);
            Assert.AreEqual("승급까지 53 LP", c.Subline);
        }

        [Test]
        public void 응답_없으면_실패_카드()
        {
            var c = RankCardFormat.Failed();
            Assert.IsFalse(c.Loaded);
            Assert.AreEqual("랭크 정보를 불러오지 못했습니다", c.Headline);
            Assert.AreEqual(string.Empty, c.Subline);
        }
    }
}
