using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LOP;
using LOP.UI;
using NUnit.Framework;
using R3;

namespace LOP.Tests
{
    /// <summary>출전 지휘 — 무대에 자세를 넘기고, 하얀 빛에서 로딩판 문을 풀고, 접속과 함께 기다린다.</summary>
    public class RunOutDirectorTests
    {
        private sealed class FakeLocation : IUserLocationService
        {
            public readonly ReactiveProperty<UserLocation> Location = new(new UserLocation { location = LOP.Location.None });
            public ReadOnlyReactiveProperty<UserLocation> UserLocation => Location;
            public string TicketId => null;
            public Observable<Unit> Faulted => Observable.Empty<Unit>();
            public UniTask<bool> RefreshAsync(CancellationToken ct = default) => UniTask.FromResult(true);
            public void OnMatchmakingRequested(string ticketId) { }
        }

        private sealed class FakeStage : IRunOutStage
        {
            public int Begins, Poses, Resets;
            public RunOutPose Last;
            public void BeginRunOut() => Begins++;
            public void PoseRunOut(RunOutPose pose, float t) { Poses++; Last = pose; }
            public void ResetRunOut() => Resets++;
        }

        private sealed class FakeDirector : IRunOutDirector
        {
            public readonly UniTaskCompletionSource Play = new();
            public int Plays, Resets;
            private readonly ReactiveProperty<bool> _running = new(false);
            public ReadOnlyReactiveProperty<bool> Running => _running;
            public UniTask PlayAsync(CancellationToken ct) { Plays++; return Play.Task; }
            public void Reset() => Resets++;
        }

        private static (RunOutDirector d, MatchLoadingViewModel vm, FakeLocation loc) Make()
        {
            var loc = new FakeLocation();
            var vm = new MatchLoadingViewModel(loc);
            return (new RunOutDirector(vm), vm, loc);
        }

        [Test]
        public void 로비가_생기면_방에_들어가도_로딩판이_바로_뜨지_않는다()
        {
            var (_, vm, loc) = Make();
            loc.Location.Value = new UserLocation { location = LOP.Location.GameRoom };
            Assert.IsFalse(vm.IsLoading.CurrentValue);
        }

        [Test]
        public void 무대에_자세를_넘기고_하얀_빛에서_문을_푼다()
        {
            var (d, vm, loc) = Make();
            var stage = new FakeStage();
            d.Attach(stage);
            loc.Location.Value = new UserLocation { location = LOP.Location.GameRoom };
            Assert.IsTrue(d.Begin());
            Assert.AreEqual(1, stage.Begins);
            Assert.IsTrue(d.Running.CurrentValue);
            float t = 0f;
            while (t < RunOutTimeline.CoverStart - 0.05f) { Assert.IsFalse(d.Step(0.05f)); t += 0.05f; }
            Assert.IsFalse(vm.IsLoading.CurrentValue, "하얀 빛 전엔 로딩판이 없다");
            bool done = false;
            for (int i = 0; i < 20 && !done; i++) done = d.Step(0.05f);
            Assert.IsTrue(done);
            Assert.IsTrue(vm.IsLoading.CurrentValue);
            Assert.Greater(stage.Poses, 10);
            Assert.IsTrue(stage.Last.Done);
        }

        [Test]
        public void 겹쳐_시작하면_한_번만_돈다()
        {
            var (d, _, _) = Make();
            var stage = new FakeStage();
            d.Attach(stage);
            Assert.IsTrue(d.Begin());
            Assert.IsFalse(d.Begin());
            Assert.AreEqual(1, stage.Begins);
        }

        [Test]
        public void 무대가_없어도_시간이_지나면_끝난다()
        {
            var (d, _, _) = Make();
            d.Begin();
            bool done = false;
            for (int i = 0; i < 40 && !done; i++) done = d.Step(0.05f);
            Assert.IsTrue(done);
        }

        [Test]
        public void 되돌리면_무대를_돌려놓고_문을_다시_건다()
        {
            var (d, vm, loc) = Make();
            var stage = new FakeStage();
            d.Attach(stage);
            loc.Location.Value = new UserLocation { location = LOP.Location.GameRoom };
            d.Begin();
            for (int i = 0; i < 40; i++) d.Step(0.05f);
            Assert.IsTrue(vm.IsLoading.CurrentValue);
            d.Reset();
            Assert.AreEqual(1, stage.Resets);
            Assert.IsFalse(d.Running.CurrentValue);
            Assert.IsFalse(vm.IsLoading.CurrentValue);
        }

        [Test]
        public void 접속과_연출이_둘_다_끝나야_들어간다()
        {
            var connect = new UniTaskCompletionSource<bool>();
            var fake = new FakeDirector();
            var task = RunOutEntry.EnterAsync(connect.Task, fake, CancellationToken.None);
            connect.TrySetResult(true);
            Assert.AreEqual(UniTaskStatus.Pending, task.Status, "접속이 먼저 끝나도 연출을 기다린다");
            fake.Play.TrySetResult();
            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            Assert.IsTrue(task.GetAwaiter().GetResult());
            Assert.AreEqual(0, fake.Resets);
        }

        [Test]
        public void 접속이_실패하면_되돌리고_false()
        {
            var connect = new UniTaskCompletionSource<bool>();
            var fake = new FakeDirector();
            var task = RunOutEntry.EnterAsync(connect.Task, fake, CancellationToken.None);
            fake.Play.TrySetResult();
            connect.TrySetResult(false);
            Assert.IsFalse(task.GetAwaiter().GetResult());
            Assert.AreEqual(1, fake.Resets);
        }

        [Test]
        public void 접속이_실패하면_연출을_기다리지_않고_바로_되돌린다()
        {
            //  끝까지 달린 뒤 하얀 빛·로딩판이 뜨고 나서야 되돌아가면, 다시 시도할 때마다 깜박인다(리뷰 지적).
            var connect = new UniTaskCompletionSource<bool>();
            var fake = new FakeDirector();
            var task = RunOutEntry.EnterAsync(connect.Task, fake, CancellationToken.None);
            connect.TrySetResult(false);
            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status, "연출이 끝나길 기다리지 않는다");
            Assert.IsFalse(task.GetAwaiter().GetResult());
            Assert.AreEqual(1, fake.Resets);
        }

        [Test]
        public void 접속이_던지면_되돌리고_다시_던진다()
        {
            var connect = new UniTaskCompletionSource<bool>();
            var fake = new FakeDirector();
            var task = RunOutEntry.EnterAsync(connect.Task, fake, CancellationToken.None);
            fake.Play.TrySetResult();
            connect.TrySetException(new InvalidOperationException("boom"));
            Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult());
            Assert.AreEqual(1, fake.Resets);
        }
    }
}
