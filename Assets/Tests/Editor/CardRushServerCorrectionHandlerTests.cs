using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 보정 — 서버가 보낸 17칸 중 하나라도 내 예측과 다르면 되감는다.
    /// 자리는 상태에서 계산되므로, 한 칸을 안 보면 "상태는 틀렸는데 자리는 아직 같은" 틱을 놓친다.
    /// </summary>
    public class CardRushServerCorrectionHandlerTests
    {
        private const long Tick = 1300;
        private const string Id = "runner-1";

        private sealed class FixedSeed : IMatchSeed { public ulong Value => 4242; }

        //  spec §6.1 시작 값 — 월드를 만드는 다른 클라 시험도 이것을 쓴다.
        internal static readonly CardRushConfig Config =
            new CardRushConfig(120, 4, 140, 11, 19, 0.5f, 28, 48, 1.2f, 1.39f, 0.7f, 2.77f, 1f, 0.22f, 0.40f, 0.20f, 0.02f, 0.5f);

        //  칸마다 서로 다른 값 — 두 칸을 바꿔 비교·적용해도 걸린다.
        private static readonly CardRushRunnerState Saved = new CardRushRunnerState
        {
            PosMm = 12345, UseCount = 17, SpinStartTick = 1281, DashStartTick = 1282, BlockEndTick = 1283,
            StunEndTick = 1284, ObstacleIndex = 5, ObstacleTick = 1286, ZoneIndex = 3, ZoneTick = 1288,
            ObstacleEvent = (int)CardRushObstacleEvent.Smashed, ObstacleEventTick = 1290,
            ZoneEvent = (int)CardRushZoneEvent.Dodged, ZoneEventTick = 1292, FinishTick = 1293, CleanStreak = 4, WhiffTick = 1294,
            BestCleanStreak = 9, Hits = 3,
        };

        private static CardRushServerCorrectionHandler Make(out GameFramework.World.Entity runner)
        {
            var registry = new GameFramework.World.EntityRegistry();
            runner = new GameFramework.World.Entity(Id);
            runner.Add(new GameFramework.World.Transform());
            runner.Add(new GameFramework.World.Simulated());
            runner.Add(new CardRushRunner { State = Saved });
            registry.Add(runner);

            //  맵 규칙 없이도 저장은 된다 — 저장은 코스와 무관하게 선수 상태만 담는다.
            var world = new CardRushWorld(registry, new GameFramework.World.WorldEventBuffer(),
                new CardRushMapRulesField(), new FixedSeed(), Config) { GameplayStartTick = 0 };
            world.SaveState(Tick);
            return new CardRushServerCorrectionHandler(world);
        }

        //  서버가 같은 상태를 보냈을 때의 스냅.
        private static EntitySnap SnapOf(in CardRushRunnerState s) => new EntitySnap
        {
            entityId = Id, tick = Tick,
            cardPosMm = s.PosMm, cardUseCount = s.UseCount, cardSpinStartTick = s.SpinStartTick,
            cardDashStartTick = s.DashStartTick, cardBlockEndTick = s.BlockEndTick, cardStunEndTick = s.StunEndTick,
            cardObstacleIndex = s.ObstacleIndex, cardObstacleTick = s.ObstacleTick, cardZoneIndex = s.ZoneIndex,
            cardZoneTick = s.ZoneTick, cardObstacleEvent = s.ObstacleEvent, cardObstacleEventTick = s.ObstacleEventTick,
            cardZoneEvent = s.ZoneEvent, cardZoneEventTick = s.ZoneEventTick, cardFinishTick = s.FinishTick,
            cardCleanStreak = s.CleanStreak, cardWhiffTick = s.WhiffTick,
        };

        [Test]
        public void 열일곱_칸이_같으면_그대로()
        {
            var handler = Make(out _);
            Assert.IsTrue(handler.Matches(Tick, SnapOf(Saved)));
        }

        //  서버 뜻 그대로 — 각 칸에서 실제로 날 수 있는 다른 값(한 틱 차이·한 칸 차이·다른 결과)을 넣는다.
        [TestCase("PosMm")]
        [TestCase("UseCount")]
        [TestCase("SpinStartTick")]
        [TestCase("DashStartTick")]
        [TestCase("BlockEndTick")]
        [TestCase("StunEndTick")]
        [TestCase("ObstacleIndex")]
        [TestCase("ObstacleTick")]
        [TestCase("ZoneIndex")]
        [TestCase("ZoneTick")]
        [TestCase("ObstacleEvent")]
        [TestCase("ObstacleEventTick")]
        [TestCase("ZoneEvent")]
        [TestCase("ZoneEventTick")]
        [TestCase("FinishTick")]
        [TestCase("CleanStreak")]
        [TestCase("WhiffTick")]
        public void 한_칸만_달라도_되감는다(string field)
        {
            var handler = Make(out _);
            var snap = SnapOf(Saved);
            switch (field)
            {
                case "PosMm": snap.cardPosMm += CardRushRules.RunMmPerTick; break;      // 한 틱 덜 달림
                case "UseCount": snap.cardUseCount -= 1; break;                         // 서버가 탭을 버림
                case "SpinStartTick": snap.cardSpinStartTick = 0; break;                // 서버는 아직 안 씀
                case "DashStartTick": snap.cardDashStartTick = 0; break;                // 대시 취소
                case "BlockEndTick": snap.cardBlockEndTick += 1; break;
                case "StunEndTick": snap.cardStunEndTick = 0; break;                    // 서버는 안 맞음
                case "ObstacleIndex": snap.cardObstacleIndex -= 1; break;               // 와장창 안 됨 — 바리케이드가 다시 선다
                case "ObstacleTick": snap.cardObstacleTick += 1; break;                 // 한 틱 늦게 닿음
                case "ZoneIndex": snap.cardZoneIndex -= 1; break;
                case "ZoneTick": snap.cardZoneTick += 1; break;
                case "ObstacleEvent": snap.cardObstacleEvent = (int)CardRushObstacleEvent.Whiff; break;
                case "ObstacleEventTick": snap.cardObstacleEventTick += 1; break;
                case "ZoneEvent": snap.cardZoneEvent = (int)CardRushZoneEvent.Hit; break;
                case "ZoneEventTick": snap.cardZoneEventTick += 1; break;
                case "FinishTick": snap.cardFinishTick = 0; break;                      // 서버는 아직 못 들어옴
                case "CleanStreak": snap.cardCleanStreak = 0; break;
                case "WhiffTick": snap.cardWhiffTick += 1; break;
                default: throw new System.ArgumentException(field);
            }
            Assert.IsFalse(handler.Matches(Tick, snap), $"{field}가 다른데 맞다고 했다");
        }

        [Test]
        public void 저장이_없는_틱이면_그대로()
        {
            var handler = Make(out _);
            var snap = SnapOf(Saved);
            snap.cardPosMm = 99_999;
            Assert.IsTrue(handler.Matches(Tick + 1, snap), "비교할 예측이 없으면 위치 판정에 맡긴다");
        }

        [Test]
        public void 서버_값_17칸을_넣고_최고_연속과_맞은_수는_지킨다()
        {
            var handler = Make(out var runner);
            var server = new CardRushRunnerState
            {
                PosMm = 54321, UseCount = 22, SpinStartTick = 2001, DashStartTick = 2002, BlockEndTick = 2003,
                StunEndTick = 2004, ObstacleIndex = 7, ObstacleTick = 2006, ZoneIndex = 4, ZoneTick = 2008,
                ObstacleEvent = (int)CardRushObstacleEvent.Tumbled, ObstacleEventTick = 2010,
                ZoneEvent = (int)CardRushZoneEvent.Held, ZoneEventTick = 2012, FinishTick = 2013, CleanStreak = 2, WhiffTick = 2014,
            };

            handler.ApplyAuthoritative(runner, SnapOf(server), 0.02f);

            var s = runner.Get<CardRushRunner>().State;
            Assert.AreEqual(54321, s.PosMm);
            Assert.AreEqual(22, s.UseCount);
            Assert.AreEqual(2001, s.SpinStartTick);
            Assert.AreEqual(2002, s.DashStartTick);
            Assert.AreEqual(2003, s.BlockEndTick);
            Assert.AreEqual(2004, s.StunEndTick);
            Assert.AreEqual(7, s.ObstacleIndex);
            Assert.AreEqual(2006, s.ObstacleTick);
            Assert.AreEqual(4, s.ZoneIndex);
            Assert.AreEqual(2008, s.ZoneTick);
            Assert.AreEqual((int)CardRushObstacleEvent.Tumbled, s.ObstacleEvent);
            Assert.AreEqual(2010, s.ObstacleEventTick);
            Assert.AreEqual((int)CardRushZoneEvent.Held, s.ZoneEvent);
            Assert.AreEqual(2012, s.ZoneEventTick);
            Assert.AreEqual(2013, s.FinishTick);
            Assert.AreEqual(2, s.CleanStreak);
            Assert.AreEqual(2014, s.WhiffTick);
            Assert.AreEqual(9, s.BestCleanStreak, "서버가 안 보내는 값 — 0으로 지워지면 결과 통계가 틀린다");
            Assert.AreEqual(3, s.Hits, "서버가 안 보내는 값");

            //  적용한 뒤엔 같은 스냅과 맞는다 — 적용과 비교가 같은 칸을 본다.
            server.BestCleanStreak = s.BestCleanStreak;
            server.Hits = s.Hits;
            Assert.AreEqual(server, s, "17칸 + 지킨 두 칸 = 상태 전부");
        }

        //  17칸은 ForMember 없이 이름만으로 옮겨진다 — 이름이 어긋나면 클라가 늘 0을 보고 매 스냅 되감는다.
        [Test]
        public void 열일곱_칸이_와이어와_AutoMapper를_건넌다()
        {
            var sent = new global::EntitySnap
            {
                CardPosMm = 12345, CardUseCount = 17, CardSpinStartTick = 1281, CardDashStartTick = 1282, CardBlockEndTick = 1283,
                CardStunEndTick = 1284, CardObstacleIndex = 5, CardObstacleTick = 1286, CardZoneIndex = 3, CardZoneTick = 1288,
                CardObstacleEvent = 1, CardObstacleEventTick = 1290, CardZoneEvent = 2, CardZoneEventTick = 1292,
                CardFinishTick = 1293, CardCleanStreak = 4, CardWhiffTick = 1294,
            };
            var proto = global::EntitySnap.Parser.ParseFrom(Google.Protobuf.MessageExtensions.ToByteArray(sent));

            EntitySnap m = MapperConfig.mapper.Map<EntitySnap>(proto);

            Assert.AreEqual(12345, m.cardPosMm);
            Assert.AreEqual(17, m.cardUseCount);
            Assert.AreEqual(1281, m.cardSpinStartTick);
            Assert.AreEqual(1282, m.cardDashStartTick);
            Assert.AreEqual(1283, m.cardBlockEndTick);
            Assert.AreEqual(1284, m.cardStunEndTick);
            Assert.AreEqual(5, m.cardObstacleIndex);
            Assert.AreEqual(1286, m.cardObstacleTick);
            Assert.AreEqual(3, m.cardZoneIndex);
            Assert.AreEqual(1288, m.cardZoneTick);
            Assert.AreEqual(1, m.cardObstacleEvent);
            Assert.AreEqual(1290, m.cardObstacleEventTick);
            Assert.AreEqual(2, m.cardZoneEvent);
            Assert.AreEqual(1292, m.cardZoneEventTick);
            Assert.AreEqual(1293, m.cardFinishTick);
            Assert.AreEqual(4, m.cardCleanStreak);
            Assert.AreEqual(1294, m.cardWhiffTick);
        }

        [Test]
        public void 최고_연속은_지금_연속보다_작을_수_없다()
        {
            var handler = Make(out var runner);
            var snap = SnapOf(Saved);
            snap.cardCleanStreak = 12;

            handler.ApplyAuthoritative(runner, snap, 0.02f);

            Assert.AreEqual(12, runner.Get<CardRushRunner>().State.BestCleanStreak);
        }

        // ── 남의 몸 상태 적용(Task 8 리뷰에서 넘어온 빈칸) ────

        [Test]
        public void 남의_몸_스냅은_카드_러시_상태_17칸을_넣는다()
        {
            var entity = new GameFramework.World.Entity("other");
            entity.Add(new CardRushRunner { State = new CardRushRunnerState { BestCleanStreak = 9, Hits = 4 } });
            var snap = new EntitySnap
            {
                entityId = "other", cardPosMm = 12345, cardUseCount = 6, cardStunEndTick = 1300, cardObstacleIndex = 2,
                cardZoneEvent = (int)CardRushZoneEvent.Hit, cardFinishTick = 1500, cardCleanStreak = 3, cardWhiffTick = 1450,
            };

            //  핸들러는 의존이 스무 개 넘는다 — 이 경로는 그중 아무것도 안 쓰므로 생성자 없이 만든다.
            var handler = (GameEntityMessageHandler)System.Runtime.Serialization.FormatterServices
                .GetUninitializedObject(typeof(GameEntityMessageHandler));
            typeof(GameEntityMessageHandler).GetMethod("ApplyRemoteSnapState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(handler, new object[] { entity, new global::EntitySnap(), snap });

            //  남의 몸도 내 보정과 같은 함수(ApplyCardState)를 지난다 — 17칸 하나하나는 그 시험이 본다.
            var s = entity.Get<CardRushRunner>().State;
            Assert.AreEqual(12345, s.PosMm);
            Assert.AreEqual(6, s.UseCount);
            Assert.AreEqual(1300, s.StunEndTick);
            Assert.AreEqual(2, s.ObstacleIndex);
            Assert.AreEqual((int)CardRushZoneEvent.Hit, s.ZoneEvent);
            Assert.AreEqual(1500, s.FinishTick);
            Assert.AreEqual(3, s.CleanStreak);
            Assert.AreEqual(1450, s.WhiffTick, "헛방 틱(52)도 남의 몸에");
            Assert.AreEqual(9, s.BestCleanStreak, "최고 연속은 지킨다");
            Assert.AreEqual(4, s.Hits, "맞은 수는 서버가 안 보낸다 — 그대로");
        }
    }
}
