using NUnit.Framework;

namespace LOP.Tests
{
    public class DodgeClientStateTests
    {
        static DodgeStateToC Msg(int version, int lives)
        {
            var m = new DodgeStateToC { Version = version };
            var w = new DodgePatternWire { Id = 1, Kind = (int)DodgePatternKind.Bomb, StartTick = 10, Seed = 0 };
            w.P.Add(1f); w.P.Add(2f); w.P.Add(3f); w.P.Add(0f);
            m.Patterns.Add(w);
            m.Players.Add(new DodgePlayerWire { EntityId = "9", Lives = lives, EliminatedTick = -1 });
            return m;
        }

        [Test]
        public void 새_판본이면_패턴과_목숨을_그대로_옮긴다()
        {
            var s = new DodgeClientState();
            Assert.IsTrue(s.Apply(Msg(1, 3)));
            Assert.AreEqual(1, s.Patterns.Count);
            Assert.AreEqual(DodgePatternKind.Bomb, s.Patterns[0].Kind);
            Assert.AreEqual(3f, s.Patterns[0].P2);
            Assert.IsTrue(s.TryGetLife("9", out int lives, out long eliminated));
            Assert.AreEqual(3, lives);
            Assert.AreEqual(-1, eliminated);
        }

        [Test]
        public void 늦게_도착한_낡은_판본은_버린다()
        {
            var s = new DodgeClientState();
            s.Apply(Msg(5, 1));
            Assert.IsFalse(s.Apply(Msg(4, 3)));
            s.TryGetLife("9", out int lives, out _);
            Assert.AreEqual(1, lives);
        }

        [Test]
        public void 예고_길이를_패턴에_옮긴다()
        {
            var m = Msg(1, 3);
            m.Patterns[0].WarnTicks = 33;
            var s = new DodgeClientState();
            s.Apply(m);
            Assert.AreEqual(33, s.Patterns[0].WarnTicks);
        }

        [Test]
        public void 무적_끝_틱을_사람마다_옮긴다()
        {
            var m = Msg(1, 3);
            m.Players[0].InvulnerableUntilTick = 777;
            var s = new DodgeClientState();
            s.Apply(m);
            Assert.AreEqual(777, s.InvulnerableUntil("9"));
            Assert.AreEqual(-1, s.InvulnerableUntil("없는사람"));
            CollectionAssert.AreEqual(new[] { "9" }, s.PlayerIds);
        }
    }
}
