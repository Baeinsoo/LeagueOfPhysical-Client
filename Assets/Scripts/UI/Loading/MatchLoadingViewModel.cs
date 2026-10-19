using R3;
using System;

namespace LOP.UI
{
    /// <summary>
    /// 매치 진입 로딩 화면의 표시 여부를 계산하는 VM.
    /// 백엔드 유저 위치(GameRoom 여부)와 게임 라이브 사실을 조합해 IsLoading을 파생한다.
    /// 어떤 View도 직접 바인딩하지 않고, MatchLoadingCoordinator가 구독해 창을 여닫는다.
    /// </summary>
    public sealed class MatchLoadingViewModel : IDisposable
    {
        private readonly ReactiveProperty<bool> _isLoading = new(false);
        //  처음부터 걸려 있다 — 재접속이면 입구 씬에서 이미 방에 있는데, 방 입장은 늘 로비의 출전 연출이 풀어 준다.
        //  (로비가 내려가면 연출 지휘가 풀고, 다음 로비가 생기면 다시 건다.)
        private bool _holdForRunOut = true;
        private readonly IDisposable _subscription;

        private bool _inGameRoom;   // 위치 관찰 결과
        private bool _gameLive;     // 게임 씬이 보고한 사실

        /// <summary>로딩 창을 여닫는 근거. 코디네이터가 구독한다.</summary>
        public ReadOnlyReactiveProperty<bool> IsLoading => _isLoading;

        public MatchLoadingViewModel(IUserLocationService userLocationService)
        {
            _subscription = userLocationService.UserLocation.Subscribe(OnUserLocation);
        }

        private void OnUserLocation(UserLocation userLocation)
        {
            _inGameRoom = userLocation.location == Location.GameRoom;
            // 룸을 벗어나면(연결 실패로 로비 복귀 등) 다음 매치를 위해 gameLive를 리셋한다.
            if (!_inGameRoom) _gameLive = false;
            Recompute();
        }

        /// <summary>게임 씬이 "게임이 실제로 시작됨"을 사실로 보고한다.</summary>
        public void NotifyGameLive()
        {
            _gameLive = true;
            Recompute();
        }

        /// <summary>같은 판에 다시 들어간다 — 게임이 다시 뜰 때까지 로딩판을 띄운다.</summary>
        public void NotifyGameNotLive()
        {
            _gameLive = false;
            Recompute();
        }

        /// <summary>출전 연출이 덮을 동안 로딩판을 미룬다. 로비가 생길 때 걸고, 연출이 하얀 빛에 닿으면 푼다.</summary>
        public void HoldForRunOut() { _holdForRunOut = true; Recompute(); }

        public void ReleaseRunOut() { _holdForRunOut = false; Recompute(); }

        private void Recompute() => _isLoading.Value = _inGameRoom && !_gameLive && !_holdForRunOut;

        public void Dispose()
        {
            _subscription.Dispose();
            _isLoading.Dispose();
        }
    }
}
