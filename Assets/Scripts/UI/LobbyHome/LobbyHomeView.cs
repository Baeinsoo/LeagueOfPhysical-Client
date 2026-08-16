using R3;
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

        private Button _gamePick;
        private Button _mapPick;
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();

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
            _gamePick = Root.Q<Button>("game-pick");
            _mapPick = Root.Q<Button>("map-pick");

            _playButton.clicked += OnPlayClicked;
            _shopButton.clicked += OnShopClicked;
            _settingsButton.clicked += OnSettingsClicked;
            _profileButton.clicked += OnProfileClicked;
            _gamePick.clicked += OnGamePickClicked;
            _mapPick.clicked += OnMapPickClicked;

            //  칩은 시작 버튼 "안"에 있다. 칩을 눌렀을 때 그 클릭이 바깥까지 올라가면 매칭이 시작돼
            //  버린다 — 선택만 하려던 사람에게는 오조작이다. 그래서 칩에서 전파를 끊는다.
            StopClickFromReachingPlay(_gamePick);
            StopClickFromReachingPlay(_mapPick);

            _matchmaking.SelectedGameName.Subscribe(name => _gamePick.text = name).AddTo(_subscriptions);
            _matchmaking.SelectedMapName.Subscribe(name => _mapPick.text = name).AddTo(_subscriptions);
        }

        private static void StopClickFromReachingPlay(Button chip)
        {
            chip.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            chip.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
            chip.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
        }

        public override void OnClose()
        {
            _subscriptions.Clear();

            if (_gamePick != null) _gamePick.clicked -= OnGamePickClicked;
            if (_mapPick != null) _mapPick.clicked -= OnMapPickClicked;
            if (_playButton != null) _playButton.clicked -= OnPlayClicked;
            if (_shopButton != null) _shopButton.clicked -= OnShopClicked;
            if (_settingsButton != null) _settingsButton.clicked -= OnSettingsClicked;
            if (_profileButton != null) _profileButton.clicked -= OnProfileClicked;
            base.OnClose();
        }

        private void OnPlayClicked() => _matchmaking.Play();
        private void OnGamePickClicked() => _matchmaking.NextGame();
        private void OnMapPickClicked() => _matchmaking.NextMap();
        private void OnShopClicked() => _viewModel.Navigate(FrontEndDestination.Shop);
        private void OnSettingsClicked() => _viewModel.Navigate(FrontEndDestination.Settings);
        private void OnProfileClicked() => _viewModel.Navigate(FrontEndDestination.Profile);
    }
}
