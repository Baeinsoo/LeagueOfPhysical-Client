using System;
using GameFramework.Runner;
using MessagePipe;
using NUnit.Framework;
using UnityEngine;
using VContainer.Unity;

namespace LOP.Tests
{
    /// <summary>
    /// 매치 종료 배선(final-review T3, 빠진 배선 테스트): <see cref="MatchEndedMessageHandler"/>가 매치 종료 때 기록지
    /// (<see cref="MatchRecordSheet.Commit"/>)를 확정해 결과(<see cref="MatchResult.myRecords"/>)에 싣는지 지킨다.
    /// 이 배선이 빠지거나(<see cref="MatchResultCapture.From"/>을 <c>null</c>로 바꾸거나 <c>Commit()</c>을 지워도) 이 테스트가
    /// 없으면 모두 초록이었다 — <c>Assets/Tests</c>에서 <see cref="MatchEndedMessageHandler"/>를 참조하는 곳이 없었다.
    /// </summary>
    public class MatchEndedMessageHandlerTests
    {
        private sealed class FakeSubscriber : ISubscriber<MatchEndedToC>
        {
            public IMessageHandler<MatchEndedToC> Handler;

            public IDisposable Subscribe(IMessageHandler<MatchEndedToC> handler, params MessageHandlerFilter<MatchEndedToC>[] filters)
            {
                Handler = handler;
                return new Subscription();
            }

            private sealed class Subscription : IDisposable
            {
                public void Dispose() { }
            }
        }

        private sealed class FakeRoomDataStore : IRoomDataStore
        {
            public Room room { get; set; }
            public Match match { get; set; }
            public void Clear() { }
        }

        private sealed class FakeMatchResultDataStore : IMatchResultDataStore
        {
            public MatchResult result { get; set; }
            public void Clear() { }
        }

        private GameObject runnerGo;

        [TearDown]
        public void TearDown()
        {
            if (runnerGo != null) { UnityEngine.Object.DestroyImmediate(runnerGo); }
        }

        private static MatchEndedToC Message()
        {
            var m = new MatchEndedToC();
            m.Placements.Add(new MatchPlacementInfo { UserId = "me", Placement = 1 });
            return m;
        }

        private (MatchEndedMessageHandler handler, FakeSubscriber subscriber, FakeRoomDataStore room,
                  FakeMatchResultDataStore result, MatchRecordSheet sheet, LOPRunner runner, FakePersonalRecordStore store) Make()
        {
            var subscriber = new FakeSubscriber();
            var roomDataStore = new FakeRoomDataStore
            {
                room = new Room { matchId = "M1" },
                match = new Match { rounds = new[] { new MatchRound { gameModeId = 6, mapId = 2 } } },
            };
            var matchResultDataStore = new FakeMatchResultDataStore();
            var store = new FakePersonalRecordStore();
            var sheet = new MatchRecordSheet(() => (6, 2), store);
            sheet.Set(FlappyRecordMetrics.FlapStreak, 14d);

            runnerGo = new GameObject("Runner_test");
            var runner = runnerGo.AddComponent<LOPRunner>();

            var handler = new MatchEndedMessageHandler(runner, roomDataStore, matchResultDataStore, subscriber, sheet);
            return (handler, subscriber, roomDataStore, matchResultDataStore, sheet, runner, store);
        }

        [Test]
        public void 기록지의_확정_결과를_결과에_싣는다()
        {
            var (handler, subscriber, _, result, sheet, _, _) = Make();
            ((IInitializable)handler).Initialize();

            subscriber.Handler.Handle(Message());

            Assert.IsTrue(sheet.Committed, "매치 종료 때 기록지가 확정된다");
            Assert.AreEqual(1, result.result.myRecords.Count);
            Assert.AreSame(sheet.Commit(), result.result.myRecords, "확정된 목록 그대로를 결과에 싣는다");
        }

        [Test]
        public void 러너를_종료_상태로_보낸다()
        {
            var (handler, subscriber, _, _, _, runner, _) = Make();
            ((IInitializable)handler).Initialize();

            subscriber.Handler.Handle(Message());

            Assert.AreEqual(RunnerState.GameOver, runner.gameState);
        }

        [Test]
        public void 매치_id와_점수_변화도_결과에_싣는다()
        {
            var (handler, subscriber, _, result, _, _, _) = Make();
            ((IInitializable)handler).Initialize();

            var message = Message();
            message.HasRatingChange = true;
            message.MyMmrAfter = 1500;
            subscriber.Handler.Handle(message);

            Assert.AreEqual("M1", result.result.matchId);
            Assert.IsTrue(result.result.hasRatingChange);
            Assert.AreEqual(1500, result.result.myMmrAfter);
        }
    }
}
