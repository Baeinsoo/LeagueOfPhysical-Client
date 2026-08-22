using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>이름 바꾸기 팝업 View. 입력을 ViewModel 커맨드로 넘기고, ViewModel의 결과를 포워딩한다.</summary>
    public class ChangeDisplayNameView : UIPopup, IResultView<bool>
    {
        private readonly ChangeDisplayNameViewModel _viewModel;

        private TextField _input;
        private Label _message;
        private Button _confirmButton;
        private Button _cancelButton;

        public ChangeDisplayNameView(ChangeDisplayNameViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        //  백드롭을 눌러도 안 닫는다 — 입력하던 이름이 말없이 사라지는 것보다 [취소]를 누르게 하는 편이 낫다.
        public override bool AutoClose => false;

        public UniTask<bool> ResultAsync => _viewModel.ResultAsync;

        public override void OnOpen()
        {
            base.OnOpen();

            _input = Root.Q<TextField>("cdn-input");
            _message = Root.Q<Label>("cdn-message");
            _confirmButton = Root.Q<Button>("cdn-confirm");
            _cancelButton = Root.Q<Button>("cdn-cancel");

            _input.maxLength = ChangeDisplayNameViewModel.MaxLength;
            _input.value = _viewModel.CurrentDisplayName;
            _input.Focus();

            _input.RegisterCallback<KeyDownEvent>(OnKeyDown);
            _confirmButton.clicked += OnConfirmClicked;
            _cancelButton.clicked += OnCancelClicked;
            _viewModel.MessageChanged += OnMessageChanged;
            _viewModel.IsBusyChanged += OnIsBusyChanged;

            OnMessageChanged();
            OnIsBusyChanged();
        }

        public override void OnClose()
        {
            if (_input != null) _input.UnregisterCallback<KeyDownEvent>(OnKeyDown);
            if (_confirmButton != null) _confirmButton.clicked -= OnConfirmClicked;
            if (_cancelButton != null) _cancelButton.clicked -= OnCancelClicked;
            _viewModel.MessageChanged -= OnMessageChanged;
            _viewModel.IsBusyChanged -= OnIsBusyChanged;

            base.OnClose();
        }

        //  모바일 키보드의 완료 키와 데스크톱 Enter를 같은 동작으로 묶는다.
        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == UnityEngine.KeyCode.Return || evt.keyCode == UnityEngine.KeyCode.KeypadEnter)
            {
                OnConfirmClicked();
            }
        }

        private void OnConfirmClicked() => _viewModel.Submit(_input.value);
        private void OnCancelClicked() => _viewModel.Cancel();

        private void OnMessageChanged()
        {
            if (_message == null)
            {
                return;
            }

            _message.text = _viewModel.Message;
            _message.style.display = string.IsNullOrEmpty(_viewModel.Message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void OnIsBusyChanged()
        {
            bool interactable = _viewModel.IsBusy == false;
            _confirmButton?.SetEnabled(interactable);
            _cancelButton?.SetEnabled(interactable);
            _input?.SetEnabled(interactable);
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    _viewModel.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
