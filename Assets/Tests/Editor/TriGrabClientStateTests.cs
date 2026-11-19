using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    public class TriGrabClientStateTests
    {
        public static TriGrabStateToC Msg(int version, TriGrabPhase phase = TriGrabPhase.Live, long phaseEnd = 200)
        {
            var m = new TriGrabStateToC
            {
                Version = version, Phase = (int)phase, PhaseEndTick = phaseEnd, Round = 2,
                RuleType = (int)TriGrabRuleType.Ink, RuleColor = 1,
                BaseAngle0 = 0.3f, Omega = -0.5f, LiveStartTick = 100, FreezeTick = long.MaxValue, TargetScore = 5,
                RoundWinnerId = "", MatchWinnerId = "",
            };
            for (int i = 0; i < TriGrabConfig.Slots; i++)
            {
                var slot = new TriGrabSlotWire { EmptyUntilTick = 0 };
                //  3번 슬롯은 비었다(카드 없음).
                if (i != 3) slot.Card = new TriGrabCardWire { Id = 10 + i, Word = i % 3, Ink = (i + 1) % 3, Cycles = i == 5, PeriodTicks = 30, T0Tick = 100 };
                else slot.EmptyUntilTick = 150;
                m.Slots.Add(slot);
            }
            m.Hooks.Add(new TriGrabHookWire
            {
                OwnerId = "b", FireTick = 120, OriginX = 7f, OriginZ = 0f, DirX = -1f, DirZ = 0f, TurnTick = 128, TurnLength = 4.6f,
                Carried = new TriGrabCardWire { Id = 99, Word = 2, Ink = 2 }, Result = (int)TriGrabHookResult.Correct, EndTick = 137,
            });
            m.Players.Add(new TriGrabPlayerWire { EntityId = "a", Score = 1, Mistakes = 2, ReloadUntilTick = 140, BestRtMs = 500 });
            m.Players.Add(new TriGrabPlayerWire { EntityId = "b", Score = 3 });
            return m;
        }

        [Test]
        public void 새_판본이면_카드_시계_갈고리_점수를_Shared_타입으로_옮긴다()
        {
            var s = new TriGrabClientState();
            Assert.IsFalse(s.Received);
            Assert.IsTrue(s.Apply(Msg(1)));
            Assert.IsTrue(s.Received);

            Assert.AreEqual(TriGrabPhase.Live, s.Phase);
            Assert.AreEqual(2, s.Round);
            Assert.AreEqual(TriGrabRuleType.Ink, s.Rule.Type);
            Assert.AreEqual(1, s.Rule.Color);
            Assert.AreEqual(-0.5f, s.Clock.Omega);
            Assert.AreEqual(100L, s.Clock.LiveStartTick);

            Assert.IsNull(s.CardAt(3), "카드 없는 슬롯은 빈 슬롯");
            Assert.AreEqual(150L, s.EmptyUntilTick(3));
            var c5 = s.CardAt(5).Value;
            Assert.AreEqual(15, c5.Id);
            Assert.IsTrue(c5.Cycles);
            Assert.AreEqual(30, c5.PeriodTicks);
            //  같은 식 — 도는 카드는 T0+주기에 한 칸 간다.
            Assert.AreEqual((c5.Ink + 1) % 3, c5.InkAt(130));

            Assert.AreEqual(1, s.Hooks.Count);
            var h = s.HookOf("b");
            Assert.AreEqual(new Vector2(7f, 0f), h.Origin);
            Assert.AreEqual(TriGrabHookResult.Correct, h.Result);
            Assert.AreEqual(99, h.Carried.Value.Id);
            Assert.IsNull(s.HookOf("a"));

            CollectionAssert.AreEqual(new[] { "a", "b" }, s.PlayerIds);
            Assert.AreEqual(1, s.SeatOf("b"));
            Assert.IsTrue(s.TryGetPlayer("a", out var a));
            Assert.AreEqual(2, a.Mistakes);
            Assert.AreEqual(140L, a.ReloadUntilTick);
        }

        [Test]
        public void 늦게_도착한_낡은_판본은_버린다()
        {
            var s = new TriGrabClientState();
            var newer = Msg(5);
            newer.Players[0].Score = 4;
            s.Apply(newer);
            var older = Msg(4);
            older.Players[0].Score = 0;
            Assert.IsFalse(s.Apply(older));
            Assert.IsFalse(s.Apply(Msg(5)), "같은 판본도 버린다");
            s.TryGetPlayer("a", out var a);
            Assert.AreEqual(4, a.Score);
        }

        [Test]
        public void 준비_끝_틱이_지나면_소식_전에도_Live로_본다()
        {
            var s = new TriGrabClientState();
            s.Apply(Msg(1, TriGrabPhase.Prep, phaseEnd: 100));
            Assert.AreEqual(TriGrabPhase.Prep, s.PhaseAt(99));
            Assert.AreEqual(TriGrabPhase.Live, s.PhaseAt(100));
            s.Apply(Msg(2, TriGrabPhase.Resolved, phaseEnd: 300));
            Assert.AreEqual(TriGrabPhase.Resolved, s.PhaseAt(400), "쉼 뒤는 다음 라운드인지 모른다");
        }

        [Test]
        public void 소수_틱의_회전판_각은_이웃한_두_틱_사이를_잇는다()
        {
            var clock = new TriGrabRoundClock { BaseAngle0 = 1f, Omega = 0.5f, LiveStartTick = 10, FreezeTick = long.MaxValue };
            Assert.AreEqual(clock.BaseAt(20), TriGrabClientState.BaseAt(clock, 20.0), 1e-6f);
            float mid = TriGrabClientState.BaseAt(clock, 20.5);
            Assert.AreEqual((clock.BaseAt(20) + clock.BaseAt(21)) * 0.5f, mid, 1e-6f);
            Assert.AreNotEqual(clock.BaseAt(20), mid, "돌고 있으면 사이 값이 달라야 한다");
        }
    }
}
