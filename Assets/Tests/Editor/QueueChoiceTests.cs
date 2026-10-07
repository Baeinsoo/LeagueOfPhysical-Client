using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>로비의 일반/랭크 전환 — 요청에 무엇을 싣는가, 무엇을 기억하는가.</summary>
    public class QueueChoiceTests
    {
        private static readonly LOP.MasterData.TbQueue Q = TestRankTables.Queues;

        [Test]
        public void 일반은_고른_게임과_맵을_싣는다() =>
            Assert.AreEqual((1, 7, 70), QueueChoice.Request(QueueKind.Casual, 7, 70, Q));

        [Test]
        public void 랭크는_게임과_맵을_비운다_서버가_고른다() =>
            Assert.AreEqual((2, 0, 0), QueueChoice.Request(QueueKind.Ranked, 7, 70, Q));

        [Test]
        public void 큐_이름()
        {
            Assert.AreEqual("일반", QueueChoice.Name(QueueKind.Casual));
            Assert.AreEqual("랭크", QueueChoice.Name(QueueKind.Ranked));
        }

        [Test]
        public void 큐도_기억한다()
        {
            var store = new LastPlayedSelectionStore();
            try
            {
                store.SaveQueue(QueueKind.Ranked);
                Assert.IsTrue(store.TryLoadQueue(out var kind));
                Assert.AreEqual(QueueKind.Ranked, kind);
            }
            finally
            {
                UnityEngine.PlayerPrefs.DeleteKey(LastPlayedSelectionStore.QueueKey);
            }
        }

        [Test]
        public void 랭크로_해도_캐주얼_게임과_맵_기억은_그대로()
        {
            var store = new LastPlayedSelectionStore();
            bool had = store.TryLoad(out int keepGame, out int keepMap);
            try
            {
                MatchmakingViewModel.RememberPlay(store, QueueKind.Casual, 7, 70);
                MatchmakingViewModel.RememberPlay(store, QueueKind.Ranked, 0, 0);

                Assert.IsTrue(store.TryLoad(out int game, out int map));
                Assert.AreEqual((7, 70), (game, map));
                Assert.IsTrue(store.TryLoadQueue(out var kind));
                Assert.AreEqual(QueueKind.Ranked, kind);
            }
            finally
            {
                UnityEngine.PlayerPrefs.DeleteKey(LastPlayedSelectionStore.QueueKey);
                if (had) store.Save(keepGame, keepMap);
            }
        }

        [Test]
        public void 랭크_요약을_못_받으면_안내만()
        {
            var failed = MatchmakingViewModel.LoadRankSummary(
                ct => Cysharp.Threading.Tasks.UniTask.FromException<GetRankResponse>(new System.Exception("401")),
                TestRankTables.Divisions, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual("랭크 정보를 불러오지 못했습니다", failed);

            var empty = MatchmakingViewModel.LoadRankSummary(
                ct => Cysharp.Threading.Tasks.UniTask.FromResult(new GetRankResponse()),
                TestRankTables.Divisions, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual("랭크 정보를 불러오지 못했습니다", empty);
        }

        [Test]
        public void 랭크_요약을_받으면_티어()
        {
            var text = MatchmakingViewModel.LoadRankSummary(
                ct => Cysharp.Threading.Tasks.UniTask.FromResult(new GetRankResponse { rank = new RankDto { divisionIndex = 14, lp = 45, placementPlayed = 5, placementGames = 5 } }),
                TestRankTables.Divisions, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual("골드 II · 45 LP", text);
        }

        [Test]
        public void 기다린_시간_표기()
        {
            Assert.AreEqual("0:05", MatchingWaitingView.FormatElapsed(5));
            Assert.AreEqual("1:15", MatchingWaitingView.FormatElapsed(75));
        }
    }
}
