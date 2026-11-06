using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>완주 뒤 관전은 아직 못 들어온 사람 중 가장 멀리 간(PosMm) 사람부터 — 다음 결승이 바로 보이게.</summary>
    public class CardRushSpectateTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public string formerEntityId { get; set; }
            public void Clear() { }
        }

        private static Entity Runner(string id, int posMm, int placement = 0)
        {
            var e = new Entity(id);
            e.Add(new EntityKind(EntityType.Character));
            e.Add(new CardRushRunner { State = new CardRushRunnerState { PosMm = posMm } });
            e.Add(new FinishPlacement { Value = placement });
            return e;
        }

        [Test]
        public void 완주_뒤엔_가장_멀리_간_사람을_본다()
        {
            var registry = new EntityRegistry();
            registry.Add(Runner("me", 140000, placement: 1));
            registry.Add(Runner("a", 61000));
            registry.Add(Runner("b", 98000));
            registry.Add(Runner("c", 72000));
            var spectate = new CardRushSpectate(registry, new FakeGameDataStore { userEntityId = "me" });

            spectate.Refresh(0f);
            Assert.AreEqual("me", spectate.Current, "완주 직후엔 나를 붙잡는다");
            spectate.Refresh(FlappySpectate.FinishHoldSeconds + 0.1f);
            Assert.AreEqual("b", spectate.Current);
            CollectionAssert.AreEqual(new[] { "b", "c", "a" }, spectate.Candidates, "간 거리 내림차순");
        }
    }
}
