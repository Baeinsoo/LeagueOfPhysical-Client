using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LOP.UI
{
    /// <summary>
    /// 퀘스트 문구(spec 2026-10-30 §7). 화면이 아니라 여기서 정해 시험이 고정한다.
    /// 글꼴에 −·⬆·⬇가 없다 — 쓰는 기호는 ·, ×, →, /, !와 이미 쓰는 🪙뿐.
    /// </summary>
    public static class QuestFormat
    {
        public const string Done = "완료!";

        public static string Progress(int progress, int target, string unit) =>
            unit == "minutes" ? $"{progress}/{target}분" : $"{progress}/{target}";

        public static float Ratio(int progress, int target) =>
            target <= 0 ? 0f : KitProgress.Clamp01((float)progress / target);

        public static string Reward(long coins, long xp)
        {
            var parts = new List<string>(2);
            if (coins > 0) parts.Add($"🪙 {Number(coins)}");
            if (xp > 0) parts.Add($"{Number(xp)} XP");
            return string.Join(" · ", parts);
        }

        public static string Tier(int tier) => tier switch
        {
            1 => "쉬움",
            2 => "보통",
            3 => "어려움",
            _ => string.Empty,
        };

        /// <summary>남은 시간. 시각은 둘 다 서버 기준 UTC(<see cref="QuestStore.ServerNow"/>) — 기기 시계를 믿지 않는다.</summary>
        public static string Remaining(DateTime endsAtUtc, DateTime nowUtc)
        {
            var left = endsAtUtc - nowUtc;
            if (left.TotalMinutes < 1) return "곧 바뀌어요";
            if (left.TotalDays >= 1) return $"{(int)left.TotalDays}일 {left.Hours}시간 남음";
            if (left.TotalHours >= 1) return $"{left.Hours}시간 {left.Minutes}분 남음";
            return $"{left.Minutes}분 남음";
        }

        public static string Rerolls(int left) => left > 0 ? $"바꾸기 {left}번 남음" : "오늘 바꾸기를 다 썼어요";

        public static string RerollMessage(int code) => code switch
        {
            ResponseCode.SUCCESS => string.Empty,
            ResponseCode.QUEST_REROLL_UNAVAILABLE => "오늘은 더 바꿀 수 없어요",
            ResponseCode.QUEST_ALREADY_COMPLETED => "끝난 퀘스트는 바꿀 수 없어요",
            ResponseCode.QUEST_NOT_FOUND => "퀘스트가 바뀌어 다시 불러왔어요",
            ResponseCode.ECONOMY_DISABLED => "퀘스트를 쉬고 있어요",
            _ => "연결이 불안정해요. 다시 해 주세요",
        };

        public static string PeriodLabel(string period) => period == "weekly" ? "이번 주" : "오늘";

        /// <summary>
        /// 결과 화면 "퀘스트" 줄 — 이번 판에 오른 칸. 끝난 칸이 먼저, 같으면 오늘이 이번 주보다 먼저(그 안은 서버 순서).
        /// 넘치면 마지막 자리를 "외 N개"로 쓴다.
        /// </summary>
        public static IReadOnlyList<string> ResultLines(MatchQuestProgressDto[] quests, int max = 3)
        {
            if (quests == null) return Array.Empty<string>();
            var lines = quests
                .Where(q => q != null)
                .OrderBy(q => q.completed ? 0 : 1)
                .ThenBy(q => q.period == "weekly" ? 1 : 0)
                .Select(q => $"{PeriodLabel(q.period)} · {q.title} {Progress(q.after, q.target, q.unit)}" + (q.completed ? $" {Done}" : string.Empty))
                .ToList();
            if (lines.Count <= max) return lines;
            var cut = lines.Take(max - 1).ToList();
            cut.Add($"외 {lines.Count - (max - 1)}개");
            return cut;
        }

        private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
