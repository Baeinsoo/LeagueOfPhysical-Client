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
    /// 직전 매치 결과가 남아 있으면 로비 진입 직후 결과 화면도 한 번 띄운다.
    /// </summary>
    public class FrontEndCoordinator : IStartable, IDisposable
    {
        private readonly IWindowManager _windowManager;
        private readonly LobbyHomeViewModel _viewModel;
        private readonly IMatchResultDataStore _matchResultDataStore;

        private IDisposable _subscription;
        private ShellView _currentShell;
        private MatchResultView _matchResultView;
        private readonly ToastSlot<KitToastView> _toast = new();
        private GameSelectView _gameSelect;
        private readonly CancellationTokenSource _cts = new();

        public FrontEndCoordinator(
            IWindowManager windowManager,
            LobbyHomeViewModel viewModel,
            IMatchResultDataStore matchResultDataStore)
        {
            _windowManager = windowManager;
            _viewModel = viewModel;
            _matchResultDataStore = matchResultDataStore;
        }

        public void Start()
        {
            _subscription = _viewModel.NavigationRequested.Subscribe(OnNavigationRequested);

            ShowPendingMatchResult();
        }

        // 직전 매치 결과가 있으면 로비 진입 직후 한 번 보여준다.
        // VContainer는 IStartable을 플레이어 루프로 돌리므로 이 시점엔 LobbyLifetimeScope의 빌드 콜백이 이미 끝나 있다
        // (= 뷰 팩토리 등록·로비 홈 오픈 완료). 이 로직을 IInitializable 같은 더 이른 훅으로 옮기면
        // 팩토리 미등록 resolve 실패 또는 로비 홈이 결과 창 위로 올라오는 z-순서 역전이 난다.
        private void ShowPendingMatchResult()
        {
            if (_matchResultDataStore.result == null)
            {
                return;
            }

            _matchResultView = _windowManager.Open<MatchResultView>();
            _matchResultView.SetConfirmCallback(CloseMatchResult);

            // 띄우는 순간 소비한다. ViewModel이 Open 안에서 값을 이미 복사해 갔으므로 화면에는 영향이 없다.
            // [확인]을 누를 때 비우면, 그 전에 창이 닫히는 경로(로비 스코프가 내려가는 등)에서 결과가
            // 남아 다음 판 결과 화면에 지난 판 등수와 점수가 뜬다.
            _matchResultDataStore.Clear();
        }

        private void CloseMatchResult()
        {
            if (_matchResultView != null)
            {
                _windowManager.Close(_matchResultView);
                _matchResultView = null;
            }
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
                FrontEndDestination.Settings => _windowManager.Open<SettingsView>(),
                FrontEndDestination.Profile => _windowManager.Open<ProfileView>(),
                _ => null,
            };

            _currentShell?.SetBackCallback(CloseCurrentShell);
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

            if (_matchResultView != null)
            {
                _windowManager.Close(_matchResultView);
                _matchResultView = null;
            }
        }
    }
}
