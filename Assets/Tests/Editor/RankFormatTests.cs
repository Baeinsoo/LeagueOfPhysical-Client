using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>랭크 표기 — 로비·대기·프로필·결과가 같은 말을 쓰도록 한 곳에서 만든다.</summary>
    public class RankFormatTests
    {
        private static readonly LOP.MasterData.TbRankDivision D = TestRankTables.Divisions;

        private static RankDto Rank(int division, int lp, int played = 5) => new RankDto
        {
            divisionIndex = division, lp = lp, placementPlayed = played, placementGames = 5,
        };

        [Test] public void 단계_이름은_티어와_로마_숫자() => Assert.AreEqual("골드 II", RankFormat.DivisionName(14, D));
        [Test] public void 마스터는_숫자가_없다() => Assert.AreEqual("마스터", RankFormat.DivisionName(28, D));
        [Test] public void 모르는_단계는_물음표() => Assert.AreEqual("?", RankFormat.DivisionName(99, D));

        [Test] public void 배치_전() => Assert.AreEqual("배치 전", RankFormat.Summary(Rank(-1, 0, 0), D));
        [Test] public void 배치_중() => Assert.AreEqual("배치 2/5", RankFormat.Summary(Rank(9, 40, 2), D));
        [Test] public void 배치_끝() => Assert.AreEqual("골드 II · 45 LP", RankFormat.Summary(Rank(14, 45), D));

        [Test]
        public void 같은_단계_안이면_단계를_한_번만()
        {
            var c = new RankChangeDto { divisionBefore = 14, lpBefore = 45, divisionAfter = 14, lpAfter = 63, placementPlayed = 5, placementGames = 5 };
            Assert.AreEqual("골드 II 45 → 63 LP (+18)", RankFormat.ResultLine(c, D));
        }

        [Test]
        public void 단계가_바뀌면_앞뒤_단계와_증감()
        {
            var c = new RankChangeDto { divisionBefore = 14, lpBefore = 45, divisionAfter = 15, lpAfter = 3, placementPlayed = 5, placementGames = 5 };
            Assert.AreEqual("골드 II 45 → 골드 I 3 LP (+58)", RankFormat.ResultLine(c, D));
        }

        [Test]
        public void 승급()
        {
            var c = new RankChangeDto { divisionBefore = 11, lpBefore = 90, divisionAfter = 12, lpAfter = 10, placementPlayed = 5, placementGames = 5, promoted = true };
            Assert.AreEqual("승급! 실버 I → 골드 IV (+20 LP)", RankFormat.ResultLine(c, D));
        }

        [Test]
        public void 강등()
        {
            var c = new RankChangeDto { divisionBefore = 12, lpBefore = 5, divisionAfter = 11, lpAfter = 75, placementPlayed = 5, placementGames = 5, demoted = true };
            Assert.AreEqual("강등 골드 IV → 실버 I (-30 LP)", RankFormat.ResultLine(c, D));
        }

        [Test]
        public void 배치_첫_판은_앞_단계가_없다()
        {
            var c = new RankChangeDto { divisionBefore = null, lpBefore = 0, divisionAfter = 4, lpAfter = 80, placementPlayed = 1, placementGames = 5, wasPlacement = true };
            Assert.AreEqual("배치 1/5 · 브론즈 IV 80 LP", RankFormat.ResultLine(c, D));
        }

        [Test]
        public void 배치_마지막_판은_완료_문구()
        {
            var c = new RankChangeDto { divisionBefore = 10, lpBefore = 20, divisionAfter = 12, lpAfter = 5, placementPlayed = 5, placementGames = 5, wasPlacement = true };
            Assert.AreEqual("배치 완료: 골드 IV", RankFormat.ResultLine(c, D));
        }

        [Test]
        public void 배치가_끝난_뒤의_판은_평소_줄()
        {
            var c = new RankChangeDto { divisionBefore = 12, lpBefore = 5, divisionAfter = 12, lpAfter = 29, placementPlayed = 5, placementGames = 5, wasPlacement = false };
            Assert.AreEqual("골드 IV 5 → 29 LP (+24)", RankFormat.ResultLine(c, D));
        }

        [Test]
        public void 증감_표기()
        {
            Assert.AreEqual("-7 LP", RankFormat.LpDelta(new RankChangeDto { divisionBefore = 12, lpBefore = 10, divisionAfter = 12, lpAfter = 3 }));
            Assert.AreEqual("+80 LP", RankFormat.LpDelta(new RankChangeDto { divisionBefore = null, divisionAfter = 4, lpAfter = 80 }));
        }

        [Test] public void 티어_색_클래스() => Assert.AreEqual("rank-tier--gold", RankFormat.TierClass(14, D));
    }
}
