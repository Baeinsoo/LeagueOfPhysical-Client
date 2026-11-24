using NUnit.Framework;
using TG = LOP.TriGrab;

namespace LOP.Tests
{
    /// <summary>원형 판 상태 받기 — 와이어를 Shared 타입으로 옮기고, 낡은 판본·되풀이된 연출은 버린다.</summary>
    public class TriGrabClientStateTests
    {
        public static TriGrabRingStateToC Msg(int version, TG.TriGrabPhase phase = TG.TriGrabPhase.Live, long phaseEnd = 600)
        {
            return new TriGrabRingStateToC
            {
                Version = version,
                Phase = (int)phase,
                PhaseEndTick = phaseEnd,
                Round = 3,
                TotalRounds = 10,
                RuleType = (int)TG.TriGrabRuleType.Ink,
                RuleValue = 2,
                RuleLure = 0,
                LiveStartTick = 200,
                Quota = 6,
                OkSpawned = 2,
            };
        }

        [Test]
        public void ClientState_AppliesCardsMolesHooksPlayers()
        {
            var state = new TriGrabClientState();
            var m = Msg(4);
            m.Cards.Add(new TriGrabRingCardWire { Id = 7, Word = 1, Ink = 2, Fill = 3, Ok = true, BornTick = 210, CarriedBy = "" });
            m.Cards.Add(new TriGrabRingCardWire { Id = 8, Word = 0, Ink = 1, Fill = 2, Ok = false, BornTick = 220, CarriedBy = "b" });
            m.Hooks.Add(new TriGrabRingHookWire
            {
                OwnerId = "b", FireTick = 230, OriginX = 7f, OriginZ = -1f, DirX = -1f, DirZ = 0f,
                State = (int)TG.TriGrabHookState.Back, End = (int)TG.TriGrabHookEnd.Card, CardId = 8,
                TurnTick = 240, TurnLength = 6.5f, TipX = 1f, TipZ = -1f,
            });
            m.Moles.Add(new TriGrabRingMoleWire { Id = 2, X = 1.5f, Z = -2f, SpawnTick = 225, StandTicks = 70, BonkTick = -1, Say = 3, BonkLine = 4 });
            m.Players.Add(new TriGrabRingPlayerWire { EntityId = "a", Score = 3, Correct = 4, Wrong = 1, StunUntilTick = 0, CooldownUntilTick = 250, Place = 1 });
            m.Players.Add(new TriGrabRingPlayerWire { EntityId = "b", Score = -1, Correct = 0, Wrong = 1, StunUntilTick = 280, Place = 2 });

            Assert.IsTrue(state.Apply(m));

            Assert.AreEqual(TG.TriGrabPhase.Live, state.Phase);
            Assert.AreEqual(3, state.Round);
            Assert.AreEqual(10, state.TotalRounds);
            Assert.AreEqual(TG.TriGrabRuleType.Ink, state.Rule.Type);
            Assert.AreEqual(2, state.Rule.Value);
            Assert.AreEqual(0, state.Rule.Lure);
            Assert.AreEqual(6, state.Quota);
            Assert.AreEqual(2, state.OkSpawned);

            Assert.AreEqual(2, state.Cards.Count);
            Assert.IsTrue(state.TryGetCard(7, out var c7));
            Assert.AreEqual((1, 2, 3, true, 210L), (c7.Word, c7.Ink, c7.Fill, c7.Ok, c7.BornTick));
            Assert.IsNull(c7.CarriedBy, "빈 문자열 = 안 끌려감");
            Assert.IsTrue(state.TryGetCard(8, out var c8));
            Assert.AreEqual("b", c8.CarriedBy);

            var hook = state.HookOf("b");
            Assert.IsNotNull(hook);
            Assert.AreEqual(230, hook.FireTick);
            Assert.AreEqual(new System.Numerics.Vector2(7f, -1f), hook.Origin);
            Assert.AreEqual(new System.Numerics.Vector2(-1f, 0f), hook.Dir);
            Assert.AreEqual(TG.TriGrabHookState.Back, hook.State);
            Assert.AreEqual(TG.TriGrabHookEnd.Card, hook.End);
            Assert.AreEqual(8, hook.CardId);
            Assert.AreEqual(240, hook.TurnTick);
            Assert.AreEqual(6.5f, hook.TurnLength);
            Assert.IsNull(state.HookOf("a"));

            Assert.AreEqual(1, state.Moles.Count);
            var mole = state.Moles[0];
            Assert.AreEqual((2, 225L, 70, -1L, 3, 4), (mole.Id, mole.SpawnTick, mole.StandTicks, mole.BonkTick, mole.SayIndex, mole.BonkLineIndex));
            Assert.AreEqual(new System.Numerics.Vector2(1.5f, -2f), mole.Pos);
            Assert.AreEqual(1f, mole.HeightAt(225 + TG.TriGrabMole.RiseTicks), "두더지 높이는 Shared 식 그대로");

            Assert.AreEqual(new[] { "a", "b" }, state.PlayerIds);
            Assert.IsTrue(state.TryGetPlayer("b", out var pb));
            Assert.AreEqual((-1, 0, 1, 280L), (pb.Score, pb.Correct, pb.Wrong, pb.StunUntilTick));
            Assert.IsTrue(state.TryGetPlayer("a", out var pa));
            Assert.AreEqual(250, pa.CooldownUntilTick);
            Assert.AreEqual(1, state.PlaceOf("a"));
            Assert.AreEqual(2, state.PlaceOf("b"));
        }

        [Test]
        public void 늦게_도착한_낡은_판본은_버린다()
        {
            var state = new TriGrabClientState();
            var m5 = Msg(5);
            m5.Players.Add(new TriGrabRingPlayerWire { EntityId = "a", Score = 5 });
            Assert.IsTrue(state.Apply(m5));
            var m4 = Msg(4);
            m4.Players.Add(new TriGrabRingPlayerWire { EntityId = "a", Score = 1 });
            Assert.IsFalse(state.Apply(m4));
            Assert.IsFalse(state.Apply(Msg(5)), "같은 판본도 버린다");
            state.TryGetPlayer("a", out var p);
            Assert.AreEqual(5, p.Score);
        }

        [Test]
        public void Fx_NotRepeatedOnResend()
        {
            var state = new TriGrabClientState();
            var m = Msg(1);
            m.Fx.Add(new TriGrabRingFxWire { Kind = (int)TG.TriGrabFxKind.Correct, X = 1f, Z = 2f, OwnerId = "a", Tick = 300 });
            m.Fx.Add(new TriGrabRingFxWire { Kind = (int)TG.TriGrabFxKind.MoleBonk, X = 0f, Z = 0f, OwnerId = "b", Line = 4, Tick = 300 });
            state.Apply(m);
            Assert.AreEqual(2, state.PendingFxCount);
            Assert.IsTrue(state.TryDequeueFx(out var first));
            Assert.AreEqual(TG.TriGrabFxKind.Correct, first.Kind);
            Assert.AreEqual(new UnityEngine.Vector2(1f, 2f), first.Pos);
            Assert.IsTrue(state.TryDequeueFx(out var second));
            Assert.AreEqual(4, second.Line);
            Assert.IsFalse(state.TryDequeueFx(out _));

            //  같은 연출이 더 높은 판본에 다시 실려 와도(재전송) 다시 띄우지 않는다.
            var again = Msg(2);
            again.Fx.Add(new TriGrabRingFxWire { Kind = (int)TG.TriGrabFxKind.Correct, X = 1f, Z = 2f, OwnerId = "a", Tick = 300 });
            //  양성 대조 — 같은 사람의 다음 틱 정답은 새 연출이다.
            again.Fx.Add(new TriGrabRingFxWire { Kind = (int)TG.TriGrabFxKind.Correct, X = 1f, Z = 2f, OwnerId = "a", Tick = 340 });
            state.Apply(again);
            Assert.AreEqual(1, state.PendingFxCount);
            Assert.IsTrue(state.TryDequeueFx(out var third));
            Assert.AreEqual(340, third.Tick);
        }

        [Test]
        public void 남은_정답_수는_할당량_빼기_만든_수_더하기_판_위_정답()
        {
            var state = new TriGrabClientState();
            var m = Msg(1);
            m.Quota = 6;
            m.OkSpawned = 4;
            m.Cards.Add(new TriGrabRingCardWire { Id = 1, Ok = true });
            m.Cards.Add(new TriGrabRingCardWire { Id = 2, Ok = true, CarriedBy = "a" });
            m.Cards.Add(new TriGrabRingCardWire { Id = 3, Ok = true, Gone = true });
            m.Cards.Add(new TriGrabRingCardWire { Id = 4, Ok = false });
            state.Apply(m);
            Assert.AreEqual(6 - 4 + 2, state.RemainingOk, "끌려가는 정답은 아직 남은 것, 사라진 것·틀린 카드는 아니다");
        }

        [Test]
        public void 준비_끝_틱과_시간_상한_틱은_소식_전에도_단계를_넘긴다()
        {
            var state = new TriGrabClientState();
            state.Apply(Msg(1, TG.TriGrabPhase.Prep, phaseEnd: 500));
            Assert.AreEqual(TG.TriGrabPhase.Prep, state.PhaseAt(499));
            Assert.AreEqual(TG.TriGrabPhase.Live, state.PhaseAt(500));
            state.Apply(Msg(2, TG.TriGrabPhase.Live, phaseEnd: 900));
            Assert.AreEqual(TG.TriGrabPhase.Live, state.PhaseAt(899));
            Assert.AreEqual(TG.TriGrabPhase.Resolved, state.PhaseAt(900));
        }
    }
}
