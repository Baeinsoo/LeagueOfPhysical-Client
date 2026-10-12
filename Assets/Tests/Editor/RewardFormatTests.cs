using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 결과 화면 보상 줄 — 보상이 0이어도(이제 서버가 reason을 담아 보낸다) 사유별 문구,
    /// 평범한 보상은 기존 포맷 그대로. <see cref="LOP.UI.RewardFormat.Line"/> 쪽 규칙.
    /// </summary>
    public class RewardFormatTests
    {
        [Test]
        public void 보상_자체가_없으면_빈_줄()
        {
            Assert.AreEqual(string.Empty, LOP.UI.RewardFormat.Line(null));
        }

        [Test]
        public void 너무_짧은_판은_사유를_말한다()
        {
            var reward = new MatchRewardDto { coins = 0, xp = 0, reason = "tooShort" };
            Assert.AreEqual("60초 미만 판은 보상이 없어요", LOP.UI.RewardFormat.Line(reward));
        }

        [Test]
        public void 중간에_나간_판은_사유를_말한다()
        {
            var reward = new MatchRewardDto { coins = 0, xp = 0, reason = "left" };
            Assert.AreEqual("중간에 나간 판은 보상이 없어요", LOP.UI.RewardFormat.Line(reward));
        }

        //  noPlayedSeconds는 운영 쪽 문제(플레이 시간 집계 실패)라 선수에게 알릴 말이 아니다 — 줄을 숨긴다.
        [Test]
        public void 집계_실패_사유는_말없이_숨긴다()
        {
            var reward = new MatchRewardDto { coins = 0, xp = 0, reason = "noPlayedSeconds" };
            Assert.AreEqual(string.Empty, LOP.UI.RewardFormat.Line(reward));
        }

        [Test]
        public void 정상_사유도_0이면_그냥_숨긴다()
        {
            var reward = new MatchRewardDto { coins = 0, xp = 0, reason = "ok" };
            Assert.AreEqual(string.Empty, LOP.UI.RewardFormat.Line(reward));
        }

        //  reason 필드 자체가 없는 옛 서버 응답(기본값 null)도 0 보상이면 그냥 숨긴다.
        [Test]
        public void 사유가_없어도_0이면_숨긴다()
        {
            var reward = new MatchRewardDto { coins = 0, xp = 0 };
            Assert.AreEqual(string.Empty, LOP.UI.RewardFormat.Line(reward));
        }

        [Test]
        public void 보상이_있으면_사유와_무관하게_평범한_줄()
        {
            var reward = new MatchRewardDto { coins = 50, xp = 120, reason = "ok", levelBefore = 7, levelAfter = 7 };
            Assert.AreEqual("+50 코인 · +120 XP", LOP.UI.RewardFormat.Line(reward));
        }
    }
}
