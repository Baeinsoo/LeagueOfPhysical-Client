using System;
using System.Collections.Generic;
using System.Globalization;

namespace LOP.UI
{
    /// <summary>최근 랭크 판의 내 순위(최근 것부터). LOP는 여러 명이 겨루는 순위 게임이라 승/패 대신 순위 숫자다.</summary>
    public static class RecentForm
    {
        public static IReadOnlyList<int> From(MatchHistoryEntryDto[] matches, string userId, int rankedQueueId, int count)
        {
            var picked = new List<(DateTimeOffset at, int place)>();
            if (matches == null || string.IsNullOrEmpty(userId)) return Array.Empty<int>();
            foreach (var m in matches)
            {
                if (m == null || m.queueId != rankedQueueId || m.participants == null) continue;
                foreach (var p in m.participants)
                {
                    if (p == null || p.userId != userId) continue;
                    DateTimeOffset.TryParse(m.endedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at);
                    picked.Add((at, p.placement));
                    break;
                }
            }
            picked.Sort((a, b) => b.at.CompareTo(a.at));
            var result = new List<int>();
            for (int i = 0; i < picked.Count && i < count; i++) result.Add(picked[i].place);
            return result;
        }
    }
}
