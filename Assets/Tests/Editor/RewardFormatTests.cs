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

        //  연속 날갯짓 고비 코인(spec 2026-10-28 §5) — 판 보상 말고 따로 들어온 코인은 합계에 넣고 괄호로 몫을 보인다.
        [Test]
        public void 콤보_코인은_합계에_넣고_괄호로_몫을_보인다()
        {
            var reward = new MatchRewardDto
            {
                coins = 25, xp = 120, reason = "ok", levelBefore = 3, levelAfter = 3,
                extras = new[] { new MatchRewardExtraDto { kind = "combo", coins = 7 } },
            };
            Assert.AreEqual("+32 코인 (콤보 +7) · +120 XP", LOP.UI.RewardFormat.Line(reward));
        }

        [Test]
        public void 첫_승이어도_같은_자리에_콤보_몫()
        {
            var reward = new MatchRewardDto
            {
                coins = 150, xp = 375, firstWin = true, reason = "ok", levelBefore = 1, levelAfter = 1,
                extras = new[] { new MatchRewardExtraDto { kind = "combo", coins = 7 } },
            };
            Assert.AreEqual("오늘 첫 승 ×3 · +157 코인 (콤보 +7) · +375 XP", LOP.UI.RewardFormat.Line(reward));
        }

        //  판 보상이 일일 상한으로 0이어도 콤보 상한은 따로라 콤보는 들어온다 — 줄이 숨으면 안 된다.
        [Test]
        public void 판_보상이_0이어도_콤보가_있으면_보인다()
        {
            var reward = new MatchRewardDto
            {
                coins = 0, xp = 312, reason = "ok", levelBefore = 9, levelAfter = 9,
                extras = new[] { new MatchRewardExtraDto { kind = "combo", coins = 12 } },
            };
            Assert.AreEqual("+12 코인 (콤보 +12) · +312 XP", LOP.UI.RewardFormat.Line(reward));
        }

        [Test]
        public void 영_이하_줄은_건너뛰고_모르는_종류는_보너스로_센다()
        {
            var reward = new MatchRewardDto
            {
                coins = 50, xp = 120, reason = "ok", levelBefore = 7, levelAfter = 7,
                extras = new[]
                {
                    new MatchRewardExtraDto { kind = "combo", coins = 0 },
                    null,
                    new MatchRewardExtraDto { kind = "quest", coins = 5 },
                },
            };
            Assert.AreEqual("+55 코인 (보너스 +5) · +120 XP", LOP.UI.RewardFormat.Line(reward));
        }

        [Test]
        public void 콤보_칸이_없거나_비면_지금_줄_그대로()
        {
            var none = new MatchRewardDto { coins = 50, xp = 120, reason = "ok", levelBefore = 7, levelAfter = 7 };
            var empty = new MatchRewardDto { coins = 50, xp = 120, reason = "ok", levelBefore = 7, levelAfter = 7, extras = new MatchRewardExtraDto[0] };
            Assert.AreEqual("+50 코인 · +120 XP", LOP.UI.RewardFormat.Line(none));
            Assert.AreEqual("+50 코인 · +120 XP", LOP.UI.RewardFormat.Line(empty));
        }
    }
}
