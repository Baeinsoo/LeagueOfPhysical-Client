using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>"서버에 연결할 수 없습니다" 팝업 View. 서버에 닿을 때까지 닫히지 않는다(로그인보다 먼저).</summary>
    public class ServerUnreachableView : UIPopup, IResultView<bool>
    {
        private readonly ServerUnreachableViewModel _viewModel;
        private Label _message;
        private Button _retry;

        public ServerUnreachableView(ServerUnreachableViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override bool AutoClose => false;

        public UniTask<bool> ResultAsync => _viewModel.ResultAsync;

        public override void OnOpen()
        {
            base.OnOpen();
            _message = Root.Q<Label>("unreachable-message");
            _retry = Root.Q<Button>("unreachable-retry");
            _retry.clicked += _viewModel.RequestRetry;
            _viewModel.Changed += Refresh;
            Refresh();
        }

        public override void OnClose()
        {
            if (_retry != null) _retry.clicked -= _viewModel.RequestRetry;
            _viewModel.Changed -= Refresh;
            base.OnClose();
        }

        private void Refresh()
        {
            if (_message != null)
            {
                _message.text = _viewModel.IsBusy ? "연결 확인 중..." : _viewModel.Message;
            }
            _retry?.SetEnabled(_viewModel.IsBusy == false);
        }
    }
}
