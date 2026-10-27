using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 공용 기록 틀(spec §7): 종목이 선언한 지표 하나의 이번 값과 저장된 최고를 비교한다. 첫 기록은 경신이 아니고, 같은 틱은 같은 기록이며,
    /// 종목·맵을 모르면 값만 보인다. 글자는 화면 글꼴에 맞춰 하이픈·점.
    /// </summary>
    public class RecordsCoreTests
    {
        private static readonly RecordMetric Time = FlappyRecordMetrics.FinishTime;
        private static readonly RecordMetric Count = FlappyRecordMetrics.FlapStreak;

        [Test]
        public void 시간은_틱의_4분의_1_넘게_빨라야_경신()
        {
            Assert.IsTrue(Time.IsBetter(52.04d, 52.4d));
            Assert.IsTrue(Time.IsBetter(52.38d, 52.4d), "한 틱(0.02초) 빠르면 경신");
            Assert.IsFalse(Time.IsBetter(52.396d, 52.4d), "0.005초 안은 같은 틱 — 계산 오차");
            Assert.IsFalse(Time.IsBetter(52.4d, 52.4d));
            Assert.IsFalse(Time.IsBetter(53d, 52.4d));
        }

        [Test]
        public void 횟수는_커야_경신()
        {
            Assert.IsTrue(Count.IsBetter(21d, 20d));
            Assert.IsFalse(Count.IsBetter(20d, 20d));
            Assert.IsFalse(Count.IsBetter(19d, 20d));
        }

        [Test]
        public void 시간_값과_차이_글자는_하이픈과_점()
        {
            Assert.AreEqual("0:52.0", RecordText.Value(Time, 52.04d));
            //  화면 글꼴에 진짜 빼기(U+2212)가 없다 — 하이픈. 숫자는 문화권과 무관하게 점.
            Assert.AreEqual("-0.36초", RecordText.Diff(Time, 52.04d, 52.4d));
            Assert.AreEqual("+0.60초", RecordText.Diff(Time, 53d, 52.4d));
        }

        [Test]
        public void 같은_틱이면_더하기_영()
        {
            Assert.AreEqual("+0.00초", RecordText.Diff(Time, 52.4d, 52.4d));
            //  음수 영(-0.0)이 "-0.00"으로 찍히지 않게.
            Assert.AreEqual("+0.00초", RecordText.Diff(Time, 52.3999985d, 52.4d));
        }

        [Test]
        public void 횟수_값과_차이_글자()
        {
            Assert.AreEqual("23회", RecordText.Value(Count, 23d));
            Assert.AreEqual("+3회", RecordText.Diff(Count, 23d, 20d));
            Assert.AreEqual("-2회", RecordText.Diff(Count, 18d, 20d));
            Assert.AreEqual("+0회", RecordText.Diff(Count, 20d, 20d));
        }

        [Test]
        public void 저장값이_없으면_첫_기록이고_경신이_아니다()
        {
            var o = new RecordOutcome(Time, 60d, compared: true, hasPrevious: false, previous: 0d);

            Assert.IsTrue(o.IsFirst);
            Assert.IsFalse(o.IsNewBest, "첫 판마다 NEW BEST가 뜨면 의미가 없다");
            Assert.IsTrue(o.ShouldSave);
            Assert.AreEqual("첫 기록", o.DiffText);
            Assert.AreEqual("1:00.0", o.ValueText);
        }

        [Test]
        public void 횟수도_저장값이_없으면_첫_기록이고_경신이_아니다()
        {
            //  기록지는 저장값이 없을 때 previous=0을 넘긴다 — 횟수(높을수록 좋음)는 5 > 0이라 HasPrevious 가드가
            //  없으면 경신으로 읽힌다(final-review T1, 사보타주 #2). Time 지표(0:00:52 < 0 - 0.005가 거짓)로는 안 걸린다.
            var o = new RecordOutcome(Count, 5d, compared: true, hasPrevious: false, previous: 0d);

            Assert.IsTrue(o.IsFirst);
            Assert.IsFalse(o.IsNewBest);
            Assert.IsTrue(o.ShouldSave);
            Assert.AreEqual("첫 기록", o.DiffText);
        }

        [Test]
        public void 더_좋으면_경신_같거나_나쁘면_아님()
        {
            var better = new RecordOutcome(Count, 21d, true, true, 20d);
            var same = new RecordOutcome(Count, 20d, true, true, 20d);
            var worse = new RecordOutcome(Time, 53d, true, true, 52.4d);

            Assert.IsTrue(better.IsNewBest);
            Assert.IsTrue(better.ShouldSave);
            Assert.AreEqual("+1회", better.DiffText);
            Assert.IsFalse(same.IsNewBest);
            Assert.IsFalse(same.ShouldSave);
            Assert.IsFalse(worse.IsNewBest);
            Assert.IsFalse(worse.ShouldSave);
            Assert.AreEqual("+0.60초", worse.DiffText);
        }

        [Test]
        public void 종목이나_맵을_모르면_값만_보인다()
        {
            var o = new RecordOutcome(Time, 52.04d, compared: false, hasPrevious: false, previous: 0d);

            Assert.IsFalse(o.IsFirst);
            Assert.IsFalse(o.IsNewBest);
            Assert.IsFalse(o.ShouldSave);
            Assert.AreEqual(string.Empty, o.DiffText);
            Assert.AreEqual("0:52.0", o.ValueText);
        }

        [Test]
        public void 키는_종목_맵_지표를_붙인다()
        {
            Assert.AreEqual("lop.record.6.2.flappy.finishTime", RecordKey.For(6, 2, "flappy.finishTime"));
            Assert.IsNull(RecordKey.For(0, 2, "flappy.finishTime"), "종목 모름");
            Assert.IsNull(RecordKey.For(6, 0, "flappy.finishTime"), "맵 모름");
            Assert.IsNull(RecordKey.For(6, 2, ""));
        }

        [Test]
        public void 플래피_지표_id는_바꾸지_않는다()
        {
            //  id가 저장 키에 들어간다 — 바꾸면 사용자 기기의 기록이 사라진다.
            Assert.AreEqual("flappy.finishTime", FlappyRecordMetrics.FinishTime.Id);
            Assert.AreEqual(RecordDirection.LowerIsBetter, FlappyRecordMetrics.FinishTime.Direction);
            Assert.AreEqual(RecordFormat.Time, FlappyRecordMetrics.FinishTime.Format);
            Assert.AreEqual("flappy.flapStreak", FlappyRecordMetrics.FlapStreak.Id);
            Assert.AreEqual(RecordDirection.HigherIsBetter, FlappyRecordMetrics.FlapStreak.Direction);
            Assert.AreEqual(RecordFormat.Count, FlappyRecordMetrics.FlapStreak.Format);
            Assert.Less(FlappyRecordMetrics.FinishTime.Order, FlappyRecordMetrics.FlapStreak.Order, "카드에서 완주 시간이 먼저");
        }
    }
}
