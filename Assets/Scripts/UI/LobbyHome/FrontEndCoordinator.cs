using Cysharp.Threading.Tasks;
using R3;
using System;
using System.Threading;
using VContainer.Unity;

namespace LOP.UI
{
    /// <summary>
    /// 프론트엔드 네비게이션 담당. LobbyHomeViewModel의 네비 신호를 구독해 상점/설정/프로필 셸 윈도우를 열고,
    /// 셸의 back으로 닫는다(로비 홈 위 push/pop). VM은 신호만 노출, 화면 교체는 여기서(작은 흐름=VM / 큰 흐름=코디네이터).
    /// </summary>
    public class FrontEndCoordinator : IStartable, IDisposable
    {
        private readonly IWindowManager _windowManager;
        private readonly LobbyHomeViewModel _viewModel;
        private readonly LobbyNotice _lobbyNotice;

        private IDisposable _subscription;
        private ShellView _currentShell;
        private readonly ToastSlot<KitToastView> _toast = new();
        private GameSelectView _gameSelect;
        private readonly CancellationTokenSource _cts = new();

        public FrontEndCoordinator(
            IWindowManager windowManager,
            LobbyHomeViewModel viewModel,
            LobbyNotice lobbyNotice)
        {
            _windowManager = windowManager;
            _viewModel = viewModel;
            _lobbyNotice = lobbyNotice;
        }

        public void Start()
        {
            _subscription = _viewModel.NavigationRequested.Subscribe(OnNavigationRequested);

            //  경기 씬이 맡긴 안내(예: 끊긴 사이 판이 끝남)를 로비에 들어오자마자 한 번 보여 준다.
            var notice = _lobbyNotice.Take();
            if (notice != null) ShowToast(notice);
        }

        private void OnNavigationRequested(FrontEndDestination destination)
        {
            if (FrontEndRoutes.IsComingSoon(destination))
            {
                ShowToast(FrontEndRoutes.ComingSoonText);
                return;
            }
            if (destination == FrontEndDestination.GameSelect)
            {
                OpenGameSelect();
                return;
            }

            CloseCurrentShell();

            _currentShell = destination switch
            {
                FrontEndDestination.Shop => _windowManager.Open<ShopView>(),
                FrontEndDestination.Locker => _windowManager.Open<LockerView>(),
                FrontEndDestination.Settings => _windowManager.Open<SettingsView>(),
                FrontEndDestination.Profile => _windowManager.Open<ProfileView>(),
                _ => null,
            };

            _currentShell?.SetBackCallback(CloseCurrentShell);

            //  상점↔라커룸, 선수 카드→라커룸은 셸 교체다(셸은 한 번에 하나만 열린다).
            switch (_currentShell)
            {
                case ShopView shop: shop.SetLockerCallback(() => OnNavigationRequested(FrontEndDestination.Locker)); break;
                case LockerView locker: locker.SetShopCallback(() => OnNavigationRequested(FrontEndDestination.Shop)); break;
                case ProfileView profile: profile.SetLockerCallback(() => OnNavigationRequested(FrontEndDestination.Locker)); break;
            }
        }

        private void OpenGameSelect()
        {
            if (_gameSelect != null) return;   //  두 번 눌러도 한 장만
            _gameSelect = _windowManager.Open<GameSelectView>();
            _gameSelect.SetCloseCallback(CloseGameSelect);
        }

        private void CloseGameSelect()
        {
            if (_gameSelect == null) return;
            _windowManager.Close(_gameSelect);
            _gameSelect = null;
        }

        //  토스트는 하나만 — 새 것이 오면 이전 것을 바로 닫는다.
        private void ShowToast(string text)
        {
            var view = _windowManager.Open<KitToastView>();
            view.Show(text);
            var previous = _toast.Replace(view);
            if (previous != null) _windowManager.Close(previous);
            CloseToastLater(view).Forget();
        }

        private async UniTaskVoid CloseToastLater(KitToastView view)
        {
            bool cancelled = await UniTask.Delay(1500, cancellationToken: _cts.Token).SuppressCancellationThrow();
            if (cancelled) return;   //  로비가 내려갔다 — Dispose가 이미 닫았다
            //  그 사이 새 토스트가 왔으면 Clear가 null — 새 것을 닫지 않는다.
            var expired = _toast.Clear(view);
            if (expired != null) _windowManager.Close(expired);
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
            _cts.Cancel();
            _cts.Dispose();
            var toast = _toast.Clear(_toast.Current);
            if (toast != null) _windowManager.Close(toast);
            CloseGameSelect();
            CloseCurrentShell();
        }
    }
}
