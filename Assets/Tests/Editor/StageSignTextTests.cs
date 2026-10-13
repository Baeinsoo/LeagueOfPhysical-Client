using LOP;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>현수막·티포 글자 — 티어가 있으면 티어, 배치 전·못 받음이면 리그.</summary>
    public class StageSignTextTests
    {
        [Test]
        public void 티어가_있으면_티어_이름과_색()
        {
            var t = StageSignText.From(new RankCard(true, "gold", "골드 II · 47 LP", "승급까지 53 LP"));
            Assert.AreEqual("gold", t.TierCode);
            Assert.AreEqual("골드", t.TierLabel);
            Assert.AreEqual("GOLD", t.TierEnglish);
            Assert.AreEqual(TierPalette.ColorOf("gold"), t.TierColor);
        }

        [Test]
        public void 배치_전이면_리그_문양과_배치_중()
        {
            var t = StageSignText.From(new RankCard(true, null, "배치 전", "배치 0/5"));
            Assert.IsNull(t.TierCode);
            Assert.AreEqual("배치 중", t.TierLabel);
            Assert.AreEqual("LOP LEAGUE", t.TierEnglish);
            Assert.AreEqual(TierPalette.League, t.TierColor);
        }

        [Test]
        public void 못_받았거나_아직_안_왔어도_리그로()
        {
            Assert.AreEqual("LOP LEAGUE", StageSignText.From(RankCardFormat.Failed()).TierEnglish);
            Assert.AreEqual("LOP LEAGUE", StageSignText.From(default).TierEnglish);
        }
    }
}
