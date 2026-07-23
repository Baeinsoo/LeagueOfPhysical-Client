using R3;
using System;
using VContainer.Unity;

namespace LOP.UI
{
    /// <summary>
    /// 매칭 흐름의 화면 전환(네비게이션) 담당. MatchMakingViewModel의 IsMatching 신호를 구독해
    /// 대기 오버레이(MatchingWaitingView)를 열고/닫고, 취소 버튼을 VM의 Cancel 커맨드에 배선한다.
    /// VM은 신호만 노출하고 화면 교체는 여기서 한다(아키텍처: 작은 흐름=VM / 큰 흐름=코디네이터).
    /// </summary>
    public class MatchMakingCoordinator : IStartable, IDisposable
    {
        private readonly IWindowManager _windowManager;
        private readonly MatchMakingViewModel _viewModel;

        private IDisposable _subscription;
        private MatchingWaitingView _waitingView;

        public MatchMakingCoordinator(IWindowManager windowManager, MatchMakingViewModel viewModel)
        {
            _windowManager = windowManager;
            _viewModel = viewModel;
        }

        public void Start()
        {
            // ReactiveProperty는 구독 즉시 현재값을 replay하므로 StartFlow 전에 구독해도 안전.
            _subscription = _viewModel.IsMatching.Subscribe(OnMatchingChanged);
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

        public void Dispose()
        {
            _subscription?.Dispose();

            if (_waitingView != null)
            {
                _windowManager.Close(_waitingView);
                _waitingView = null;
            }
        }
    }
}
