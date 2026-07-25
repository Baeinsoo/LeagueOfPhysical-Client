using R3;
using System;
using VContainer.Unity;

namespace LOP.UI
{
    /// <summary>
    /// IsLoading(파생 상태)을 보고 로딩 창을 여닫는 코디네이터.
    /// 창 하나의 수명이 씬 경계(로비 → 게임)를 넘으므로 앱(Root) 스코프가 소유한다
    /// — 씬 스코프 코디네이터는 MatchFound 때 파괴되어 창을 계속 쥘 수 없다.
    /// </summary>
    public sealed class MatchLoadingCoordinator : IStartable, IDisposable
    {
        private readonly MatchLoadingViewModel _viewModel;
        private readonly IWindowManager _windowManager;
        private readonly CompositeDisposable _disposables = new();

        private GameLoadingView _view;

        public MatchLoadingCoordinator(MatchLoadingViewModel viewModel, IWindowManager windowManager)
        {
            _viewModel = viewModel;
            _windowManager = windowManager;
        }

        public void Start()
        {
            _viewModel.IsLoading
                .Subscribe(on => { if (on) Show(); else Hide(); })
                .AddTo(_disposables);
        }

        private void Show()
        {
            if (_view == null) _view = _windowManager.Open<GameLoadingView>();
        }

        private void Hide()
        {
            if (_view != null)
            {
                _windowManager.Close(_view);
                _view = null;
            }
        }

        public void Dispose()
        {
            _disposables.Dispose();
            Hide();
        }
    }
}
