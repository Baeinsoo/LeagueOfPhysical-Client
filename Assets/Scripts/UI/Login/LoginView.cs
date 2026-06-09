using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>로그인 팝업 View. 버튼 클릭을 ViewModel 커맨드로 전달, 플랫폼별 버튼 노출 토글.</summary>
    public class LoginView : UIPopup
    {
        public LoginViewModel ViewModel { get; }

        private Button _guestButton;
        private Button _gpgsButton;
        private Button _gamecenterButton;

        public LoginView(LoginViewModel viewModel)
        {
            ViewModel = viewModel;
        }

        public override void OnOpen()
        {
            base.OnOpen();

            _guestButton = Root.Q<Button>("guest-login");
            _gpgsButton = Root.Q<Button>("gpgs-login");
            _gamecenterButton = Root.Q<Button>("gamecenter-login");

            SetVisible(_guestButton, ViewModel.ShowGuest);
            SetVisible(_gpgsButton, ViewModel.ShowGpgs);
            SetVisible(_gamecenterButton, ViewModel.ShowGameCenter);

            _guestButton.clicked += OnGuestClicked;
            _gpgsButton.clicked += OnGpgsClicked;
            _gamecenterButton.clicked += OnGameCenterClicked;
        }

        public override void OnClose()
        {
            if (_guestButton != null) _guestButton.clicked -= OnGuestClicked;
            if (_gpgsButton != null) _gpgsButton.clicked -= OnGpgsClicked;
            if (_gamecenterButton != null) _gamecenterButton.clicked -= OnGameCenterClicked;

            base.OnClose();
        }

        private void OnGuestClicked() => ViewModel.RequestLogin(LoginType.Guest);
        private void OnGpgsClicked() => ViewModel.RequestLogin(LoginType.GooglePlayGame);
        private void OnGameCenterClicked() => ViewModel.RequestLogin(LoginType.GameCenter);

        private static void SetVisible(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public override void Dispose()
        {
            ViewModel.Dispose();
            base.Dispose();
        }
    }
}
