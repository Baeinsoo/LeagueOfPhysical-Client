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
            rules.Set(CardRushTheme.DoorDash, 2.6f, UnityEngine.Vector3.zero, UnityEngine.Vector3.forward);
            var world = new CardRushWorld(registry, new WorldEventBuffer(), rules, new FixedSeed(), CardRushServerCorrectionHandlerTests.Config);
            world.Tick(1, 0.02f);   //  코스를 만든다(Simulated 몸이 없어 아무도 안 움직인다)
            world.GameplayStartTick = Start - 100;

            var clock = new FakeTickUpdater();
            var runner = new FakeRunner { tickUpdater = clock };
            var context = new PlayerContext { entityId = "me", session = new FakeSession() };
            var history = new GameFramework.Netcode.SequenceBuffer<InputCommand>(128);
            var input = new PlayerInputManager(runner, context, null, registry, new InputBufferSystem(), history);

            long boundary = Start + CardRushRules.CardDwellTicks;
            clock.tick = boundary - 1;
            input.Tick(boundary - 1, 0.02f);
            Assert.AreEqual(boundary, input.NextInputTick);
            return new Rig { Registry = registry, World = world, Input = input, Context = context, Runner = runner, Boundary = boundary, History = history };
        }
        private const long Start = 1000;
        private const long G = 500;   //  출발 틱
        private const int Lane = 2;
        private static readonly CardRushTrack track =
            CardRushTrack.ForTests(140000, new CardRushObstacle[0], new CardRushZone[0], 77);

        private static CardRushHeadCardFace At(CardRushRunnerState s, long tick)
            => CardRushHeadCard.Compute(s, tick, G, Lane, track);

        [Test]
        public void 출발_전엔_흐리지_않은_뒷면()
        {
            var before = At(new CardRushRunnerState(), G - 50);
            Assert.IsTrue(before.Visible); Assert.IsFalse(before.FaceUp); Assert.IsFalse(before.Dim);
            Assert.IsTrue(At(new CardRushRunnerState(), G).FaceUp, "출발 틱부터 돈다");
        }

        [Test]
        public void 돌_때_색은_그_틱의_카드()
        {
            var s = new CardRushRunnerState { UseCount = 4, SpinStartTick = 1200 };
            foreach (long tick in new long[] { 1200, 1222, 1223, 1250, 1300 })
            {
                var f = At(s, tick);
                Assert.IsTrue(f.Visible && f.FaceUp, "틱 " + tick);
                Assert.IsFalse(f.Dim);
                Assert.IsTrue(CardRushCard.TryCardAt(s, tick, G, Lane, track, out var c));
                Assert.AreEqual(c, f.Color, "틱 " + tick);
            }
        }

        [Test]
        public void 쿨다운엔_흐린_뒷면()
        {
            var s = new CardRushRunnerState { UseCount = 1, SpinStartTick = 1200 };
            var f = At(s, 1190);
            Assert.IsTrue(f.Visible); Assert.IsFalse(f.FaceUp); Assert.IsTrue(f.Dim);
            Assert.IsTrue(At(s, 1200).FaceUp, "쿨다운이 끝나는 틱에 앞면");
        }

        [Test]
        public void 기절_중엔_돌지만_흐리다()
        {
            var s = new CardRushRunnerState { StunEndTick = 1060 };
            var f = At(s, 1040);
            Assert.IsTrue(f.FaceUp, "기절 중에도 카드는 돈다");
            Assert.IsTrue(f.Dim);
            Assert.IsFalse(At(s, 1060).Dim, "기절이 끝나는 틱엔 또렷");
        }

        [Test]
        public void 완주하면_숨는다()
        {
            var s = new CardRushRunnerState { FinishTick = 3000, PosMm = 140000 };
            Assert.IsTrue(At(s, 2999).Visible);
            Assert.IsFalse(At(s, 3000).Visible);
            Assert.IsFalse(At(s, 3100).Visible);
        }

        [Test]
        public void 색이_바뀐_장은_가로로_펴진다()
        {
            var s = new CardRushRunnerState { SpinStartTick = 1200, UseCount = 2 };
            Assert.AreEqual(0f, At(s, 1200).FlipT01, 1e-6f, "새 장이 뜬 틱");
            Assert.AreEqual(1f / CardRushHeadCard.FlipTicks, At(s, 1201).FlipT01, 1e-6f);
            Assert.AreEqual(1f, At(s, 1200 + CardRushHeadCard.FlipTicks).FlipT01, 1e-6f);
            Assert.AreEqual(0f, At(s, 1200 + CardRushRules.CardDwellTicks).FlipT01, 1e-6f, "다음 장이 뜬 틱");
            Assert.AreEqual(1f, At(s, 1190).FlipT01, 1e-6f, "뒷면은 다 펴진 채");
        }

        //  리뷰 m2: 남의 최신 상태엔 아직 그려지지 않은 꽈당의 기절 끝 틱이 있다 — 기절이 시작한 틱(결과 틱)부터만 흐리게.
        [Test]
        public void 남의_카드는_기절이_시작한_뒤에만_흐리다()
        {
            var s = new CardRushRunnerState { StunEndTick = 2035, ObstacleEvent = (int)CardRushObstacleEvent.Tumbled, ObstacleEventTick = 2000 };
            Assert.IsFalse(CardRushHeadCard.ComputeOther(s, 1997, G, Lane, track).Dim, "꽈당 그림 전");
            Assert.IsTrue(CardRushHeadCard.ComputeOther(s, 2000, G, Lane, track).Dim);
            var hit = new CardRushRunnerState { StunEndTick = 3050, ZoneEvent = (int)CardRushZoneEvent.Hit, ZoneEventTick = 3000 };
            Assert.IsFalse(At(hit, 2998).Dim);
            Assert.IsTrue(At(hit, 3010).Dim);
        }

        //  남의 상태는 최신 스냅 — 그림 틱이 마지막 사용(돌기 시작 − 쿨다운)보다 이르면 그 전 카드는 상태에 없다. 뒷면으로 남는다.
        [Test]
        public void 남의_카드는_그림_틱이_마지막_사용_전이면_뒷면()
        {
            var s = new CardRushRunnerState { UseCount = 3, SpinStartTick = 2000 };
            var early = CardRushHeadCard.ComputeOther(s, 1979, G, Lane, track);
            Assert.IsTrue(early.Visible); Assert.IsFalse(early.FaceUp); Assert.IsFalse(early.Dim);

            var cooling = CardRushHeadCard.ComputeOther(s, 1980, G, Lane, track);
            Assert.IsFalse(cooling.FaceUp); Assert.IsTrue(cooling.Dim, "사용 틱부터는 쿨다운(흐린 뒷면)");

            var spun = CardRushHeadCard.ComputeOther(s, 2000, G, Lane, track);
            Assert.IsTrue(spun.FaceUp);
            Assert.AreEqual(track.FirstCard(Lane, 3), spun.Color, "새 첫 장");

            Assert.AreEqual(At(s, 2010).Color, CardRushHeadCard.ComputeOther(s, 2010, G, Lane, track).Color, "그림 틱이 따라잡으면 내 식과 같다");
        }

        //  옛 CardRushPadViewTests.카드는_다음_입력_틱으로_그린다를 옮겨 온 자리(최종 리뷰 M6: 순수 함수 시험만으로는 뷰가 다른 틱을
        //  넘겨도 초록이었다). 내 몸의 카드가 S에 돌기 시작했으면 다음 입력 틱 S+22는 첫 장, S+23은 다음 장 — 뷰가 그 틱으로 칠하는지 본다.
        [Test]
        public void 내_카드는_다음_입력_틱으로_칠한다()
        {
            var rig = AtBoundary();
            //  그림 시계도 진짜로 준다 — 뷰가 그림 틱(내 몸 = 러너 틱 = 경계 − 1)으로 칠하면 여기서 갈린다.
            var clock = new EntityRenderClock(rig.Context, rig.Runner, null, new FakeGameDataStore { userEntityId = "me" });
            Assert.AreEqual(rig.Boundary - 1, clock.TickFor("me"));
            var view = new CardRushHeadCardView(rig.World, rig.Registry, new ActorRegistry(), rig.Context, clock, rig.Input, null);
            var state = rig.Registry.Get("me").Get<CardRushRunner>().State;

            var runner = rig.Registry.Get("me").Get<CardRushRunner>();
            var before = CardRushHeadCard.Compute(state, rig.Boundary - 1, rig.World.GameplayStartTick, runner.Lane, rig.World.Track);
            var atBoundary = CardRushHeadCard.Compute(state, rig.Boundary, rig.World.GameplayStartTick, runner.Lane, rig.World.Track);
            Assert.IsTrue(before.FaceUp); Assert.IsTrue(atBoundary.FaceUp);
            Assert.AreNotEqual(before.Color, atBoundary.Color, "경계 앞뒤 색이 달라야 틱 하나의 어긋남을 잡는다");
            Assert.IsTrue(CardRushCard.TryCardAt(state, rig.Boundary, rig.World.GameplayStartTick, runner.Lane, rig.World.Track, out var judged));
            Assert.AreEqual(judged, atBoundary.Color, "경계 틱에 누르면 쓰는 카드");

            Assert.IsTrue(view.TryComputeMine(out var mine));
            Assert.IsTrue(mine.FaceUp);
            Assert.AreEqual(atBoundary.Color, mine.Color, "지금 누르면 쓰는 틱(S+23)의 카드");

            //  틱을 막 올린 사이(아직 이번 틱 명령을 안 단 때)엔 러너 틱이 곧 다음 입력 틱 — S+25면 첫 장.
            var rig2 = AtBoundary();
            ((FakeTickUpdater)rig2.Runner.tickUpdater).tick = rig2.Boundary - 1;
            var fresh = new PlayerInputManager(rig2.Runner, rig2.Context, null, rig2.Registry, new InputBufferSystem(),
                new GameFramework.Netcode.SequenceBuffer<InputCommand>(128));
            Assert.AreEqual(rig2.Boundary - 1, fresh.NextInputTick);
            var view2 = new CardRushHeadCardView(rig2.World, rig2.Registry, new ActorRegistry(), rig2.Context,
                new EntityRenderClock(rig2.Context, rig2.Runner, null, new FakeGameDataStore { userEntityId = "me" }), fresh, null);
            Assert.IsTrue(view2.TryComputeMine(out var first));
            Assert.AreEqual(before.Color, first.Color, "S+22 = 첫 장");
        }

        //  컨트롤러 메모(Task 4): 째깍은 머리 위 카드와 같은 틱으로 색을 본다 — 러너 틱으로 보면 경계에서 한 틱 늦게 째깍인다.
        [Test]
        public void 내_째깍은_머리_위_카드와_같은_틱으로_색을_본다()
        {
            var rig = AtBoundary();
            var store = new FakeGameDataStore { userEntityId = "me" };
            var clock = new EntityRenderClock(rig.Context, rig.Runner, null, store);
            var audio = new CardRushAudio(rig.World, rig.Registry, store, rig.Input);
            try
            {
                var view = new CardRushHeadCardView(rig.World, rig.Registry, new ActorRegistry(), rig.Context, clock, rig.Input, null);
                Assert.IsTrue(view.TryComputeMine(out var mine));
                var frame = audio.ReadFrame("me");
                Assert.IsTrue(frame.FaceUp);
                Assert.AreEqual(mine.Color, frame.Color, "본 색 = 들은 째깍의 색");
                var me = rig.Registry.Get("me").Get<CardRushRunner>();
                Assert.IsTrue(CardRushCard.TryCardAt(me.State, rig.Boundary, rig.World.GameplayStartTick, me.Lane, rig.World.Track, out var judged));
                Assert.AreEqual(judged, frame.Color, "다음 입력 틱(경계)의 카드 — 러너 틱(경계 − 1)이면 한 장 앞 카드");
            }
            finally
            {
                audio.Dispose();
            }
        }

        [Test]
        public void 넘김은_잠깐이고_색이_바뀐_장은_06에서_펴진다()
        {
            Assert.AreEqual(1f, CardRushHeadCardView.FlipWidth(-1f));
            Assert.AreEqual(0.15f, CardRushHeadCardView.FlipWidth(CardRushHeadCardView.FlipSeconds * 0.5f), 1e-4f);
            Assert.AreEqual(1f, CardRushHeadCardView.FlipWidth(CardRushHeadCardView.FlipSeconds));
            Assert.AreEqual(0.6f, CardRushHeadCardView.ChangeWidth(new CardRushHeadCardFace(true, true, CardColor.Red, false, 0f)), 1e-5f);
            Assert.AreEqual(1f, CardRushHeadCardView.ChangeWidth(new CardRushHeadCardFace(true, true, CardColor.Red, false, 1f)), 1e-5f);
            Assert.AreEqual(1f, CardRushHeadCardView.ChangeWidth(new CardRushHeadCardFace(true, false, default, true, 0f)), 1e-5f, "뒷면은 그대로");
        }

        //  흐린 카드(기절·쿨다운)는 같은 판의 알파 0.35 짝 — 색·모양은 그대로 읽히되 "지금은 못 쓴다"가 보인다.
        [Test]
        public void 흐린_카드는_알파_035()
        {
            var palette = new CardRushCardPalette();
            var materials = new CardRushCardMaterials(palette);
            var parent = new UnityEngine.GameObject("plate-test");
            try
            {
                var plate = new CardRushCardPlate(parent.transform, "Plate", materials);
                plate.SetFace(true, CardColor.Blue, false);
                Assert.AreSame(palette.Of(CardColor.Blue), plate.PlateMaterial);
                plate.SetFace(true, CardColor.Blue, true);
                var dim = plate.PlateMaterial;
                Assert.AreNotSame(palette.Of(CardColor.Blue), dim);
                var c = dim.HasProperty("_BaseColor") ? dim.GetColor("_BaseColor") : dim.color;
                Assert.AreEqual(CardRushHeadCardView.DimAlpha, c.a, 1e-4f);
                Assert.AreEqual(CardRushCardPalette.Blue.b, c.b, 1e-4f, "색은 그대로");
                plate.SetFace(false, default, true);
                Assert.AreEqual(CardRushHeadCardView.DimAlpha, (plate.PlateMaterial.HasProperty("_BaseColor") ? plate.PlateMaterial.GetColor("_BaseColor") : plate.PlateMaterial.color).a, 1e-4f, "쿨다운 뒷면도 흐리게");
                Assert.IsFalse(parent.GetComponentInChildren<UnityEngine.Collider>(), "그림 전용 — 콜라이더 없음");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                materials.Dispose();
                palette.Dispose();
            }
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
        public void 카드_째깍_몸개그_장애물은_틱과_보간_뒤에_칠하고_탭은_틱_앞에서_읽는다()
        {
            var tick = SlotOf(typeof(GameFramework.Runner.ITickUpdater));
            var interpolator = SlotOf(typeof(UnityEngine.MonoBehaviour));
            foreach (var view in new[] { typeof(CardRushHeadCardView), typeof(CardRushAudio), typeof(CardRushRunnerFxView), typeof(CardRushTrackView), typeof(CardRushHazardView) })
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
            var me = rig.Registry.Get("me").Get<CardRushRunner>();
            Assert.IsTrue(CardRushCard.TryCardAt(me.State, rig.Boundary, rig.World.GameplayStartTick, me.Lane, rig.World.Track, out var judged));
            Assert.AreEqual(judged, shown.Color, "본 카드 = 쓰는 카드");
        }
    }
}
