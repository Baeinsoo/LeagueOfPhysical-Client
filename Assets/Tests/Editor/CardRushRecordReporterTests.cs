using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시의 개인 기록(spec §7): 완주 시간은 출발 틱부터 서버가 확정한 마지막 정타 틱까지(틱 0.02초),
    /// 서버가 등수를 확정하면(<see cref="FinishPlacement"/> &gt; 0) 한 번 적는다. 연속 최고는 서버 확정 상태
    /// (<see cref="CardRushRunnerState.BestStreak"/> — 보정으로도 줄지 않는 Task 8 값)를 본 값들의 최댓값으로 적는다.
    ///
    /// <para>완주 시간은 <see cref="FinishRecordGate"/>를 그대로 재사용한다(Flappy와 같은 넷코드 모양 — 리뷰 판정 11-02):
    /// 처음 본 틱에 이미 등수가 있으면 재접속 전 판이라 안 적고, 등수가 아예 없으면(DNF) 영원히 안 적는다.</para>
    /// </summary>
    public class CardRushRecordReporterTests
    {
        private static CardRushRecordReporter MakeReporter(EntityRegistry registry, MatchStartState start, MatchRecordSheet sheet)
            => new CardRushRecordReporter(registry, new RigGameDataStore { userEntityId = "me" }, start, sheet);

        [Test]
        public void 완주_시간은_출발부터_마지막_정타까지()
        {
            var registry = new EntityRegistry();
            var me = new Entity("me");
            var runner = new CardRushRunner();
            var finish = new FinishState();
            var placement = new FinishPlacement();
            me.Add(runner);
            me.Add(finish);
            me.Add(placement);
            registry.Add(me);
            var start = new MatchStartState();
            start.Update(1000, 2, 2);
            var store = new FakePersonalRecordStore();
            var sheet = new MatchRecordSheet(() => (13, 17), store);
            var reporter = MakeReporter(registry, start, sheet);

            //  아직 등수가 없으면 적지 않는다 — 이 장이 게이트의 기준(처음 본 틱)이 된다.
            runner.State.MoveStartTick = 4000;
            reporter.Tick();

            //  서버가 등수를 확정하고 월드가 결승 틱을 적은 장(CardRushWorld가 실제로 하는 일).
            placement.Value = 1;
            finish.FinishedTick = 4000;
            reporter.Tick();

            //  그 뒤로도 — 한 번만.
            reporter.Tick();

            var outcomes = sheet.Commit();
            Assert.AreEqual(1, outcomes.Count);
            Assert.AreSame(CardRushRecordMetrics.FinishTime, outcomes[0].Metric);
            Assert.AreEqual(60d, outcomes[0].Value, 1e-9d);
        }

        [Test]
        public void 재접속으로_이미_완주한_장을_처음_보면_안_적는다()
        {
            //  이번 스코프(리포터)가 생기기 전에 이미 완주한 판 — 처음 보는 틱에 등수·결승 틱이 둘 다 서 있다.
            //  이 세션이 본 판이 아니므로 적지 않는다(FlappyFinishRecorder의 M-3과 같은 규칙).
            var registry = new EntityRegistry();
            var me = new Entity("me");
            var runner = new CardRushRunner { State = new CardRushRunnerState { MoveStartTick = 4000 } };
            var finish = new FinishState { FinishedTick = 4000 };
            var placement = new FinishPlacement { Value = 1 };
            me.Add(runner);
            me.Add(finish);
            me.Add(placement);
            registry.Add(me);
            var start = new MatchStartState();
            start.Update(1000, 2, 2);
            var store = new FakePersonalRecordStore();
            var sheet = new MatchRecordSheet(() => (13, 17), store);
            var reporter = MakeReporter(registry, start, sheet);

            reporter.Tick();
            reporter.Tick();

            var outcomes = sheet.Commit();
            Assert.IsEmpty(outcomes, "재접속 전 판의 완주 시간은 이번 스코프가 본 것이 아니다");
        }

        [Test]
        public void DNF면_안_적는다()
        {
            //  등수가 끝까지 안 생기면(완주 못 함) 아무리 틱이 돌아도 적지 않는다.
            var registry = new EntityRegistry();
            var me = new Entity("me");
            var runner = new CardRushRunner { State = new CardRushRunnerState { MoveStartTick = 3500 } };
            me.Add(runner);
            me.Add(new FinishState());
            me.Add(new FinishPlacement());
            registry.Add(me);
            var start = new MatchStartState();
            start.Update(1000, 2, 2);
            var store = new FakePersonalRecordStore();
            var sheet = new MatchRecordSheet(() => (13, 17), store);
            var reporter = MakeReporter(registry, start, sheet);

            for (int i = 0; i < 5; i++)
            {
                reporter.Tick();
            }

            var outcomes = sheet.Commit();
            Assert.IsEmpty(outcomes);
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
            var reporter = MakeReporter(registry, start, sheet);

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
