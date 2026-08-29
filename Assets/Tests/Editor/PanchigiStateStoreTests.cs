using System.Collections.Generic;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// "지금 내가 칠 차례인가"는 입력을 열지와 게이지를 띄울지가 함께 참조하는 한 벌 판단이다.
    /// 이 판단이 <see cref="PanchigiStateStore.IsAimingTurnOf"/> 하나로 모여 있는지를 고정한다 —
    /// 두 소비처가 각자 손으로 베껴 쓰면 한쪽만 바뀌었을 때 조용히 어긋난다.
    /// </summary>
    public class PanchigiStateStoreTests
    {
        private const int AimingPhase = 1;
        private const int SettlingPhase = 0;
        private const string Me = "player-1";
        private const string Other = "player-2";

        private static readonly Dictionary<string, int> NoDropOuts = new();
        private static readonly string[] NoEliminated = System.Array.Empty<string>();

        private static PanchigiStateStore Store() => new PanchigiStateStore();

        [Test]
        public void 조준_국면에서_내_차례면_참이다()
        {
            var store = Store();
            store.Set(AimingPhase, Me, 0, 0, NoDropOuts, NoEliminated);

            Assert.IsTrue(store.IsAimingTurnOf(Me));
        }

        [Test]
        public void 조준_국면이어도_남의_차례면_거짓이다()
        {
            var store = Store();
            store.Set(AimingPhase, Other, 0, 0, NoDropOuts, NoEliminated);

            Assert.IsFalse(store.IsAimingTurnOf(Me));
        }

        [Test]
        public void 내_차례여도_정산_국면이면_거짓이다()
        {
            var store = Store();
            store.Set(SettlingPhase, Me, 0, 0, NoDropOuts, NoEliminated);

            Assert.IsFalse(store.IsAimingTurnOf(Me));
        }

        [Test]
        public void 내_차례여도_탈락자면_거짓이다()
        {
            var store = Store();
            store.Set(AimingPhase, Me, 0, 0, NoDropOuts, new[] { Me });

            Assert.IsFalse(store.IsAimingTurnOf(Me));
        }
    }
}
