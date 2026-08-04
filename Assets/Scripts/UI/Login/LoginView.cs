using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>로그인 팝업 View. 버튼 클릭을 ViewModel 커맨드로 전달하고, ViewModel이 만든 결과를 포워딩한다.</summary>
    public class LoginView : UIPopup, IResultView<AuthSession>
    {
        private readonly LoginViewModel _viewModel;

        private Button _guestButton;
        private Button _gpgsButton;
        private Button _gamecenterButton;
        private Label _errorLabel;

        public LoginView(LoginViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        /// <summary>로그인은 임의로 닫을 수 없는 필수 모달.</summary>
        public override bool AutoClose => false;

        public UniTask<AuthSession> ResultAsync => _viewModel.ResultAsync;

        public override void OnOpen()
        {
            base.OnOpen();

            _guestButton = Root.Q<Button>("guest-login");
            _gpgsButton = Root.Q<Button>("gpgs-login");
            _gamecenterButton = Root.Q<Button>("gamecenter-login");
            _errorLabel = Root.Q<Label>("login-error");

            SetVisible(_guestButton, _viewModel.ShowGuest);
            SetVisible(_gpgsButton, _viewModel.ShowGpgs);
            SetVisible(_gamecenterButton, _viewModel.ShowGameCenter);

            _guestButton.clicked += OnGuestClicked;
            _gpgsButton.clicked += OnGpgsClicked;
            _gamecenterButton.clicked += OnGameCenterClicked;
            _viewModel.ErrorMessageChanged += OnErrorMessageChanged;
            _viewModel.IsBusyChanged += OnIsBusyChanged;

            OnErrorMessageChanged();
            OnIsBusyChanged();
        }

        public override void OnClose()
        {
            if (_guestButton != null) _guestButton.clicked -= OnGuestClicked;
            if (_gpgsButton != null) _gpgsButton.clicked -= OnGpgsClicked;
            if (_gamecenterButton != null) _gamecenterButton.clicked -= OnGameCenterClicked;
            _viewModel.ErrorMessageChanged -= OnErrorMessageChanged;
            _viewModel.IsBusyChanged -= OnIsBusyChanged;

            base.OnClose();
        }

        private void OnGuestClicked() => _viewModel.RequestLogin(AuthProvider.Anonymous);
        private void OnGpgsClicked() => _viewModel.RequestLogin(AuthProvider.GooglePlayGames);
        private void OnGameCenterClicked() => _viewModel.RequestLogin(AuthProvider.GameCenter);

        private void OnErrorMessageChanged()
        {
            if (_errorLabel == null)
            {
                return;
            }

            _errorLabel.text = _viewModel.ErrorMessage;
            SetVisible(_errorLabel, string.IsNullOrEmpty(_viewModel.ErrorMessage) == false);
        }

        //  로그인 진행 중엔 버튼을 눌러도 반응하지 않게 막는다 — 느린 연결에서 두 번째 클릭이
        //  두 번째 로그인 요청을 만들어 계정이 두 개 생기는 것을 View 쪽에서도 막는 이중 방어.
        private void OnIsBusyChanged()
        {
            bool interactable = _viewModel.IsBusy == false;
            SetEnabled(_guestButton, interactable);
            SetEnabled(_gpgsButton, interactable);
            SetEnabled(_gamecenterButton, interactable);
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element == null)
            {
                return;
            }

            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void SetEnabled(VisualElement element, bool enabled)
        {
            if (element == null)
            {
                return;
            }

            element.SetEnabled(enabled);
        }

        public override void Dispose()
        {
            _viewModel.Dispose();
            base.Dispose();
        }
    }
}
