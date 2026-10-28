using System.Collections.Generic;
using System.Globalization;

namespace LOP.UI
{
    /// <summary>
    /// 결과 화면의 보상 줄 표기. "+50 코인 · +120 XP" / 오늘 첫 승이면 ×3 문구 / 레벨업했으면 끝에 Lv 표기.
    /// 판 보상 말고 따로 들어온 코인(<see cref="MatchRewardDto.extras"/> — 연속 날갯짓 고비 코인 등)은 코인 합계에 넣고
    /// 괄호로 몫을 보인다: "+32 코인 (콤보 +7) · +120 XP".
    /// 보상 자체가 없던 사람(나감·너무 짧음·동결·지급 꺼짐)은 빈 문자열 — 줄을 숨긴다.
    /// 코인·XP가 0이면 사유에 따라 "너무 짧았다"/"나갔다"만 짧게 말하고, 그 밖의 사유(운영 쪽
    /// 집계 문제인 noPlayedSeconds 포함)는 선수에게 할 말이 없어 그냥 숨긴다.
    /// 판 보상·XP가 둘 다 0이어도 콤보 몫이 있으면 줄은 보이되(위 숨김 조건에 걸리지 않음), XP는
    /// "+0 XP"로 할 말이 없는 숫자라 그 칸 자체를 생략한다 — 코인·콤보 몫만 보인다(최종 리뷰 I3).
    /// </summary>
    public static class RewardFormat
    {
        public static string Line(MatchRewardDto reward)
        {
            if (reward == null) return string.Empty;

            long extraCoins = 0;
            var extraParts = new List<string>();
            if (reward.extras != null)
            {
                foreach (var extra in reward.extras)
                {
                    if (extra == null || extra.coins <= 0) continue;
                    extraCoins += extra.coins;
                    extraParts.Add($"{ExtraLabel(extra.kind)} +{Number(extra.coins)}");
                }
            }

            if (reward.coins == 0 && reward.xp == 0 && extraCoins == 0)
            {
                if (reward.reason == "tooShort") return "60초 미만 판은 보상이 없어요";
                if (reward.reason == "left") return "중간에 나간 판은 보상이 없어요";
                return string.Empty;
            }

            string coins = $"+{Number(reward.coins + extraCoins)} 코인";
            if (extraParts.Count > 0)
            {
                coins += $" ({string.Join(" · ", extraParts)})";
            }

            //  XP가 0이고 콤보 등 extras로만 줄이 보이는 경우 "+0 XP"는 할 말이 없어 칸을 생략한다.
            //  xp != 0이면(기존 동작) 그대로 보인다.
            string line;
            if (reward.xp == 0 && extraParts.Count > 0)
            {
                line = reward.firstWin ? $"오늘 첫 승 ×3 · {coins}" : coins;
            }
            else
            {
                string xp = $"+{Number(reward.xp)} XP";
                line = reward.firstWin
                    ? $"오늘 첫 승 ×3 · {coins} · {xp}"
                    : $"{coins} · {xp}";
            }

            if (reward.levelAfter > reward.levelBefore)
            {
                line += $" · Lv {reward.levelBefore} → {reward.levelAfter}";
            }

            return line;
        }

        //  모르는 종류(새 서버가 앱보다 먼저 나간 경우)도 합계엔 넣는다 — 지갑엔 이미 들어가 있다.
        private static string ExtraLabel(string kind) => kind == "combo" ? "콤보" : "보너스";

        private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
