using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LOP;
using LOP.UI;
using NUnit.Framework;
using R3;

namespace LOP.Tests
{
    /// <summary>로딩판 문 — 출전 연출이 걸어 둔 동안은 방에 들어가도 뜨지 않는다.</summary>
    public class MatchLoadingGateTests
    {
        private sealed class FakeLocation : IUserLocationService
        {
            public readonly ReactiveProperty<UserLocation> Location = new(new UserLocation { location = LOP.Location.None });
            public ReadOnlyReactiveProperty<UserLocation> UserLocation => Location;
            public string TicketId => null;
            public Observable<Unit> Faulted => Observable.Empty<Unit>();
            public UniTask<bool> RefreshAsync(CancellationToken ct = default) => UniTask.FromResult(true);
            public void OnMatchmakingRequested(string ticketId) { }
            public void Enter() => Location.Value = new UserLocation { location = LOP.Location.GameRoom };
        }

        [Test]
        public void 걸어_둔_동안은_방에_들어가도_뜨지_않고_풀면_뜬다()
        {
            var loc = new FakeLocation();
            var vm = new MatchLoadingViewModel(loc);
            vm.HoldForRunOut();
            loc.Enter();
            Assert.IsFalse(vm.IsLoading.CurrentValue);
            vm.ReleaseRunOut();
            Assert.IsTrue(vm.IsLoading.CurrentValue);
            vm.HoldForRunOut();
            Assert.IsFalse(vm.IsLoading.CurrentValue, "되돌리면 다시 걸린다");
        }

        [Test]
        public void 앱이_켜질_땐_걸려_있다()
        {
            //  재접속이면 입구 씬에서 이미 방에 있다 — 로비가 연출로 덮기 전에 로딩판이 먼저 번쩍이면 안 된다(리뷰 지적).
            var loc = new FakeLocation();
            var vm = new MatchLoadingViewModel(loc);
            loc.Enter();
            Assert.IsFalse(vm.IsLoading.CurrentValue);
            vm.ReleaseRunOut();
            Assert.IsTrue(vm.IsLoading.CurrentValue);
        }
    }
}
