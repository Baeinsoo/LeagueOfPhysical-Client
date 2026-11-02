using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시의 개인 기록(spec §7): 완주 시간은 출발 틱부터 서버가 확정한 마지막 정타 틱까지(틱 0.02초),
    /// 서버가 등수를 확정하면(<see cref="FinishPlacement"/> &gt; 0) 한 번 적는다. 연속 최고는 서버 확정 상태
    /// (<see cref="CardRushRunnerState.BestStreak"/> — 보정으로도 줄지 않는 Task 8 값)를 본 값들의 최댓값으로 적는다.
    /// </summary>
    public class CardRushRecordReporterTests
    {
        [Test]
        public void 완주_시간은_출발부터_마지막_정타까지()
        {
            var registry = new EntityRegistry();
            var me = new Entity("me");
            var runner = new CardRushRunner();
            me.Add(runner);
            me.Add(new FinishPlacement());
            registry.Add(me);
            var start = new MatchStartState();
            start.Update(1000, 2, 2);
            var store = new FakePersonalRecordStore();
            var sheet = new MatchRecordSheet(() => (13, 17), store);
            var reporter = new CardRushRecordReporter(registry, new RigGameDataStore { userEntityId = "me" }, start, sheet);

            //  아직 등수가 없으면 적지 않는다.
            runner.State.MoveStartTick = 4000;
            reporter.Tick();

            //  서버가 등수를 확정한 장.
            me.Get<FinishPlacement>().Value = 1;
            reporter.Tick();

            //  그 뒤로도 — 한 번만.
            reporter.Tick();

            var outcomes = sheet.Commit();
            Assert.AreEqual(1, outcomes.Count);
            Assert.AreSame(CardRushRecordMetrics.FinishTime, outcomes[0].Metric);
            Assert.AreEqual(60d, outcomes[0].Value, 1e-9d);
        }

        [Test]
        public void 연속_최고는_본_값의_최댓값()
        {
            var registry = new EntityRegistry();
            var me = new Entity("me");
            var runner = new CardRushRunner();
            me.Add(runner);
            me.Add(new FinishPlacement());
            registry.Add(me);
            var start = new MatchStartState();
            var store = new FakePersonalRecordStore();
            var sheet = new MatchRecordSheet(() => (13, 17), store);
            var reporter = new CardRushRecordReporter(registry, new RigGameDataStore { userEntityId = "me" }, start, sheet);

            foreach (int streak in new[] { 3, 7, 0, 5 })
            {
                runner.State.BestStreak = streak;
                reporter.Tick();
            }

            var outcomes = sheet.Commit();
            Assert.AreEqual(1, outcomes.Count);
            Assert.AreSame(CardRushRecordMetrics.StreakBest, outcomes[0].Metric);
            Assert.AreEqual(7d, outcomes[0].Value, 1e-9d);
        }
    }
}
