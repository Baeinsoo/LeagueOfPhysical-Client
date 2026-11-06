using System.Collections.Generic;
using GameFramework.World;
using LOP.Event.Entity;
using NUnit.Framework;

namespace LOP.Tests
{
    /// <summary>
    /// 카드 러시 화면 흐름 — 조작판은 <b>서버가 완주를 확정한 뒤</b>(등수가 온 뒤) 닫는다(최종 리뷰 I2).
    /// 예측한 완주에 닫으면, 서버가 마지막 탭을 버리고 보정으로 완주가 풀렸을 때 다시 누를 길이 없다.
    /// </summary>
    public class CardRushHudCoordinatorTests
    {
        private sealed class FakeGameDataStore : IGameDataStore
        {
            public GameInfo gameInfo { get; set; }
            public string userEntityId { get; set; }
            public string formerEntityId { get; set; }
            public void Clear() { }
        }

        //  열린 뷰는 진짜 화면 없이 껍데기만 만든다 — 코디네이터는 참조를 들고 닫기만 한다.
        private sealed class RecordingWindowManager : LOP.UI.IWindowManager
        {
            public readonly List<LOP.UI.UIView> Opened = new List<LOP.UI.UIView>();
            public readonly List<LOP.UI.UIView> Closed = new List<LOP.UI.UIView>();

            public T Open<T>() where T : LOP.UI.UIView
            {
                var view = (T)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(T));
                Opened.Add(view);
                return view;
            }

            public System.IDisposable RegisterViewFactory<T>(System.Func<T> factory) where T : LOP.UI.UIView => null;
            public void Close(LOP.UI.UIView view) => Closed.Add(view);
            public bool Back() => false;
            public Cysharp.Threading.Tasks.UniTask<TResult> OpenModalAsync<TView, TResult>()
                where TView : LOP.UI.UIView, LOP.UI.IResultView<TResult> => default;
        }

        private sealed class CapturingSubscriber<T> : MessagePipe.ISubscriber<T>
        {
            private readonly List<MessagePipe.IMessageHandler<T>> handlers = new List<MessagePipe.IMessageHandler<T>>();

            public System.IDisposable Subscribe(MessagePipe.IMessageHandler<T> handler, params MessagePipe.MessageHandlerFilter<T>[] filters)
            {
                handlers.Add(handler);
                return new Noop();
            }

            public void Publish(T message)
            {
                foreach (var h in handlers) h.Handle(message);
            }

            private sealed class Noop : System.IDisposable
            {
                public void Dispose() { }
            }
        }

        private sealed class NoSpectate : ISpectateTargets
        {
            public IReadOnlyList<string> Candidates => System.Array.Empty<string>();
            public string Current => null;
            public void Refresh() { }
            public void Next() { }
            public void Prev() { }
        }

        private RecordingWindowManager windows;
        private Entity me;
        private CardRushHudCoordinator coordinator;

        [SetUp]
        public void SetUp()
        {
            var registry = new EntityRegistry();
            me = new Entity("me");
            me.Add(new FinishState());
            me.Add(new FinishPlacement());
            registry.Add(me);

            windows = new RecordingWindowManager();
            var created = new CapturingSubscriber<EntityCreated>();
            coordinator = new CardRushHudCoordinator(new FakeGameDataStore { userEntityId = "me" }, windows, registry,
                created, new CapturingSubscriber<MatchEndedToC>(), new NoSpectate(), new AppStateMachine(null, null),
                new MatchLeaveRequester(new CapturingSubscriber<LeaveMatchToC>(), _ => { }));
            ((VContainer.Unity.IInitializable)coordinator).Initialize();
            created.Publish(new EntityCreated("me"));
        }

        [TearDown]
        public void TearDown() => coordinator.Dispose();

        private LOP.UI.CardRushPadView Pad()
        {
            foreach (var v in windows.Opened)
            {
                if (v is LOP.UI.CardRushPadView pad) return pad;
            }
            Assert.Fail("조작판이 안 열렸다");
            return null;
        }

        //  GameInfo(내 몸 id)가 오기 전에도 틱은 돈다 — 그때 null id로 레지스트리를 찾으면 매 틱 예외가 난다(Task 14 실제 판).
        [Test]
        public void 내_몸_id가_오기_전_틱은_조용히_넘어간다()
        {
            var early = new CardRushHudCoordinator(new FakeGameDataStore { userEntityId = null }, new RecordingWindowManager(),
                new EntityRegistry(), new CapturingSubscriber<EntityCreated>(), new CapturingSubscriber<MatchEndedToC>(),
                new NoSpectate(), new AppStateMachine(null, null),
                new MatchLeaveRequester(new CapturingSubscriber<LeaveMatchToC>(), _ => { }));
            ((VContainer.Unity.IInitializable)early).Initialize();
            try
            {
                Assert.DoesNotThrow(() => early.Tick());
            }
            finally
            {
                early.Dispose();
            }
        }

        [Test]
        public void 예측_완주만으로는_조작판을_닫지_않는다()
        {
            var pad = Pad();

            //  내 시뮬이 마지막 칸을 맞혔다고 예측했다 — 서버 등수는 아직 없다.
            me.Get<FinishState>().FinishedTick = 4000;
            coordinator.Tick();
            Assert.IsFalse(windows.Closed.Contains(pad), "등수(서버 확정) 없이 닫으면 보정으로 풀렸을 때 다시 못 누른다");

            //  서버가 그 탭을 버렸다 — 보정이 완주를 되돌린다. 조작판은 그대로 열려 있어야 한다.
            me.Get<FinishState>().FinishedTick = FinishState.NotFinished;
            coordinator.Tick();
            Assert.IsFalse(windows.Closed.Contains(pad));

            //  다시 눌러 서버가 완주를 확정했다(등수가 온다) — 이제 닫고 완주·관전으로 간다.
            me.Get<FinishState>().FinishedTick = 4012;
            me.Get<FinishPlacement>().Value = 2;
            coordinator.Tick();
            Assert.IsTrue(windows.Closed.Contains(pad), "서버가 확정하면 닫는다");
            Assert.IsTrue(windows.Opened.Exists(v => v is LOP.UI.RaceFinishView), "완주 화면");
        }

        [Test]
        public void ShouldEnterFinish는_판이_안_끝났고_조작판이_있고_내가_완주했을_때만()
        {
            Assert.IsTrue(CardRushHudCoordinator.ShouldEnterFinish(matchEnded: false, padOpen: true, mineFinished: true));
            Assert.IsFalse(CardRushHudCoordinator.ShouldEnterFinish(matchEnded: true, padOpen: true, mineFinished: true), "결과 화면 위에 겹치지 않는다");
            Assert.IsFalse(CardRushHudCoordinator.ShouldEnterFinish(matchEnded: false, padOpen: false, mineFinished: true), "이미 넘어갔다");
            Assert.IsFalse(CardRushHudCoordinator.ShouldEnterFinish(matchEnded: false, padOpen: true, mineFinished: false));
        }
    }
}
