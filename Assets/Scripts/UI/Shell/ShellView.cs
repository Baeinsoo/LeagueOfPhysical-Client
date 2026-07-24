using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 프론트엔드 셸(상점/설정/프로필) 공유 베이스. title을 세팅하고 back 버튼을 콜백에 잇는 얇은 바인더.
    /// 여는 쪽(FrontEndCoordinator)이 SetBackCallback으로 닫기 동작을 배선한다(MatchingWaitingView 패턴).
    /// 내용은 플레이스홀더 — 화면별 콘텐츠는 후속 스펙.
    /// </summary>
    public abstract class ShellView : UIView
    {
        private Button _backButton;
        private System.Action _onBack;

        public override UILayer Layer => UILayer.Window;

        /// <summary>헤더에 표시할 셸 제목(서브클래스가 지정).</summary>
        protected abstract string Title { get; }

        public void SetBackCallback(System.Action onBack) => _onBack = onBack;

        public override void OnOpen()
        {
            base.OnOpen();

            var titleLabel = Root.Q<Label>("shell-title");
            if (titleLabel != null) titleLabel.text = Title;

            _backButton = Root.Q<Button>("back-button");
            _backButton.clicked += OnBackClicked;
        }

        public override void OnClose()
        {
            if (_backButton != null) _backButton.clicked -= OnBackClicked;
            base.OnClose();
        }

        private void OnBackClicked() => _onBack?.Invoke();
    }
}
