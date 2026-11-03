using System;
using System.Threading.Tasks;
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    public class CardRushHeadCardTests
    {
        // ── 뷰 배선용 가짜(옛 CardRushPadViewTests.카드는_다음_입력_틱으로_그린다에서 옮김) ──

        private sealed class FakeTickUpdater : GameFramework.Runner.ITickUpdater
        {
            public event Action<long> onTick { add { } remove { } }
            public long tick { get; set; }
            public double interval { get; set; } = 0.02;
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

        private sealed class FakeSession : GameFramework.ISession
        {
            public string sessionId => "s";
            public string userId => "u";
            public bool isConnected => true;
            public void Send<T>(T message, bool reliable = true) where T : GameFramework.IMessage { }
            public GameFramework.IMessage Receive() => null;
            public void Dispose() { }
        }

        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public string formerEntityId { get; set; }
            public void Clear() { }
        }

        private sealed class FixedSeed : IMatchSeed { public ulong Value => 777; }

        //  내 몸이 Start 틱에 돌기 시작했고, 러너가 색 경계 바로 앞 틱을 막 돌렸다(아직 tick을 안 올린 그 틱 안) —
        //  러너 틱 = 경계 − 1, 다음 입력 틱 = 경계.
        private sealed class Rig
        {
            public EntityRegistry Registry;
            public CardRushWorld World;
            public PlayerInputManager Input;
            public PlayerContext Context;
            public FakeRunner Runner;
            public long Boundary;
        }

        private static Rig AtBoundary()
        {
            var registry = new EntityRegistry();
            var me = new Entity("me");
            me.Add(new InputBuffer());
            me.Add(new CardRushRunner { State = new CardRushRunnerState { SpinStartTick = Start } });
            registry.Add(me);

            var rules = new CardRushMapRulesField();
            rules.Set(CardRushTheme.DoorDash, 40, 2.2f, 2.6f, UnityEngine.Vector3.zero, UnityEngine.Vector3.forward);
            var world = new CardRushWorld(registry, new WorldEventBuffer(), rules, new FixedSeed(), timing);
            world.Tick(1, 0.02f);   //  코스를 만든다(Simulated 몸이 없어 아무도 안 움직인다)
            world.GameplayStartTick = Start - 100;

            var clock = new FakeTickUpdater();
            var runner = new FakeRunner { tickUpdater = clock };
            var context = new PlayerContext { entityId = "me", session = new FakeSession() };
            var input = new PlayerInputManager(runner, context, null, registry, new InputBufferSystem(),
                new GameFramework.Netcode.SequenceBuffer<InputCommand>(128));

            long boundary = Start + CardRushCard.DwellTicks;
            clock.tick = boundary - 1;
            input.Tick(boundary - 1, 0.02f);
            Assert.AreEqual(boundary, input.NextInputTick);
            return new Rig { Registry = registry, World = world, Input = input, Context = context, Runner = runner, Boundary = boundary };
        }
        private const long Start = 1000;
        private static readonly CardRushCourse course = new CardRushCourse(77, 40);
        private static readonly CardRushTiming timing = new CardRushTiming(15, 30);

        private static CardRushHeadCardFace At(CardRushRunnerState s, long tick)
            => CardRushHeadCard.Compute(s, tick, Start, course, timing);

        [Test]
        public void 출발_전과_대기는_뒷면()
        {
            var s = new CardRushRunnerState();
            var before = At(s, Start - 50);
            Assert.IsTrue(before.Visible); Assert.IsFalse(before.FaceUp);
            var waiting = At(new CardRushRunnerState { Step = 3, MoveStartTick = 2000 }, 2100);
            Assert.IsTrue(waiting.Visible); Assert.IsFalse(waiting.FaceUp);
        }

        [Test]
        public void 돌면_그_틱의_색과_남은_시간()
        {
            var s = new CardRushRunnerState { Step = 3, MoveStartTick = 2000, SpinStartTick = 2100 };
            var f = At(s, 2150);
            Assert.IsTrue(f.Visible); Assert.IsTrue(f.FaceUp);
            Assert.IsTrue(CardRushCard.TryColorAt(s, 2150, course, out var c)); Assert.AreEqual(c, f.Color);
            Assert.AreEqual(1f - 50f / CardRushCard.WindowTicks, f.Remaining01, 1e-5f);
            Assert.AreEqual(1f, At(s, 2100).Remaining01, 1e-5f);
            Assert.Greater(At(s, 2399).Remaining01, 0f);
        }

        [Test]
        public void 이동_멈춤_완주엔_숨는다()
        {
            Assert.IsFalse(At(new CardRushRunnerState { Step = 3, MoveStartTick = 2000 }, 2010).Visible, "이동 중 — 카드는 던져졌다");
            Assert.IsFalse(At(new CardRushRunnerState { StunEndTick = 3000, Attempt = 1 }, 2990).Visible, "멈춤 중");
            Assert.IsFalse(At(new CardRushRunnerState { Step = 40, MoveStartTick = 2000 }, 2500).Visible, "완주 뒤");
        }

        //  옛 CardRushPadViewTests.카드는_다음_입력_틱으로_그린다를 옮겨 온 자리(최종 리뷰 M6: 순수 함수 시험만으로는 뷰가 다른 틱을
        //  넘겨도 초록이었다). 내 몸이 S에 돌기 시작했으면 다음 입력 틱 S+25는 첫 장, S+26은 다음 색 — 뷰가 그 틱으로 칠하는지 본다.
        [Test]
        public void 내_카드는_다음_입력_틱으로_칠한다()
        {
            var rig = AtBoundary();
            //  그림 시계도 진짜로 준다 — 뷰가 그림 틱(내 몸 = 러너 틱 = 경계 − 1)으로 칠하면 여기서 갈린다.
            var clock = new EntityRenderClock(rig.Context, rig.Runner, null, new FakeGameDataStore { userEntityId = "me" });
            Assert.AreEqual(rig.Boundary - 1, clock.TickFor("me"));
            var view = new CardRushHeadCardView(rig.World, rig.Registry, new ActorRegistry(), rig.Context, clock, rig.Input, null);
            var state = rig.Registry.Get("me").Get<CardRushRunner>().State;

            var before = CardRushHeadCard.Compute(state, rig.Boundary - 1, rig.World.GameplayStartTick, rig.World.Course, rig.World.Timing);
            var atBoundary = CardRushHeadCard.Compute(state, rig.Boundary, rig.World.GameplayStartTick, rig.World.Course, rig.World.Timing);
            Assert.IsTrue(before.FaceUp); Assert.IsTrue(atBoundary.FaceUp);
            Assert.AreNotEqual(before.Color, atBoundary.Color, "경계 앞뒤 색이 달라야 틱 하나의 어긋남을 잡는다");
            Assert.IsTrue(CardRushCard.TryColorAt(state, rig.Boundary, rig.World.Course, out var judged));
            Assert.AreEqual(judged, atBoundary.Color, "경계 틱에 누르면 판정되는 색");

            Assert.IsTrue(view.TryComputeMine(out var mine));
            Assert.IsTrue(mine.FaceUp);
            Assert.AreEqual(atBoundary.Color, mine.Color, "지금 누르면 판정될 틱(S+26)의 색");

            //  틱을 막 올린 사이(아직 이번 틱 명령을 안 단 때)엔 러너 틱이 곧 다음 입력 틱 — S+25면 첫 장.
            var rig2 = AtBoundary();
            ((FakeTickUpdater)rig2.Runner.tickUpdater).tick = rig2.Boundary - 1;
            var fresh = new PlayerInputManager(rig2.Runner, rig2.Context, null, rig2.Registry, new InputBufferSystem(),
                new GameFramework.Netcode.SequenceBuffer<InputCommand>(128));
            Assert.AreEqual(rig2.Boundary - 1, fresh.NextInputTick);
            var view2 = new CardRushHeadCardView(rig2.World, rig2.Registry, new ActorRegistry(), rig2.Context,
                new EntityRenderClock(rig2.Context, rig2.Runner, null, new FakeGameDataStore { userEntityId = "me" }), fresh, null);
            Assert.IsTrue(view2.TryComputeMine(out var first));
            Assert.AreEqual(before.Color, first.Color, "S+25 = 첫 장");
        }

        //  컨트롤러 메모(Task 4): 째깍은 머리 위 카드와 같은 틱으로 색을 본다 — 러너 틱으로 보면 경계에서 한 틱 늦게 째깍인다.
        [Test]
        public void 내_째깍은_머리_위_카드와_같은_틱으로_색을_본다()
        {
            var rig = AtBoundary();
            var store = new FakeGameDataStore { userEntityId = "me" };
            var clock = new EntityRenderClock(rig.Context, rig.Runner, null, store);
            var audio = new CardRushAudio(rig.World, rig.Registry, store, clock, rig.Input);
            try
            {
                var view = new CardRushHeadCardView(rig.World, rig.Registry, new ActorRegistry(), rig.Context, clock, rig.Input, null);
                Assert.IsTrue(view.TryComputeMine(out var mine));
                var frame = audio.ReadFrame("me");
                Assert.IsTrue(frame.FaceUp);
                Assert.AreEqual(mine.Color, frame.Color, "본 색 = 들은 째깍의 색");
                Assert.IsTrue(CardRushCard.TryColorAt(rig.Registry.Get("me").Get<CardRushRunner>().State, rig.Boundary, rig.World.Course, out var judged));
                Assert.AreEqual(judged, frame.Color, "다음 입력 틱(경계)의 색 — 러너 틱(경계 − 1)이면 한 장 앞 색");
                Assert.AreEqual(rig.Boundary - 1, (long)frame.DrawnTick, "꽂힘·철썩은 그대로 그림(러너) 틱");
            }
            finally
            {
                audio.Dispose();
            }
        }

        [Test]
        public void 남은_시간_링은_12시부터_시계_방향으로_먹힌다()
        {
            var pts = new UnityEngine.Vector3[CardRushHeadCardView.RingSegments + 1];
            int full = CardRushHeadCardView.RingArc(1f, pts);
            Assert.AreEqual(CardRushHeadCardView.RingSegments + 1, full);
            Assert.AreEqual(CardRushHeadCardView.RingRadius, pts[0].y, 1e-4f, "12시에서 시작");
            Assert.AreEqual(CardRushHeadCardView.RingRadius, pts[full - 1].y, 1e-4f, "한 바퀴 돌아 12시");
            Assert.Greater(pts[1].x, 0f, "시계 방향(보는 쪽에서 오른쪽으로)");

            int half = CardRushHeadCardView.RingArc(0.5f, pts);
            Assert.Less(half, full);
            Assert.AreEqual(-CardRushHeadCardView.RingRadius, pts[0].y, 1e-4f, "반이 먹히면 6시부터 남는다");
            Assert.Less(pts[1].x, 0f, "6시에서 시계 방향 = 왼쪽 위로");
            Assert.AreEqual(CardRushHeadCardView.RingRadius, pts[half - 1].y, 1e-4f);
        }

        [Test]
        public void 넘김과_통통은_잠깐이고_끝나면_원래_크기()
        {
            Assert.AreEqual(1f, CardRushHeadCardView.FlipWidth(-1f));
            Assert.AreEqual(0.15f, CardRushHeadCardView.FlipWidth(CardRushHeadCardView.FlipSeconds * 0.5f), 1e-4f);
            Assert.AreEqual(1f, CardRushHeadCardView.FlipWidth(CardRushHeadCardView.FlipSeconds));
            Assert.AreEqual(CardRushHeadCardView.PopScale, CardRushHeadCardView.PopSize(0f), 1e-5f);
            Assert.AreEqual(1f, CardRushHeadCardView.PopSize(CardRushHeadCardView.PopSeconds));
        }

        [Test]
        public void 남의_카드는_레인_셋_안만()
        {
            Assert.AreEqual(3, CardRushHeadCardView.LaneGap(new UnityEngine.Vector3(7.8f, 0f, 0f), UnityEngine.Vector3.zero, UnityEngine.Vector3.forward, 2.6f));
            Assert.AreEqual(4, CardRushHeadCardView.LaneGap(new UnityEngine.Vector3(-10.4f, 0f, 5f), UnityEngine.Vector3.zero, UnityEngine.Vector3.forward, 2.6f),
                "앞뒤 거리는 세지 않는다");
        }
    }
}
