using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>결과 저장 — 경기 끝 순간 아는 큐·종목·맵과 서버가 실어 준 이름·룩을 함께 남긴다(결과 단계가 방 없이 그린다).</summary>
    public class MatchResultCaptureTests
    {
        private static MatchEndedToC Message()
        {
            var m = new MatchEndedToC { HasRatingChange = true, MyMmrBefore = 1, MyMmrAfter = 2 };
            var a = new MatchPlacementInfo { UserId = "a", Placement = 1, DisplayName = "가나#1", AccountLevel = 3 };
            a.Look.Add("hat", "hat_01");
            m.Placements.Add(a);
            m.Placements.Add(new MatchPlacementInfo { UserId = "b", Placement = 2 });
            return m;
        }

        private static Match Match() => new Match
        {
            queueId = 2,
            rounds = new[] { new MatchRound { gameModeId = 5, mapId = 12 } },
        };

        [Test]
        public void 경기_정보에서_큐_종목_맵을_채운다()
        {
            var r = MatchResultCapture.From(Message(), "M1", Match());
            Assert.AreEqual(("M1", 2, 5, 12), (r.matchId, r.queueId, r.gameModeId, r.mapId));
        }

        [Test]
        public void 경기_정보가_없으면_0()
        {
            var r = MatchResultCapture.From(Message(), "M1", null);
            Assert.AreEqual((0, 0, 0), (r.queueId, r.gameModeId, r.mapId));
        }

        [Test]
        public void 라운드가_비었으면_종목_맵은_0()
        {
            var r = MatchResultCapture.From(Message(), "M1", new Match { queueId = 1, rounds = new MatchRound[0] });
            Assert.AreEqual((1, 0, 0), (r.queueId, r.gameModeId, r.mapId));
        }

        [Test]
        public void 이름_룩을_저장한다()
        {
            var a = MatchResultCapture.From(Message(), "M1", Match()).participants[0];
            Assert.AreEqual("가나#1", a.displayName);
            Assert.AreEqual("hat_01", a.look.SlotOrNull("hat"));
            Assert.AreEqual(3, a.look.AccountLevel);
        }

        [Test]
        public void 새_칸이_비면_이름은_빈칸_룩은_null()
        {
            var b = MatchResultCapture.From(Message(), "M1", Match()).participants[1];
            Assert.AreEqual(string.Empty, b.displayName);
            Assert.IsNull(b.look);
        }

        [Test]
        public void 등수와_점수_변화는_그대로()
        {
            var r = MatchResultCapture.From(Message(), "M1", Match());
            Assert.AreEqual(2, r.participants[1].placement);
            Assert.IsTrue(r.hasRatingChange);
            Assert.AreEqual(2, r.myMmrAfter);
        }
    }
}
