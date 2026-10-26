using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 완주 시간을 기록지에 적는 때(spec §7): 서버 등수와 내 결승 틱이 <b>둘 다</b> 있는 첫 틱에 한 번(옛 리뷰 M-1 — 예측만으로 적으면 되감기에
    /// 가짜 최고가 영구히 남는다). 처음 본 틱에 이미 등수가 있으면(재접속) 안 적는다(M-3). 출발 틱 100, 틱 0.02초.
    /// </summary>
    public class FlappyFinishRecorderTests
    {
        private const long Start = 100;
        private const double Spt = 0.02d;

        [Test]
        public void 등수와_결승_틱이_둘_다_있는_첫_틱에_한_번_적는다()
        {
            var gate = new FinishRecordGate();

            Assert.IsNull(gate.Observe(0, FinishState.NotFinished, Start, Spt));
            Assert.IsNull(gate.Observe(0, 1500, Start, Spt), "결승은 넘었지만 서버 등수가 아직");
            Assert.AreEqual(28d, gate.Observe(2, 1500, Start, Spt).Value, 1e-9d);
            Assert.IsNull(gate.Observe(2, 1500, Start, Spt), "한 번만");
        }

        [Test]
        public void 등수만_먼저_오면_결승_틱을_기다린다()
        {
            //  등수가 먼저 오고 예측이 아직 결승선을 못 넘은 장 — 그때 재면 달리는 시계다.
            var gate = new FinishRecordGate();
            gate.Observe(0, FinishState.NotFinished, Start, Spt);

            Assert.IsNull(gate.Observe(1, FinishState.NotFinished, Start, Spt));
            Assert.AreEqual(28d, gate.Observe(1, 1500, Start, Spt).Value, 1e-9d);
        }

        [Test]
        public void 처음_본_틱에_이미_등수가_있으면_안_적는다()
        {
            var gate = new FinishRecordGate();

            Assert.IsNull(gate.Observe(1, 1500, Start, Spt));
            Assert.IsNull(gate.Observe(1, 1500, Start, Spt), "그 뒤로도 — 재접속 전 판의 기록은 이번 스코프가 본 것이 아니다");
        }

        [Test]
        public void 출발_틱이나_틱_길이를_모르면_기다린다()
        {
            var gate = new FinishRecordGate();
            gate.Observe(0, FinishState.NotFinished, Start, Spt);

            Assert.IsNull(gate.Observe(1, 1500, long.MaxValue, Spt));
            Assert.IsNull(gate.Observe(1, 1500, Start, 0d));
            Assert.IsNotNull(gate.Observe(1, 1500, Start, Spt));
        }

        [Test]
        public void 완주_시간은_틱으로_정확하다()
        {
            var gate = new FinishRecordGate();
            gate.Observe(0, FinishState.NotFinished, Start, Spt);

            double seconds = gate.Observe(3, 2715, Start, Spt).Value;

            Assert.AreEqual(52.3d, seconds, 1e-9d);
            Assert.AreEqual("0:52.3", RaceClock.Format(seconds), "HUD 시계가 멈춘 숫자와 같다");
        }

        [Test]
        public void 내_새의_등수와_결승_틱을_읽어_기록지에_적는다()
        {
            var registry = new EntityRegistry();
            var me = new Entity("me");
            var finish = new FinishState();
            var placement = new FinishPlacement();
            me.Add(finish);
            me.Add(placement);
            registry.Add(me);
            var start = new MatchStartState();
            start.Update(Start, 2, 2);
            var runner = new RigRunner { tickUpdater = new RigTickUpdater { interval = Spt, tick = 2800 } };
            var store = new FakePersonalRecordStore();
            var sheet = new MatchRecordSheet(() => (6, 2), store);
            var recorder = new FlappyFinishRecorder(registry, new RigGameDataStore { userEntityId = "me" }, start, runner, sheet);

            recorder.Tick();
            finish.FinishedTick = 2715;
            recorder.Tick();
            placement.Value = 2;
            recorder.Tick();

            var outcomes = sheet.Commit();
            Assert.AreEqual(1, outcomes.Count);
            Assert.AreSame(FlappyRecordMetrics.FinishTime, outcomes[0].Metric);
            Assert.AreEqual(52.3d, outcomes[0].Value, 1e-9d);
        }
    }
}
