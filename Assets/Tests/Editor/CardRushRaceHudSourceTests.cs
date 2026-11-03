using System;
using System.Threading.Tasks;
using GameFramework.World;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 정보 HUD 소스 — 몸마다 x를 <see cref="CardRushLanes.Progress"/>로 다시 계산한다(칸 단위라
    /// 레인 방향과 무관, Flappy의 <see cref="RaceHudSource"/>와 달리 그려진 좌표를 그대로 읽지 않는다).
    /// </summary>
    public class CardRushRaceHudSourceTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public string formerEntityId { get; set; }
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

        private sealed class FixedSeed : IMatchSeed { public ulong Value => 4242; }

        private EntityRegistry registry;
        private CardRushRaceHudSource source;

        [SetUp]
        public void SetUp()
        {
            registry = new EntityRegistry();

            var rules = new CardRushMapRulesField();
            rules.Set(CardRushTheme.StoneBridge, 40, 2.2f, 2.6f, CardRushSections.Default,
                      UnityEngine.Vector3.zero, UnityEngine.Vector3.forward);
            var world = new CardRushWorld(registry, new WorldEventBuffer(), rules, new FixedSeed(), new CardRushTiming(20, 30));
            world.Tick(1, 0.02f);   //  Mutation이 EnsureCourse를 돌려 Course를 만든다(Simulated 몸이 없어 아무도 안 움직인다)

            var gameDataStore = new FakeGameDataStore { userEntityId = "b" };
            var runner = new FakeRunner { tickUpdater = new FakeTickUpdater { tick = 1000, interval = 0.02d, elapsedTime = 20d } };
            var matchStart = new MatchStartState();
            matchStart.Update(100, 2, 2);

            //  내 몸 = "b"(예측 틱), 남은 스냅이 아직 없어 보간 시계가 내 틱으로 메운다.
            var renderClock = new EntityRenderClock(new PlayerContext { entityId = "b" }, runner,
                                                    new RemoteInterpolationClock(gameDataStore), gameDataStore);
            source = new CardRushRaceHudSource(registry, gameDataStore, matchStart, runner, world, renderClock);
        }

        private static Entity Runner(string id, int step, long moveStartTick, int placement = 0)
        {
            var e = new Entity(id);
            e.Add(new EntityKind(EntityType.Character));
            e.Add(new CardRushRunner { State = new CardRushRunnerState { Step = step, MoveStartTick = moveStartTick } });
            e.Add(new FinishPlacement { Value = placement });
            return e;
        }

        private static RaceBirdSample Find(RaceHudFrame frame, string id)
        {
            foreach (var b in frame.Birds)
            {
                if (b.Id == id)
                {
                    return b;
                }
            }
            Assert.Fail($"{id}가 프레임에 없다");
            return default;
        }

        [Test]
        public void 세_몸의_걸음_진행이_x_값이_된다()
        {
            registry.Add(Runner("a", step: 0, moveStartTick: 0));                  //  출발선 그대로
            registry.Add(Runner("b", step: 10, moveStartTick: 990));               //  tick 1000, moveTicks 20 → 절반
            registry.Add(Runner("c", step: 40, moveStartTick: 0, placement: 1));   //  완주

            var frame = new RaceHudFrame();
            source.Fill(frame);

            Assert.AreEqual(0f, Find(frame, "a").X, 1e-5f);
            Assert.AreEqual(9.5f, Find(frame, "b").X, 1e-5f);
            Assert.AreEqual(40f, Find(frame, "c").X, 1e-5f);
            Assert.AreEqual(0f, frame.StartX);
            Assert.AreEqual(40f, frame.FinishX, 1e-5f);
            Assert.IsTrue(frame.HasCourse);
        }
    
        //  최종 리뷰 M1: 남의 상태는 최신 스냅이라 그려지는 몸(보간 틱)보다 앞선다. 그림 틱이 이동 시작 전이면 아직 앞 칸에
        //  서 있다 — 새 칸을 먼저 보여 줬다가 이동 시작 틱에 한 칸 물러서면 진행 막대 점이 튄다.
        [Test]
        public void 그림_틱이_이동_시작_전이면_앞_칸()
        {
            registry.Add(Runner("x", step: 5, moveStartTick: 1005));   //  그림 틱 1000 < 이동 시작 1005

            var frame = new RaceHudFrame();
            source.Fill(frame);

            Assert.AreEqual(4f, Find(frame, "x").X, 1e-5f);
        }
}
}
