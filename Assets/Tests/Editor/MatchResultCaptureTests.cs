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

        [Test]
        public void 캐릭터_외형을_저장한다()
        {
            var m = Message();
            m.Placements[0].CharacterAppearance = new CharacterAppearanceInfo { SkinColorId = 105, HairColorId = 202 };

            var a = MatchResultCapture.From(m, "M1", Match()).participants[0];

            Assert.AreEqual(new CharacterAppearance(105, 202), a.look.CharacterAppearance);
        }

        [Test]
        public void 기록지_결과를_싣는다()
        {
            var records = new[] { new RecordOutcome(FlappyRecordMetrics.FinishTime, 50d, true, false, 0d) };

            var r = MatchResultCapture.From(Message(), "M1", Match(), records);

            Assert.AreSame(records, r.myRecords);
        }

        [Test]
        public void 기록이_없으면_빈_목록()
        {
            //  기록을 안 적는 종목 — 결과판은 카드를 안 띄운다. null이면 결과판이 매번 null을 따져야 한다.
            Assert.IsNotNull(MatchResultCapture.From(Message(), "M1", Match()).myRecords);
            Assert.AreEqual(0, MatchResultCapture.From(Message(), "M1", Match(), null).myRecords.Count);
        }
    }
}
