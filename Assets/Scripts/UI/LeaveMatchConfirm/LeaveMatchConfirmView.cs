using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 나가기 확인 팝업. 버튼을 ViewModel 커맨드로 넘기고 결과를 포워딩한다.
    ///
    /// <para>카드 밖(딤 영역)을 눌러도 닫힌다 — 되돌릴 수 없는 행동이라 실수로 닫히는 편이
    /// 실수로 나가는 것보다 안전하다. <see cref="WindowManager"/>의 백드롭은 모달 뷰 루트
    /// <b>아래</b>에 깔리는데 이 뷰 루트가 전체화면이라 클릭이 거기서 멈춰 백드롭까지
    /// 못 내려간다 — 그래서 바깥 클릭은 이 뷰가 직접 받는다(닫는 주체는 백드롭이 아니라
    /// 이 뷰다). 그때도 결과가 확정되는 것은 ViewModel의 Dispose가 보장한다.</para>
    /// </summary>
    public class LeaveMatchConfirmView : UIPopup, IResultView<bool>
    {
        private readonly LeaveMatchConfirmViewModel _viewModel;

        private Button _confirmButton;
        private Button _cancelButton;
        private VisualElement _card;

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

            _card = Root.Q<VisualElement>("leave-card");
            Root.RegisterCallback<PointerDownEvent>(OnRootPointerDown);
        }

        public override void OnClose()
        {
            if (_confirmButton != null) { _confirmButton.clicked -= OnConfirmClicked; }
            if (_cancelButton != null) { _cancelButton.clicked -= OnCancelClicked; }

            Root.UnregisterCallback<PointerDownEvent>(OnRootPointerDown);

            base.OnClose();
        }

        private void OnConfirmClicked() => _viewModel.Confirm();
        private void OnCancelClicked() => _viewModel.Cancel();

        //  카드 밖(딤 영역)을 눌러도 취소한다. WindowManager의 백드롭은 모달 뷰 루트 *아래*에
        //  깔리는데 이 뷰 루트가 전체화면이라 클릭이 거기서 멈춰 백드롭까지 못 내려간다 —
        //  그래서 바깥 클릭을 뷰가 직접 받는다.
        private void OnRootPointerDown(PointerDownEvent evt)
        {
            if (_card != null && evt.target is VisualElement element && _card.Contains(element))
            {
                return;
            }

            _viewModel.Cancel();
        }

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
