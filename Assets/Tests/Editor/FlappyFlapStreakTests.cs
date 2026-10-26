using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 연속 날갯짓 진입점(spec §7): 먹힌 날갯짓(<see cref="FlappyFlapSound.LiveFlap"/>)을 세어 머리 위 "+N"으로 보이고, 내 새가 기절에 들어가면 0,
    /// 결승하면 숨긴다. 이번 판 최고가 오를 때마다 기록지에 적는다. 출발 틱 100, 지금 틱 150.
    /// </summary>
    public class FlappyFlapStreakTests
    {
        private EntityRegistry registry;
        private FlappyStun stun;
        private FinishState finish;
        private RigTickUpdater clock;
        private FlappyFlapSound sound;
        private FakePersonalRecordStore store;
        private MatchRecordSheet sheet;
        private FlappyFlapStreak streak;
        private double now;

        [SetUp]
        public void SetUp()
        {
            registry = new EntityRegistry();
            var me = new Entity("me");
            stun = new FlappyStun();
            finish = new FinishState();
            me.Add(stun);
            me.Add(finish);
            registry.Add(me);
            var store1 = new RigGameDataStore { userEntityId = "me" };
            var start = new MatchStartState();
            start.Update(100, 2, 2);
            clock = new RigTickUpdater { interval = 0.02d, tick = 150 };
            var runner = new RigRunner { tickUpdater = clock };
            sound = new FlappyFlapSound(registry, store1, start, runner, new FakeFlappyAudio());
            store = new FakePersonalRecordStore();
            sheet = new MatchRecordSheet(() => (6, 2), store);
            streak = new FlappyFlapStreak(registry, store1, runner, sound, sheet);
            now = 10d;
            streak.now = () => now;
            streak.Tick();   //  기준 잡기(기절 아님)
        }

        [TearDown]
        public void TearDown() => streak.Dispose();

        private void FlapAt(long tick)
        {
            clock.tick = tick;
            sound.OnFlap();
        }

        [Test]
        public void 날갯짓마다_하나씩_세고_머리_위에_더하기_N()
        {
            FlapAt(150);
            FlapAt(151);
            FlapAt(152);
            streak.Tick();

            var frame = streak.Frame();
            Assert.IsTrue(frame.Visible);
            Assert.AreEqual("+3", frame.Text);
            Assert.AreEqual(FlapStreakLook.PopScale, frame.Scale, 1e-4f, "방금 눌렀다 — 팝");
        }

        [Test]
        public void 날갯짓이_안_먹히는_때는_안_센다()
        {
            stun.StunRemaining = 0.8f;
            FlapAt(150);
            stun.StunRemaining = 0f;
            finish.FinishedTick = 140;
            FlapAt(151);

            Assert.AreEqual(0, streak.Count, "날갯짓 소리가 안 나는 때는 +N도 안 오른다");
        }

        [Test]
        public void 기절에_들어가면_사라지고_다음은_더하기_1()
        {
            FlapAt(150);
            FlapAt(151);
            FlapAt(152);
            streak.Tick();

            stun.StunRemaining = 0.8f;
            streak.Tick();
            Assert.IsFalse(streak.Frame().Visible);
            Assert.AreEqual(0, streak.Count);

            stun.StunRemaining = 0f;
            streak.Tick();
            FlapAt(200);
            Assert.AreEqual("+1", streak.Frame().Text);
            Assert.AreEqual(3, streak.Best);
        }

        [Test]
        public void 안_누르면_0_7초_뒤에_사라지고_수는_이어진다()
        {
            FlapAt(150);
            streak.Tick();

            now = 10.85d;
            Assert.AreEqual(0.5f, streak.Frame().Alpha, 1e-3f);
            now = 11.0d;
            Assert.IsFalse(streak.Frame().Visible);

            now = 11.2d;
            FlapAt(210);
            Assert.AreEqual("+2", streak.Frame().Text, "사라져도 기절 전까지는 이어진다");
        }

        [Test]
        public void 결승하면_숨긴다()
        {
            FlapAt(150);
            finish.FinishedTick = 151;
            streak.Tick();

            Assert.IsFalse(streak.Frame().Visible);
        }

        [Test]
        public void 최고가_오를_때마다_기록지에_적는다()
        {
            FlapAt(150);
            FlapAt(151);
            FlapAt(152);
            streak.Tick();
            stun.StunRemaining = 0.8f;
            streak.Tick();
            stun.StunRemaining = 0f;
            streak.Tick();
            FlapAt(300);

            var outcomes = sheet.Commit();
            Assert.AreEqual(1, outcomes.Count);
            Assert.AreSame(FlappyRecordMetrics.FlapStreak, outcomes[0].Metric);
            Assert.AreEqual(3d, outcomes[0].Value, "기절 뒤 +1은 최고를 안 바꾼다");
        }
    }
}
