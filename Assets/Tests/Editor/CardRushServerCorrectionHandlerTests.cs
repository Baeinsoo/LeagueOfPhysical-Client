using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 보정 — 서버가 보낸 다섯 칸(칸·시도·이동 시작·멈춤 끝·연속) 중 하나라도 내 예측과 다르면 되감는다.
    /// 자리는 상태에서 계산되므로, 한 칸을 안 보면 "상태는 틀렸는데 자리는 아직 같은" 틱을 놓친다.
    /// </summary>
    public class CardRushServerCorrectionHandlerTests
    {
        private const long Tick = 1300;
        private const string Id = "runner-1";

        private sealed class FixedSeed : IMatchSeed { public ulong Value => 4242; }

        private static readonly CardRushRunnerState Saved = new CardRushRunnerState
        {
            Step = 7, Attempt = 2, MoveStartTick = 1234, StunEndTick = 1300, Streak = 0, BestStreak = 5, Misses = 3,
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
                new CardRushMapRulesField(), new FixedSeed(), new CardRushTiming(15, 30)) { GameplayStartTick = 0 };
            world.SaveState(Tick);
            return new CardRushServerCorrectionHandler(world);
        }

        private static EntitySnap Snap(int step = 7, int attempt = 2, long moveStart = 1234, long stunEnd = 1300, int streak = 0)
            => new EntitySnap
            {
                entityId = Id, tick = Tick,
                cardStep = step, cardAttempt = attempt, cardMoveStartTick = moveStart, cardStunEndTick = stunEnd, cardStreak = streak,
            };

        [Test]
        public void 다섯_칸이_같으면_그대로()
        {
            var handler = Make(out _);
            Assert.IsTrue(handler.Matches(Tick, Snap()));
        }

        [TestCase("Step")]
        [TestCase("Attempt")]
        [TestCase("MoveStartTick")]
        [TestCase("StunEndTick")]
        [TestCase("Streak")]
        public void 한_칸만_달라도_되감는다(string field)
        {
            var handler = Make(out _);
            var snap = field switch
            {
                "Step" => Snap(step: 8),
                "Attempt" => Snap(attempt: 3),
                "MoveStartTick" => Snap(moveStart: 1235),
                "StunEndTick" => Snap(stunEnd: 1301),
                "Streak" => Snap(streak: 1),
                _ => throw new System.ArgumentException(field),
            };
            Assert.IsFalse(handler.Matches(Tick, snap), $"{field}가 다른데 맞다고 했다");
        }

        [Test]
        public void 저장이_없는_틱이면_그대로()
        {
            var handler = Make(out _);
            Assert.IsTrue(handler.Matches(Tick + 1, Snap(step: 30)), "비교할 예측이 없으면 위치 판정에 맡긴다");
        }

        [Test]
        public void 서버_값을_넣고_최고_연속과_틀린_수는_지킨다()
        {
            var handler = Make(out var runner);

            handler.ApplyAuthoritative(runner, Snap(step: 9, attempt: 0, moveStart: 1290, stunEnd: 1250, streak: 2), 0.02f);

            var s = runner.Get<CardRushRunner>().State;
            Assert.AreEqual(9, s.Step);
            Assert.AreEqual(0, s.Attempt);
            Assert.AreEqual(1290, s.MoveStartTick);
            Assert.AreEqual(1250, s.StunEndTick);
            Assert.AreEqual(2, s.Streak);
            Assert.AreEqual(5, s.BestStreak, "서버가 안 보내는 값 — 0으로 지워지면 결과 통계가 틀린다");
            Assert.AreEqual(3, s.Misses, "서버가 안 보내는 값");
        }

        //  다섯 칸은 ForMember 없이 이름만으로 옮겨진다 — 이름이 어긋나면 클라가 늘 0을 보고 매 스냅 되감는다.
        [Test]
        public void 다섯_칸이_와이어와_AutoMapper를_건넌다()
        {
            var sent = new global::EntitySnap { CardStep = 7, CardAttempt = 2, CardMoveStartTick = 1234, CardStunEndTick = 1300, CardStreak = 4 };
            var proto = global::EntitySnap.Parser.ParseFrom(Google.Protobuf.MessageExtensions.ToByteArray(sent));

            EntitySnap mapped = MapperConfig.mapper.Map<EntitySnap>(proto);

            Assert.AreEqual(7, mapped.cardStep);
            Assert.AreEqual(2, mapped.cardAttempt);
            Assert.AreEqual(1234, mapped.cardMoveStartTick);
            Assert.AreEqual(1300, mapped.cardStunEndTick);
            Assert.AreEqual(4, mapped.cardStreak);
        }

        [Test]
        public void 최고_연속은_지금_연속보다_작을_수_없다()
        {
            var handler = Make(out var runner);

            handler.ApplyAuthoritative(runner, Snap(streak: 8), 0.02f);

            Assert.AreEqual(8, runner.Get<CardRushRunner>().State.BestStreak);
        }
    }
}
