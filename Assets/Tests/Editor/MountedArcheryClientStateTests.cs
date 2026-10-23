using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    public class MountedArcheryClientStateTests
    {
        private static MountedArcheryStateToC Msg(int version, params (string id, string name, long tick, string killer)[] ps)
        {
            var m = new MountedArcheryStateToC { Version = version };
            foreach (var p in ps)
            {
                m.Players.Add(new MountedArcheryPlayerWire
                {
                    EntityId = p.id, DisplayName = p.name, EliminatedTick = p.tick, KillerEntityId = p.killer,
                });
            }
            return m;
        }

        [Test]
        public void 옛_판본은_무시한다()
        {
            var s = new MountedArcheryClientState();
            Assert.IsTrue(s.Apply(Msg(5, ("a", "에이", -1, ""), ("b", "비", 3, "a"))));
            Assert.IsFalse(s.Apply(Msg(4, ("a", "에이", -1, ""), ("b", "비", -1, ""))));
            Assert.IsTrue(s.IsEliminated("b"));
            Assert.AreEqual(1, s.AliveCount);
        }

        [Test]
        public void 새로_탈락한_사람마다_한_번_알린다()
        {
            var s = new MountedArcheryClientState();
            var got = new List<MountedArcheryClientState.Elimination>();
            s.Eliminated += got.Add;
            s.Apply(Msg(1, ("a", "에이", -1, ""), ("b", "비", 3, "a")));
            s.Apply(Msg(2, ("a", "에이", -1, ""), ("b", "비", 3, "a")));
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual("b", got[0].VictimId);
            Assert.AreEqual("에이", got[0].KillerName);
            s.Apply(Msg(3, ("a", "에이", 9, "b"), ("b", "비", 3, "a")));
            Assert.AreEqual(2, got.Count);
        }

        [Test]
        public void 처치자가_없으면_킬러_이름이_빈다()
        {
            var s = new MountedArcheryClientState();
            MountedArcheryClientState.Elimination e = default;
            s.Eliminated += x => e = x;
            s.Apply(Msg(1, ("a", "에이", -1, ""), ("b", "비", 3, "")));
            Assert.AreEqual("b", e.VictimId);
            Assert.AreEqual("", e.KillerName);
            Assert.AreEqual("비", e.VictimName);
        }
    }
}
