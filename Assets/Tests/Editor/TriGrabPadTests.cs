using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

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

        // ── 놓을 때의 판정(시안: 14 px 넘게 끌려 있으면 쏨, 30 px 넘게 끌었다 돌아오면 취소, 아니면 톡) ──

        [Test]
        public void 거의_안_끌고_놓으면_톡()
        {
            Assert.AreEqual(TriGrabPadViewModel_.Tap, TriGrabPadViewModel_.Outcome(0f, 0f));
            Assert.AreEqual(TriGrabPadViewModel_.Tap, TriGrabPadViewModel_.Outcome(29f, 10f), "30 px 못 미치게 끌다 돌아와도 톡");
        }

        [Test]
        public void 끌린_채_놓으면_그_방향으로_쏜다()
        {
            Assert.AreEqual(TriGrabPadViewModel_.Fire, TriGrabPadViewModel_.Outcome(15f, 15f));
            Assert.AreEqual(TriGrabPadViewModel_.Fire, TriGrabPadViewModel_.Outcome(80f, 40f), "크게 끌었어도 놓을 때 끌려 있으면 쏜다");
        }

        [Test]
        public void 크게_끌었다_제자리로_돌아와_놓으면_취소()
        {
            Assert.AreEqual(TriGrabPadViewModel_.Cancel, TriGrabPadViewModel_.Outcome(30f, 14f));
            Assert.AreEqual(TriGrabPadViewModel_.Cancel, TriGrabPadViewModel_.Outcome(90f, 3f));
        }

        [Test]
        public void 쏘기는_Live_갈고리_없음_재장전_끝일_때만()
        {
            Assert.IsTrue(LOP.UI.TriGrabPadViewModel.CanFire(TriGrabPhase.Live, false, 100, 100), "양성 대조");
            Assert.IsFalse(LOP.UI.TriGrabPadViewModel.CanFire(TriGrabPhase.Prep, false, 100, 0));
            Assert.IsFalse(LOP.UI.TriGrabPadViewModel.CanFire(TriGrabPhase.Resolved, false, 100, 0));
            Assert.IsFalse(LOP.UI.TriGrabPadViewModel.CanFire(TriGrabPhase.Live, true, 100, 0));
            Assert.IsFalse(LOP.UI.TriGrabPadViewModel.CanFire(TriGrabPhase.Live, false, 99, 100));
        }

        [Test]
        public void 톡은_회전판_가운데를_겨눈다()
        {
            //  yaw 0 = +z. 남쪽(z −7)에 서면 북쪽(가운데)을, 동쪽(x +7)에 서면 서쪽(−90)을 본다.
            Assert.AreEqual(0f, LOP.UI.TriGrabPadViewModel.TapYaw(new Vector2(0f, -7f)).Value, 1e-4f);
            Assert.AreEqual(-90f, LOP.UI.TriGrabPadViewModel.TapYaw(new Vector2(7f, 0f)).Value, 1e-4f);
            Assert.IsNull(LOP.UI.TriGrabPadViewModel.TapYaw(Vector2.zero));
        }

        [Test]
        public void 갈고리가_나가_있으면_이동은_0()
        {
            var move = new Vector2(0.6f, 0.8f);
            Assert.AreEqual(move, LOP.UI.TriGrabPadViewModel.MoveFor(move, false), "양성 대조");
            Assert.AreEqual(Vector2.zero, LOP.UI.TriGrabPadViewModel.MoveFor(move, true));
        }

        // ── 내 갈고리: 미리 그리기 → 서버 확인 / 거절 ──

        [Test]
        public void 서버가_같은_쏜_틱의_갈고리를_보내면_서버_것으로_바꾼다()
        {
            var state = new TriGrabClientState();
            var mine = new TriGrabMyHook(state);
            var predicted = mine.Fire("me", new Vector2(0f, -7f), 0f, 300);
            Assert.AreSame(predicted, mine.Current("me", 301));
            Assert.IsTrue(mine.IsOut("me", 301));

            var m = TriGrabClientStateTests.Msg(9);
            m.Hooks.Clear();
            m.Hooks.Add(new TriGrabHookWire { OwnerId = "me", FireTick = 301, OriginZ = -7f, DirZ = 1f, TurnTick = 319, TurnLength = 9f, EndTick = 333 });
            state.Apply(m);
            var current = mine.Current("me", 305);
            Assert.AreNotSame(predicted, current);
            Assert.AreEqual(301L, current.FireTick);
            Assert.IsNull(mine.Predicted);
            Assert.IsTrue(mine.IsOut("me", 333));
            Assert.IsFalse(mine.IsOut("me", 334), "돌아온 뒤엔 나가 있지 않다");
        }

        [Test]
        public void 확인이_안_오면_기다림이_끝난_뒤_버린다()
        {
            var state = new TriGrabClientState();
            var mine = new TriGrabMyHook(state);
            mine.Fire("me", Vector2.zero, 90f, 300);
            Assert.IsNotNull(mine.Current("me", 300 + TriGrabMyHook.ConfirmWindowTicks));
            Assert.IsNull(mine.Current("me", 301 + TriGrabMyHook.ConfirmWindowTicks));
            Assert.IsFalse(mine.IsOut("me", 302 + TriGrabMyHook.ConfirmWindowTicks));
        }

        [Test]
        public void 미리_그린_갈고리는_서버와_같은_식이다()
        {
            var mine = new TriGrabMyHook(new TriGrabClientState());
            var h = mine.Fire("me", new Vector2(0f, -7f), 0f, 100);
            Assert.AreEqual(100L + TriGrabConfig.ReachTicks, h.TurnTick);
            Assert.AreEqual(TriGrabConfig.HookLength, h.TurnLength);
            Assert.AreEqual(TriGrabHook.ComputeEndTick(h.TurnTick, TriGrabConfig.HookLength, false), h.EndTick);
            Assert.AreEqual(0f, h.Dir.x, 1e-5f);
            Assert.AreEqual(1f, h.Dir.y, 1e-5f);
        }

        // ── 조작판 전체: 쏘면 다음 입력 틱에 Fire가 실리고, 갈고리가 나가 있는 동안 이동은 0 ──

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
            go.transform.rotation = Quaternion.Euler(56f, 0f, 0f);
            var controller = go.AddComponent<CameraController>();
            typeof(CameraController).GetField("mainCamera", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, camera);

            var state = new TriGrabClientState();
            var live = TriGrabClientStateTests.Msg(1, TriGrabPhase.Live);
            live.Hooks.Clear();
            live.Players.Add(new TriGrabPlayerWire { EntityId = "me", ReloadUntilTick = 0 });
            state.Apply(live);
            var myHook = new TriGrabMyHook(state);
            var vm = new LOP.UI.TriGrabPadViewModel(input, controller, state, myHook, context, registry, runner);
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

        [Test]
        public void 톡하면_다음_입력_틱에_가운데로_쏘고_갈고리가_돌아올_때까지_서_있다()
        {
            var rig = Build();
            rig.Vm.Move(new Vector2(0f, 1f));
            rig.Vm.Apply();
            var walking = Step(rig);
            Assert.Greater(walking.Vertical, 0.5f, "양성 대조 — 갈고리가 없으면 걷는다");

            long fireTick = rig.Input.NextInputTick;
            Assert.IsTrue(rig.Vm.Ready);
            Assert.IsTrue(rig.Vm.FireTap());
            Assert.AreEqual(fireTick, rig.MyHook.Predicted.FireTick, "미리 그린 갈고리의 쏜 틱 = 쏘기가 실리는 입력 틱");
            Assert.IsFalse(rig.Vm.Ready, "나가 있는 동안은 또 못 쏜다");
            Assert.IsFalse(rig.Vm.FireTap());

            rig.Vm.Apply();
            var fired = Step(rig);
            Assert.IsTrue(fired.Fire);
            Assert.AreEqual(0f, fired.AimYaw, 1e-4f, "남쪽에서 톡 = 북쪽(가운데)");
            Assert.AreEqual(0f, fired.Horizontal);
            Assert.AreEqual(0f, fired.Vertical, "쏘는 틱부터 선다");

            for (int i = 0; i < 10; i++)
            {
                rig.Vm.Apply();
                var held = Step(rig);
                Assert.IsFalse(held.Fire, "쏘기는 한 번만 실린다");
                Assert.AreEqual(0f, held.Vertical, "스틱을 밀고 있어도 갈고리가 나가 있으면 0");
            }
        }

        [Test]
        public void 준비_중엔_쏘지_못한다()
        {
            var rig = Build();
            var prep = TriGrabClientStateTests.Msg(2, TriGrabPhase.Prep, phaseEnd: 1000);
            prep.Hooks.Clear();
            rig.State.Apply(prep);
            Assert.IsFalse(rig.Vm.Ready);
            Assert.IsFalse(rig.Vm.FireTap());
            Assert.IsNull(rig.MyHook.Predicted);
        }

        [Test]
        public void 재장전_중엔_쏘지_못한다()
        {
            var rig = Build();
            var m = TriGrabClientStateTests.Msg(2, TriGrabPhase.Live);
            m.Hooks.Clear();
            m.Players.Add(new TriGrabPlayerWire { EntityId = "me", ReloadUntilTick = 400 });
            rig.State.Apply(m);
            Assert.IsFalse(rig.Vm.FireTap());
        }

        [Test]
        public void 끌어_쏘기는_화면_방향을_카메라_기준_월드_방향으로()
        {
            var rig = Build();
            //  카메라가 북쪽(+z)을 보니 화면 오른쪽 = 동쪽(+x) = yaw 90.
            Assert.IsTrue(rig.Vm.FireDrag(new Vector2(40f, 0f)));
            rig.Vm.Apply();
            var fired = Step(rig);
            Assert.IsTrue(fired.Fire);
            Assert.AreEqual(90f, fired.AimYaw, 1e-3f);
        }
    }

    //  판정 결과를 짧게 쓰려는 별칭.
    internal static class TriGrabPadViewModel_
    {
        public const LOP.UI.TriGrabPadViewModel.ReleaseOutcome Tap = LOP.UI.TriGrabPadViewModel.ReleaseOutcome.Tap;
        public const LOP.UI.TriGrabPadViewModel.ReleaseOutcome Fire = LOP.UI.TriGrabPadViewModel.ReleaseOutcome.Fire;
        public const LOP.UI.TriGrabPadViewModel.ReleaseOutcome Cancel = LOP.UI.TriGrabPadViewModel.ReleaseOutcome.Cancel;
        public static LOP.UI.TriGrabPadViewModel.ReleaseOutcome Outcome(float maxPx, float px)
            => LOP.UI.TriGrabPadViewModel.OutcomeOnRelease(maxPx, px);
    }
}
