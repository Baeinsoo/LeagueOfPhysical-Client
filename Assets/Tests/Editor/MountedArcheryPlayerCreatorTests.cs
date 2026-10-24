using NUnit.Framework;

namespace LOP.Tests
{
    public class MountedArcheryPlayerCreatorTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        // 체력이 몸에 없으면 서버가 보낸 HP 스냅샷을 적용할 곳이 없어 머리 위 체력 바가 영원히 가득 찬다.
        [Test]
        public void 만든_몸에_체력이_생성_데이터대로_붙는다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var creator = new MountedArcheryPlayerCreator(new FakeGameDataStore(), new PlayerContext(), registry);

            creator.Create(new CharacterCreationData { entityId = "m1", visualId = "", maxHP = 100, currentHP = 66 });

            var health = registry.Get("m1").Get<GameFramework.World.Health>();
            Assert.IsNotNull(health);
            Assert.AreEqual(100, health.Max);
            Assert.AreEqual(66, health.Current);
        }
    }
}
