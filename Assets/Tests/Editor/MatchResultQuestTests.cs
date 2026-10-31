using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LOP.UI;
using NUnit.Framework;
using R3;

namespace LOP.Tests
{
    /// <summary>결과 화면 "퀘스트" 줄(spec 2026-10-30 §7) — 보상 줄과 같은 조회(GetMyMatch)의 reward.quests로 채운다.</summary>
    public class MatchResultQuestTests
    {
        private sealed class FakeUsers : IUserDataStore
        {
            public User user { get; set; } = new User { id = "me" };
            public UserProfile userProfile { get; set; }
            public ReadOnlyReactiveProperty<UserLocation> userLocation => null;
            public IReadOnlyDictionary<int, UserRating> userRatingByQueueId => null;
            public void Clear() { }
        }

        private sealed class FakeResults : IMatchResultDataStore
        {
            public MatchResult result { get; set; } = new MatchResult
            {
                matchId = "M1",
                participants = new[] { new MatchParticipantResult { userId = "me", placement = 1, stats = new Dictionary<string, int>() } },
            };
            public void Clear() { }
        }

        private static MatchResultViewModel ViewModel(MatchRewardDto reward) =>
            new MatchResultViewModel(new FakeResults(), new FakeUsers(), TestRankTables.Queues, TestRankTables.Divisions,
                (userId, matchId, ct) => UniTask.FromResult(new GetMyMatchResponse
                {
                    match = new MatchHistoryEntryDto
                    {
                        matchId = matchId, queueId = 1,
                        participants = new[] { new MatchHistoryParticipantDto { userId = userId, placement = 1, reward = reward } },
                    },
                }));

        [Test]
        public void 오른_칸을_줄로_완료가_먼저()
        {
            var vm = ViewModel(new MatchRewardDto
            {
                coins = 25, xp = 120, reason = "ok",
                quests = new[]
                {
                    new MatchQuestProgressDto { id = "a", period = "daily", title = "아무 종목 4판 하기", unit = "count", after = 3, target = 4 },
                    new MatchQuestProgressDto { id = "b", period = "weekly", title = "1위 3번 하기", unit = "count", after = 3, target = 3, completed = true },
                },
            });

            CollectionAssert.AreEqual(new[] { "이번 주 · 1위 3번 하기 3/3 완료!", "오늘 · 아무 종목 4판 하기 3/4" }, vm.QuestLines.CurrentValue);
        }

        [Test]
        public void 오른_칸이_없으면_빈_목록()
        {
            Assert.AreEqual(0, ViewModel(new MatchRewardDto { coins = 25, xp = 120, reason = "ok" }).QuestLines.CurrentValue.Count);
            Assert.AreEqual(0, ViewModel(null).QuestLines.CurrentValue.Count);
        }
    }
}
