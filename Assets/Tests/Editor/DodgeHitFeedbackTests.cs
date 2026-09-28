using NUnit.Framework;

namespace LOP.Tests
{
    public class DodgeHitFeedbackTests
    {
        [Test]
        public void 무적이_아니면_늘_보인다()
        {
            Assert.IsTrue(DodgeHitFeedback.BodyVisible(100, -1));
            Assert.IsTrue(DodgeHitFeedback.BodyVisible(100, 100));   // 끝 틱부터는 무적이 아니다
            Assert.IsTrue(DodgeHitFeedback.BodyVisible(100, 50));
        }

        [Test]
        public void 무적_동안에는_깜빡인다()
        {
            // 무적 끝까지 남은 틱을 BlinkTicks씩 끊어 켜고 끈다 — 보이다 안 보이다가 번갈아 나와야 한다.
            long until = 100;
            int hidden = 0, shown = 0;
            for (long t = until - 4 * DodgeHitFeedback.BlinkTicks; t < until; t++)
            {
                if (DodgeHitFeedback.BodyVisible(t, until)) shown++; else hidden++;
            }
            Assert.AreEqual(2 * DodgeHitFeedback.BlinkTicks, hidden);
            Assert.AreEqual(2 * DodgeHitFeedback.BlinkTicks, shown);
        }

        [Test]
        public void 목숨이_줄면_그_틱을_맞은_틱으로_기억한다()
        {
            Assert.AreEqual(500, DodgeHitFeedback.NextHitTick(true, 3, 2, -1, 500));
            Assert.AreEqual(-1, DodgeHitFeedback.NextHitTick(true, 3, 3, -1, 500));
            Assert.AreEqual(420, DodgeHitFeedback.NextHitTick(true, 2, 2, 420, 500));
        }

        [Test]
        public void 처음_받은_목숨은_맞음이_아니다()
        {
            // 판 시작 전(모름) → 99를 처음 받는 순간이나, 모르게 된 순간은 번쩍이지 않는다.
            Assert.AreEqual(-1, DodgeHitFeedback.NextHitTick(false, 0, 99, -1, 500));
        }

        [Test]
        public void 번쩍임은_잠깐만()
        {
            Assert.IsFalse(DodgeHitFeedback.Flashing(500, -1));
            Assert.IsTrue(DodgeHitFeedback.Flashing(500, 500));
            Assert.IsTrue(DodgeHitFeedback.Flashing(500 + DodgeHitFeedback.FlashTicks - 1, 500));
            Assert.IsFalse(DodgeHitFeedback.Flashing(500 + DodgeHitFeedback.FlashTicks, 500));
        }
    }
}
