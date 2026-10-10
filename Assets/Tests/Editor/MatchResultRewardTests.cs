using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using LOP.UI;
using NUnit.Framework;
using R3;

namespace LOP.Tests
{
    /// <summary>
    /// 결과 화면의 보상 줄 — GetMyMatch 조회(랭크 줄과 같은 경로) 한 번으로 채워진다.
    /// MatchEndedToC(게임서버 메시지)엔 보상이 없다 — 여기서 그 경로를 안 쓰는지도 함께 지킨다.
    /// </summary>
    public class MatchResultRewardTests
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
        private static MatchResultViewModel ViewModel(int queueOfMatch, MatchRewardDto reward)
        {
            var store = new FakeMatchResultDataStore
            {
                result = new MatchResult
                {
                    matchId = "M1",
                    participants = new[] { new MatchParticipantResult { userId = "me", placement = 1, stats = new Dictionary<string, int>() } },
                },
            };
            var users = new FakeUserDataStore { user = new User { id = "me" } };

            Func<string, string, CancellationToken, UniTask<GetMyMatchResponse>> fetch = (userId, matchId, ct) =>
                UniTask.FromResult(new GetMyMatchResponse
                {
                    match = new MatchHistoryEntryDto
                    {
                        matchId = matchId, queueId = queueOfMatch,
                        participants = new[] { new MatchHistoryParticipantDto { userId = userId, placement = 1, reward = reward } },
                    },
                });

            return new MatchResultViewModel(store, users, TestRankTables.Queues, TestRankTables.Divisions, fetch);
        }

        [Test]
        public void 평범한_보상은_코인과_XP_줄이_된다()
        {
            var vm = ViewModel(queueOfMatch: 2, reward: new MatchRewardDto { coins = 50, xp = 120, levelBefore = 7, levelAfter = 7 });
            Assert.AreEqual("+50 코인 · +120 XP", vm.RewardLine.CurrentValue);
        }

        [Test]
        public void 오늘_첫_승이면_3배_문구가_붙는다()
        {
            var vm = ViewModel(queueOfMatch: 2, reward: new MatchRewardDto { coins = 150, xp = 375, firstWin = true, levelBefore = 7, levelAfter = 7 });
            Assert.AreEqual("오늘 첫 승 ×3 · +150 코인 · +375 XP", vm.RewardLine.CurrentValue);
        }

        [Test]
        public void 레벨이_올랐으면_끝에_레벨_변화를_적는다()
        {
            var vm = ViewModel(queueOfMatch: 2, reward: new MatchRewardDto { coins = 50, xp = 120, levelBefore = 7, levelAfter = 8 });
            Assert.AreEqual("+50 코인 · +120 XP · Lv 7 → 8", vm.RewardLine.CurrentValue);
        }

        [Test]
        public void 보상이_없으면_줄을_숨긴다()
        {
            var vm = ViewModel(queueOfMatch: 2, reward: null);
            Assert.AreEqual(string.Empty, vm.RewardLine.CurrentValue);
        }

        [Test]
        public void 캐주얼_큐에서도_보상_줄은_뜨고_랭크_줄만_숨는다()
        {
            var vm = ViewModel(queueOfMatch: 1, reward: new MatchRewardDto { coins = 50, xp = 120, levelBefore = 7, levelAfter = 7 });
            Assert.AreEqual("+50 코인 · +120 XP", vm.RewardLine.CurrentValue);
            Assert.AreEqual(string.Empty, vm.RankLine.CurrentValue);
        }
    }
}
