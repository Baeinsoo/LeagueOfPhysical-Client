using NUnit.Framework;

namespace LOP.Tests
{
    public class CardRushThrowFxTests
    {
        private static readonly CardRushTiming timing = new CardRushTiming(15, 30);

        [Test]
        public void 맞음은_3틱_동안_문으로_날고_꽂힌다()
        {
            var s = new CardRushRunnerState { Step = 4, MoveStartTick = 2000 };
            Assert.AreEqual(CardRushThrowKind.None, CardRushThrowFx.Phase(s, 1999.5, timing).kind);
            Assert.AreEqual(CardRushThrowKind.ToDoor, CardRushThrowFx.Phase(s, 2001.0, timing).kind);
            Assert.AreEqual(CardRushThrowKind.None, CardRushThrowFx.Phase(s, 2003.0, timing).kind, "3틱에 꽂혀 날던 카드는 끝");
            Assert.IsFalse(CardRushThrowFx.IsStuckIn(s, 2002.9, 3));
            Assert.IsTrue(CardRushThrowFx.IsStuckIn(s, 2003.0, 3), "방금 건넌 문(칸 3)에 꽂힘");
            Assert.IsTrue(CardRushThrowFx.IsStuckIn(s, 2000.0, 2), "그 전 문들은 이미 꽂혀 있다");
            Assert.IsFalse(CardRushThrowFx.IsStuckIn(s, 2500.0, 4), "아직 안 맞힌 문");
        }

        [Test]
        public void 틀림은_문에_튕겨_7틱에_얼굴()
        {
            var s = new CardRushRunnerState { StunEndTick = 3030, Attempt = 1 };   // 던짐 3000
            var (k0, _) = CardRushThrowFx.Phase(s, 3001.0, timing);
            Assert.AreEqual(CardRushThrowKind.ToDoor, k0);
            var (k1, since1) = CardRushThrowFx.Phase(s, 3005.0, timing);
            Assert.AreEqual(CardRushThrowKind.BackToFace, k1); Assert.AreEqual(5.0, since1, 1e-9);
            Assert.AreEqual(CardRushThrowKind.BackToFace, CardRushThrowFx.Phase(s, 3007.0 + CardRushThrowFx.DropTicks - 0.1, timing).kind);
            Assert.AreEqual(CardRushThrowKind.None, CardRushThrowFx.Phase(s, 3007.0 + CardRushThrowFx.DropTicks, timing).kind);
            Assert.AreEqual(7, CardRushThrowFx.SlapTick);
        }

        [Test]
        public void 맞힌_뒤의_틀림은_틀림_시간표_틀린_뒤의_맞음은_맞음_시간표()
        {
            //  상태엔 맞음·틀림이 둘 다 남는다 — 더 늦은 던짐이 지금 것이다.
            var wrongLater = new CardRushRunnerState { Step = 4, MoveStartTick = 2000, StunEndTick = 3030, Attempt = 1 };
            Assert.AreEqual(CardRushThrowKind.BackToFace, CardRushThrowFx.Phase(wrongLater, 3005.0, timing).kind);
            var rightLater = new CardRushRunnerState { Step = 5, MoveStartTick = 3100, StunEndTick = 3030 };
            Assert.AreEqual(CardRushThrowKind.ToDoor, CardRushThrowFx.Phase(rightLater, 3101.0, timing).kind);
            Assert.AreEqual(CardRushThrowKind.None, CardRushThrowFx.Phase(rightLater, 3005.0, timing).kind, "지난 틀림은 다시 안 그린다");
        }

        //  원장 판정: 틀린 카드도 던진 색으로 날아간다. 시뮬이 던진 색과 같아야 한다 — 판정기로 실제로 던져 본다.
        [Test]
        public void 날아가는_카드는_던진_색이다()
        {
            var course = new CardRushCourse(77, 40);
            const long Start = 1000;
            int right = 0, wrong = 0;
            //  칸·시도마다 시작 색이 다르다 — 틀린 던짐은 "던지기 전 시도"로 다시 세야 맞는다.
            for (int step = 0; step < 4; step++)
            for (int attempt = 0; attempt < 4; attempt++)
            for (int held = 1; held <= CardRushCard.WindowTicks; held++)
            {
                var s = new CardRushRunnerState { Step = step, Attempt = attempt };
                Assert.AreEqual(CardRushTapResult.Started, CardRushJudge.Advance(ref s, Start, true, Start, course, timing));
                long spinStart = s.SpinStartTick;
                long throwTick = spinStart + held;
                Assert.IsTrue(CardRushCard.TryColorAt(s, throwTick, course, out var shown));
                var result = CardRushJudge.Advance(ref s, throwTick, true, Start, course, timing);
                if (result == CardRushTapResult.Correct) right++;
                else if (result == CardRushTapResult.Wrong) wrong++;
                else Assert.Fail($"{result} 칸 {step} 시도 {attempt} @ {held}틱");

                Assert.IsTrue(CardRushThrowFx.TryThrownColor(s, spinStart, course, timing, out var drawn), $"{result} 칸 {step} 시도 {attempt} @ {held}틱");
                Assert.AreEqual(shown, drawn, $"{result} 칸 {step} 시도 {attempt} @ {held}틱");
            }
            Assert.Greater(right, 0); Assert.Greater(wrong, 0, "틀린 던짐도 시험해야 판정의 뜻이 있다");
        }

        [Test]
        public void 시작_틱을_모르면_틀린_카드는_뒷면()
        {
            var course = new CardRushCourse(77, 40);
            var s = new CardRushRunnerState { StunEndTick = 3030, Attempt = 1 };
            Assert.IsFalse(CardRushThrowFx.TryThrownColor(s, 0, course, timing, out _));
            Assert.IsFalse(CardRushThrowFx.TryThrownColor(s, 3001, course, timing, out _), "던진 뒤에 시작했을 리 없다");
        }
    }
}
