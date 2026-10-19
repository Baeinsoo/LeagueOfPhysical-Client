using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>재접속 실패 팝업 View. [다시 시도]로만 닫힌다 — 바깥을 눌러 닫히면 덮개 뒤 멈춘 경기가 드러난다.</summary>
    public class ReconnectFailedView : UIPopup, IResultView<bool>
    {
        private readonly ReconnectFailedViewModel _viewModel;
        private Button _retry;

        public ReconnectFailedView(ReconnectFailedViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override bool AutoClose => false;

        //  로딩판(Loading 층) 위에 뜬다 — 처음 입장·다시 들어가기 중에 끊기면 로딩판이 떠 있다.
        public override UILayer Layer => UILayer.System;

        public UniTask<bool> ResultAsync => _viewModel.ResultAsync;

        public override void OnOpen()
        {
            base.OnOpen();
            Root.Q<Label>("reconnect-failed-message").text = ReconnectFailedViewModel.Message;
            _retry = Root.Q<Button>("reconnect-failed-retry");
            _retry.clicked += _viewModel.RequestRetry;
        }

        public override void OnClose()
        {
            if (_retry != null) _retry.clicked -= _viewModel.RequestRetry;
            base.OnClose();
        }
    }
}
