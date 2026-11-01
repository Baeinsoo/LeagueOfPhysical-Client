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
        public void 재접속해_시작하기_전에_판이_끝나면_로딩판이_내려간다()
        {
            //  11-01 재현: 재접속 직후 서버가 판을 끝내면 러너는 Playing 없이 바로 GameOver로 간다.
            //  위치는 아직 GameRoom이라(경기 중엔 다시 묻지 않는다) 로딩판이 결과 화면을 덮은 채 남았다.
            var loc = new FakeLocation();
            var vm = new MatchLoadingViewModel(loc);
            vm.ReleaseRunOut();
            loc.Enter();
            vm.OnGameStateChanged(GameFramework.Runner.RunnerState.Playing);
            Assert.IsFalse(vm.IsLoading.CurrentValue, "처음 판은 시작돼 있다");

            vm.NotifyGameNotLive();   //  재접속 — 다시 뜰 때까지 덮는다
            Assert.IsTrue(vm.IsLoading.CurrentValue, "재접속 중엔 덮는다");

            vm.OnGameStateChanged(GameFramework.Runner.RunnerState.GameOver);
            Assert.IsFalse(vm.IsLoading.CurrentValue, "판이 끝났으면 기다릴 것이 없다");
        }

        [Test]
        public void 판이_끝난_뒤_다시_들어가면_다시_덮는다()
        {
            //  끝남 표시가 다음 입장까지 남으면 그 판의 로딩판이 안 뜬다.
            var loc = new FakeLocation();
            var vm = new MatchLoadingViewModel(loc);
            vm.ReleaseRunOut();
            loc.Enter();
            vm.OnGameStateChanged(GameFramework.Runner.RunnerState.GameOver);
            Assert.IsFalse(vm.IsLoading.CurrentValue);

            vm.NotifyGameNotLive();
            Assert.IsTrue(vm.IsLoading.CurrentValue, "재접속이면 다시 덮는다");
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
