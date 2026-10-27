using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 연속 날갯짓 진입점(spec §7): 먹힌 날갯짓(<see cref="FlappyFlapSound.LiveFlap"/>)을 세어 연출에 수를 알리고(<see cref="FlappyFlapStreak.Flapped"/>),
    /// 내 새가 기절에 들어가면 0으로 만들며 한 번 알린다(<see cref="FlappyFlapStreak.Crashed"/>). 결승하면 달리는 중이 아니다(꼬리 끝).
    /// 이번 판 최고가 오를 때마다 기록지에 적는다. 출발 틱 100, 지금 틱 150.
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
        private System.Collections.Generic.List<int> flapped;
        private int crashes;

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
            flapped = new System.Collections.Generic.List<int>();
            crashes = 0;
            streak.Flapped += n => flapped.Add(n);
            streak.Crashed += () => crashes++;
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
        public void 날갯짓마다_하나씩_세고_수를_알린다()
        {
            FlapAt(150);
            FlapAt(151);
            FlapAt(152);
            streak.Tick();

            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, flapped);
            Assert.IsTrue(streak.Racing);
        }

        [Test]
        public void 같은_틱에_두_번_눌러도_한_번만_알린다()
        {
            FlapAt(150);
            FlapAt(150);

            CollectionAssert.AreEqual(new[] { 1 }, flapped);
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
            Assert.IsEmpty(flapped);
        }

        [Test]
        public void 기절에_들어가면_0이고_한_번_알린다_다음은_1()
        {
            FlapAt(150);
            FlapAt(151);
            FlapAt(152);
            streak.Tick();

            stun.StunRemaining = 0.8f;
            streak.Tick();
            streak.Tick();
            Assert.AreEqual(1, crashes, "기절이 이어지는 동안 다시 알리지 않는다");
            Assert.AreEqual(0, streak.Count);
            Assert.IsFalse(streak.Racing);

            stun.StunRemaining = 0f;
            streak.Tick();
            FlapAt(200);
            Assert.AreEqual(1, flapped[flapped.Count - 1]);
            Assert.AreEqual(3, streak.Best);
        }

        [Test]
        public void 결승하면_달리는_중이_아니다()
        {
            FlapAt(150);
            finish.FinishedTick = 151;
            streak.Tick();

            Assert.IsFalse(streak.Racing);
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
