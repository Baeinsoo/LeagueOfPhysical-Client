using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>클라가 만드는 새도 연속 날갯짓 칸을 갖는다 — 없으면 내 새 예측이 안 세서 고비마다 서버 게이지와 갈린다.</summary>
    public class FlappyBirdCreatorComboTests
    {
        [Test]
        public void 새는_연속_날갯짓_칸을_갖는다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var creator = new FlappyBirdCreator(new RigGameDataStore { userEntityId = "me" }, new PlayerContext(),
                                                registry, FlappyCorrectionFixture.Config());

            creator.Create(new CharacterCreationData { entityId = "me", visualId = "", characterCode = "" });

            Assert.IsNotNull(registry.Get("me").Get<FlappyFlapCombo>());
        }
    }
}
