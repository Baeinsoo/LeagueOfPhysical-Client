using System.Collections.Generic;
using LOP.UI;
using NUnit.Framework;
using R3;

namespace LOP.Tests
{
    /// <summary>
    /// 모드별 결과 점수 자루(stats)가 결과 화면/프로필 전적 줄에 어떻게 반영되는지 고정한다.
    /// 자루가 비어 오는 모드(플랩왕 등)와 값이 채워져 오는 모드(활쏘기)를 반드시 갈라야 한다 —
    /// "점수 없음"과 "0점"은 다른 사실이다.
    /// </summary>
    public class MatchResultScoreTests
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

        private static MatchResultViewModel ViewModel(MatchParticipantResult[] participants, string myUserId)
        {
            var matchResultDataStore = new FakeMatchResultDataStore
            {
                result = new MatchResult { participants = participants },
            };
            var userDataStore = new FakeUserDataStore { user = new User { id = myUserId } };
            return new MatchResultViewModel(matchResultDataStore, userDataStore);
        }

        // ── ExtractScore: 빈 자루 ≠ 0점 ─────────────────────────────────────

        [Test]
        public void 자루가_null이면_점수가_없다()
        {
            var (hasScore, score, gained, lost) = MatchResultViewModel.ExtractScore(null);

            Assert.IsFalse(hasScore);
            Assert.AreEqual(0, score);
            Assert.AreEqual(0, gained);
            Assert.AreEqual(0, lost);
        }

        [Test]
        public void 자루가_비어있으면_점수가_없다()
        {
            //  플랩왕·스카이다이브·판치기처럼 점수 개념이 없는 모드는 빈 사전으로 온다.
            var (hasScore, _, _, _) = MatchResultViewModel.ExtractScore(new Dictionary<string, int>());

            Assert.IsFalse(hasScore);
        }

        [Test]
        public void 자루에_점수가_있으면_점수와_획득_벌점을_뽑는다()
        {
            var stats = new Dictionary<string, int>
            {
                { ArcheryStatKeys.Score, 12 },
                { ArcheryStatKeys.Gained, 15 },
                { ArcheryStatKeys.Lost, 3 },
            };

            var (hasScore, score, gained, lost) = MatchResultViewModel.ExtractScore(stats);

            Assert.IsTrue(hasScore);
            Assert.AreEqual(12, score);
            Assert.AreEqual(15, gained);
            Assert.AreEqual(3, lost);
        }

        // ── FormatScore: 벌점 0이면 내역을 안 보여준다 ──────────────────────

        [Test]
        public void 벌점이_0이면_점수만_적는다()
        {
            Assert.AreEqual("15점", MatchResultViewModel.FormatScore(score: 15, gained: 15, lost: 0));
        }

        [Test]
        public void 벌점이_있으면_획득과_벌점을_같이_적는다()
        {
            Assert.AreEqual("12점 (획득 15 · 벌점 3)", MatchResultViewModel.FormatScore(score: 12, gained: 15, lost: 3));
        }

        // ── ViewModel 통합: 줄에 자루가 그대로 반영된다 ─────────────────────

        [Test]
        public void 빈_자루로_온_참가자는_줄에서도_점수가_없다()
        {
            var participants = new[]
            {
                new MatchParticipantResult { userId = "me", placement = 1, stats = new Dictionary<string, int>() },
                new MatchParticipantResult { userId = "other", placement = 2, stats = new Dictionary<string, int>() },
            };

            var vm = ViewModel(participants, "me");

            Assert.AreEqual(2, vm.Rows.Count);
            foreach (var row in vm.Rows)
            {
                Assert.IsFalse(row.HasScore, "점수 없는 모드는 어느 줄도 HasScore가 true면 안 된다");
            }
        }

        [Test]
        public void 자루가_채워진_참가자는_줄에_점수가_붙는다()
        {
            var participants = new[]
            {
                new MatchParticipantResult
                {
                    userId = "me", placement = 1,
                    stats = new Dictionary<string, int>
                    {
                        { ArcheryStatKeys.Score, 20 },
                        { ArcheryStatKeys.Gained, 20 },
                        { ArcheryStatKeys.Lost, 0 },
                    },
                },
            };

            var vm = ViewModel(participants, "me");

            Assert.AreEqual(1, vm.Rows.Count);
            var row = vm.Rows[0];
            Assert.IsTrue(row.HasScore);
            Assert.AreEqual(20, row.Score);
            Assert.AreEqual(20, row.Gained);
            Assert.AreEqual(0, row.Lost);
        }

        [Test]
        public void 참가자마다_다른_자루_상태를_각자_들고_간다()
        {
            //  한 판 안에서도 참가자별로 자루가 다를 수 있다 — 한쪽 값을 보고 다른 쪽을 유추하면 안 된다.
            var participants = new[]
            {
                new MatchParticipantResult
                {
                    userId = "me", placement = 1,
                    stats = new Dictionary<string, int> { { ArcheryStatKeys.Score, 10 } },
                },
                new MatchParticipantResult { userId = "other", placement = 2, stats = new Dictionary<string, int>() },
            };

            var vm = ViewModel(participants, "me");
            var byPlacement = new Dictionary<int, MatchResultRow>();
            foreach (var row in vm.Rows) byPlacement[row.Placement] = row;

            Assert.IsTrue(byPlacement[1].HasScore);
            Assert.IsFalse(byPlacement[2].HasScore);
        }

        // ── 기존 단언 보존: 동점 판정·정렬 결정론 ───────────────────────────

        [Test]
        public void 동점끼리는_userId_서수비교로_순서가_고정된다()
        {
            var participants = new[]
            {
                new MatchParticipantResult { userId = "zzz", placement = 1, stats = new Dictionary<string, int>() },
                new MatchParticipantResult { userId = "aaa", placement = 1, stats = new Dictionary<string, int>() },
            };

            var vm1 = ViewModel(participants, "zzz");
            var vm2 = ViewModel(participants, "zzz");

            //  실행마다 같은 순서가 나와야 한다(비결정적 정렬이면 화면이 매번 흔들린다).
            Assert.AreEqual(vm1.Rows[0].DisplayName, vm2.Rows[0].DisplayName);
            Assert.AreEqual(vm1.Rows[1].DisplayName, vm2.Rows[1].DisplayName);
            Assert.IsTrue(vm1.IsDraw, "1등이 둘이면 무승부다");
        }
    }
}
