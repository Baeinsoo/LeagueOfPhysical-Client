using R3;
using System;
using VContainer.Unity;

namespace LOP.UI
{
    /// <summary>
    /// 프론트엔드 네비게이션 담당. LobbyHomeViewModel의 네비 신호를 구독해 상점/설정/프로필 셸 윈도우를 열고,
    /// 셸의 back으로 닫는다(로비 홈 위 push/pop). VM은 신호만 노출, 화면 교체는 여기서(작은 흐름=VM / 큰 흐름=코디네이터).
    /// MatchmakingCoordinator와 같은 코디네이터 패턴.
    /// </summary>
    public class FrontEndCoordinator : IStartable, IDisposable
    {
        private readonly IWindowManager _windowManager;
        private readonly LobbyHomeViewModel _viewModel;

        private IDisposable _subscription;
        private ShellView _currentShell;

        public FrontEndCoordinator(IWindowManager windowManager, LobbyHomeViewModel viewModel)
        {
            _windowManager = windowManager;
            _viewModel = viewModel;
        }

        public void Start()
        {
            _subscription = _viewModel.NavigationRequested.Subscribe(OnNavigationRequested);
        }

        private void OnNavigationRequested(FrontEndDestination destination)
        {
            CloseCurrentShell();

            _currentShell = destination switch
            {
                FrontEndDestination.Shop => _windowManager.Open<ShopView>(),
                FrontEndDestination.Settings => _windowManager.Open<SettingsView>(),
                FrontEndDestination.Profile => _windowManager.Open<ProfileView>(),
                _ => null,
            };

            _currentShell?.SetBackCallback(CloseCurrentShell);
        }

        private void CloseCurrentShell()
        {
            if (_currentShell != null)
            {
                _windowManager.Close(_currentShell);
                _currentShell = null;
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            CloseCurrentShell();
        }
    }
}
