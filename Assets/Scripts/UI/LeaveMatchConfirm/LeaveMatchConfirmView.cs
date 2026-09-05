using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 나가기 확인 팝업. 버튼을 ViewModel 커맨드로 넘기고 결과를 포워딩한다.
    ///
    /// <para>백드롭을 눌러도 닫힌다(<see cref="UIPopup.AutoClose"/> 기본값) — 되돌릴 수 없는
    /// 행동이라 실수로 닫히는 편이 실수로 나가는 것보다 안전하다. 그때도 결과가 확정되는 것은
    /// ViewModel의 Dispose가 보장한다.</para>
    /// </summary>
    public class LeaveMatchConfirmView : UIPopup, IResultView<bool>
    {
        private readonly LeaveMatchConfirmViewModel _viewModel;

        private Button _confirmButton;
        private Button _cancelButton;

        public LeaveMatchConfirmView(LeaveMatchConfirmViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public UniTask<bool> ResultAsync => _viewModel.ResultAsync;

        public override void OnOpen()
        {
            base.OnOpen();

            _confirmButton = Root.Q<Button>("leave-confirm");
            _cancelButton = Root.Q<Button>("leave-cancel");

            _confirmButton.clicked += OnConfirmClicked;
            _cancelButton.clicked += OnCancelClicked;
        }

        public override void OnClose()
        {
            if (_confirmButton != null) { _confirmButton.clicked -= OnConfirmClicked; }
            if (_cancelButton != null) { _cancelButton.clicked -= OnCancelClicked; }

            base.OnClose();
        }

        private void OnConfirmClicked() => _viewModel.Confirm();
        private void OnCancelClicked() => _viewModel.Cancel();

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (_disposed == false)
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
