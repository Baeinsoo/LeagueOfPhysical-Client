using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class MountedSpectateTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        private static Entity Rider(string id)
        {
            var e = new Entity(id);
            e.Add(new Mount());
            return e;
        }

        private static MountedArcheryStateToC Msg(int version, params (string id, long tick, string killer)[] ps)
        {
            var m = new MountedArcheryStateToC { Version = version };
            foreach (var p in ps)
            {
                m.Players.Add(new MountedArcheryPlayerWire
                {
                    EntityId = p.id, DisplayName = p.id, EliminatedTick = p.tick, KillerEntityId = p.killer,
                });
            }
            return m;
        }

        private static (MountedSpectate spectate, MountedArcheryClientState state, EntityRegistry registry) Make(
            params string[] ids)
        {
            var registry = new EntityRegistry();
            foreach (var id in ids)
            {
                registry.Add(Rider(id));
            }
            var state = new MountedArcheryClientState();
            var spectate = new MountedSpectate(registry, new FakeGameDataStore { userEntityId = "me" }, state);
            return (spectate, state, registry);
        }

        [Test]
        public void 처치자를_먼저_본다()
        {
            var (spectate, state, _) = Make("a", "b", "c", "me");
            state.Apply(Msg(1, ("a", -1, ""), ("b", -1, ""), ("c", -1, ""), ("me", -1, "")));
            state.Apply(Msg(2, ("a", -1, ""), ("b", -1, ""), ("c", -1, ""), ("me", 5, "c")));
            spectate.Refresh();

            Assert.AreEqual("c", spectate.Current);
            CollectionAssert.AreEqual(new[] { "c", "a", "b" }, spectate.Candidates);
        }

        [Test]
        public void 보던_사람이_쓰러지면_다음_사람()
        {
            var (spectate, state, registry) = Make("a", "b", "c", "me");
            state.Apply(Msg(1, ("a", -1, ""), ("b", -1, ""), ("c", -1, ""), ("me", -1, "")));
            state.Apply(Msg(2, ("a", -1, ""), ("b", -1, ""), ("c", -1, ""), ("me", 5, "a")));
            spectate.Refresh();
            Assert.AreEqual("a", spectate.Current);

            registry.Remove("a");
            state.Apply(Msg(3, ("a", 9, "b"), ("b", -1, ""), ("c", -1, ""), ("me", 5, "a")));
            spectate.Refresh();

            Assert.AreEqual("b", spectate.Current);
        }

        [Test]
        public void 나는_후보가_아니다()
        {
            var (spectate, _, _) = Make("a", "me");
            spectate.Refresh();

            CollectionAssert.AreEqual(new[] { "a" }, spectate.Candidates);
        }

        [Test]
        public void 탈락했지만_몸이_남은_사람은_후보가_아니다()
        {
            var (spectate, state, _) = Make("a", "b", "me");
            state.Apply(Msg(1, ("a", 3, "b"), ("b", -1, ""), ("me", -1, "")));
            spectate.Refresh();

            CollectionAssert.AreEqual(new[] { "b" }, spectate.Candidates);
        }
    }
}
