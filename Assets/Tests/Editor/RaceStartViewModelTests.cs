using System;
using System.Threading.Tasks;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카운트다운 화면 값과 박자 소리(spec §3, 플래피만): 숫자가 바뀔 때마다 박자 한 번, GO에 한 번. 프레임마다 같은 숫자를 다시 봐도
    /// 소리는 한 번. 기마 궁술(Plain)은 예전 문구·시간 그대로, 팝·소리 없음. 출발 틱 200 × 0.02초 = 4초.
    /// </summary>
    public class RaceStartViewModelTests
    {
        private sealed class FakeTickUpdater : GameFramework.Runner.ITickUpdater
        {
            public event Action<long> onTick { add { } remove { } }
            public long tick { get; set; }
            public double interval { get; set; }
            public double elapsedTime { get; set; }
            public long processibleTick => 0;
            public double deltaTime => interval;
            public int catchUpCappedCount => 0;
            public long maxTicksBehind => 0;
            public bool isFaulted => false;
            public long faultedTick => 0;
            public void Run(long tick, double interval, double elapsedTime) { }
            public void Stop() { }
        }

        private sealed class FakeRunner : GameFramework.Runner.IRunner
        {
            public event Action<GameFramework.Runner.RunnerState> onGameStateChanged { add { } remove { } }
            public GameFramework.Runner.RunnerState gameState => GameFramework.Runner.RunnerState.Playing;
            public GameFramework.Runner.ITickUpdater tickUpdater { get; set; }
            public GameFramework.Netcode.INetworkTime networkTime => null;
            public bool initialized => true;
            public Task InitializeAsync() => Task.CompletedTask;
            public Task DeinitializeAsync() => Task.CompletedTask;
            public void Run(long tick, double interval, double elapsedTime) { }
            public void Stop() { }
            public void RegisterSystem<TPhase>(GameFramework.Runner.ITickSystem system) { }
            public void UnregisterSystem(GameFramework.Runner.ITickSystem system) { }
        }

        private sealed class FakeLook : IRaceStartLook
        {
            public bool Animated { get; set; } = true;
            public int Beats;
            public int Gos;
            public void Beat() => Beats++;
            public void Go() => Gos++;
        }

        private MatchStartState start;
        private FakeTickUpdater clock;
        private FakeLook look;
        private RaceStartViewModel vm;

        [SetUp]
        public void SetUp()
        {
            start = new MatchStartState();
            start.Update(200, 4, 4);
            clock = new FakeTickUpdater { interval = 0.02d };
            look = new FakeLook();
            vm = new RaceStartViewModel(start, new FakeRunner { tickUpdater = clock }, look);
        }

        //  연속 시각과 틱을 같이 맞춘다(플래피는 연속 시각, 예전 계산은 틱을 쓴다).
        private void At(double seconds)
        {
            clock.elapsedTime = seconds;
            clock.tick = (long)Math.Floor(seconds / 0.02d + 1e-6);
            vm.Refresh();
        }

        [Test]
        public void 숫자가_바뀔_때마다_박자_한_번_GO에_한_번()
        {
            At(0.5d);
            Assert.AreEqual(0, look.Beats, "대기 중엔 소리 없음");
            At(1.05d);
            At(1.5d);
            At(2.05d);
            At(3.05d);
            Assert.AreEqual(3, look.Beats, "3·2·1 — 같은 숫자를 다시 봐도 한 번");
            At(4.05d);
            At(4.3d);
            Assert.AreEqual(1, look.Gos);
        }

        [Test]
        public void 숫자는_1_5배에서_0_25초에_1배로()
        {
            At(1.05d);
            Assert.AreEqual("3", vm.Text);
            Assert.IsTrue(vm.IsPop);
            Assert.AreEqual(1.4f, vm.TextScale, 1e-4f);
            At(1.175d);
            Assert.AreEqual(1.15f, vm.TextScale, 1e-4f);
            At(1.4d);
            Assert.AreEqual(1f, vm.TextScale, 1e-4f);
            Assert.AreEqual(1f, vm.Alpha);
        }

        [Test]
        public void GO는_초록으로_0_8초에_걸쳐_사라지고_그_뒤_숨는다()
        {
            At(4.4d);
            Assert.AreEqual("GO!", vm.Text);
            Assert.IsTrue(vm.IsGo);
            Assert.AreEqual(0.5f, vm.Alpha, 1e-4f);

            At(4.85d);
            Assert.AreEqual(string.Empty, vm.Text);
            Assert.IsFalse(vm.IsGo);
        }

        [Test]
        public void 출발_전엔_대기_인원()
        {
            start.Update(long.MaxValue, 2, 4);

            At(1d);

            Assert.AreEqual("2 / 4 대기 중", vm.Text);
            Assert.IsFalse(vm.IsPop, "대기 문구는 팝 글씨가 아니다(예전 크기)");
            Assert.AreEqual(0, look.Beats);
        }

        [Test]
        public void 기마_궁술은_예전_그대로()
        {
            //  사용자 결정(10-25): 개편은 플래피만. 기마 궁술은 예전 문구·GO 1초·팝 없음·무음.
            look.Animated = false;

            At(1.1d);
            Assert.AreEqual("3", vm.Text);
            Assert.AreEqual(1f, vm.TextScale);
            Assert.AreEqual(1f, vm.Alpha);
            Assert.IsFalse(vm.IsPop);

            At(4.9d);
            Assert.AreEqual("GO!", vm.Text, "예전처럼 GO는 1초");
            Assert.IsFalse(vm.IsGo, "초록 GO는 플래피만");

            At(5.1d);
            Assert.AreEqual(string.Empty, vm.Text);
            Assert.AreEqual(0, look.Beats);
            Assert.AreEqual(0, look.Gos);
            Assert.AreEqual(vm.CurrentText(), vm.Text, "예전 계산 한 곳");
        }

        [Test]
        public void 플래피는_박자와_GO에_제_소리를_낸다()
        {
            var audio = new FakeFlappyAudio();
            var flappy = new FlappyRaceStartLook(audio);

            flappy.Beat();
            flappy.Go();

            Assert.IsTrue(flappy.Animated);
            Assert.IsFalse(new PlainRaceStartLook().Animated);
            CollectionAssert.AreEqual(new[] { FlappySfx.CountBeat, FlappySfx.Go }, audio.Played);
        }
    }
}
