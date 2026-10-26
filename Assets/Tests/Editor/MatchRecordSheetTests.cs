using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 게임 스코프 기록지(spec §7): 종목이 경기 중 값을 적고, 매치 종료(또는 스코프 치우기) 때 한 번 저장된 최고와 비교·저장해 결과에 싣는다.
    /// 종목을 가리지 않는다 — 가짜 종목 지표 하나로도 붙는다.
    /// </summary>
    public class MatchRecordSheetTests
    {
        private const int Mode = 6;
        private const int Map = 2;
        private static readonly RecordMetric Time = FlappyRecordMetrics.FinishTime;
        private static readonly RecordMetric Streak = FlappyRecordMetrics.FlapStreak;

        private FakePersonalRecordStore store;

        [SetUp]
        public void SetUp() => store = new FakePersonalRecordStore();

        private MatchRecordSheet Sheet(int mode = Mode, int map = Map) => new MatchRecordSheet(() => (mode, map), store);

        private static string KeyOf(RecordMetric m) => RecordKey.For(Mode, Map, m.Id);

        [Test]
        public void 처음_기록은_첫_기록으로_저장하고_결과에_싣는다()
        {
            var sheet = Sheet();
            sheet.Set(Time, 60d);

            var outcomes = sheet.Commit();

            Assert.AreEqual(1, outcomes.Count);
            Assert.IsTrue(outcomes[0].IsFirst);
            Assert.IsFalse(outcomes[0].IsNewBest);
            Assert.AreEqual(60d, store.Values[KeyOf(Time)]);
        }

        [Test]
        public void 저장값이_없는_새_기기의_첫_판은_연속_날갯짓도_경신이_아니다()
        {
            //  운영 경로(final-review T1): 빈 저장소에서 Commit은 TryGetBest의 out 값 0을 previous로 넘긴다.
            //  횟수(높을수록 좋음) 지표는 가드가 없으면 5 > 0이라 새 기기의 첫 판마다 NEW BEST!가 뜬다.
            var sheet = Sheet();
            sheet.Set(Streak, 14d);

            var outcomes = sheet.Commit();

            Assert.IsFalse(outcomes[0].IsNewBest);
            Assert.IsFalse(LOP.UI.RecordCard.AnyNewBest(sheet.Commit()));
        }

        [Test]
        public void 더_좋으면_경신하고_저장한다()
        {
            store.Values[KeyOf(Time)] = 52.4d;
            var sheet = Sheet();
            sheet.Set(Time, 52.04d);

            var o = sheet.Commit()[0];

            Assert.IsTrue(o.IsNewBest);
            Assert.AreEqual(52.4d, o.Previous);
            Assert.AreEqual(52.04d, store.Values[KeyOf(Time)]);
        }

        [Test]
        public void 나쁘면_저장하지_않는다()
        {
            store.Values[KeyOf(Streak)] = 20d;
            var sheet = Sheet();
            sheet.Set(Streak, 12d);

            var o = sheet.Commit()[0];

            Assert.IsFalse(o.IsNewBest);
            Assert.AreEqual("-8회", o.DiffText);
            Assert.AreEqual(0, store.Saves);
            Assert.AreEqual(20d, store.Values[KeyOf(Streak)]);
        }

        [Test]
        public void 같은_지표는_마지막_값()
        {
            var sheet = Sheet();
            sheet.Set(Streak, 3d);
            sheet.Set(Streak, 5d);

            var outcomes = sheet.Commit();

            Assert.AreEqual(1, outcomes.Count);
            Assert.AreEqual(5d, outcomes[0].Value);
        }

        [Test]
        public void 판마다_한_번만_비교하고_저장한다()
        {
            var sheet = Sheet();
            sheet.Set(Time, 55d);

            var first = sheet.Commit();
            sheet.Set(Time, 40d);
            var second = sheet.Commit();

            Assert.AreSame(first, second, "두 번째 부름은 첫 결과를 그대로 돌려준다");
            Assert.AreEqual(55d, second[0].Value, "확정 뒤에 적은 값은 버린다");
            Assert.AreEqual(1, store.Saves);
            Assert.IsTrue(sheet.Committed);
        }

        [Test]
        public void 카드_순서는_지표_순서()
        {
            var sheet = Sheet();
            sheet.Set(Streak, 9d);   //  연속 날갯짓이 먼저 적혀도
            sheet.Set(Time, 50d);

            var outcomes = sheet.Commit();

            Assert.AreSame(Time, outcomes[0].Metric, "완주 시간이 위(Order 0)");
            Assert.AreSame(Streak, outcomes[1].Metric);
        }

        [Test]
        public void 종목이나_맵을_모르면_값만_싣고_저장하지_않는다()
        {
            var sheet = Sheet(mode: 6, map: 0);
            sheet.Set(Time, 50d);

            var o = sheet.Commit()[0];

            Assert.IsFalse(o.Compared);
            Assert.AreEqual("0:50.0", o.ValueText);
            Assert.AreEqual(0, store.Saves);
        }

        [Test]
        public void 맵_정보는_나중에_알아도_잡는다()
        {
            //  기록지가 방 정보보다 먼저 지어져도(스코프 빌드 순서) 확정 때 다시 읽는다.
            (int, int) ids = (0, 0);
            var sheet = new MatchRecordSheet(() => ids, store);
            sheet.Set(Time, 50d);
            ids = (Mode, Map);

            var o = sheet.Commit()[0];

            Assert.IsTrue(o.Compared);
            Assert.AreEqual(1, store.Saves);
        }

        [Test]
        public void 치우기만_해도_비교하고_저장한다()
        {
            //  판 중에 나가면 매치 종료 메시지가 없다 — Commit을 따로 부르지 않는 판 중 나가기 경로 그대로(final-review T2, 사보타주 #6).
            var sheet = Sheet();
            sheet.Set(Streak, 14d);

            sheet.Dispose();

            Assert.IsTrue(sheet.Committed);
            Assert.AreEqual(1, store.Saves);
            Assert.AreEqual(14d, store.Values[KeyOf(Streak)]);
        }

        [Test]
        public void 확정_뒤에_치우면_다시_저장하지_않는다()
        {
            var sheet = Sheet();
            sheet.Set(Streak, 14d);

            sheet.Commit();
            sheet.Dispose();
            sheet.Dispose();

            Assert.AreEqual(1, store.Saves);
        }

        [Test]
        public void 가짜_종목도_지표_하나로_붙는다()
        {
            //  다른 종목이 기록을 더할 때 드는 일: 지표 선언 하나 + Set 한 줄. 결과판·저장은 손대지 않는다.
            var fake = new RecordMetric("fake.streak", "가짜 연속", RecordDirection.HigherIsBetter, RecordFormat.Count, 0);
            var sheet = Sheet(mode: 99, map: 7);
            sheet.Set(fake, 12d);

            var o = sheet.Commit()[0];

            Assert.IsTrue(o.IsFirst);
            Assert.AreEqual("12회", o.ValueText);
            Assert.IsTrue(store.Values.ContainsKey("lop.record.99.7.fake.streak"));
        }

        [Test]
        public void 지금_라운드의_종목과_맵을_읽는다()
        {
            var match = new Match { rounds = new[] { new MatchRound { index = 0, gameModeId = 6, mapId = 2 } } };

            Assert.AreEqual((6, 2), MatchRecordSheet.IdsOf(match));
            Assert.AreEqual((0, 0), MatchRecordSheet.IdsOf(null));
            Assert.AreEqual((0, 0), MatchRecordSheet.IdsOf(new Match { rounds = new MatchRound[0] }));
        }
    }
}
