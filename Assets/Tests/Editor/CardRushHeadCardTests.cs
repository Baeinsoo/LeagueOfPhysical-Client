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
            public GameFramework.Netcode.SequenceBuffer<InputCommand> History;
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
            var world = new CardRushWorld(registry, new WorldEventBuffer(), rules, new FixedSeed(), CardRushServerCorrectionHandlerTests.Config);
            world.Tick(1, 0.02f);   //  코스를 만든다(Simulated 몸이 없어 아무도 안 움직인다)
            world.GameplayStartTick = Start - 100;

            var clock = new FakeTickUpdater();
            var runner = new FakeRunner { tickUpdater = clock };
            var context = new PlayerContext { entityId = "me", session = new FakeSession() };
            var history = new GameFramework.Netcode.SequenceBuffer<InputCommand>(128);
            var input = new PlayerInputManager(runner, context, null, registry, new InputBufferSystem(), history);

            long boundary = Start + CardRushCard.DwellTicks;
            clock.tick = boundary - 1;
            input.Tick(boundary - 1, 0.02f);
            Assert.AreEqual(boundary, input.NextInputTick);
            return new Rig { Registry = registry, World = world, Input = input, Context = context, Runner = runner, Boundary = boundary, History = history };
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

        // ── 최종 리뷰 I1: 칠하는 단계 ──

        //  Unity 한 프레임 안의 자리(단계, 그 안 순서). 틱 코루틴(TickUpdaterBase, yield null)은 Update.ScriptRunDelayedDynamicFrameRate,
        //  보간기(Snapshot/PredictedEntityInterpolator)는 PreLateUpdate.ScriptRunBehaviourLateUpdate에서 돈다. VContainer는
        //  ITickable을 Update.ScriptRunBehaviourUpdate 앞에, IPostTickable을 Update 끝에, ILateTickable을 ScriptRunBehaviourLateUpdate 앞에,
        //  IPostLateTickable을 PreLateUpdate 끝에 끼운다(PlayerLoopHelper). 자리는 Unity의 실제 기본 루프에서 잰다.
        private static (int phase, float order) SlotOf(System.Type stage)
        {
            var loop = UnityEngine.LowLevel.PlayerLoop.GetDefaultPlayerLoop();
            int Phase(System.Type t) { for (int i = 0; i < loop.subSystemList.Length; i++) if (loop.subSystemList[i].type == t) return i; throw new System.Exception(t.Name); }
            float Index(System.Type parent, System.Type child)
            {
                var subs = loop.subSystemList[Phase(parent)].subSystemList;
                for (int i = 0; i < subs.Length; i++) if (subs[i].type == child) return i;
                throw new System.Exception(child.Name + " not in " + parent.Name);
            }
            int End(System.Type parent) => loop.subSystemList[Phase(parent)].subSystemList.Length;
            var update = typeof(UnityEngine.PlayerLoop.Update);
            var late = typeof(UnityEngine.PlayerLoop.PreLateUpdate);
            if (stage == typeof(VContainer.Unity.ITickable)) return (Phase(update), Index(update, typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate)) - 0.5f);
            if (stage == typeof(VContainer.Unity.IPostTickable)) return (Phase(update), End(update));
            if (stage == typeof(VContainer.Unity.ILateTickable)) return (Phase(late), Index(late, typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate)) - 0.5f);
            if (stage == typeof(VContainer.Unity.IPostLateTickable)) return (Phase(late), End(late));
            if (stage == typeof(GameFramework.Runner.ITickUpdater)) return (Phase(update), Index(update, typeof(UnityEngine.PlayerLoop.Update.ScriptRunDelayedDynamicFrameRate)));
            if (stage == typeof(UnityEngine.MonoBehaviour)) return (Phase(late), Index(late, typeof(UnityEngine.PlayerLoop.PreLateUpdate.ScriptRunBehaviourLateUpdate)));
            throw new System.ArgumentException(stage.Name);
        }

        private static (int, float) StageOf(System.Type viewType)
        {
            var stages = new[] { typeof(VContainer.Unity.ITickable), typeof(VContainer.Unity.IPostTickable), typeof(VContainer.Unity.ILateTickable), typeof(VContainer.Unity.IPostLateTickable) };
            var found = System.Array.FindAll(stages, s => s.IsAssignableFrom(viewType));
            Assert.AreEqual(1, found.Length, viewType.Name + "는 한 단계에서만 돈다");
            return SlotOf(found[0]);
        }

        private static bool After((int phase, float order) a, (int phase, float order) b) => a.phase > b.phase || (a.phase == b.phase && a.order > b.order);

        //  카드를 틱 앞에서 칠하면 그 프레임 틱이 돌아 화면 색이 판정 틱보다 한 틱 늦다(60 fps에서 프레임 대부분).
        //  몸 자리·머리 뼈는 보간기 LateUpdate가 옮기므로 그 뒤여야 카드·손 자리가 몸에 붙는다. 탭은 틱 앞에서 읽는다.
        [Test]
        public void 카드_째깍_몸개그는_틱과_보간_뒤에_칠하고_탭은_틱_앞에서_읽는다()
        {
            var tick = SlotOf(typeof(GameFramework.Runner.ITickUpdater));
            var interpolator = SlotOf(typeof(UnityEngine.MonoBehaviour));
            foreach (var view in new[] { typeof(CardRushHeadCardView), typeof(CardRushAudio), typeof(CardRushRunnerFxView), typeof(CardRushCourseView) })
            {
                var at = StageOf(view);
                Assert.IsTrue(After(at, tick), view.Name + "는 그 프레임 틱 뒤");
                Assert.IsTrue(After(at, interpolator), view.Name + "는 보간기 LateUpdate 뒤");
            }
            Assert.IsFalse(After(StageOf(typeof(CardRushTapGrab)), tick), "탭은 틱 앞(그 프레임 첫 틱에 실린다)");
        }

        //  프레임 F: (틱 앞 탭 없음) → 틱 N → (틱·보간 뒤) 카드를 칠한다. 프레임 F+1: 틱 앞 탭 → 틱 N+1.
        //  화면에 보인 색이 그 탭이 실린 틱의 판정 색이어야 한다.
        [Test]
        public void 탭은_지난_프레임_카드가_보여준_틱에_실린다()
        {
            var rig = AtBoundary();   //  러너가 경계 − 1 틱을 막 돌렸다(F의 틱)
            var clock = (FakeTickUpdater)rig.Runner.tickUpdater;
            clock.tick = rig.Boundary;   //  틱 코루틴이 틱을 돌린 뒤 러너 틱을 올린다
            var view = new CardRushHeadCardView(rig.World, rig.Registry, new ActorRegistry(), rig.Context,
                new EntityRenderClock(rig.Context, rig.Runner, null, new FakeGameDataStore { userEntityId = "me" }), rig.Input, null);
            Assert.IsTrue(view.TryComputeMine(out var shown));

            var grab = new CardRushTapGrab(rig.Input, new CardRushTapTarget(), () => true, _ => { });
            grab.Tick();                                 //  F+1 틱 앞
            rig.Input.Tick(clock.tick, 0.02f);           //  F+1 첫 틱
            Assert.IsTrue(rig.History.TryGet(rig.Boundary, out var command) && command.Grab, "탭은 경계 틱에 실린다");
            Assert.IsTrue(CardRushCard.TryColorAt(rig.Registry.Get("me").Get<CardRushRunner>().State, rig.Boundary, rig.World.Course, out var judged));
            Assert.AreEqual(judged, shown.Color, "본 색 = 판정 색");
        }

        // ── 최종 리뷰 m3: 남의 카드는 던지기 전에 사라지지 않는다 ──

        //  남의 상태는 최신 스냅이라 그려지는 몸보다 앞선다 — 그림 틱이 던짐 전이면 던지기 전 상태로 칠한다.
        [Test]
        public void 남의_카드는_그림_틱이_던짐_전이면_돌던_카드를_보인다()
        {
            var right = new CardRushRunnerState { Step = 3, MoveStartTick = 1000 };
            var memory = new CardRushSpinMemory(2, 0, 900);
            var f = CardRushHeadCard.ComputeDrawn(right, 997, Start - 500, course, timing, memory);
            Assert.IsTrue(f.Visible); Assert.IsTrue(f.FaceUp);
            Assert.IsTrue(CardRushCard.TryColorAt(new CardRushRunnerState { Step = 2, SpinStartTick = 900 }, 997, course, out var c));
            Assert.AreEqual(c, f.Color, "던지기 전 상태(칸 2, 시작 900)의 그 틱 색");
            Assert.IsFalse(CardRushHeadCard.ComputeDrawn(right, 1000, Start - 500, course, timing, memory).Visible, "던진 틱부터는 손을 떠났다");

            var noMemory = CardRushHeadCard.ComputeDrawn(right, 997, Start - 500, course, timing, default);
            Assert.IsTrue(noMemory.Visible, "시작을 못 봤어도 사라지지 않고 뒷면으로 남는다");
            Assert.IsFalse(noMemory.FaceUp);

            var wrong = new CardRushRunnerState { Step = 3, Attempt = 1, StunEndTick = 1030, MoveStartTick = 500 };
            var w = CardRushHeadCard.ComputeDrawn(wrong, 998, Start - 500, course, timing, new CardRushSpinMemory(3, 0, 950));
            Assert.IsTrue(w.Visible && w.FaceUp, "틀린 던짐 전에도");
            Assert.AreEqual(At(new CardRushRunnerState { Step = 3, Attempt = 1, StunEndTick = 1030 }, 1040).Visible,
                CardRushHeadCard.ComputeDrawn(wrong, 1040, Start - 500, course, timing, default).Visible, "그림 틱이 따라잡으면 보통 식과 같다");
        }
    }
}
