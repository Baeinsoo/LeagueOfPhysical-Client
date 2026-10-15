using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>결과 단계 계산 — 반응 규칙, 등장 시각표, 공용 시상대 P1 자리, 등수 칸, 등수표 순서.</summary>
    public class ResultMathTests
    {
        //  ── 반응 규칙
        [TestCase(1, false, ResultReaction.Happy)]
        [TestCase(3, false, ResultReaction.Sad)]
        [TestCase(1, true, ResultReaction.None)]
        [TestCase(8, false, ResultReaction.Sad)]
        public void 반응_규칙(int placement, bool draw, ResultReaction expected) =>
            Assert.AreEqual(expected, ResultReactionRule.For(placement, draw));

        //  ── 등장 시각표
        [Test]
        public void 여덟_줄_시각()
        {
            var r = new ResultReveal(8);
            Assert.AreEqual(0.45f, r.RowAt(0), 1e-4);
            Assert.AreEqual(0.94f, r.RowAt(7), 1e-4);
            Assert.AreEqual(0.94f + 0.07f + 0.15f, r.LineAt(0), 1e-4);
            Assert.AreEqual(r.LineAt(1) + 0.10f, r.Ok, 1e-4);
        }

        [Test]
        public void 줄이_없어도_확인_시각이_있고_순서가_맞다()
        {
            var r = new ResultReveal(0);
            Assert.Greater(r.LineAt(0), ResultReveal.Board);
            Assert.Greater(r.Ok, r.LineAt(1));
        }

        [Test]
        public void 확인은_확인_시각_전엔_잠기고_건너뛰면_바로_열린다()
        {
            var r = new ResultReveal(4);
            Assert.IsFalse(r.ConfirmEnabled(r.Ok - 0.01f, skipped: false));
            Assert.IsTrue(r.ConfirmEnabled(r.Ok, skipped: false));
            Assert.IsTrue(r.ConfirmEnabled(0f, skipped: true));
        }

        //  ── P1 자리
        [Test]
        public void 여덟_명_P1()
        {
            var s = PodiumLayout.P1(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, false);
            Assert.AreEqual(8, s.Count);
            Assert.IsTrue(s[0].OnStep && s[1].OnStep && s[2].OnStep && !s[3].OnStep);
            Assert.Greater(s[0].Position.y, s[1].Position.y);          // 1위가 가장 높다
            Assert.Greater(s[1].Position.y, s[2].Position.y);
            Assert.Less(s[1].Position.x, s[0].Position.x);             // 2위 왼쪽
            Assert.Greater(s[2].Position.x, s[0].Position.x);          // 3위 오른쪽
            Assert.AreEqual(PodiumLayout.FrontRowScale, s[4].Scale, 1e-4);
            Assert.AreEqual(PodiumLayout.FrontRowZ, s[7].Position.z, 1e-4);
            Assert.AreEqual(s[3].Position.x + PodiumLayout.FrontRowGap, s[4].Position.x, 1e-4);
        }

        [Test]
        public void 두_명이면_1_2위_단만()
        {
            var s = PodiumLayout.P1(new[] { 1, 2 }, false);
            Assert.AreEqual(2, s.Count);
            Assert.IsTrue(s[0].OnStep && s[1].OnStep);
            Assert.Greater(s[0].Position.y, s[1].Position.y);
            Assert.Greater(s[0].Position.x, s[1].Position.x);          // 2위가 왼쪽
            //  가운데 둘로 붙는다 — 중심을 사이에 두고 반 칸씩
            Assert.AreEqual(PodiumLayout.Center.x + PodiumLayout.StepWidth / 2f, s[0].Position.x, 1e-4);
            Assert.AreEqual(PodiumLayout.Center.x - PodiumLayout.StepWidth / 2f, s[1].Position.x, 1e-4);
        }

        [Test]
        public void 혼자면_1위_단만() => Assert.AreEqual(1, PodiumLayout.P1(new[] { 1 }, false).Count);

        [Test]
        public void 단_번호는_등수_공동이면_같고_무승부면_빈칸()
        {
            var tie = PodiumLayout.P1(new[] { 1, 2, 2, 4 }, false);
            Assert.AreEqual(("1", "2", "2"), (tie[0].StepLabel, tie[1].StepLabel, tie[2].StepLabel));
            var draw = PodiumLayout.P1(new[] { 1, 1, 1 }, true);
            Assert.AreEqual("-", draw[0].StepLabel);
        }

        //  ── 등수 칸
        [Test]
        public void 등수_칸()
        {
            Assert.AreEqual("1", MatchResultViewModel.FormatRank(1, false, 1));
            Assert.AreEqual("공동 2", MatchResultViewModel.FormatRank(2, false, 2));
            Assert.AreEqual("-", MatchResultViewModel.FormatRank(1, true, 3));
        }

        //  ── 정렬
        [Test]
        public void 등수_다음_userId_순()
        {
            var sorted = MatchResultOrder.Sort(new[]
            {
                new MatchParticipantResult { userId = "b", placement = 2 },
                new MatchParticipantResult { userId = "z", placement = 1 },
                new MatchParticipantResult { userId = "a", placement = 2 },
            });
            Assert.AreEqual(new[] { "z", "a", "b" }, sorted.ConvertAll(p => p.userId).ToArray());
        }

        [Test]
        public void 참가자가_null이면_빈_목록() => Assert.AreEqual(0, MatchResultOrder.Sort(null).Count);
    }
}
