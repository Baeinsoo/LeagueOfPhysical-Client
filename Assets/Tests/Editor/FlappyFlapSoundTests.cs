using System;
using System.Threading.Tasks;
using GameFramework.World;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 날갯짓 소리(spec §6, 사용자 결정 10-25): 화면을 누르면 짧은 잡음 소리. 시뮬이 날갯짓을 무시하는 때(출발 전·기절 중·결승 뒤·몸 없음)엔
    /// 소리도 안 낸다 — 소리가 나는데 새가 안 뜨면 고장처럼 보인다. 출발 틱 100.
    /// </summary>
    public class FlappyFlapSoundTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public void Clear() { }
        }

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

        private EntityRegistry registry;
        private FakeGameDataStore store;
        private MatchStartState start;
        private FakeTickUpdater clock;
        private FakeRunner runner;
        private FakeFlappyAudio audio;
        private FlappyFlapSound sound;
        private FlappyStun stun;
        private FinishState finish;

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
            store = new FakeGameDataStore { userEntityId = "me" };
            start = new MatchStartState();
            start.Update(100, 2, 2);
            clock = new FakeTickUpdater { interval = 0.02d, tick = 150 };
            runner = new FakeRunner { tickUpdater = clock };
            audio = new FakeFlappyAudio();
            sound = new FlappyFlapSound(registry, store, start, runner, audio);
        }

        [Test]
        public void 달리는_중이면_날갯짓_소리()
        {
            sound.OnFlap();

            CollectionAssert.AreEqual(new[] { FlappySfx.Flap }, audio.Played);
        }

        [Test]
        public void 출발_전엔_소리_없음()
        {
            clock.tick = 99;
            sound.OnFlap();

            start.Update(long.MaxValue, 1, 2);
            clock.tick = 500;
            sound.OnFlap();

            Assert.IsEmpty(audio.Played, "카운트다운 중·출발 틱 모름");
        }

        [Test]
        public void 기절_중엔_소리_없음()
        {
            stun.StunRemaining = 0.8f;

            sound.OnFlap();

            Assert.IsEmpty(audio.Played);
        }

        [Test]
        public void 결승_뒤엔_소리_없음()
        {
            finish.FinishedTick = 140;

            sound.OnFlap();

            Assert.IsEmpty(audio.Played);
        }

        [Test]
        public void 내_새가_없으면_소리_없음()
        {
            store.userEntityId = null;
            Assert.DoesNotThrow(() => sound.OnFlap(), "판 시작 직후엔 내 id가 비어 있다");

            store.userEntityId = "me";
            registry.Remove("me");   //  잡혀서 몸이 사라졌다
            sound.OnFlap();

            Assert.IsEmpty(audio.Played);
        }

        [Test]
        public void 화면을_누르면_소리가_난다()
        {
            //  실제 입력 길: FlapPadViewModel.Flap()이 소리를 거친다. Flap()이 안 쓰는 칸(추격자·맵 규칙·알갱이 우편함)은 null.
            var input = new PlayerInputManager(runner, new PlayerContext(), null, registry, null, null);
            var vm = new FlapPadViewModel(input, new PlayerContext { entityId = "me" }, registry, null, null, null, sound);

            vm.Flap();

            CollectionAssert.AreEqual(new[] { FlappySfx.Flap }, audio.Played);
        }
    }
}
