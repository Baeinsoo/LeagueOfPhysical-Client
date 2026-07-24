using R3;
using System;
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
        }

        private void CloseMatchResult()
        {
            if (_matchResultView != null)
            {
                _windowManager.Close(_matchResultView);
                _matchResultView = null;
            }

            // 소비했으니 비운다 — 안 비우면 로비를 오갈 때마다 다시 뜬다.
            _matchResultDataStore.Clear();
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

            if (_matchResultView != null)
            {
                _windowManager.Close(_matchResultView);
                _matchResultView = null;
            }
        }
    }
}
