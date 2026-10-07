using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LOP.UI;
using NUnit.Framework;
using R3;

namespace LOP.Tests
{
    /// <summary>결과 화면의 랭크 줄 — 캐주얼은 점수를 안 보이고, 랭크는 매치 조회가 도착하면 티어·LP 줄이 된다.</summary>
    public class MatchResultRankTests
    {
        private sealed class FakeUserDataStore : IUserDataStore
        {
            public User user { get; set; }
            public UserProfile userProfile { get; set; }
            public ReadOnlyReactiveProperty<UserLocation> userLocation => null;
            public IReadOnlyDictionary<int, UserRating> userRatingByQueueId => null;
            public void Clear() { }
        }

        private sealed class FakeMatchResultDataStore : IMatchResultDataStore
        {
            public MatchResult result { get; set; }
            public void Clear() { }
        }

        //  가짜 조회는 이미 끝난 UniTask를 돌려준다 — 생성자 안에서 바로 줄이 채워진다.
        private static MatchResultViewModel ViewModel(int queueOfMatch, RankChangeDto rank, bool fail = false)
        {
            var store = new FakeMatchResultDataStore
            {
                result = new MatchResult
                {
                    matchId = "M1",
                    participants = new[] { new MatchParticipantResult { userId = "me", placement = 1, stats = new Dictionary<string, int>() } },
                    hasRatingChange = true, myMmrBefore = 1000, myMmrAfter = 1138,
                },
            };
            var users = new FakeUserDataStore { user = new User { id = "me" } };

            Func<string, string, CancellationToken, UniTask<GetMyMatchResponse>> fetch = (userId, matchId, ct) =>
                fail
                    ? UniTask.FromException<GetMyMatchResponse>(new Exception("down"))
                    : UniTask.FromResult(new GetMyMatchResponse
                    {
                        match = new MatchHistoryEntryDto
                        {
                            matchId = matchId, queueId = queueOfMatch,
                            participants = new[] { new MatchHistoryParticipantDto { userId = userId, placement = 1, rank = rank } },
                        },
                    });

            return new MatchResultViewModel(store, users, TestRankTables.Queues, TestRankTables.Divisions, fetch);
        }

        [Test]
        public void 캐주얼은_점수_줄이_없다()
        {
            var vm = ViewModel(queueOfMatch: 1, rank: null);
            Assert.AreEqual(string.Empty, vm.RankLine.CurrentValue);
        }

        [Test]
        public void 랭크_결과는_도착하면_줄이_된다()
        {
            var change = new RankChangeDto { divisionBefore = 14, lpBefore = 45, divisionAfter = 14, lpAfter = 63, placementPlayed = 5, placementGames = 5 };
            var vm = ViewModel(queueOfMatch: 2, rank: change);
            Assert.AreEqual("골드 II 45 → 63 LP (+18)", vm.RankLine.CurrentValue);
        }

        [Test]
        public void 랭크_결과를_못_받으면_줄을_숨긴다()
        {
            var vm = ViewModel(queueOfMatch: 2, rank: null, fail: true);
            Assert.AreEqual(string.Empty, vm.RankLine.CurrentValue);
        }

        [Test]
        public void 배치_마지막_판은_완료_문구()
        {
            var change = new RankChangeDto { divisionBefore = 10, lpBefore = 20, divisionAfter = 12, lpAfter = 5, placementPlayed = 5, placementGames = 5, wasPlacement = true };
            var vm = ViewModel(queueOfMatch: 2, rank: change);
            Assert.AreEqual("배치 완료: 골드 IV", vm.RankLine.CurrentValue);
        }
    }
}
