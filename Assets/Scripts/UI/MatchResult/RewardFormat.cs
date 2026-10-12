using System.Globalization;

namespace LOP.UI
{
    /// <summary>
    /// 결과 화면의 보상 줄 표기. "+50 코인 · +120 XP" / 오늘 첫 승이면 ×3 문구 / 레벨업했으면 끝에 Lv 표기.
    /// 보상 자체가 없던 사람(나감·너무 짧음·동결·지급 꺼짐)은 빈 문자열 — 줄을 숨긴다.
    /// 코인·XP가 0이면 사유에 따라 "너무 짧았다"/"나갔다"만 짧게 말하고, 그 밖의 사유(운영 쪽
    /// 집계 문제인 noPlayedSeconds 포함)는 선수에게 할 말이 없어 그냥 숨긴다.
    /// </summary>
    public static class RewardFormat
    {
        public static string Line(MatchRewardDto reward)
        {
            if (reward == null) return string.Empty;

            if (reward.coins == 0 && reward.xp == 0)
            {
                if (reward.reason == "tooShort") return "60초 미만 판은 보상이 없어요";
                if (reward.reason == "left") return "중간에 나간 판은 보상이 없어요";
                return string.Empty;
            }

            string coins = reward.coins.ToString("N0", CultureInfo.InvariantCulture);
            string xp = reward.xp.ToString("N0", CultureInfo.InvariantCulture);

            string line = reward.firstWin
                ? $"오늘 첫 승 ×3 · +{coins} 코인 · +{xp} XP"
                : $"+{coins} 코인 · +{xp} XP";

            if (reward.levelAfter > reward.levelBefore)
            {
                line += $" · Lv {reward.levelBefore} → {reward.levelAfter}";
            }

            return line;
        }
    }
}
