using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class KnockbackSpectateTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public string formerEntityId { get; set; }
            public void Clear() { }
        }

        private static Entity Body(string id, float y = 0f, EntityType type = EntityType.Character)
        {
            var e = new Entity(id);
            e.Add(new EntityKind(type));
            e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(0f, y, 0f) });
            return e;
        }

        private static KnockbackStateToC Msg(int version, params (string id, long tick)[] ps)
        {
            var m = new KnockbackStateToC { Version = version };
            foreach (var p in ps)
            {
                m.Players.Add(new KnockbackPlayerWire { EntityId = p.id, EliminatedTick = p.tick });
            }
            return m;
        }

        private static (KnockbackSpectate spectate, KnockbackClientState state, EntityRegistry registry) Make(
            params Entity[] entities)
        {
            var registry = new EntityRegistry();
            foreach (var e in entities)
            {
                registry.Add(e);
            }
            var state = new KnockbackClientState();
            return (new KnockbackSpectate(registry, new FakeGameDataStore { userEntityId = "me" }, state), state, registry);
        }

        [Test]
        public void 관전_후보는_나와_탈락한_사람과_떨어지는_몸을_뺀다()
        {
            var (spectate, state, _) = Make(Body("me"), Body("a"), Body("b"), Body("c", y: -3f),
                                            Body("coin", type: EntityType.Coin));
            state.Apply(Msg(1, ("b", 50)));
            spectate.Refresh();
            CollectionAssert.AreEqual(new[] { "a" }, spectate.Candidates);
            Assert.AreEqual("a", spectate.Current);
        }

        [Test]
        public void 보던_사람이_탈락하면_다음_사람으로_넘어간다()
        {
            var (spectate, state, _) = Make(Body("me"), Body("a"), Body("b"));
            spectate.Refresh();
            Assert.AreEqual("a", spectate.Current);
            state.Apply(Msg(1, ("a", 50)));
            spectate.Refresh();
            Assert.AreEqual("b", spectate.Current);
        }

        [Test]
        public void 우리_팀이_먼저_나온다()
        {
            var (spectate, state, _) = Make(Body("me"), Body("a"), Body("b"), Body("c"));
            var m = new KnockbackStateToC { Version = 1, TeamCount = 2 };
            m.Players.Add(new KnockbackPlayerWire { EntityId = "me", EliminatedTick = -1, Team = 1 });
            m.Players.Add(new KnockbackPlayerWire { EntityId = "a", EliminatedTick = -1, Team = 2 });
            m.Players.Add(new KnockbackPlayerWire { EntityId = "b", EliminatedTick = -1, Team = 2 });
            m.Players.Add(new KnockbackPlayerWire { EntityId = "c", EliminatedTick = -1, Team = 1 });
            state.Apply(m);
            spectate.Refresh();
            CollectionAssert.AreEqual(new[] { "c", "a", "b" }, spectate.Candidates);
        }

        [Test]
        public void 개인전은_id_순_그대로()
        {
            var (spectate, state, _) = Make(Body("me"), Body("b"), Body("a"));
            var m = new KnockbackStateToC { Version = 1 };
            m.Players.Add(new KnockbackPlayerWire { EntityId = "a", EliminatedTick = -1 });
            state.Apply(m);
            spectate.Refresh();
            CollectionAssert.AreEqual(new[] { "a", "b" }, spectate.Candidates);
        }

        [Test]
        public void 후보가_없으면_Current는_null()
        {
            var (spectate, _, _) = Make(Body("me"));
            spectate.Refresh();
            Assert.IsNull(spectate.Current);
            Assert.AreEqual(0, spectate.Candidates.Count);
        }

        [Test]
        public void 다음_이전은_돌아가며_고른다()
        {
            var (spectate, _, _) = Make(Body("me"), Body("a"), Body("b"));
            spectate.Refresh();
            spectate.Next();
            Assert.AreEqual("b", spectate.Current);
            spectate.Next();
            Assert.AreEqual("a", spectate.Current);
            spectate.Prev();
            Assert.AreEqual("b", spectate.Current);
        }

        [Test]
        public void 몸_없이_상태만_온_재접속은_관전으로_간다()
        {
            Assert.IsTrue(KnockbackHudCoordinator.ShouldSpectateWithoutBody(false, false, true, true, "", false));
            Assert.IsFalse(KnockbackHudCoordinator.ShouldSpectateWithoutBody(false, false, true, true, "e1", false));
        }
    }
}
