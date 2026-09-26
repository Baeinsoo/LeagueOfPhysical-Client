using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryArrowDisplayTests
    {
        const float Trail = 0.2f;

        [Test]
        public void 제때_본_화살의_꼬리는_정해진_길이만큼_뒤에_있다()
        {
            Assert.AreEqual(0.1f, ArcheryArrowDisplay.TrailTailSeconds(0.3f, 0f, Trail), 1e-5f);
        }

        [Test]
        public void 막_쏜_화살의_꼬리는_활에서_시작한다()
        {
            Assert.AreEqual(0f, ArcheryArrowDisplay.TrailTailSeconds(0.05f, 0f, Trail), 1e-5f);
        }

        [Test]
        public void 늦게_본_화살의_꼬리는_처음_본_자리에서_시작한다()
        {
            //  0.4초 지점에서 처음 봤다 — 그 전 길은 본 적이 없으니 꼬리가 없다.
            Assert.AreEqual(0.4f, ArcheryArrowDisplay.TrailTailSeconds(0.4f, 0.4f, Trail), 1e-5f);
        }

        [Test]
        public void 늦게_본_화살의_꼬리는_자라서_보통_길이가_된다()
        {
            Assert.AreEqual(0.4f, ArcheryArrowDisplay.TrailTailSeconds(0.5f, 0.4f, Trail), 1e-5f);
            Assert.AreEqual(0.6f - Trail, ArcheryArrowDisplay.TrailTailSeconds(0.6f, 0.4f, Trail), 1e-5f);
            Assert.AreEqual(0.9f - Trail, ArcheryArrowDisplay.TrailTailSeconds(0.9f, 0.4f, Trail), 1e-5f);
        }

        [Test]
        public void 꼬리는_앞으로만_간다()
        {
            float prev = -1f;
            for (float t = 0.4f; t < 1f; t += 0.01f)
            {
                float tail = ArcheryArrowDisplay.TrailTailSeconds(t, 0.4f, Trail);
                Assert.GreaterOrEqual(tail, prev - 1e-5f);
                Assert.LessOrEqual(tail, t);
                prev = tail;
            }
        }

        [Test]
        public void 제때_본_화살은_늦추지_않는다()
        {
            Assert.AreEqual(0f, ArcheryArrowDisplay.DisplayDelaySeconds(0f, 0.5f), 1e-5f);
        }

        [Test]
        public void 늦게_본_화살은_늦게_본_만큼_늦춰_활에서_출발한다()
        {
            float delay = ArcheryArrowDisplay.DisplayDelaySeconds(0.2f, 0.5f);
            Assert.AreEqual(0.2f, delay, 1e-5f);
            Assert.AreEqual(0f, 0.2f - delay, 1e-5f);   // 처음 본 순간 그려지는 시각 = 활(0초)
        }

        [Test]
        public void 너무_늦게_본_화살은_상한까지만_늦춘다()
        {
            Assert.AreEqual(0.5f, ArcheryArrowDisplay.DisplayDelaySeconds(2f, 0.5f), 1e-5f);
        }

        [Test]
        public void 서버가_맞았다고_한_화살은_사라지는_과녁이면_숨긴다()
        {
            Assert.IsTrue(ArcheryArrowDisplay.HideConfirmedHit(true, hasImpact: true, targetConsumedOnHit: true));
        }

        [Test]
        public void 안_사라지는_과녁에_꽂힌_화살은_남긴다()
        {
            Assert.IsFalse(ArcheryArrowDisplay.HideConfirmedHit(true, hasImpact: true, targetConsumedOnHit: false));
        }

        [Test]
        public void 서버는_맞았다는데_클라가_꽂힌_자리를_모르면_숨긴다()
        {
            Assert.IsTrue(ArcheryArrowDisplay.HideConfirmedHit(true, hasImpact: false, targetConsumedOnHit: false));
        }

        [Test]
        public void 아직_맞았다는_소식이_없으면_숨기지_않는다()
        {
            Assert.IsFalse(ArcheryArrowDisplay.HideConfirmedHit(false, hasImpact: true, targetConsumedOnHit: true));
        }
    }
}
