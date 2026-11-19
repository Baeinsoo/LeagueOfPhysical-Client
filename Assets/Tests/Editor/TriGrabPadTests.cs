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
            Assert.IsFalse(LOP.UI.TriGrabPadViewModel.CanFire(TriGrabPhase.Live, false, 100, 100, doneThisRound: true), "이번 라운드 정답을 냈다");
        }

        [Test]
        public void 쏘기_버튼_글은_정답을_냈으면_완료()
        {
            Assert.AreEqual("완료", LOP.UI.TriGrabPadViewModel.FireLabelFor(true));
            Assert.AreEqual("갈고리", LOP.UI.TriGrabPadViewModel.FireLabelFor(false));
        }

        [Test]
        public void 남이_정답을_내도_미리_그린_내_갈고리는_그대로()
        {
            var state = new TriGrabClientState();
            state.Apply(TriGrabClientStateTests.Msg(1));   // 라운드 2, Live
            var mine = new TriGrabMyHook(state);
            var predicted = mine.Fire("me", Vector2.zero, 90f, 300);

            //  남(b)이 1등을 했다 — 2차부터 라운드는 안 끝나고 회전판도 안 멈춘다.
            var other = TriGrabClientStateTests.Msg(2);
            other.Hooks.Clear();
            other.Players.Add(new TriGrabPlayerWire { EntityId = "me" });
            state.Apply(other);
            Assert.IsTrue(state.DoneThisRound("b"), "양성 대조 — 남이 정답을 냈다");
            Assert.AreSame(predicted, mine.Current("me", 302));
            Assert.IsTrue(mine.IsOut("me", 302));
        }

        [Test]
        public void 내가_이미_정답을_냈으면_확인_안_된_미리_그린_갈고리는_버린다()
        {
            var state = new TriGrabClientState();
            state.Apply(TriGrabClientStateTests.Msg(1));
            var mine = new TriGrabMyHook(state);
            mine.Fire("me", Vector2.zero, 90f, 300);
            var done = TriGrabClientStateTests.Msg(2);
            done.Hooks.Clear();
            done.Players.Add(new TriGrabPlayerWire { EntityId = "me", Score = 9, RoundPlace = 2, RoundPoints = 9 });
            state.Apply(done);
            Assert.IsFalse(mine.IsOut("me", 302));
            Assert.IsNull(mine.Predicted);
        }

        [Test]
        public void 정답을_낸_내_갈고리는_같은_상태의_서버_갈고리로_확인된다()
        {
            var state = new TriGrabClientState();
            state.Apply(TriGrabClientStateTests.Msg(1));
            var mine = new TriGrabMyHook(state);
            mine.Fire("me", Vector2.zero, 90f, 300);
            var done = TriGrabClientStateTests.Msg(2);
            done.Hooks.Clear();
            done.Hooks.Add(new TriGrabHookWire { OwnerId = "me", FireTick = 300, DirX = 1f, TurnTick = 306, TurnLength = 3f,
                Result = (int)TriGrabHookResult.Correct, EndTick = 314, Carried = new TriGrabCardWire { Id = 5 } });
            done.Players.Add(new TriGrabPlayerWire { EntityId = "me", Score = 10, RoundPlace = 1, RoundPoints = 10 });
            state.Apply(done);
            var current = mine.Current("me", 305);
            Assert.IsNotNull(current, "끌어오는 갈고리는 그린다");
            Assert.AreEqual(TriGrabHookResult.Correct, current.Result);
            Assert.IsNull(mine.Predicted);
        }

        [Test]
        public void 시간_상한_틱_뒤에_쏜_미리_그린_갈고리는_버린다()
        {
            var state = new TriGrabClientState();
            var m = TriGrabClientStateTests.Msg(1);
            m.RoundLimitTick = 310;
            state.Apply(m);
            var early = new TriGrabMyHook(state);
            early.Fire("me", Vector2.zero, 90f, 300);
            Assert.IsTrue(early.IsOut("me", 301), "양성 대조 — 상한 전에 쏨");
            var late = new TriGrabMyHook(state);
            late.Fire("me", Vector2.zero, 90f, 310);
            Assert.IsFalse(late.IsOut("me", 311));
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
        public void 미리_그린_갈고리는_그림이_끝나면_발을_묶지_않는다()
        {
            var mine = new TriGrabMyHook(new TriGrabClientState());
            var h = mine.Fire("me", Vector2.zero, 90f, 300);
            Assert.IsTrue(mine.IsOut("me", h.EndTick), "양성 대조 — 끝 틱까지는 나가 있다");
            Assert.IsFalse(mine.IsOut("me", h.EndTick + 1), "그림이 사라진 뒤엔 풀린다");
        }

        [Test]
        public void 서버가_멈춘_뒤의_쏘기와_새_라운드는_미리_그린_갈고리를_버린다()
        {
            var state = new TriGrabClientState();
            state.Apply(TriGrabClientStateTests.Msg(1));   // 라운드 2, Live
            var mine = new TriGrabMyHook(state);
            mine.Fire("me", Vector2.zero, 90f, 300);
            Assert.IsTrue(mine.IsOut("me", 301), "양성 대조");

            var resolved = TriGrabClientStateTests.Msg(2, TriGrabPhase.Resolved);
            resolved.Hooks.Clear();
            resolved.FreezeTick = 290;   // 내가 쏜 틱(300)보다 앞서 멈췄다
            state.Apply(resolved);
            Assert.IsFalse(mine.IsOut("me", 301));
            Assert.IsNull(mine.Predicted);

            var mine2 = new TriGrabMyHook(state);
            mine2.Fire("me", Vector2.zero, 90f, 300);
            var next = TriGrabClientStateTests.Msg(3);
            next.Round = 3;
            next.Hooks.Clear();
            state.Apply(next);
            Assert.IsFalse(mine2.IsOut("me", 301), "새 라운드");
            Assert.IsNull(mine2.Predicted);
        }

        [Test]
        public void 판_시작_전에는_주문_띠에_쓸_주문이_없다()
        {
            var state = new TriGrabClientState();
            var m = TriGrabClientStateTests.Msg(1, TriGrabPhase.Waiting);
            m.Round = 0;
            state.Apply(m);
            Assert.AreEqual("", LOP.UI.TriGrabHudViewModel.RuleTextFor(state));
            m = TriGrabClientStateTests.Msg(2, TriGrabPhase.Prep);
            m.Round = 1;
            state.Apply(m);
            Assert.AreNotEqual("", LOP.UI.TriGrabHudViewModel.RuleTextFor(state));
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
        public void 이번_라운드에_정답을_냈으면_쏘지_못하고_완료지만_걷기는_된다()
        {
            var rig = Build();
            Assert.IsTrue(rig.Vm.Ready, "양성 대조");
            Assert.IsFalse(rig.Vm.Done);
            var m = TriGrabClientStateTests.Msg(2, TriGrabPhase.Live);
            m.Hooks.Clear();
            m.Players.Add(new TriGrabPlayerWire { EntityId = "me", Score = 10, RoundPlace = 1, RoundPoints = 10 });
            rig.State.Apply(m);
            Assert.IsTrue(rig.Vm.Done);
            Assert.IsFalse(rig.Vm.Ready);
            Assert.IsFalse(rig.Vm.FireTap());
            Assert.IsNull(rig.MyHook.Predicted);
            rig.Vm.Move(new Vector2(0f, 1f));
            rig.Vm.Apply();
            Assert.Greater(Step(rig).Vertical, 0.5f, "정답을 낸 뒤에도 로봇은 움직인다");
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
