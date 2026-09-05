using GameFramework.World;
using NUnit.Framework;
using LOP.UI;

namespace LOP.Tests
{
    /// <summary>관전 화면의 문구. 몇 번째 사람을 보고 있는지가 유일한 정보다 —
    /// 인게임에 표시 이름이 없어 이름을 띄울 수 없다.</summary>
    public class RaceSpectateViewModelTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        private static Entity Bird(string id, float x)
        {
            var bird = new Entity(id);
            bird.Add(new EntityKind(EntityType.Character));
            bird.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(x, 0f, 0f) });
            return bird;
        }

        private static RaceSpectateViewModel ViewModel(params Entity[] entities)
        {
            var registry = new EntityRegistry();
            foreach (var entity in entities)
            {
                registry.Add(entity);
            }
            var spectate = new FlappySpectate(registry, new FakeGameDataStore { userEntityId = "watcher" });
            spectate.Refresh();
            return new RaceSpectateViewModel(spectate);
        }

        [Test]
        public void 몇_번째를_보는지_알려준다()
        {
            //  내가 없으므로 꼴찌 "b"(3번째)에서 시작한다.
            var viewModel = ViewModel(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f));

            Assert.AreEqual("관전 3 / 3", viewModel.StatusText());
        }

        [Test]
        public void 다음을_누르면_번호가_바뀐다()
        {
            var viewModel = ViewModel(Bird("a", 50f), Bird("b", 10f), Bird("c", 30f));

            viewModel.Next();

            Assert.AreEqual("관전 1 / 3", viewModel.StatusText(), "끝에서 처음으로 되돌아온다");
        }

        [Test]
        public void 볼_사람이_없으면_아무것도_안_띄운다()
        {
            var viewModel = ViewModel();

            Assert.AreEqual(string.Empty, viewModel.StatusText());
        }
    }
}
