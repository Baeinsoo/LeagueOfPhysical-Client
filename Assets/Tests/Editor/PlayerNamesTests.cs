using NUnit.Framework;

namespace LOP.Tests
{
    //  엔티티에 PlayerLook(표시 이름)이 있으면 그 이름, 없으면(룩이 없거나·이름이 비었거나·엔티티를 못 찾으면)
    //  호출부가 건넨 기존 문구("1P 선수" 같은)를 그대로 쓴다 — PlayerNames.For 쪽 규칙.
    public class PlayerNamesTests
    {
        [Test]
        public void 룩이_있으면_표시_이름을_쓴다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var entity = new GameFramework.World.Entity("e1");
            entity.Add(new PlayerLook(null, "Kim", 1));
            registry.Add(entity);

            Assert.AreEqual("Kim", PlayerNames.For(registry, "e1", "1P 선수"));
        }

        [Test]
        public void 룩이_없으면_대체_문구를_쓴다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            registry.Add(new GameFramework.World.Entity("e1"));

            Assert.AreEqual("1P 선수", PlayerNames.For(registry, "e1", "1P 선수"));
        }

        [Test]
        public void 표시_이름이_비었으면_대체_문구를_쓴다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var entity = new GameFramework.World.Entity("e1");
            entity.Add(new PlayerLook(null, "", 1));
            registry.Add(entity);

            Assert.AreEqual("1P 선수", PlayerNames.For(registry, "e1", "1P 선수"));
        }

        [Test]
        public void 모르는_엔티티면_대체_문구를_쓴다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            Assert.AreEqual("1P 선수", PlayerNames.For(registry, "unknown", "1P 선수"));
        }
    }
}
