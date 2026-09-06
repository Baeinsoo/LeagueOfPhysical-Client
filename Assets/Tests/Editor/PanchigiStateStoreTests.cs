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

        //  아래 셋은 "조용히 false·0을 돌려주던 것"이 이제 터지는지 지킨다. 그 침묵이 실제로
        //  버그를 감췄다 — 부르는 쪽이 정체(userEntityId) 대신 생존(playerContext.entityId)을
        //  넘기고 있었는데, 두 값이 우연히 같아서 아무도 몰랐다.
        [Test]
        public void 조준_차례를_id_없이_물으면_터진다()
        {
            var store = Store();
            store.Set(AimingPhase, Me, 0, 0, NoDropOuts, NoEliminated);

            Assert.Throws<System.ArgumentException>(() => store.IsAimingTurnOf(null));
            Assert.Throws<System.ArgumentException>(() => store.IsAimingTurnOf(string.Empty));
        }

        [Test]
        public void 조준_국면이_아니어도_id_없는_질문은_터진다()
        {
            //  단축평가로 빠져나가던 자리다 — 국면이 아니면 검사까지 못 갔다.
            var store = Store();
            store.Set(SettlingPhase, Me, 0, 0, NoDropOuts, NoEliminated);

            Assert.Throws<System.ArgumentException>(() => store.IsAimingTurnOf(null));
        }

        [Test]
        public void 탈락_여부와_낙하_횟수도_id_없이_물으면_터진다()
        {
            var store = Store();
            store.Set(AimingPhase, Me, 0, 0, NoDropOuts, NoEliminated);

            Assert.Throws<System.ArgumentException>(() => store.IsEliminated(null));
            Assert.Throws<System.ArgumentException>(() => store.GetDropOutCount(string.Empty));
        }

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
