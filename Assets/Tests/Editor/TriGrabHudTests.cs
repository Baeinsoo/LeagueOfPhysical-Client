using System.Collections.Generic;
using NUnit.Framework;
using TG = LOP.TriGrab;

namespace LOP.Tests
{
    /// <summary>HUD 글 — 라운드·남은 정답 수·시간 막대·주문 띠·판 끝 순위.</summary>
    public class TriGrabHudTests
    {
        [Test]
        public void RemainingOk_Count()
        {
            var state = new TriGrabClientState();
            var m = TriGrabClientStateTests.Msg(1);
            m.Quota = 5;
            m.OkSpawned = 3;
            m.Cards.Add(new TriGrabRingCardWire { Id = 1, Ok = true });
            m.Cards.Add(new TriGrabRingCardWire { Id = 2, Ok = true, Gone = true });
            state.Apply(m);
            Assert.AreEqual(3, state.RemainingOk);
            Assert.AreEqual("라운드 3 / 10 · 정답 3장 남음",
                LOP.UI.TriGrabHudViewModel.RoundLine(state.Round, state.TotalRounds, TG.TriGrabPhase.Live, state.RemainingOk));
            Assert.AreEqual("라운드 3 / 10",
                LOP.UI.TriGrabHudViewModel.RoundLine(state.Round, state.TotalRounds, TG.TriGrabPhase.Prep, state.RemainingOk),
                "준비 중엔 남은 수를 안 붙인다");
            Assert.AreEqual("", LOP.UI.TriGrabHudViewModel.RoundLine(0, 10, TG.TriGrabPhase.Waiting, 0));
        }

        [Test]
        public void 시간_막대는_준비_가득_잡기_8초에_걸쳐_줄고_그_밖엔_숨긴다()
        {
            Assert.AreEqual(1f, LOP.UI.TriGrabHudViewModel.TimeLeft(TG.TriGrabPhase.Prep, 100, 155));
            long end = 1000;
            Assert.AreEqual(1f, LOP.UI.TriGrabHudViewModel.TimeLeft(TG.TriGrabPhase.Live, end - TG.TriGrabConfig.LiveLimitTicks, end), 1e-5f);
            Assert.AreEqual(0.5f, LOP.UI.TriGrabHudViewModel.TimeLeft(TG.TriGrabPhase.Live, end - TG.TriGrabConfig.LiveLimitTicks / 2, end), 1e-5f);
            Assert.AreEqual(-1f, LOP.UI.TriGrabHudViewModel.TimeLeft(TG.TriGrabPhase.Resolved, end, end));
        }

        [Test]
        public void 주문_띠는_목표만_미끼_색으로_칠한다()
        {
            var ink = new TG.TriGrabRule(TG.TriGrabRuleType.Ink, 0, 2);
            Assert.AreEqual($"글씨 색이 <color={TriGrabPalette.InkHtml(2)}>빨강</color>인 카드를 잡아라",
                LOP.UI.TriGrabHudViewModel.RuleRichText(ink));
            var match = new TG.TriGrabRule(TG.TriGrabRuleType.Match, 0, 1);
            Assert.AreEqual("글자와 글씨 색이 같은 카드를 잡아라", LOP.UI.TriGrabHudViewModel.RuleRichText(match));
        }

        [Test]
        public void 판_끝_띠는_1위_이름_라운드_끝은_라운드_끝()
        {
            Assert.AreEqual("가 우승!", LOP.UI.TriGrabHudViewModel.Toast(TG.TriGrabPhase.Over, 10, new[] { "가" }));
            Assert.AreEqual("가 · 나 우승!", LOP.UI.TriGrabHudViewModel.Toast(TG.TriGrabPhase.Over, 10, new[] { "가", "나" }), "공동 1위");
            Assert.AreEqual("라운드 4 끝", LOP.UI.TriGrabHudViewModel.Toast(TG.TriGrabPhase.Resolved, 4, new string[0]));
            Assert.AreEqual("", LOP.UI.TriGrabHudViewModel.Toast(TG.TriGrabPhase.Live, 4, new string[0]));
            Assert.AreEqual("1위 가 5 · 1위 나 5 · 3위 다 -2",
                LOP.UI.TriGrabHudViewModel.FinalLine(new List<(string, int, int)> { ("가", 1, 5), ("나", 1, 5), ("다", 3, -2) }));
        }

        [Test]
        public void 점수판은_점수_높은_순_같으면_좌석_순()
        {
            var state = new TriGrabClientState();
            var m = TriGrabClientStateTests.Msg(1);
            m.Players.Add(new TriGrabRingPlayerWire { EntityId = "a", Score = 1 });
            m.Players.Add(new TriGrabRingPlayerWire { EntityId = "b", Score = 4 });
            m.Players.Add(new TriGrabRingPlayerWire { EntityId = "c", Score = 1 });
            m.Players.Add(new TriGrabRingPlayerWire { EntityId = "d", Score = -2 });
            state.Apply(m);
            Assert.AreEqual(new[] { 1, 0, 2, 3 }, LOP.UI.TriGrabHudViewModel.RankOrder(state));
        }
    }
}
