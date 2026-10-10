using System.Globalization;

namespace LOP.UI
{
    /// <summary>
    /// 결과 화면의 보상 줄 표기. "+50 코인 · +120 XP" / 오늘 첫 승이면 ×3 문구 / 레벨업했으면 끝에 Lv 표기.
    /// 보상 자체가 없던 사람(나감·너무 짧음·동결·지급 꺼짐)은 빈 문자열 — 줄을 숨긴다.
    /// </summary>
    public static class RewardFormat
    {
        public static string Line(MatchRewardDto reward)
        {
            if (reward == null) return string.Empty;

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
