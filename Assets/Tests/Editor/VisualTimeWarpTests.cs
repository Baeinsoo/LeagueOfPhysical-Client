using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 멈칫(0.06초)·결승 느려짐(0.4배 0.25초)은 시계가 아니라 "그림 지연"으로 한다(spec §3 — 시뮬은 그대로 간다).
    /// 그림 시각 = 실제 시각 − 지연. 멈칫 동안 지연이 실제 시간만큼 늘어 그림이 서 있고, 끝나면 2배속으로 갚는다.
    /// </summary>
    public class VisualTimeWarpTests
    {
        [Test]
        public void 멈칫_동안은_그림_시각이_서_있다()
        {
            var warp = new VisualTimeWarp();
            warp.Hitstop(0.06f);

            float now = 0f;
            for (int i = 0; i < 3; i++)
            {
                now += 0.02f;
                float delay = warp.Advance(0.02f);
                Assert.AreEqual(0f, now - delay, 1e-5f, $"{i}번째 프레임 — 그림 시각이 0에 서 있다");
            }
        }

        [Test]
        public void 멈칫이_끝나면_두_배로_따라잡아_0으로()
        {
            var warp = new VisualTimeWarp();
            warp.Hitstop(0.06f);
            warp.Advance(0.06f);

            Assert.AreEqual(0.03f, warp.Advance(0.03f), 1e-5f, "0.03초 동안 그림은 0.06초 흐른다");
            Assert.AreEqual(0f, warp.Advance(0.03f), 1e-5f);
            Assert.AreEqual(0f, warp.Advance(0.1f), 1e-5f, "0 아래로 안 간다 — 그림이 시뮬을 앞지르지 않는다");
        }

        [Test]
        public void 한_프레임이_멈칫보다_길면_남는_시간은_따라잡기에_쓴다()
        {
            var warp = new VisualTimeWarp();
            warp.Hitstop(0.06f);

            //  0.06초 서 있고 남는 0.04초는 2배속 → 그림 0.08초 흐름, 지연 0.02.
            Assert.AreEqual(0.02f, warp.Advance(0.1f), 1e-5f);
        }

        [Test]
        public void 느리게는_0_4배로_흐른다()
        {
            var warp = new VisualTimeWarp();
            warp.Slow(0.25f, 0.4f);

            float delay = warp.Advance(0.25f);

            Assert.AreEqual(0.15f, delay, 1e-5f, "0.25초 동안 그림은 0.1초 흐른다");
        }

        [Test]
        public void 지연은_0_2초를_넘지_않는다()
        {
            //  예측 보간기는 샘플을 20개(50 Hz면 0.4초)만 쥔다 — 그보다 늦은 자리는 그릴 수 없다.
            var warp = new VisualTimeWarp();
            warp.Hitstop(1f);

            Assert.AreEqual(VisualTimeWarp.MaxDelay, warp.Advance(0.5f), 1e-5f);
        }
    }
}
