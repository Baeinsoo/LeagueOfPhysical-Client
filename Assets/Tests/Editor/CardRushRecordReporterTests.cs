using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시의 개인 기록(spec 2026-11-05 §10·§12): 완주 시간은 출발 틱부터 완주 틱까지(틱 0.02초),
    /// 서버가 등수를 확정하면(<see cref="FinishPlacement"/> &gt; 0) 한 번 적는다. 연속 최고는 서버 확정 상태
    /// (스냅의 연속 무사 통과 — <see cref="CardRushServerStreak"/>)를 본 값들의 최댓값으로 적는다.
    ///
    /// <para>완주 시간은 <see cref="FinishRecordGate"/>를 그대로 재사용한다(Flappy와 같은 넷코드 모양 — 리뷰 판정 11-02):
    /// 처음 본 틱에 이미 등수가 있으면 재접속 전 판이라 안 적고, 등수가 아예 없으면(DNF) 영원히 안 적는다.</para>
    /// </summary>
    public class CardRushRecordReporterTests
    {
        private static CardRushRecordReporter MakeReporter(EntityRegistry registry, MatchStartState start, MatchRecordSheet sheet)
            => new CardRushRecordReporter(registry, new RigGameDataStore { userEntityId = "me" }, start, sheet);

        [Test]
        public void 완주_시간은_출발부터_완주_틱까지()
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
            runner.State.FinishTick = 4000;
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
            var runner = new CardRushRunner { State = new CardRushRunnerState { FinishTick = 4000 } };
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
            var runner = new CardRushRunner { State = new CardRushRunnerState { PosMm = 90000 } };
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

        //  스냅이 실어 온 연속(card_clean_streak)이 실제로 지나는 길 — 메시지 처리기가 모든 스냅에 부르는 그것.
        private static void ServerSnap(Entity entity, int streak)
            => GameEntityMessageHandler.ApplyServerOnlyValues(entity, new EntitySnap { entityId = entity.Id, cardCleanStreak = streak });

        //  4차는 3차와 규칙이 달라 새 키로 시작한다 — 옛 키와 비교하면 안 된다.
        [Test]
        public void 지표는_4차_새_키()
        {
            Assert.AreEqual("cardrush.v4.finishTime", CardRushRecordMetrics.FinishTime.Id);
            Assert.AreEqual("완주 시간", CardRushRecordMetrics.FinishTime.Label);
            Assert.AreEqual("cardrush.v4.cleanBest", CardRushRecordMetrics.CleanBest.Id);
            Assert.AreEqual("연속 무사 통과", CardRushRecordMetrics.CleanBest.Label);
            Assert.AreEqual(RecordDirection.HigherIsBetter, CardRushRecordMetrics.CleanBest.Direction);
        }

        [Test]
        public void 연속_최고는_서버가_보낸_연속의_최댓값()
        {
            var registry = new EntityRegistry();
            var me = new Entity("me");
            var runner = new CardRushRunner();
            me.Add(runner);
            me.Add(new CardRushServerStreak());
            me.Add(new FinishPlacement());
            registry.Add(me);
            var start = new MatchStartState();
            var store = new FakePersonalRecordStore();
            var sheet = new MatchRecordSheet(() => (13, 17), store);
            var reporter = MakeReporter(registry, start, sheet);

            foreach (int streak in new[] { 3, 7, 0, 5 })
            {
                ServerSnap(me, streak);
                runner.State.CleanStreak = streak;
                runner.State.BestCleanStreak = System.Math.Max(runner.State.BestCleanStreak, streak);
                reporter.Tick();
            }

            var outcomes = sheet.Commit();
            Assert.AreEqual(1, outcomes.Count);
            Assert.AreSame(CardRushRecordMetrics.CleanBest, outcomes[0].Metric);
            Assert.AreEqual(7d, outcomes[0].Value, 1e-9d);
        }
    
        //  최종 리뷰 I3: 연속 9에서 무사 통과를 예측했다(최고 10). 서버는 그 탭을 못 받아 연속 9 그대로다.
        //  되감기는 앵커의 예측 상태(최고 10)를 되살리고 보정은 그 최고를 지킨다 — 기록지에 10이 가면 안 된다.
        [Test]
        public void 서버가_버린_예측_통과는_기록지에_안_간다()
        {
            var registry = new EntityRegistry();
            var me = new Entity("me");
            me.Add(new GameFramework.World.Transform());
            me.Add(new GameFramework.World.Simulated());
            var runner = new CardRushRunner { State = new CardRushRunnerState { PosMm = 20000, CleanStreak = 10, BestCleanStreak = 10, ObstacleIndex = 10, ObstacleEventTick = 1290 } };
            me.Add(runner);
            me.Add(new CardRushServerStreak());
            me.Add(new FinishPlacement());
            registry.Add(me);
            var world = new CardRushWorld(registry, new WorldEventBuffer(), new CardRushMapRulesField(), new Seed(), CardRushServerCorrectionHandlerTests.Config)
                { GameplayStartTick = 0 };
            world.SaveState(1300);   //  예측 앵커: 연속 10
            var handler = new CardRushServerCorrectionHandler(world);
            var sheet = new MatchRecordSheet(() => (13, 17), new FakePersonalRecordStore());
            var reporter = MakeReporter(registry, new MatchStartState(), sheet);

            //  서버 스냅: 장애물 9·연속 9. 메시지 처리기 → 되감기(앵커 복원) → 권위 적용 순서 그대로.
            var snap = new EntitySnap { entityId = "me", tick = 1300, cardPosMm = 20000, cardObstacleIndex = 9, cardCleanStreak = 9, cardObstacleEventTick = 1290 };
            GameEntityMessageHandler.ApplyServerOnlyValues(me, snap);
            Assert.IsFalse(handler.Matches(1300, snap));
            world.LoadState(1300);
            handler.ApplyAuthoritative(me, snap, 0.02f);
            reporter.Tick();

            var outcomes = sheet.Commit();
            Assert.AreEqual(1, outcomes.Count);
            Assert.AreSame(CardRushRecordMetrics.CleanBest, outcomes[0].Metric);
            Assert.AreEqual(9d, outcomes[0].Value, 1e-9d, "서버가 확정한 연속은 9뿐이다");
        }

        private sealed class Seed : IMatchSeed { public ulong Value => 4242; }
}
}
