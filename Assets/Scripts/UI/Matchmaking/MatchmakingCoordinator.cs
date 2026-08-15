using R3;
using System;
using VContainer.Unity;

namespace LOP.UI
{
    /// <summary>
    /// 매칭 흐름의 화면 전환(네비게이션) 담당. MatchmakingViewModel의 IsMatching 신호를 구독해
    /// 대기 오버레이(MatchingWaitingView)를 열고/닫고, 취소 버튼을 VM의 Cancel 커맨드에 배선한다.
    /// VM은 신호만 노출하고 화면 교체는 여기서 한다(아키텍처: 작은 흐름=VM / 큰 흐름=코디네이터).
    /// </summary>
    public class MatchmakingCoordinator : IStartable, IDisposable
    {
        private readonly IWindowManager _windowManager;
        private readonly MatchmakingViewModel _viewModel;

        private IDisposable _matchingSubscription;
        private IDisposable _failedSubscription;
        private MatchingWaitingView _waitingView;
        private MatchmakingFailedView _failedView;

        public MatchmakingCoordinator(IWindowManager windowManager, MatchmakingViewModel viewModel)
        {
            _windowManager = windowManager;
            _viewModel = viewModel;
        }

        public void Start()
        {
            // ReactiveProperty는 구독 즉시 현재값을 replay하므로 StartFlow 전에 구독해도 안전.
            _matchingSubscription = _viewModel.IsMatching.Subscribe(OnMatchingChanged);
            _failedSubscription = _viewModel.MatchmakingFailed.Subscribe(_ => ShowFailed());
            _viewModel.StartFlow();
        }

        private void OnMatchingChanged(bool matching)
        {
            if (matching)
            {
                if (_waitingView == null)
                {
                    _waitingView = _windowManager.Open<MatchingWaitingView>();
                    _waitingView.SetCancelCallback(_viewModel.Cancel);
                }
            }
            else if (_waitingView != null)
            {
                _windowManager.Close(_waitingView);
                _waitingView = null;
            }
        }

        private void ShowFailed()
        {
            //  연달아 실패해도 안내는 하나만 띄운다.
            if (_failedView != null)
            {
                return;
            }

            _failedView = _windowManager.Open<MatchmakingFailedView>();
            _failedView.Confirmed += OnFailedConfirmed;
            _failedView.Closed += OnFailedViewClosed;
        }

        // 확인 버튼은 닫기 "요청"만 한다. 참조 정리는 OnFailedViewClosed에서 한다 —
        // 그래야 백드롭 클릭·Back()/ESC처럼 코디네이터를 거치지 않는 경로로 닫혀도
        // 같은 정리 로직을 탄다(안 그러면 _failedView가 죽은 View를 계속 참조해서
        // 다음 매칭 실패 때 ShowFailed의 중복 방지 가드가 계속 참이 되어버린다).
        private void OnFailedConfirmed()
        {
            _windowManager.Close(_failedView);
        }

        // Closed는 Close() 안에서 View.OnClose()가 호출하는 시점에 발화하므로,
        // 여기서 다시 _windowManager.Close(...)를 부르면 재귀가 된다 — 부르지 않는다.
        private void OnFailedViewClosed()
        {
            if (_failedView == null)
            {
                return;
            }

            var view = _failedView;
            _failedView = null;
            view.Confirmed -= OnFailedConfirmed;
            view.Closed -= OnFailedViewClosed;
        }

        public void Dispose()
        {
            _matchingSubscription?.Dispose();
            _failedSubscription?.Dispose();

            if (_failedView != null)
            {
                _windowManager.Close(_failedView);
            }

            if (_waitingView != null)
            {
                _windowManager.Close(_waitingView);
                _waitingView = null;
            }
        }
    }
}
