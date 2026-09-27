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

        private static readonly (string, IReadOnlyList<PanchigiRoll>)[] NoPlayers = System.Array.Empty<(string, IReadOnlyList<PanchigiRoll>)>();

        private static PanchigiStateStore Store() => new PanchigiStateStore();

        //  아래 셋은 "조용히 false·0을 돌려주던 것"이 이제 터지는지 지킨다. 그 침묵이 실제로
        //  버그를 감췄다 — 부르는 쪽이 정체(userEntityId) 대신 생존(playerContext.entityId)을
        //  넘기고 있었는데, 두 값이 우연히 같아서 아무도 몰랐다.
        [Test]
        public void 조준_차례를_id_없이_물으면_터진다()
        {
            var store = Store();
            store.Set(AimingPhase, Me, 0, NoPlayers);

            Assert.Throws<System.ArgumentException>(() => store.IsAimingTurnOf(null));
            Assert.Throws<System.ArgumentException>(() => store.IsAimingTurnOf(string.Empty));
        }

        [Test]
        public void 조준_국면이_아니어도_id_없는_질문은_터진다()
        {
            //  단축평가로 빠져나가던 자리다 — 국면이 아니면 검사까지 못 갔다.
            var store = Store();
            store.Set(SettlingPhase, Me, 0, NoPlayers);

            Assert.Throws<System.ArgumentException>(() => store.IsAimingTurnOf(null));
        }

        [Test]
        public void 타격_기록도_id_없이_물으면_터진다()
        {
            var store = Store();
            store.Set(AimingPhase, Me, 0, NoPlayers);

            Assert.Throws<System.ArgumentException>(() => store.Rolls(null));
        }

        [Test]
        public void 조준_국면에서_내_차례면_참이다()
        {
            var store = Store();
            store.Set(AimingPhase, Me, 0, NoPlayers);

            Assert.IsTrue(store.IsAimingTurnOf(Me));
        }

        [Test]
        public void 조준_국면이어도_남의_차례면_거짓이다()
        {
            var store = Store();
            store.Set(AimingPhase, Other, 0, NoPlayers);

            Assert.IsFalse(store.IsAimingTurnOf(Me));
        }

        [Test]
        public void 내_차례여도_정산_국면이면_거짓이다()
        {
            var store = Store();
            store.Set(SettlingPhase, Me, 0, NoPlayers);

            Assert.IsFalse(store.IsAimingTurnOf(Me));
        }

        [Test]
        public void 판_주인은_조준하는_사람이다()
        {
            var store = Store();
            store.Set(AimingPhase, Other, 0, NoPlayers);

            Assert.AreEqual(Other, store.BoardOwnerEntityId);
        }

        [Test]
        public void 정산_중에는_직전_조준자의_판이다()
        {
            //  서버는 동전이 구르는 동안 차례를 비워 보낸다 — 그때 화면의 판은 방금 친 사람 것이다.
            var store = Store();
            store.Set(AimingPhase, Me, 0, NoPlayers);
            store.Set(SettlingPhase, string.Empty, 0, NoPlayers);

            Assert.AreEqual(Me, store.BoardOwnerEntityId);
        }

        private static (string, IReadOnlyList<PanchigiRoll>) P(string id, params int[] flips)
        {
            var list = new List<PanchigiRoll>();
            foreach (int f in flips) { list.Add(f < 0 ? PanchigiRoll.Fouled : new PanchigiRoll(f)); }
            return (id, list);
        }

        [Test]
        public void 사람별_타격_기록을_참가_순서대로_담는다()
        {
            var store = Store();
            store.Set(AimingPhase, Other, 0, new[] { P(Me, 6, 2, 1), P(Other, -1) });

            CollectionAssert.AreEqual(new[] { Me, Other }, store.PlayerEntityIds);
            Assert.AreEqual(3, store.Rolls(Me).Count);
            Assert.IsTrue(store.Rolls(Other)[0].Foul);
        }

        [Test]
        public void 다섯_프레임을_다_친_사람은_끝났다()
        {
            var store = Store();
            store.Set(AimingPhase, Other, 0, new[] { P(Me, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1), P(Other) });

            Assert.IsTrue(store.IsFinished(Me, 5, 6));
            Assert.IsFalse(store.IsFinished(Other, 5, 6));
        }
    }
}
