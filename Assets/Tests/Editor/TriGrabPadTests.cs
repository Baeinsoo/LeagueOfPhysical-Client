using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using TG = LOP.TriGrab;

namespace LOP.Tests
{
    public class TriGrabPadTests
    {
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

        // ── 뗄 때의 판정: 바깥(14 px 이상)에서 떼면 쏘기, 그 밖(톡·되돌아옴)은 취소 ──

        [Test]
        public void Tap_Cancels()
        {
            Assert.AreEqual(Cancel, Outcome(0f), "톡은 취소(가운데 쏘기 없음)");
            Assert.AreEqual(Cancel, Outcome(13.9f));
        }

        [Test]
        public void DragOutRelease_FiresThatYaw()
        {
            Assert.AreEqual(Fire, Outcome(LOP.UI.TriGrabPadViewModel.FireDragPx));
            var rig = Build();
            //  바로 위 카메라 — 화면 오른쪽 = 동쪽(+x) = yaw 90.
            Assert.IsTrue(rig.Vm.FireDrag(new Vector2(40f, 0f)));
            rig.Vm.Apply();
            var fired = Step(rig);
            Assert.IsTrue(fired.Fire);
            Assert.AreEqual(90f, fired.AimYaw, 1e-3f);
        }

        [Test]
        public void DragBackInside_Cancels()
        {
            //  크게 끌었다가도 뗄 때 안쪽이면 취소다 — 뗄 때 거리만 본다.
            Assert.AreEqual(Fire, Outcome(80f), "양성 대조 — 바깥에서 떼면 쏜다");
            Assert.AreEqual(Cancel, Outcome(5f));
            var rig = Build();
            Assert.IsFalse(rig.Vm.TryGetAim(new Vector2(3f, 4f), 5f, out _, out _), "안쪽이면 조준선도 없다(떼면 안 나간다)");
            Assert.IsTrue(rig.Vm.TryGetAim(new Vector2(0f, 80f), 80f, out var origin, out var dir));
            Assert.AreEqual(new Vector2(0f, -7f), origin);
            Assert.Less(Vector2.Distance(new Vector2(0f, 1f), dir), 1e-4f, "화면 위 = 월드 +z");
        }

        [Test]
        public void CannotFire_WhenStunned()
        {
            Assert.IsTrue(LOP.UI.TriGrabPadViewModel.CanFire(TG.TriGrabPhase.Live, false, 300, 0, 0), "양성 대조");
            Assert.IsFalse(LOP.UI.TriGrabPadViewModel.CanFire(TG.TriGrabPhase.Live, false, 300, 301, 0), "기절");
            Assert.IsFalse(LOP.UI.TriGrabPadViewModel.CanFire(TG.TriGrabPhase.Live, false, 300, 0, 301), "손 쉼");
            Assert.IsFalse(LOP.UI.TriGrabPadViewModel.CanFire(TG.TriGrabPhase.Live, true, 300, 0, 0), "손이 나가 있다");
            Assert.IsFalse(LOP.UI.TriGrabPadViewModel.CanFire(TG.TriGrabPhase.Prep, false, 300, 0, 0), "준비 중");

            var rig = Build();
            long next = rig.Input.NextInputTick;
            var m = TriGrabClientStateTests.Msg(2);
            m.Players.Add(new TriGrabRingPlayerWire { EntityId = "me", StunUntilTick = next + 10 });
            rig.State.Apply(m);
            Assert.IsFalse(rig.Vm.Ready);
            Assert.IsFalse(rig.Vm.FireDrag(new Vector2(0f, 40f)));
            Assert.IsNull(rig.MyHook.Predicted);

            rig.Vm.Move(new Vector2(0f, 1f));
            rig.Vm.Apply();
            Assert.AreEqual(0f, Step(rig).Vertical, "기절 중엔 이동 0 — 서버도 막는다");
        }

        [Test]
        public void 쏘면_그_입력_틱에_Fire가_실리고_손이_돌아올_때까지_서_있다()
        {
            var rig = Build();
            rig.Vm.Move(new Vector2(0f, 1f));
            rig.Vm.Apply();
            var walking = Step(rig);
            Assert.Greater(walking.Vertical, 0.5f, "양성 대조 — 손이 없으면 걷는다");

            long fireTick = rig.Input.NextInputTick;
            Assert.IsTrue(rig.Vm.Ready);
            Assert.IsTrue(rig.Vm.FireDrag(new Vector2(0f, 40f)));
            Assert.AreEqual(fireTick, rig.MyHook.Predicted.FireTick, "미리 그린 손의 쏜 틱 = 쏘기가 실리는 입력 틱");
            Assert.IsFalse(rig.Vm.Ready, "나가 있는 동안은 또 못 쏜다");

            rig.Vm.Apply();
            var fired = Step(rig);
            Assert.IsTrue(fired.Fire);
            Assert.AreEqual(0f, fired.AimYaw, 1e-4f, "화면 위 = 북쪽");
            Assert.AreEqual(0f, fired.Vertical, "쏘는 틱부터 선다");

            long end = TriGrabHookPath.EndTick(rig.MyHook.Predicted);
            int held = 0;
            for (int i = 0; i < 200 && rig.Input.NextInputTick <= end; i++, held++)
            {
                rig.Vm.Apply();
                var step = Step(rig);
                Assert.IsFalse(step.Fire, "쏘기는 한 번만 실린다");
                Assert.AreEqual(0f, step.Vertical, "스틱을 밀고 있어도 손이 나가 있으면 0");
            }
            Assert.Greater(held, 30, "사거리 끝까지 갔다 오는 동안");
            rig.Vm.Apply();
            Assert.Greater(Step(rig).Vertical, 0.5f, "손이 돌아오면 다시 걷는다");
            Assert.IsFalse(rig.Vm.Ready, "돌아온 뒤 0.15초는 쉰다(서버 소식 전에도)");
            for (int i = 0; i < TG.TriGrabConfig.HookCooldownTicks; i++) Step(rig);
            Assert.IsTrue(rig.Vm.Ready);
        }

        //  서버 상태: 내 손이 카드(ok)를 끌고 돌아오는 중. 그 손이 몸에 닿는 틱을 돌려준다.
        private static long ApplyMyHookCarrying(Rig rig, bool ok, long phaseEnd = 100000)
        {
            long now = rig.Input.NextInputTick;
            var m = TriGrabClientStateTests.Msg(rig.State.Version + 1, TG.TriGrabPhase.Live, phaseEnd: phaseEnd);
            m.Players.Add(new TriGrabRingPlayerWire { EntityId = "me" });
            m.Cards.Add(new TriGrabRingCardWire { Id = 9, Ok = ok, CarriedBy = "me" });
            m.Hooks.Add(new TriGrabRingHookWire
            {
                OwnerId = "me", FireTick = now - 4, OriginX = 0f, OriginZ = -7f, DirX = 0f, DirZ = 1f,
                State = (int)TG.TriGrabHookState.Back, End = (int)TG.TriGrabHookEnd.Card, CardId = 9,
                TurnTick = now - 1, TurnLength = 2.5f,
            });
            rig.State.Apply(m);
            return TriGrabHookPath.EndTick(rig.State.HookOf("me"));
        }

        private static void StepUntilInputTick(Rig rig, long tick)
        {
            for (int i = 0; i < 200 && rig.Input.NextInputTick < tick; i++) Step(rig);
            Assert.AreEqual(tick, rig.Input.NextInputTick);
        }

        [Test]
        public void 오답_카드를_끌고_오면_서버_소식_전에도_기절로_본다()
        {
            var rig = Build();
            long end = ApplyMyHookCarrying(rig, ok: false);
            StepUntilInputTick(rig, end + 1);
            Assert.IsTrue(rig.Vm.MoveBlocked, "손이 돌아온 다음 틱 — 서버는 기절로 이동을 막는다");
            Assert.IsFalse(rig.Vm.Ready, "기절 중엔 쏘지 않는다(유령 손 없음)");
            Assert.IsFalse(rig.Vm.FireDrag(new Vector2(0f, 40f)));
            Assert.IsNull(rig.MyHook.Predicted);

            StepUntilInputTick(rig, end + TG.TriGrabConfig.StunTicks - 1);
            Assert.IsTrue(rig.Vm.MoveBlocked, "기절 마지막 틱");
            StepUntilInputTick(rig, end + TG.TriGrabConfig.StunTicks);
            Assert.IsFalse(rig.Vm.MoveBlocked, "서버 기절 끝 틱(도착 틱 + 50)부터 걷는다");
            Assert.IsTrue(rig.Vm.Ready, "기절이 풀린 틱에 바로 쏜다(손 쉼은 이미 지났다)");
        }

        [Test]
        public void 정답_카드면_기절이_아니다()
        {
            var rig = Build();
            long end = ApplyMyHookCarrying(rig, ok: true);
            StepUntilInputTick(rig, end + 1);
            Assert.IsFalse(rig.Vm.MoveBlocked);
            StepUntilInputTick(rig, end + TG.TriGrabConfig.HookCooldownTicks);
            Assert.IsTrue(rig.Vm.Ready);
        }

        [Test]
        public void 라운드가_먼저_끝나면_오답_카드를_끌던_손도_기절이_없다()
        {
            var rig = Build();
            //  잡기 시간 상한이 손 도착 전이면 서버는 손과 카드를 점수 없이 지운다(기절 없음).
            long now = rig.Input.NextInputTick;
            long end = ApplyMyHookCarrying(rig, ok: false, phaseEnd: now + 1);
            Assert.Greater(end, now + 1, "전제: 손 도착이 라운드 끝 뒤");
            StepUntilInputTick(rig, end + 1);
            Assert.IsFalse(rig.Vm.MoveBlocked);
        }

        [Test]
        public void 준비_중엔_쏘지_못한다()
        {
            var rig = Build();
            var prep = TriGrabClientStateTests.Msg(2, TG.TriGrabPhase.Prep, phaseEnd: 100000);
            prep.Players.Add(new TriGrabRingPlayerWire { EntityId = "me" });
            rig.State.Apply(prep);
            Assert.IsFalse(rig.Vm.Ready);
            Assert.IsFalse(rig.Vm.FireDrag(new Vector2(0f, 40f)));
            Assert.IsNull(rig.MyHook.Predicted);
        }

        [Test]
        public void 바로_위_카메라는_화면_위가_월드_북쪽()
        {
            var go = new GameObject("trigrab-screen-yaw");
            spawned.Add(go);
            var camera = go.AddComponent<Camera>();
            go.transform.rotation = TriGrabCameraFit.TopDown;
            Assert.AreEqual(0f, LOP.UI.TriGrabPadViewModel.ScreenYaw(camera), 1e-3f);
            var east = LOP.UI.TriGrabPadViewModel.ScreenToWorld(new Vector2(1f, 0f), 0f);
            Assert.Less(Vector2.Distance(new Vector2(1f, 0f), east), 1e-4f);
            //  비스듬한 카메라는 앞 방향으로 잰다.
            go.transform.rotation = Quaternion.Euler(56f, 90f, 0f);
            Assert.AreEqual(90f, LOP.UI.TriGrabPadViewModel.ScreenYaw(camera), 1e-3f);
        }

        [Test]
        public void 삼색_그랩_몸은_7_m_s로_걷는다()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var creator = new TriGrabCharacterCreator(new StubCreator(registry), registry);
            creator.Create(new CharacterCreationData { entityId = "e1" });
            var stats = registry.Get("e1").Get<GameFramework.World.Stats>();
            Assert.AreEqual(TG.TriGrabConfig.MoveSpeed, stats.BaseStats[(int)GameFramework.World.EntityStatType.MoveSpeed]);
            Assert.AreEqual(2f, stats.BaseStats[(int)GameFramework.World.EntityStatType.JumpPower], "다른 스탯은 그대로");
        }

        private sealed class StubCreator : ICharacterCreator
        {
            private readonly GameFramework.World.EntityRegistry registry;
            public StubCreator(GameFramework.World.EntityRegistry registry) { this.registry = registry; }
            public void Create(CharacterCreationData data)
            {
                var e = new GameFramework.World.Entity(data.entityId);
                var stats = new GameFramework.World.Stats();
                stats.BaseStats[(int)GameFramework.World.EntityStatType.MoveSpeed] = 4.5f;
                stats.BaseStats[(int)GameFramework.World.EntityStatType.JumpPower] = 2f;
                e.Add(stats);
                registry.Add(e);
            }
        }

        // ── 조작판 전체 ──

        private sealed class Rig
        {
            public PlayerInputManager Input;
            public InputBuffer Buffer;
            public FakeTickUpdater Clock;
            public TriGrabClientState State;
            public TriGrabMyHook MyHook;
            public LOP.UI.TriGrabPadViewModel Vm;
        }

        private readonly List<GameObject> spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            spawned.Clear();
        }

        private Rig Build()
        {
            var registry = new GameFramework.World.EntityRegistry();
            var me = new GameFramework.World.Entity("me");
            var buffer = new InputBuffer();
            me.Add(buffer);
            me.Add(new GameFramework.World.Transform { Position = new System.Numerics.Vector3(0f, 0f, -7f) });
            registry.Add(me);

            var clock = new FakeTickUpdater { tick = 300, elapsedTime = 300 * 0.02 };
            var runner = new FakeRunner { tickUpdater = clock };
            var context = new PlayerContext { entityId = "me", session = new FakeSession() };
            var input = new PlayerInputManager(runner, context, null, registry, new InputBufferSystem(),
                new GameFramework.Netcode.SequenceBuffer<InputCommand>(128));

            var go = new GameObject("trigrab-pad-camera");
            spawned.Add(go);
            var camera = go.AddComponent<Camera>();
            go.transform.rotation = TriGrabCameraFit.TopDown;
            var controller = go.AddComponent<CameraController>();
            typeof(CameraController).GetField("mainCamera", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, camera);

            var state = new TriGrabClientState();
            var live = TriGrabClientStateTests.Msg(1, TG.TriGrabPhase.Live, phaseEnd: 100000);
            live.Players.Add(new TriGrabRingPlayerWire { EntityId = "me" });
            state.Apply(live);
            var myHook = new TriGrabMyHook(state);
            var vm = new LOP.UI.TriGrabPadViewModel(input, controller, state, myHook, context, registry);
            return new Rig { Input = input, Buffer = buffer, Clock = clock, State = state, MyHook = myHook, Vm = vm };
        }

        private static InputCommand Step(Rig rig)
        {
            rig.Input.Tick(rig.Clock.tick, 0.02f);
            var command = rig.Buffer.Current;
            rig.Clock.tick++;
            rig.Clock.elapsedTime += 0.02;
            return command;
        }

        private const LOP.UI.TriGrabPadViewModel.ReleaseOutcome Fire = LOP.UI.TriGrabPadViewModel.ReleaseOutcome.Fire;
        private const LOP.UI.TriGrabPadViewModel.ReleaseOutcome Cancel = LOP.UI.TriGrabPadViewModel.ReleaseOutcome.Cancel;
        private static LOP.UI.TriGrabPadViewModel.ReleaseOutcome Outcome(float px) => LOP.UI.TriGrabPadViewModel.OutcomeOnRelease(px);
    }
}
