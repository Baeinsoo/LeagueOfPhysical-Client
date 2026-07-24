using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 매치 결과 화면(플레이스홀더). 여는 쪽(FrontEndCoordinator)이 SetConfirmCallback으로 닫기 동작을 배선한다.
    /// 점수·순위 표시는 후속 스펙 — 지금은 판이 끝났음을 알리고 닫는 역할만 한다.
    /// </summary>
    public class MatchResultView : UIView
    {
        // LOP.Action(MonoBehaviour 컴포넌트)이 System.Action을 가리므로 풀 한정한다.
        private Button _confirmButton;
        private System.Action _onConfirm;

        public override UILayer Layer => UILayer.Window;

        public void SetConfirmCallback(System.Action onConfirm) => _onConfirm = onConfirm;

        public override void OnOpen()
        {
            base.OnOpen();

            _confirmButton = Root.Q<Button>("confirm-button");
            _confirmButton.clicked += OnConfirmClicked;
        }

        public override void OnClose()
        {
            if (_confirmButton != null) _confirmButton.clicked -= OnConfirmClicked;
            base.OnClose();
        }

        private void OnConfirmClicked() => _onConfirm?.Invoke();
    }
}
