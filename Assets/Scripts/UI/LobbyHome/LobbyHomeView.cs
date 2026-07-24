using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 로비 홈 허브 View(프론트엔드 베이스). Play는 매칭 커맨드, 네비바 버튼은 LobbyHomeViewModel 네비 커맨드로
    /// 전달하는 얇은 바인더. 매칭 흐름·대기 오버레이는 MatchmakingCoordinator, 네비 화면 교체는 FrontEndCoordinator가 담당.
    /// </summary>
    public class LobbyHomeView : UIView
    {
        private readonly MatchmakingViewModel _matchmaking;
        private readonly LobbyHomeViewModel _viewModel;

        private Button _playButton;
        private Button _shopButton;
        private Button _settingsButton;
        private Button _profileButton;

        public LobbyHomeView(MatchmakingViewModel matchmaking, LobbyHomeViewModel viewModel)
        {
            _matchmaking = matchmaking;
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _playButton = Root.Q<Button>("play-button");
            _shopButton = Root.Q<Button>("nav-shop");
            _settingsButton = Root.Q<Button>("nav-settings");
            _profileButton = Root.Q<Button>("nav-profile");

            _playButton.clicked += OnPlayClicked;
            _shopButton.clicked += OnShopClicked;
            _settingsButton.clicked += OnSettingsClicked;
            _profileButton.clicked += OnProfileClicked;
        }

        public override void OnClose()
        {
            if (_playButton != null) _playButton.clicked -= OnPlayClicked;
            if (_shopButton != null) _shopButton.clicked -= OnShopClicked;
            if (_settingsButton != null) _settingsButton.clicked -= OnSettingsClicked;
            if (_profileButton != null) _profileButton.clicked -= OnProfileClicked;
            base.OnClose();
        }

        private void OnPlayClicked() => _matchmaking.Play();
        private void OnShopClicked() => _viewModel.Navigate(FrontEndDestination.Shop);
        private void OnSettingsClicked() => _viewModel.Navigate(FrontEndDestination.Settings);
        private void OnProfileClicked() => _viewModel.Navigate(FrontEndDestination.Profile);
    }
}
