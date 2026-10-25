using GameFramework.World;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 결승 도장(spec §3): 창이 열리는 순간(내가 결승선을 넘은 순간) FINISH! + 결승음·도장음, 0.45초 뒤 순위 도장 + 도장음.
    /// 등수는 서버 답이라 0.2초쯤 늦게 오고, 더 늦으면 오는 순간 찍는다. 도장이 사라지면 위쪽 작은 판에 등수가 남는다
    /// (조각 1이 결승 뒤 HUD 순위 판을 숨긴다).
    /// </summary>
    public class RaceFinishViewModelTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

        private EntityRegistry registry;
        private FinishPlacement placement;
        private FakeFlappyAudio audio;
        private RaceFinishViewModel vm;

        [SetUp]
        public void SetUp()
        {
            registry = new EntityRegistry();
            var me = new Entity("me");
            placement = new FinishPlacement();
            me.Add(placement);
            registry.Add(me);
            audio = new FakeFlappyAudio();
            vm = new RaceFinishViewModel(new FakeGameDataStore { userEntityId = "me" }, registry, audio);
        }

        [Test]
        public void 열리는_순간_결승음과_도장음()
        {
            vm.Refresh(0.016f);

            CollectionAssert.AreEqual(new[] { FlappySfx.Finish, FlappySfx.Stamp }, audio.Played);
            Assert.AreEqual(FinishStampTimeline.StampFrom, vm.FinishScale, 1e-5f, "첫 프레임은 찍히는 순간 — 그 프레임의 dt를 나이에 넣지 않는다");
            Assert.AreEqual(1f, vm.FinishAlpha);
            Assert.IsFalse(vm.RankStampVisible);
        }

        [Test]
        public void 순위_도장은_0_45초_뒤_등수가_있을_때()
        {
            placement.Value = 2;
            vm.Refresh(0f);
            vm.Refresh(0.3f);
            Assert.IsFalse(vm.RankStampVisible);
            Assert.AreEqual(2, audio.Played.Count);

            vm.Refresh(0.2f);

            Assert.IsTrue(vm.RankStampVisible);
            CollectionAssert.AreEqual(new[] { FlappySfx.Finish, FlappySfx.Stamp, FlappySfx.Stamp }, audio.Played);
            Assert.AreEqual("2위", vm.RankText);
            Assert.IsFalse(vm.Winner);
            Assert.AreEqual(FinishStampTimeline.StampFrom, vm.RankScale, 1e-5f);
        }

        [Test]
        public void 등수가_늦게_오면_오는_순간_찍힌다()
        {
            vm.Refresh(0f);
            vm.Refresh(1f);
            Assert.IsFalse(vm.RankStampVisible, "등수를 지어내지 않는다");

            placement.Value = 3;
            vm.Refresh(0.016f);

            Assert.IsTrue(vm.RankStampVisible);
            Assert.AreEqual("3위", vm.RankText);
            Assert.AreEqual(3, audio.Played.Count);
        }

        [Test]
        public void 일등이면_우승과_금색()
        {
            placement.Value = 1;
            vm.Refresh(0f);
            vm.Refresh(0.5f);

            Assert.IsTrue(vm.Winner);
            Assert.AreEqual(FlappyRacePalette.RankColor(1), vm.RankColor);
        }

        [Test]
        public void 도장이_사라지면_작은_판에_남는다()
        {
            placement.Value = 2;
            vm.Refresh(0f);
            vm.Refresh(0.5f);
            Assert.IsFalse(vm.ShowSettled);

            vm.Refresh(2.2f);

            Assert.AreEqual(0f, vm.FinishAlpha);
            Assert.IsFalse(vm.RankStampVisible);
            Assert.IsTrue(vm.ShowSettled);
            Assert.AreEqual("2위 · 완주", vm.PlacementText);
        }

        [Test]
        public void 엔티티가_사라져도_작은_판은_남는다()
        {
            placement.Value = 2;
            vm.Refresh(0f);
            vm.Refresh(0.5f);
            vm.Refresh(2.2f);

            registry.Remove("me");   //  판이 끝나며 방을 정리한다
            vm.Refresh(0.1f);

            Assert.AreEqual("2위 · 완주", vm.PlacementText);
        }
    }
}
