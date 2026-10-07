using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>프로필 — 캐주얼은 숨은 점수를 안 보이고, 랭크는 티어·LP로.</summary>
    public class ProfileRankTests
    {
        private static readonly LOP.MasterData.TbRankDivision D = TestRankTables.Divisions;

        [Test]
        public void 캐주얼_전적은_등수만()
        {
            var mine = new MatchHistoryParticipantDto { placement = 2, mmrBefore = 1000, mmrAfter = 1100 };
            Assert.AreEqual("2등", ProfileViewModel.MyResultText(mine, false, D));
        }

        [Test]
        public void 랭크_전적은_LP_증감()
        {
            var mine = new MatchHistoryParticipantDto
            {
                placement = 2,
                rank = new RankChangeDto { divisionBefore = 14, lpBefore = 45, divisionAfter = 14, lpAfter = 63, placementPlayed = 5, placementGames = 5 },
            };
            Assert.AreEqual("2등  +18 LP", ProfileViewModel.MyResultText(mine, false, D));
        }

        [Test]
        public void 나간_판은_나감()
        {
            var mine = new MatchHistoryParticipantDto { placement = 2, stats = new System.Collections.Generic.Dictionary<string, int> { [MatchStatKeys.Left] = 1 } };
            Assert.AreEqual("2등 · 나감", ProfileViewModel.MyResultText(mine, false, D));
        }

        [Test]
        public void 무승부()
        {
            Assert.AreEqual("무승부", ProfileViewModel.MyResultText(new MatchHistoryParticipantDto { placement = 1 }, true, D));
        }

        [Test]
        public void 랭크_칸은_요약과_시즌_최고()
        {
            var rank = new RankDto { divisionIndex = 14, lp = 45, placementPlayed = 5, placementGames = 5, peakDivisionIndex = 15, gamesPlayed = 12 };
            Assert.AreEqual("골드 II · 45 LP", ProfileViewModel.RankLineOf(rank, D));
            Assert.AreEqual("이번 시즌 최고: 골드 I", ProfileViewModel.PeakLineOf(rank, D));
        }

        [Test]
        public void 배치_중엔_최고_티어를_말하지_않는다()
        {
            var rank = new RankDto { divisionIndex = 9, lp = 40, placementPlayed = 2, placementGames = 5, peakDivisionIndex = 9 };
            Assert.AreEqual("배치 2/5", ProfileViewModel.RankLineOf(rank, D));
            Assert.AreEqual(string.Empty, ProfileViewModel.PeakLineOf(rank, D));
        }
    }
}
