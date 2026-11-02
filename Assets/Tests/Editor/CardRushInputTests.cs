using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GameFramework.World;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 잡기 입력 — 누른 순간 한 틱에만 실린다. 다음 틱까지 남으면 한 번 누른 게 두 번 잡기가 되어
    /// 틀림(멈춤) 뒤 첫 틱에 공짜 탭이 생긴다. 표시 틱(다음 입력이 달 틱)도 여기서 나온다.
    /// </summary>
    public class CardRushInputTests
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
            public readonly List<GameFramework.IMessage> Sent = new List<GameFramework.IMessage>();
            public string sessionId => "s";
            public string userId => "u";
            public bool isConnected => true;
            public void Send<T>(T message, bool reliable = true) where T : GameFramework.IMessage => Sent.Add(message);
            public GameFramework.IMessage Receive() => null;
            public void Dispose() { }
        }

        private FakeTickUpdater clock;
        private FakeSession session;
        private GameFramework.Netcode.SequenceBuffer<InputCommand> history;
        private PlayerInputManager input;

        [SetUp]
        public void SetUp()
        {
            var registry = new EntityRegistry();
            var me = new Entity("me");
            me.Add(new InputBuffer());
            registry.Add(me);

            clock = new FakeTickUpdater();
            session = new FakeSession();
            history = new GameFramework.Netcode.SequenceBuffer<InputCommand>(128);
            input = new PlayerInputManager(new FakeRunner { tickUpdater = clock },
                new PlayerContext { entityId = "me", session = session }, null, registry, new InputBufferSystem(), history);
        }

        //  러너는 틱을 처리한 뒤 tick을 하나 올린다 — 실제 순서대로 흉내 낸다.
        private void RunTick(long t)
        {
            clock.tick = t;
            input.Tick(t, 0.02f);
            clock.tick = t + 1;
        }

        private global::InputCommand SentFor(long t)
            => ((InputCommandToS)session.Sent.Last()).RecentInputs.Single(e => e.Tick == t).InputCommand;

        [Test]
        public void SetGrab은_한_틱만_실린다()
        {
            input.SetGrab();
            RunTick(500);
            RunTick(501);

            Assert.IsTrue(history.TryGet(500, out var first) && first.Grab, "누른 틱의 명령에 잡기가 있다");
            Assert.IsTrue(history.TryGet(501, out var second) && second.Grab == false, "다음 틱엔 없다");
            Assert.IsTrue(SentFor(500).Grab, "서버로 가는 명령에도 실린다");
            Assert.IsFalse(SentFor(501).Grab);
        }

        //  최종 리뷰 I5(+터치): 한 프레임 = [VContainer ITickable(탭·Space 읽기)] → [틱 코루틴] → [패널(카드 그림·PointerDown)].
        //  지난 프레임 카드가 보여 준 틱(NextInputTick)에 탭이 실려야 한다.
        private static readonly UnityEngine.Vector2 OnPad = new UnityEngine.Vector2(100f, 100f);
        private static readonly UnityEngine.Vector2 OnButton = new UnityEngine.Vector2(900f, 900f);

        private bool keyThisFrame;
        private readonly List<UnityEngine.Vector2> pressesThisFrame = new List<UnityEngine.Vector2>();

        //  조작판 면은 OnButton 자리만 빼고 화면 전체(위에 나가기 버튼이 겹쳐 있다).
        private CardRushTapGrab MakeGrab(CardRushTapTarget target = null)
        {
            target ??= new CardRushTapTarget { HitTest = p => p != OnButton };
            return new CardRushTapGrab(input, target, () => keyThisFrame, into => into.AddRange(pressesThisFrame));
        }

        //  한 프레임: 장치 상태를 정하고 → ITickable → 그 프레임에 도는 틱들.
        private void Frame(CardRushTapGrab grab, bool key, UnityEngine.Vector2[] presses, params long[] ticks)
        {
            keyThisFrame = key;
            pressesThisFrame.Clear();
            pressesThisFrame.AddRange(presses);
            grab.Tick();
            foreach (var t in ticks) RunTick(t);
        }

        private bool GrabAt(long t) => history.TryGet(t, out var c) && c != null && c.Grab;

        [Test]
        public void 스페이스는_카드가_보여_준_틱에_실린다()
        {
            clock.tick = 500;
            long shown = input.NextInputTick;   //  지난 프레임 끝 패널 스케줄러가 이 틱의 색을 그렸다
            var grab = MakeGrab();

            Frame(grab, key: true, new UnityEngine.Vector2[0], 500);

            Assert.IsTrue(GrabAt(shown), "본 틱에 잡기가 실린다");
        }

        [Test]
        public void 탭은_카드가_보여_준_틱에_실린다()
        {
            clock.tick = 500;
            long shown = input.NextInputTick;
            var grab = MakeGrab();

            Frame(grab, key: false, new[] { OnPad }, 500);

            Assert.IsTrue(GrabAt(shown), "본 틱에 잡기가 실린다(패널 PointerDown이면 501)");
        }

        //  손가락을 대고 있는 동안(다음 프레임들) 다시 세지 않고, 따라잡기 프레임에 틱이 여럿 돌아도 한 번뿐이다.
        [Test]
        public void 한_번_탭은_잡기_한_번()
        {
            clock.tick = 500;
            var grab = MakeGrab();

            Frame(grab, key: false, new[] { OnPad }, 500, 501, 502);   //  닿은 프레임(따라잡기 3틱)
            Frame(grab, key: false, new UnityEngine.Vector2[0], 503);   //  누르고 있는 프레임 — wasPressedThisFrame 거짓
            Frame(grab, key: false, new UnityEngine.Vector2[0]);        //  틱 없는 프레임
            Frame(grab, key: false, new UnityEngine.Vector2[0], 504);

            int grabs = 0;
            for (long t = 500; t <= 504; t++) if (GrabAt(t)) grabs++;
            Assert.AreEqual(1, grabs);
            Assert.IsTrue(GrabAt(500));
        }

        [Test]
        public void 두_손가락_같은_프레임도_잡기_한_번()
        {
            clock.tick = 500;
            var grab = MakeGrab();

            Frame(grab, key: true, new[] { OnPad, OnPad }, 500, 501);

            Assert.IsTrue(GrabAt(500));
            Assert.IsFalse(GrabAt(501));
        }

        [Test]
        public void 겹친_버튼_위_탭과_조작판이_없을_때_탭은_잡기가_아니다()
        {
            clock.tick = 500;
            Frame(MakeGrab(), key: false, new[] { OnButton }, 500);
            Frame(MakeGrab(new CardRushTapTarget()), key: false, new[] { OnPad }, 501);   //  조작판 닫힘(HitTest 없음)

            Assert.IsFalse(GrabAt(500), "나가기 버튼 위 탭");
            Assert.IsFalse(GrabAt(501), "조작판이 없다");
        }

        //  위 시험의 전제 — Unity 기본 플레이어 루프에서 Update(코루틴 포함)가 UI Toolkit 패널 갱신(스케줄러)보다 앞이고,
        //  코루틴(ScriptRunDelayedDynamicFrameRate)이 ScriptRunBehaviourUpdate(그 앞에 VContainer가 ITickable을 끼운다) 뒤다.
        [Test]
        public void 플레이어_루프는_업데이트_코루틴_패널_순서()
        {
            var loop = UnityEngine.LowLevel.PlayerLoop.GetDefaultPlayerLoop();
            int Phase(System.Type sub)
            {
                for (int i = 0; i < loop.subSystemList.Length; i++)
                {
                    var list = loop.subSystemList[i].subSystemList;
                    if (list != null && list.Any(s => s.type == sub)) return i;
                }
                return -1;
            }
            int Index(System.Type phase, System.Type sub)
                => System.Array.FindIndex(loop.subSystemList.Single(p => p.type == phase).subSystemList, s => s.type == sub);

            var update = typeof(UnityEngine.PlayerLoop.Update);
            Assert.Less(Index(update, typeof(UnityEngine.PlayerLoop.Update.ScriptRunBehaviourUpdate)),
                        Index(update, typeof(UnityEngine.PlayerLoop.Update.ScriptRunDelayedDynamicFrameRate)));
            Assert.Less(Phase(typeof(UnityEngine.PlayerLoop.Update.ScriptRunDelayedDynamicFrameRate)),
                        Phase(typeof(UnityEngine.PlayerLoop.PreLateUpdate.UIElementsUpdatePanels)));
        }

        [Test]
        public void 다음_입력_틱은_방금_단_틱_다음()
        {
            clock.tick = 700;
            Assert.AreEqual(700, input.NextInputTick, "아직 한 틱도 안 돌았으면 러너가 다음에 돌릴 틱");

            RunTick(700);
            Assert.AreEqual(701, input.NextInputTick);

            //  틱 안에서(러너가 tick을 올리기 전) 물어도 방금 단 틱이 아니라 다음 틱이다.
            clock.tick = 701;
            input.Tick(701, 0.02f);
            Assert.AreEqual(702, input.NextInputTick);
        }
    }
}
