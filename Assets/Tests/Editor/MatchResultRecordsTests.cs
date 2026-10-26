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
    /// 결과판 "내 기록" 카드(spec §7, 종목 공용): 실린 기록마다 "이름 값 차이" 한 줄, 경신한 줄의 차이만 초록, 하나라도 깼으면 도장.
    /// 기록이 없으면 빈 카드(결과판은 카드를 안 띄운다).
    /// </summary>
    public class MatchResultRecordsTests
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

        private static readonly RecordOutcome FasterTime = new RecordOutcome(FlappyRecordMetrics.FinishTime, 52.04d, true, true, 52.4d);
        private static readonly RecordOutcome FirstStreak = new RecordOutcome(FlappyRecordMetrics.FlapStreak, 18d, true, false, 0d);
        private static readonly RecordOutcome SlowerTime = new RecordOutcome(FlappyRecordMetrics.FinishTime, 53d, true, true, 52.4d);

        private static MatchResultViewModel Make(IReadOnlyList<RecordOutcome> records)
        {
            var results = new FakeResults
            {
                result = new MatchResult
                {
                    matchId = "M1",
                    participants = new[] { new MatchParticipantResult { userId = "me", placement = 1, stats = new Dictionary<string, int>() } },
                    myRecords = records,
                },
            };
            Func<string, string, CancellationToken, UniTask<GetMyMatchResponse>> fetch = (u, m, ct) => UniTask.FromResult<GetMyMatchResponse>(null);
            return new MatchResultViewModel(results, new FakeUsers { user = new User { id = "me" } },
                TestRankTables.Queues, TestRankTables.Divisions, null, null, fetch);
        }

        [Test]
        public void 지표마다_이름_값_차이_한_줄()
        {
            var lines = RecordCard.Lines(new[] { FasterTime, FirstStreak });

            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual(("완주 시간", "0:52.0", "-0.36초"), (lines[0].Label, lines[0].ValueText, lines[0].DiffText));
            Assert.AreEqual(("연속 날갯짓 최고", "18회", "첫 기록"), (lines[1].Label, lines[1].ValueText, lines[1].DiffText));
        }

        [Test]
        public void 경신한_줄만_초록()
        {
            var lines = RecordCard.Lines(new[] { FasterTime, FirstStreak, SlowerTime });

            Assert.IsTrue(lines[0].Improved);
            Assert.IsFalse(lines[1].Improved, "첫 기록은 보상(초록)이 아니다");
            Assert.IsFalse(lines[2].Improved);
        }

        [Test]
        public void 기록이_없으면_빈_카드()
        {
            Assert.AreEqual(0, RecordCard.Lines(null).Count);
            Assert.AreEqual(0, RecordCard.Lines(Array.Empty<RecordOutcome>()).Count);
            Assert.IsFalse(RecordCard.AnyNewBest(null));
        }

        [Test]
        public void 결과의_내_기록을_카드_줄로()
        {
            using var vm = Make(new[] { FasterTime, FirstStreak });

            Assert.AreEqual(2, vm.Records.Count);
            Assert.IsTrue(vm.HasNewBest);
        }

        [Test]
        public void 경신이_없으면_도장도_없다()
        {
            using var vm = Make(new[] { FirstStreak, SlowerTime });

            Assert.AreEqual(2, vm.Records.Count);
            Assert.IsFalse(vm.HasNewBest);
        }
    }
}
