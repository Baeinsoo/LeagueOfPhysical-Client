using System;
using System.Reflection;
using System.Threading.Tasks;
using GameFramework.World;
using LOP.UI;
using NUnit.Framework;
using UnityEngine;

namespace LOP.Tests
{
    /// <summary>
    /// HUD 한 장을 월드에서 채운다. 핵심은 x를 <b>그려진 자리</b>(보간된 그림)에서 읽는 것 — 시뮬 x를 쓰면 막대·순위가
    /// 화면 속 새와 0.23초만큼 다른 말을 한다(spec §1).
    /// </summary>
    public class RaceHudSourceTests
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
        private ActorRegistry actors;
        private FinishLineBounds finish;
        private RaceHudSource source;
        private readonly System.Collections.Generic.List<GameObject> spawned = new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            registry = new EntityRegistry();
            actors = new ActorRegistry();
            finish = new FinishLineBounds(FinishAxis.X);
            var start = new MatchStartState();
            start.Update(100, 2, 2);
            var runner = new FakeRunner { tickUpdater = new FakeTickUpdater { interval = 0.02d, elapsedTime = 5d } };
            source = new RaceHudSource(registry, actors, new FakeGameDataStore { userEntityId = "me" }, start, runner, finish);
            source.spawnStartX = () => 10f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) { if (go != null) { UnityEngine.Object.DestroyImmediate(go); } }
            spawned.Clear();
        }

        private Entity Bird(string id, float simX, int placement = 0, long finishedTick = FinishState.NotFinished)
        {
            var e = new Entity(id);
            e.Add(new EntityKind(EntityType.Character));
            e.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(simX, 0f, 0f) });
            e.Add(new FinishPlacement { Value = placement });
            e.Add(new FinishState { FinishedTick = finishedTick });
            registry.Add(e);
            return e;
        }

        //  MountedHorseViewTests와 같은 모양: 액터 + 뷰 + 그림(visual)을 반사로 꽂는다.
        private void Visual(string id, float x)
        {
            var actorGo = new GameObject("Actor_" + id);
            spawned.Add(actorGo);
            var entityView = actorGo.AddComponent<LOPEntityView>();
            var actor = actorGo.AddComponent<LOPActor>();
            actor.SetEntityId(id);
            actor.SetView(entityView);
            var visual = new GameObject("Visual_" + id);
            visual.transform.SetParent(actorGo.transform, false);
            visual.transform.position = new Vector3(x, 0f, 0f);
            typeof(LOPEntityView).GetField("_visualGameObject", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(entityView, visual);
            actors.Add(actor);
        }

        private RaceHudFrame Fill()
        {
            var frame = new RaceHudFrame();
            source.Fill(frame);
            return frame;
        }

        private static RaceBirdSample Find(RaceHudFrame frame, string id)
        {
            foreach (var b in frame.Birds) { if (b.Id == id) { return b; } }
            Assert.Fail($"{id}가 프레임에 없다");
            return default;
        }

        [Test]
        public void 남의_x는_그려진_자리로_읽는다()
        {
            Bird("a", 50f);
            Visual("a", 47f);

            Assert.AreEqual(47f, Find(Fill(), "a").X, 1e-5f);
        }

        [Test]
        public void 그림이_아직_없으면_시뮬_x()
        {
            Bird("b", 33f);

            Assert.AreEqual(33f, Find(Fill(), "b").X, 1e-5f);
        }

        [Test]
        public void 등수와_내_결승_틱과_시계를_싣는다()
        {
            Bird("me", 120f, placement: 2, finishedTick: 300);

            var frame = Fill();

            Assert.AreEqual("me", frame.MyId);
            Assert.AreEqual(2, Find(frame, "me").Placement);
            Assert.AreEqual(300, frame.MyFinishedTick);
            Assert.AreEqual(100, frame.StartTick);
            Assert.AreEqual(5d, frame.NowSeconds);
            Assert.AreEqual(0.02d, frame.SecondsPerTick);
        }

        [Test]
        public void 출발_x는_SpawnPoint_결승_x는_결승선_앞면()
        {
            Bird("me", 20f);
            finish.Register(new Bounds(new Vector3(110f, 0f, 0f), new Vector3(2f, 10f, 10f)));

            var frame = Fill();

            Assert.AreEqual(10f, frame.StartX, 1e-5f);
            Assert.AreEqual(109f, frame.FinishX, 1e-5f);
            Assert.IsTrue(frame.HasCourse);
        }

        [Test]
        public void SpawnPoint가_없으면_처음_본_새들_중_가장_뒤를_출발로_고정한다()
        {
            source.spawnStartX = () => null;
            Bird("me", 30f);
            Bird("a", 20f);
            finish.Register(new Bounds(new Vector3(110f, 0f, 0f), Vector3.zero));
            Fill();

            registry.Get("a").Get<GameFramework.World.Transform>().Position = new System.Numerics.Vector3(60f, 0f, 0f);
            var frame = Fill();

            Assert.AreEqual(20f, frame.StartX, 1e-5f, "새가 앞으로 가도 출발 x는 그대로");
        }

        [Test]
        public void 결승선이_없으면_코스를_모른다()
        {
            Bird("me", 20f);

            Assert.IsFalse(Fill().HasCourse);
        }

        [Test]
        public void 새가_아니면_뺀다()
        {
            Bird("me", 20f);
            var rock = new Entity("rock");
            rock.Add(new GameFramework.World.Transform());
            registry.Add(rock);

            Assert.AreEqual(1, Fill().Birds.Count);
        }
    }
}
