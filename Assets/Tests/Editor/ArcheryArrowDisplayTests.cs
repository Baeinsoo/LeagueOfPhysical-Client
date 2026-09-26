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
        public void 늦게_본_화살은_처음_보는_순간_활부터_지나온_길_전체를_그린다()
        {
            //  0.4초 지점에서 처음 봤다 — 순간이동처럼 보이지 않게 활(0초)부터 선을 긋는다.
            Assert.AreEqual(0f, ArcheryArrowDisplay.TrailTailSeconds(0.4f, 0.4f, Trail), 1e-5f);
        }

        [Test]
        public void 늦게_본_화살의_긴_꼬리는_정해진_시간_안에_보통_길이로_줄어든다()
        {
            float mid = ArcheryArrowDisplay.TrailTailSeconds(0.5f, 0.4f, Trail);
            Assert.Greater(mid, 0f);
            Assert.Less(mid, 0.5f - Trail);
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
