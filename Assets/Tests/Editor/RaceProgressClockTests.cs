using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>진행 막대 비율과 기록 시간(spec §1). 시간은 출발 틱부터 재고 내가 결승한 틱에서 멈춘다.</summary>
    public class RaceProgressClockTests
    {
        [Test]
        public void 비율은_출발에서_결승까지_0에서_1()
        {
            Assert.AreEqual(0f, RaceProgress.Ratio(10f, 10f, 110f), 1e-5f);
            Assert.AreEqual(0.5f, RaceProgress.Ratio(60f, 10f, 110f), 1e-5f);
            Assert.AreEqual(1f, RaceProgress.Ratio(110f, 10f, 110f), 1e-5f);
        }

        [Test]
        public void 비율은_넘쳐도_0과_1로_자른다()
        {
            Assert.AreEqual(0f, RaceProgress.Ratio(-5f, 10f, 110f));
            Assert.AreEqual(1f, RaceProgress.Ratio(130f, 10f, 110f), "결승선을 넘어 날아가도 막대 끝");
        }

        [Test]
        public void 코스가_뒤집혀_있으면_0()
        {
            Assert.AreEqual(0f, RaceProgress.Ratio(50f, 110f, 10f));
            Assert.AreEqual(0f, RaceProgress.Ratio(50f, 10f, 10f));
        }

        [Test]
        public void 출발을_모르면_0초()
        {
            Assert.AreEqual(0d, RaceClock.ElapsedSeconds(9d, long.MaxValue, 0.02d, FinishState.NotFinished));
            Assert.AreEqual(0d, RaceClock.ElapsedSeconds(9d, 100, 0d, FinishState.NotFinished), "간격을 아직 모름");
        }

        [Test]
        public void 출발_전이면_0초()
        {
            //  출발 틱 100 = 2초. 지금 1.5초(카운트다운 중).
            Assert.AreEqual(0d, RaceClock.ElapsedSeconds(1.5d, 100, 0.02d, FinishState.NotFinished));
        }

        [Test]
        public void 달리는_중엔_출발부터_잰다()
        {
            Assert.AreEqual(3.25d, RaceClock.ElapsedSeconds(5.25d, 100, 0.02d, FinishState.NotFinished), 1e-9);
        }

        [Test]
        public void 결승하면_그_틱에서_멈춘다()
        {
            //  결승 틱 400 = 8초 → 6초. 지금이 몇 초든 그대로.
            Assert.AreEqual(6d, RaceClock.ElapsedSeconds(20d, 100, 0.02d, 400), 1e-9);
            Assert.AreEqual(6d, RaceClock.ElapsedSeconds(99d, 100, 0.02d, 400), 1e-9);
        }

        [Test]
        public void 글자는_분_초_10분의1초()
        {
            Assert.AreEqual("0:00.0", RaceClock.Format(0d));
            Assert.AreEqual("0:05.3", RaceClock.Format(5.3d), "5.3×10이 52.999…가 돼도 0:05.2로 내려가지 않는다");
            Assert.AreEqual("0:59.9", RaceClock.Format(59.99d), "반올림하면 0:60.0이 된다 — 버린다");
            Assert.AreEqual("1:02.4", RaceClock.Format(62.45d));
            Assert.AreEqual("0:00.0", RaceClock.Format(-1d));
        }
    }
}
