using R3;
using System.Collections.Generic;
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

        private DropdownField _gamePick;
        private DropdownField _mapPick;
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();

        //  VM 값을 드롭다운에 밀어넣는 동안은 드롭다운의 변경 콜백을 무시한다 — 안 그러면
        //  "VM이 바꿈 → 드롭다운이 알림 → VM에 다시 씀"으로 되돌아온다.
        private bool _applyingFromViewModel;

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
            _gamePick = Root.Q<DropdownField>("game-pick");
            _mapPick = Root.Q<DropdownField>("map-pick");

            _playButton.clicked += OnPlayClicked;
            _shopButton.clicked += OnShopClicked;
            _settingsButton.clicked += OnSettingsClicked;
            _profileButton.clicked += OnProfileClicked;

            //  드롭다운은 시작 버튼 "안"에 있다. 여기서 전파를 끊지 않으면 목록을 여는 클릭이
            //  바깥 버튼까지 올라가 매칭이 시작된다 — 고르려던 사람에게는 오조작이다.
            StopClickFromReachingPlay(_gamePick);
            StopClickFromReachingPlay(_mapPick);

            var gameNames = new List<string>(_matchmaking.Games.Count);
            foreach (var game in _matchmaking.Games)
            {
                gameNames.Add(game.Name);
            }
            _gamePick.choices = gameNames;

            _gamePick.RegisterValueChangedCallback(_ =>
            {
                if (_applyingFromViewModel == false) _matchmaking.SelectGame(_gamePick.index);
            });
            _mapPick.RegisterValueChangedCallback(_ =>
            {
                if (_applyingFromViewModel == false) _matchmaking.SelectMap(_mapPick.index);
            });

            _matchmaking.SelectedGameIndex.Subscribe(OnGameSelected).AddTo(_subscriptions);
            _matchmaking.SelectedMapIndex.Subscribe(OnMapSelected).AddTo(_subscriptions);
        }

        //  게임이 바뀌면 맵 목록 자체가 바뀐다 — 목록을 다시 채운 뒤 지금 맵을 표시한다.
        private void OnGameSelected(int index)
        {
            _applyingFromViewModel = true;

            _gamePick.index = index;

            var mapNames = new List<string>(_matchmaking.CurrentMaps.Count);
            foreach (var map in _matchmaking.CurrentMaps)
            {
                mapNames.Add(map.Name);
            }
            _mapPick.choices = mapNames;
            _mapPick.index = _matchmaking.SelectedMapIndex.CurrentValue;

            _applyingFromViewModel = false;
        }

        private void OnMapSelected(int index)
        {
            _applyingFromViewModel = true;
            _mapPick.index = index;
            _applyingFromViewModel = false;
        }

        private void StopClickFromReachingPlay(DropdownField pick)
        {
            pick.RegisterCallback<PointerDownEvent>(evt =>
            {
                evt.StopPropagation();

                //  목록은 이 클릭으로 열린다. 열린 뒤라야 항목을 만질 수 있어 다음 틱으로 미룬다.
                pick.schedule.Execute(() => MarkSelectedDropdownItem(pick));
            });
            pick.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
            pick.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
        }

        /// <summary>
        /// 펼쳐진 목록에서 지금 고른 항목에 표시용 클래스를 붙인다.
        /// 유니티는 선택을 클래스로 알려주지 않고 체크마크의 visibility로만 표시하는데,
        /// USS에는 "자식이 보이는 부모"를 고르는 방법이 없어 코드가 대신 붙여 준다.
        ///
        /// 판정은 <b>인덱스</b>로 한다. 항목은 choices와 같은 순서로 만들어지므로 n번째 항목이
        /// 곧 choices[n]이다. 체크마크 visibility는 메뉴가 갓 열린 프레임에 확정되지 않고,
        /// 글자 비교는 이름이 겹치면 깨진다. 목록은 열 때마다 새로 만들어지므로 매번 다시 붙인다.
        /// </summary>
        private void MarkSelectedDropdownItem(DropdownField pick)
        {
            //  목록은 우리 문서가 아니라 패널 루트에 붙는다.
            var panelRoot = Root.panel?.visualTree;
            if (panelRoot == null)
            {
                return;
            }

            //  두 드롭다운의 목록이 동시에 남아 있어도 섞이지 않도록 방금 열린 것만 본다.
            var menus = panelRoot.Query<VisualElement>(className: "unity-base-dropdown").ToList();
            if (menus.Count == 0)
            {
                return;
            }

            var items = menus[menus.Count - 1].Query<VisualElement>(className: "unity-base-dropdown__item").ToList();
            for (var i = 0; i < items.Count; i++)
            {
                items[i].EnableInClassList("lop-dropdown-item--selected", i == pick.index);
            }
        }

        public override void OnClose()
        {
            _subscriptions.Clear();

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
