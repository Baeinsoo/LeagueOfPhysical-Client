using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>방송 자막의 랭크 줄 — 받기 전·못 받음에도 빈칸이나 "★ "만 남지 않는다.</summary>
    public class LowerThirdTextTests
    {
        [Test]
        public void 받은_랭크는_별과_함께()
        {
            var t = LowerThirdText.From(new RankCard(true, "gold", "골드 II · 47 LP", "승급까지 53 LP"));
            Assert.AreEqual("★ 골드 II · 47 LP", t.Rank);
            Assert.AreEqual("승급까지 53 LP", t.Sub);
        }

        [Test]
        public void 받기_전엔_확인_중()
        {
            var t = LowerThirdText.From(default);
            Assert.AreEqual("★ 랭크 확인 중", t.Rank);
            Assert.AreEqual(string.Empty, t.Sub);
        }

        [Test]
        public void 못_받으면_짧게()
        {
            var t = LowerThirdText.From(RankCardFormat.Failed());
            Assert.AreEqual("★ 랭크 정보 없음", t.Rank);
            Assert.AreEqual(string.Empty, t.Sub);
        }
    }
}
