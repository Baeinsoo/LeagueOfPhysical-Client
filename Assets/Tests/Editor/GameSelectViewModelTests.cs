using System.Collections.Generic;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>종목 선택 화면 — 확인을 눌러야만 로비 선택이 바뀐다.</summary>
    public class GameSelectViewModelTests
    {
        private static readonly List<GameChoice> Games = new()
        {
            new GameChoice(1, "활쏘기", "과녁을 맞혀라", new[] { new MapChoice(10, "들판"), new MapChoice(11, "성벽") }),
            new GameChoice(2, "판치기", "딱지를 뒤집어라", new[] { new MapChoice(20, "교실") }),
        };

        private (GameSelectViewModel vm, List<(int, int)> applied) Make(int game = 0, int map = 1)
        {
            var applied = new List<(int, int)>();
            return (new GameSelectViewModel(Games, game, map, (g, m) => applied.Add((g, m))), applied);
        }

        [Test]
        public void 연_순간에는_지금_선택()
        {
            var (vm, _) = Make(0, 1);
            Assert.AreEqual(0, vm.GameIndex.CurrentValue);
            Assert.AreEqual(1, vm.MapIndex.CurrentValue);
            Assert.AreEqual(2, vm.Maps.Count);
        }

        [Test]
        public void 종목을_바꾸면_첫_경기장()
        {
            var (vm, _) = Make(0, 1);
            vm.SelectGame(1);
            Assert.AreEqual(1, vm.GameIndex.CurrentValue);
            Assert.AreEqual(0, vm.MapIndex.CurrentValue);
            Assert.AreEqual("교실", vm.Maps[0].Name);
        }

        [Test]
        public void 확인만_반영한다()
        {
            var (vm, applied) = Make(0, 1);
            vm.SelectGame(1);
            Assert.IsEmpty(applied);
            vm.Confirm();
            CollectionAssert.AreEqual(new[] { (1, 0) }, applied);
        }

        [Test]
        public void 뒤로는_반영하지_않는다()
        {
            var (vm, applied) = Make(0, 1);
            vm.SelectGame(1);
            vm.Dispose();   // 뒤로 = 확인 없이 닫힘
            Assert.IsEmpty(applied);
        }

        [Test]
        public void 범위_밖은_무시()
        {
            var (vm, _) = Make(0, 0);
            vm.SelectGame(9);
            vm.SelectMap(-1);
            vm.SelectMap(5);
            Assert.AreEqual(0, vm.GameIndex.CurrentValue);
            Assert.AreEqual(0, vm.MapIndex.CurrentValue);
        }

        [Test]
        public void 시작_인덱스가_범위_밖이면_첫_것()
        {
            var (vm, _) = Make(7, 7);
            Assert.AreEqual(0, vm.GameIndex.CurrentValue);
            Assert.AreEqual(0, vm.MapIndex.CurrentValue);
        }
    }
}
