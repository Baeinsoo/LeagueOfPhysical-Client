using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>매칭 대기 오버레이. 정적 텍스트 + 취소 버튼. 여는 쪽이 SetCancelCallback으로 취소 동작을 배선한다.</summary>
    public class MatchingWaitingView : UIView
    {
        // LOP.Action(MonoBehaviour 컴포넌트)이 System.Action을 가리므로 풀 한정한다.
        private Button _cancelButton;
        private System.Action _onCancel;

        public override UILayer Layer => UILayer.Loading;
        public override bool BlocksUnderlyingInput => true;

        public void SetCancelCallback(System.Action onCancel) => _onCancel = onCancel;

        public override void OnOpen()
        {
            base.OnOpen();

            _cancelButton = Root.Q<Button>("cancel-button");
            _cancelButton.clicked += OnCancelClicked;
        }

        public override void OnClose()
        {
            if (_cancelButton != null) _cancelButton.clicked -= OnCancelClicked;
            base.OnClose();
        }

        private void OnCancelClicked() => _onCancel?.Invoke();
    }
}
