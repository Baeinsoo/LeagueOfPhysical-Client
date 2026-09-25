using NUnit.Framework;

namespace LOP.Tests
{
    public class ArcheryArrowDisplayTests
    {
        [Test]
        public void 늦지_않게_받은_화살은_실제_시각_그대로()
        {
            Assert.AreEqual(0.3f, ArcheryArrowDisplay.VisualSeconds(0.3f, 0f, 0.5f), 1e-5f);
        }

        [Test]
        public void 늦게_받은_화살은_받은_순간_활에서_출발한다()
        {
            //  0.1초 늦게 받았다 — 그 순간 그림은 0초 지점(활)이다.
            Assert.AreEqual(0f, ArcheryArrowDisplay.VisualSeconds(0.1f, 0.1f, 0.4f), 1e-5f);
        }

        [Test]
        public void 과녁에_닿는_순간_실제_궤적과_만난다()
        {
            //  0.1초 늦게 받았고 비행은 0.4초 — 0.4초에 그림도 0.4초 지점이어야 한다.
            Assert.AreEqual(0.4f, ArcheryArrowDisplay.VisualSeconds(0.4f, 0.1f, 0.4f), 1e-5f);
        }

        [Test]
        public void 따라잡는_동안은_실제보다_뒤에서_더_빨리_간다()
        {
            float v = ArcheryArrowDisplay.VisualSeconds(0.25f, 0.1f, 0.4f);
            Assert.Less(v, 0.25f);
            Assert.Greater(v, 0.25f - 0.1f);
        }

        [Test]
        public void 다_따라잡은_뒤는_실제_시각_그대로()
        {
            Assert.AreEqual(0.7f, ArcheryArrowDisplay.VisualSeconds(0.7f, 0.1f, 0.4f), 1e-5f);
        }

        [Test]
        public void 비행_시간을_모르면_정해진_시간_안에_따라잡는다()
        {
            float arrival = 0.1f;
            float end = arrival + ArcheryArrowDisplay.DefaultCatchUpSeconds;
            Assert.AreEqual(0f, ArcheryArrowDisplay.VisualSeconds(arrival, arrival, 0f), 1e-5f);
            Assert.AreEqual(end, ArcheryArrowDisplay.VisualSeconds(end, arrival, 0f), 1e-5f);
        }

        [Test]
        public void 비행이_거의_끝난_뒤_받았어도_최소한은_날아가_보인다()
        {
            //  0.38초에 받았는데 비행이 0.4초 — 남은 0.02초가 아니라 최소 시간에 걸쳐 따라잡는다.
            float arrival = 0.38f;
            float mid = arrival + ArcheryArrowDisplay.MinCatchUpSeconds * 0.5f;
            Assert.Less(ArcheryArrowDisplay.VisualSeconds(mid, arrival, 0.4f), mid);
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
