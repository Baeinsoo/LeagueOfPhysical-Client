using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LOP.UI;
using NUnit.Framework;
using R3;

namespace LOP.Tests
{
    /// <summary>결과판 VM v2 — 랭크(판 색)는 연 순간 정하고 응답이 와도 안 바꾼다. 부제·이름·랭크 문구.</summary>
    public class MatchResultViewModelV2Tests
    {
        private sealed class FakeUsers : IUserDataStore
        {
            public User user { get; set; }
            public UserProfile userProfile { get; set; }
            public ReadOnlyReactiveProperty<UserLocation> userLocation => null;
            public IReadOnlyDictionary<int, UserRating> userRatingByQueueId => null;
            public void Clear() { }
        }

        private sealed class FakeResults : IMatchResultDataStore
        {
            public MatchResult result { get; set; }
            public void Clear() { }
        }

        private static MatchResultViewModel Make(int storedQueue, int responseQueue, string myName = "재나르도#1234",
            string storedNameMe = "", string storedNameOther = "", RankChangeDto rank = null)
        {
            var results = new FakeResults
            {
                result = new MatchResult
                {
                    matchId = "M1", queueId = storedQueue,
                    participants = new[]
                    {
                        new MatchParticipantResult { userId = "me", placement = 1, stats = new Dictionary<string, int>(), displayName = storedNameMe },
                        new MatchParticipantResult { userId = "x", placement = 2, stats = new Dictionary<string, int>(), displayName = storedNameOther },
                    },
                },
            };
            var users = new FakeUsers { user = new User { id = "me", displayName = myName } };
            Func<string, string, CancellationToken, UniTask<GetMyMatchResponse>> fetch = (u, m, ct) =>
                UniTask.FromResult(new GetMyMatchResponse
                {
                    match = new MatchHistoryEntryDto
                    {
                        matchId = m, queueId = responseQueue,
                        participants = new[] { new MatchHistoryParticipantDto { userId = u, placement = 1, rank = rank } },
                    },
                });
            return new MatchResultViewModel(results, users, TestRankTables.Queues, TestRankTables.Divisions, null, null, fetch);
        }

        private static readonly RankChangeDto SameTier = new RankChangeDto
        {
            divisionBefore = 14, lpBefore = 45, divisionAfter = 14, lpAfter = 63, placementPlayed = 5, placementGames = 5,
        };

        [Test]
        public void 저장_큐가_랭크면_랭크판() => Assert.IsTrue(Make(storedQueue: 2, responseQueue: 2).IsRanked);

        [Test]
        public void 저장_큐가_0이면_일반판() => Assert.IsFalse(Make(storedQueue: 0, responseQueue: 2).IsRanked);

        [Test]
        public void 응답이_랭크여도_판_색은_안_바뀐다()
        {
            var vm = Make(storedQueue: 1, responseQueue: 2, rank: SameTier);
            Assert.IsFalse(vm.IsRanked);
            Assert.AreEqual("랭크", vm.RankCaption.CurrentValue.tag);   // 랭크 줄은 응답의 큐로
        }

        [Test]
        public void 랭크_판이면_랭크_문구()
        {
            var vm = Make(storedQueue: 2, responseQueue: 2, rank: SameTier);
            Assert.AreEqual(("랭크", "골드 II  45 → 63 LP (+18)"), vm.RankCaption.CurrentValue);
        }

        [Test]
        public void 저장_이름을_쓴다()
        {
            var vm = Make(1, 1, storedNameMe: "나의이름#1", storedNameOther: "상대#2");
            Assert.AreEqual(("나의이름#1", "상대#2"), (vm.Rows[0].DisplayName, vm.Rows[1].DisplayName));
        }

        [Test]
        public void 저장_이름이_비면_나는_내_이름_남은_플레이어_N()
        {
            var vm = Make(1, 1);
            Assert.AreEqual(("재나르도#1234", "플레이어 1"), (vm.Rows[0].DisplayName, vm.Rows[1].DisplayName));
        }

        [Test]
        public void 내_이름도_비면_나() => Assert.AreEqual("나", Make(1, 1, myName: "").Rows[0].DisplayName);

        [Test]
        public void 종목을_모르면_부제는_빈칸() => Assert.AreEqual(string.Empty, Make(1, 1).Subtitle.CurrentValue);

        [Test]
        public void 일반_판이면_랭크_문구는_빈칸() => Assert.AreEqual(string.Empty, Make(1, 1).RankCaption.CurrentValue.tag);
    }
}
