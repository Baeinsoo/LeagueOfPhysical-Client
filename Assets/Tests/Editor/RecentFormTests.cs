using LOP;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>최근 랭크 순위 — 랭크 판만, 최근 순, 최대 5, 내 순위.</summary>
    public class RecentFormTests
    {
        private static MatchHistoryEntryDto Match(int queueId, string endedAt, params (string user, int place)[] ps)
        {
            var parts = new MatchHistoryParticipantDto[ps.Length];
            for (int i = 0; i < ps.Length; i++) parts[i] = new MatchHistoryParticipantDto { userId = ps[i].user, placement = ps[i].place };
            return new MatchHistoryEntryDto { queueId = queueId, endedAt = endedAt, participants = parts };
        }

        [Test]
        public void 랭크_판만_최근_순으로_내_순위()
        {
            var ms = new[]
            {
                Match(2, "2026-10-10T10:00:00Z", ("me", 3), ("x", 1)),
                Match(1, "2026-10-12T10:00:00Z", ("me", 1)),   // 일반 — 빠진다
                Match(2, "2026-10-13T10:00:00Z", ("me", 2)),
                Match(2, "2026-10-11T10:00:00Z", ("x", 1)),    // 내가 없다 — 빠진다
            };
            CollectionAssert.AreEqual(new[] { 2, 3 }, RecentForm.From(ms, "me", rankedQueueId: 2, count: 5));
        }

        [Test]
        public void 다섯_개까지만()
        {
            var ms = new MatchHistoryEntryDto[7];
            for (int i = 0; i < 7; i++) ms[i] = Match(2, $"2026-10-0{i + 1}T00:00:00Z", ("me", i + 1));
            CollectionAssert.AreEqual(new[] { 7, 6, 5, 4, 3 }, RecentForm.From(ms, "me", 2, 5));
        }

        [Test]
        public void 비었거나_null이면_빈_목록()
        {
            Assert.AreEqual(0, RecentForm.From(null, "me", 2, 5).Count);
            Assert.AreEqual(0, RecentForm.From(new MatchHistoryEntryDto[0], "me", 2, 5).Count);
            Assert.AreEqual(0, RecentForm.From(new[] { new MatchHistoryEntryDto { queueId = 2 } }, "me", 2, 5).Count);
        }
    }
}
