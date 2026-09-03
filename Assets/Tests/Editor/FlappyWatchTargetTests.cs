using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// "지금 누구를 보고 있나". 카메라와 벽 그리기가 같은 답을 봐야 해서 규칙이 한 곳에 있다 —
    /// 벽은 보고 있는 새와 같은 시각으로 그려야 하는데(내 새는 앞, 남의 새는 뒤),
    /// 둘이 다른 새를 고르면 벽이 엉뚱한 시각에 그려진다.
    /// </summary>
    public class FlappyWatchTargetTests
    {
        private static Entity Bird(string id, float x)
        {
            var bird = new Entity(id);
            bird.Add(new EntityKind(EntityType.Character));
            bird.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, 0f, 0f) });
            return bird;
        }

        private static EntityRegistry Registry(params Entity[] entities)
        {
            var registry = new EntityRegistry();
            foreach (var entity in entities)
            {
                registry.Add(entity);
            }
            return registry;
        }

        [Test]
        public void 내_새가_살아_있으면_내_새다()
        {
            var registry = Registry(Bird("me", 50f), Bird("other", 10f));

            Assert.AreEqual("me", FlappyWatchTarget.Resolve(registry, "me"));
        }

        [Test]
        public void 내_새가_없으면_가장_뒤처진_새다()
        {
            //  선두가 아니라 꼴찌를 본다 — 다음에 잡힐 사람이라 벽이 같은 화면에 있다.
            var registry = Registry(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f));

            Assert.AreEqual("b", FlappyWatchTarget.Resolve(registry, "me"));
        }

        [Test]
        public void 같은_자리면_id가_작은_쪽이다()
        {
            //  레지스트리 순회 순서는 정해져 있지 않다. 안 정하면 프레임마다 카메라가 오간다.
            var registry = Registry(Bird("b", 10f), Bird("a", 10f));

            Assert.AreEqual("a", FlappyWatchTarget.Resolve(registry, "me"));
        }

        [Test]
        public void 새가_하나도_없으면_아무도_아니다()
        {
            Assert.IsNull(FlappyWatchTarget.Resolve(Registry(), "me"));
        }

        [Test]
        public void 새가_아닌_것은_세지_않는다()
        {
            //  아이템도 레지스트리에 있고 x가 더 작을 수 있다. 카메라가 그쪽으로 가면 안 된다.
            var item = new Entity("item");
            item.Add(new EntityKind(EntityType.Item));
            item.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(-100f, 0f, 0f) });

            Assert.AreEqual("bird", FlappyWatchTarget.Resolve(Registry(item, Bird("bird", 10f)), "me"));
        }
    }
}
