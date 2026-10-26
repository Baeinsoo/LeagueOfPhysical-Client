using NUnit.Framework;

namespace LOP.Tests
{
    public class KnockbackClientStateTests
    {
        static KnockbackStateToC Msg(int version, string id, long eliminatedTick, int percent)
        {
            var m = new KnockbackStateToC { Version = version };
            m.Players.Add(new KnockbackPlayerWire { EntityId = id, EliminatedTick = eliminatedTick, DamagePercent = percent });
            return m;
        }

        [Test]
        public void 상태는_판본이_낮으면_버린다()
        {
            var state = new KnockbackClientState();
            Assert.IsTrue(state.Apply(Msg(5, "a", -1, 40)));
            Assert.IsFalse(state.Apply(Msg(4, "a", 100, 90)));
            Assert.IsFalse(state.Apply(Msg(5, "a", 100, 90)));
            Assert.AreEqual(40, state.DamageOf("a"));
            Assert.IsFalse(state.IsEliminated("a"));
        }

        [Test]
        public void 새_판본은_적용되고_알린다()
        {
            var state = new KnockbackClientState();
            int changed = 0;
            state.Changed += () => changed++;
            state.Apply(Msg(1, "a", -1, 0));
            Assert.IsTrue(state.Apply(Msg(2, "a", 77, 60)));
            Assert.IsTrue(state.IsEliminated("a"));
            Assert.AreEqual(60, state.DamageOf("a"));
            Assert.AreEqual(2, changed);
        }

        [Test]
        public void 모르는_사람은_탈락_아님_0퍼()
        {
            var state = new KnockbackClientState();
            Assert.IsFalse(state.IsEliminated("x"));
            Assert.AreEqual(0, state.DamageOf("x"));
            Assert.AreEqual(0, state.DamageOf(null));
        }

        [Test]
        public void 피해_라벨_색은_48에서_노랑_80에서_빨강()
        {
            Assert.AreEqual(UnityEngine.Color.white, KnockbackDamageLabel.ColorFor(47));
            Assert.AreEqual(KnockbackDamageLabel.ColorFor(48), KnockbackDamageLabel.ColorFor(79));
            Assert.AreNotEqual(UnityEngine.Color.white, KnockbackDamageLabel.ColorFor(48));
            Assert.AreNotEqual(KnockbackDamageLabel.ColorFor(79), KnockbackDamageLabel.ColorFor(80));
        }
    }
}
