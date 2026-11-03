using NUnit.Framework;

namespace LOP.Tests
{
    public class CardRushHeadCardTests
    {
        private const long Start = 1000;
        private static readonly CardRushCourse course = new CardRushCourse(77, 40);
        private static readonly CardRushTiming timing = new CardRushTiming(15, 30);

        private static CardRushHeadCardFace At(CardRushRunnerState s, long tick)
            => CardRushHeadCard.Compute(s, tick, Start, course, timing);

        [Test]
        public void 출발_전과_대기는_뒷면()
        {
            var s = new CardRushRunnerState();
            var before = At(s, Start - 50);
            Assert.IsTrue(before.Visible); Assert.IsFalse(before.FaceUp);
            var waiting = At(new CardRushRunnerState { Step = 3, MoveStartTick = 2000 }, 2100);
            Assert.IsTrue(waiting.Visible); Assert.IsFalse(waiting.FaceUp);
        }

        [Test]
        public void 돌면_그_틱의_색과_남은_시간()
        {
            var s = new CardRushRunnerState { Step = 3, MoveStartTick = 2000, SpinStartTick = 2100 };
            var f = At(s, 2150);
            Assert.IsTrue(f.Visible); Assert.IsTrue(f.FaceUp);
            Assert.IsTrue(CardRushCard.TryColorAt(s, 2150, course, out var c)); Assert.AreEqual(c, f.Color);
            Assert.AreEqual(1f - 50f / CardRushCard.WindowTicks, f.Remaining01, 1e-5f);
            Assert.AreEqual(1f, At(s, 2100).Remaining01, 1e-5f);
            Assert.Greater(At(s, 2399).Remaining01, 0f);
        }

        [Test]
        public void 이동_멈춤_완주엔_숨는다()
        {
            Assert.IsFalse(At(new CardRushRunnerState { Step = 3, MoveStartTick = 2000 }, 2010).Visible, "이동 중 — 카드는 던져졌다");
            Assert.IsFalse(At(new CardRushRunnerState { StunEndTick = 3000, Attempt = 1 }, 2990).Visible, "멈춤 중");
            Assert.IsFalse(At(new CardRushRunnerState { Step = 40, MoveStartTick = 2000 }, 2500).Visible, "완주 뒤");
        }

        //  옛 CardRushPadViewTests.카드는_다음_입력_틱으로_그린다를 옮겨 온 자리. 여기선 경계 틱(앞뒤 색이 갈리는 틱)만 잡는다 —
        //  뷰(TryComputeMine)가 PlayerInputManager.NextInputTick으로 칠하는지는 Task 5에서 뷰가 생기면 이 시험에 더한다.
        [Test]
        public void 내_카드는_다음_입력_틱으로_칠한다()
        {
            var s = new CardRushRunnerState { SpinStartTick = Start };
            long boundary = Start + CardRushCard.DwellTicks;
            var before = At(s, boundary - 1);
            var atBoundary = At(s, boundary);
            Assert.IsTrue(before.FaceUp); Assert.IsTrue(atBoundary.FaceUp);
            Assert.AreNotEqual(before.Color, atBoundary.Color, "경계 앞뒤 색이 달라야 틱 하나의 어긋남을 잡는다");
            Assert.IsTrue(CardRushCard.TryColorAt(s, boundary, course, out var judged));
            Assert.AreEqual(judged, atBoundary.Color, "경계 틱에 누르면 판정되는 색");
        }
    }
}
