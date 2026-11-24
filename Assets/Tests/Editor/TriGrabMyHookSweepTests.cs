using NUnit.Framework;
using UnityEngine;
using TG = LOP.TriGrab;

namespace LOP.Tests
{
    /// <summary>
    /// 미리 그린 내 손(리뷰 I1) — 서버 소식 전에도 화면에 보이는 카드·두더지에서 돌아선다.
    /// 판정은 서버가 하고, 이건 그림만이다.
    /// </summary>
    public class TriGrabMyHookSweepTests
    {
        private const double Remote = 150.0;
        private const long FireTick = 200;

        private static TriGrabClientState LiveState(params TriGrabRingCardWire[] cards)
        {
            var state = new TriGrabClientState();
            var m = TriGrabClientStateTests.Msg(1, TG.TriGrabPhase.Live, phaseEnd: 100000);
            foreach (var c in cards) m.Cards.Add(c);
            state.Apply(m);
            return state;
        }

        private static TriGrabCardTrack Track(TriGrabClientState state, params (int id, float x, float z)[] cards)
        {
            var track = new TriGrabCardTrack(state);
            foreach (long t in new long[] { 140, 160 })
            {
                var ids = new int[cards.Length];
                var xs = new float[cards.Length];
                var zs = new float[cards.Length];
                for (int i = 0; i < cards.Length; i++) { ids[i] = cards[i].id; xs[i] = cards[i].x; zs[i] = cards[i].z; }
                track.Push(t, ids, xs, zs);
            }
            return track;
        }

        // yaw 90 = +x 쪽.
        private static TriGrabMyHook FireEast(TriGrabClientState state, TriGrabCardTrack track)
        {
            var mine = new TriGrabMyHook(state, track, () => Remote);
            mine.Fire("me", Vector2.zero, 90f, FireTick);
            return mine;
        }

        [Test]
        public void 미리_그린_손은_화면_속_카드에서_돌아선다()
        {
            var state = LiveState(new TriGrabRingCardWire { Id = 5, Ok = true, BornTick = 0 });
            var mine = FireEast(state, Track(state, (5, 3f, 0f)));

            var h = mine.Current("me", FireTick + 10);
            Assert.AreSame(mine.Predicted, h, "서버 소식 전엔 미리 그린 손");
            Assert.AreEqual(TG.TriGrabHookState.Back, h.State, "첫 닿음에서 돌아섰다");
            Assert.AreEqual(TG.TriGrabHookEnd.Card, h.End);
            Assert.AreEqual(5, h.CardId);
            for (long t = FireTick; t < FireTick + 60; t++)
            {
                Assert.LessOrEqual(TriGrabHookPath.LengthAt(h, t), 3f, "카드를 지나쳐 뻗지 않는다(틱 " + t + ")");
            }
            Assert.Less(TriGrabHookPath.EndTick(h), FireTick + 15, "사거리 끝까지 갔다 오지 않는다");
        }

        [Test]
        public void 돌아오는_동안_닿은_카드는_집게에_붙는다()
        {
            var state = LiveState(new TriGrabRingCardWire { Id = 5, Ok = true, BornTick = 0 },
                                  new TriGrabRingCardWire { Id = 6, Ok = true, BornTick = 0 });
            var track = Track(state, (5, 3f, 0f), (6, 0f, 3f));
            var mine = FireEast(state, track);
            var h = mine.Current("me", FireTick + 10);
            double local = h.TurnTick + 1.5;
            state.TryGetCard(5, out var card);
            Assert.IsTrue(TriGrabCardsView.TryCardPosition(card, state, track, mine, "me", Remote, local, out var pos, out bool carried));
            Assert.IsTrue(carried);
            Assert.Less(Vector2.Distance(TriGrabHookPath.TipAt(h, local), pos), 1e-4f);

            state.TryGetCard(6, out var other);
            Assert.IsTrue(TriGrabCardsView.TryCardPosition(other, state, track, mine, "me", Remote, local, out var otherPos, out bool otherCarried));
            Assert.IsFalse(otherCarried, "안 닿은 카드는 판 위(양성 대조)");
            Assert.Less(Vector2.Distance(new Vector2(0f, 3f), otherPos), 1e-4f);

            Assert.IsFalse(TriGrabCardsView.TryCardPosition(card, state, track, mine, "me", Remote, TriGrabHookPath.EndTick(h) + 1, out _, out _),
                           "몸에 닿은 뒤엔 서버 소식 전에도 판에 다시 나타나지 않는다");
        }

        [Test]
        public void 두더지가_카드보다_먼저_막는다()
        {
            var state = new TriGrabClientState();
            var m = TriGrabClientStateTests.Msg(1, TG.TriGrabPhase.Live, phaseEnd: 100000);
            m.Cards.Add(new TriGrabRingCardWire { Id = 5, Ok = true, BornTick = 0 });
            //  원격 시각 150엔 다 솟아 서 있다.
            m.Moles.Add(new TriGrabRingMoleWire { Id = 1, X = 2f, Z = 0f, SpawnTick = 120, StandTicks = 90, BonkTick = -1 });
            state.Apply(m);
            var mine = FireEast(state, Track(state, (5, 3f, 0f)));
            var h = mine.Current("me", FireTick + 10);
            Assert.AreEqual(TG.TriGrabHookEnd.Mole, h.End);
            Assert.AreEqual(-1, h.CardId);
        }

        [Test]
        public void 끌려가는_카드와_막_나타나는_카드는_잡지_않는다()
        {
            var state = LiveState(new TriGrabRingCardWire { Id = 5, Ok = true, BornTick = 0, CarriedBy = "b" },
                                  new TriGrabRingCardWire { Id = 6, Ok = true, BornTick = 149 });
            var mine = FireEast(state, Track(state, (5, 3f, 0f), (6, 5f, 0f)));
            var h = mine.Current("me", FireTick + 30);
            Assert.AreNotEqual(TG.TriGrabHookEnd.Card, h.End, "끌려가는 카드·덜 나타난 카드(PopK < 0.5)는 통과");
        }

        [Test]
        public void 서버가_아직_나가는_중이면_돌아선_미리_그린_손을_계속_그린다()
        {
            var state = LiveState(new TriGrabRingCardWire { Id = 5, Ok = true, BornTick = 0 });
            var mine = FireEast(state, Track(state, (5, 3f, 0f)));
            var predicted = mine.Current("me", FireTick + 5);
            Assert.AreEqual(TG.TriGrabHookState.Back, predicted.State);

            var m = TriGrabClientStateTests.Msg(2, TG.TriGrabPhase.Live, phaseEnd: 100000);
            m.Cards.Add(new TriGrabRingCardWire { Id = 5, Ok = true, BornTick = 0 });
            m.Hooks.Add(new TriGrabRingHookWire { OwnerId = "me", FireTick = FireTick, DirX = 1f, State = (int)TG.TriGrabHookState.Out });
            state.Apply(m);
            Assert.AreSame(predicted, mine.Current("me", FireTick + 6), "서버가 아직 정하지 않았다 — 사거리 끝으로 다시 뻗지 않는다");
        }

        [Test]
        public void 서버가_다른_결과면_서버가_이긴다()
        {
            var state = LiveState(new TriGrabRingCardWire { Id = 5, Ok = true, BornTick = 0 });
            var mine = FireEast(state, Track(state, (5, 3f, 0f)));
            Assert.AreEqual(5, mine.Current("me", FireTick + 5).CardId);

            var m = TriGrabClientStateTests.Msg(2, TG.TriGrabPhase.Live, phaseEnd: 100000);
            m.Cards.Add(new TriGrabRingCardWire { Id = 5, Ok = true, BornTick = 0, CarriedBy = "b" });
            m.Hooks.Add(new TriGrabRingHookWire
            {
                OwnerId = "me", FireTick = FireTick, DirX = 1f, State = (int)TG.TriGrabHookState.Back,
                End = (int)TG.TriGrabHookEnd.Range, CardId = -1, TurnTick = FireTick + 18, TurnLength = TG.TriGrabConfig.HookRange,
            });
            state.Apply(m);
            var h = mine.Current("me", FireTick + 7);
            Assert.IsNull(mine.Predicted);
            Assert.AreEqual(-1, h.CardId);
            state.TryGetCard(5, out var card);
            var track = Track(state, (5, 3f, 0f));
            TriGrabCardsView.TryCardPosition(card, state, track, mine, "me", Remote, FireTick + 7, out _, out bool carried);
            Assert.IsFalse(carried, "서버가 안 준 카드는 내 집게에 붙이지 않는다");
        }
    }
}
