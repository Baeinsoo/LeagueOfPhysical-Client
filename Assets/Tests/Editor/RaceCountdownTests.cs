using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카운트다운(spec §3): 출발 3초 전부터 3·2·1(올림), 숫자마다 1.5배 → 1배 0.25초 팝, 출발 뒤 0.8초 GO!가 사라진다.
    /// 출발 틱 200 × 0.02초 = 4초.
    /// </summary>
    public class RaceCountdownTests
    {
        [Test]
        public void 출발까지_3초보다_많이_남으면_대기()
        {
            Assert.AreEqual(CountdownKind.Waiting, RaceCountdown.At(200, 0.02d, 0.5d, true).Kind);
        }

        [Test]
        public void 시계를_모르면_대기()
        {
            //  러너가 돌기 전엔 tick·interval이 0이다 — 그대로 셈하면 "0"이 뜬다.
            Assert.AreEqual(CountdownKind.Waiting, RaceCountdown.At(long.MaxValue, 0.02d, 3.5d, true).Kind, "출발 틱 모름");
            Assert.AreEqual(CountdownKind.Waiting, RaceCountdown.At(200, 0d, 3.5d, true).Kind, "간격 모름");
            Assert.AreEqual(CountdownKind.Waiting, RaceCountdown.At(200, 0.02d, 3.5d, false).Kind, "러너가 안 돈다");
        }

        [Test]
        public void 남은_시간을_올림한_숫자와_팝()
        {
            var three = RaceCountdown.At(200, 0.02d, 1.1d, true);
            Assert.AreEqual(CountdownKind.Number, three.Kind);
            Assert.AreEqual(3, three.Number, "올림이라야 3·2·1이 각각 1초씩 보인다");
            Assert.AreEqual(0.1d, three.SecondsIn, 1e-9);

            var one = RaceCountdown.At(200, 0.02d, 3.99d, true);
            Assert.AreEqual(1, one.Number);
            Assert.AreEqual(0.99d, one.SecondsIn, 1e-9);

            Assert.AreEqual(1.5f, RaceCountdown.Scale(0d), 1e-5f);
            Assert.AreEqual(1.25f, RaceCountdown.Scale(0.125d), 1e-5f);
            Assert.AreEqual(1f, RaceCountdown.Scale(0.3d), 1e-5f);
        }

        [Test]
        public void 출발_뒤_0_8초는_GO_그_뒤는_숨김()
        {
            var go = RaceCountdown.At(200, 0.02d, 4.3d, true);
            Assert.AreEqual(CountdownKind.Go, go.Kind);
            Assert.AreEqual(0.3d, go.SecondsIn, 1e-9);
            Assert.AreEqual(0.5f, RaceCountdown.GoAlpha(0.4d), 1e-5f);

            Assert.AreEqual(CountdownKind.Hidden, RaceCountdown.At(200, 0.02d, 4.8d, true).Kind);
        }
    }
}
