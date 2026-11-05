using System;
using System.Threading.Tasks;
using GameFramework.World;
using LOP.UI;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 정보 HUD 소스 — 진행은 출발선에서 간 거리(<see cref="CardRushRunnerState.PosMm"/>), 끝은 코스 길이(TrackMm).
    /// 레인 방향과 무관하다(Flappy의 <see cref="RaceHudSource"/>와 달리 그려진 좌표를 읽지 않는다).
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
            rules.Set(CardRushTheme.StoneBridge, 40, 2.2f, 2.6f,
                      UnityEngine.Vector3.zero, UnityEngine.Vector3.forward);
            var world = new CardRushWorld(registry, new WorldEventBuffer(), rules, new FixedSeed(), CardRushServerCorrectionHandlerTests.Config);
            world.Tick(1, 0.02f);   //  Mutation이 EnsureCourse를 돌려 Course를 만든다(Simulated 몸이 없어 아무도 안 움직인다)

            var gameDataStore = new FakeGameDataStore { userEntityId = "b" };
            var runner = new FakeRunner { tickUpdater = new FakeTickUpdater { tick = 1000, interval = 0.02d, elapsedTime = 20d } };
            var matchStart = new MatchStartState();
            matchStart.Update(100, 2, 2);

            source = new CardRushRaceHudSource(registry, gameDataStore, matchStart, runner, world);
        }

        private static Entity Runner(string id, int posMm, int placement = 0)
        {
            var e = new Entity(id);
            e.Add(new EntityKind(EntityType.Character));
            e.Add(new CardRushRunner { State = new CardRushRunnerState { PosMm = posMm } });
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
        public void 진행은_간_거리_끝은_코스_길이()
        {
            registry.Add(Runner("a", 0));                         //  출발선 그대로
            registry.Add(Runner("b", 61234));
            registry.Add(Runner("c", 140000, placement: 1));      //  완주

            var frame = new RaceHudFrame();
            source.Fill(frame);

            Assert.AreEqual(0f, Find(frame, "a").X, 1e-5f);
            Assert.AreEqual(61234f, Find(frame, "b").X, 1e-5f);
            Assert.AreEqual(140000f, Find(frame, "c").X, 1e-5f);
            Assert.AreEqual(0f, frame.StartX);
            Assert.AreEqual(140000f, frame.FinishX, 1e-5f, "시작 튜닝 140 m");
            Assert.IsTrue(frame.HasCourse);
        }
}
}
