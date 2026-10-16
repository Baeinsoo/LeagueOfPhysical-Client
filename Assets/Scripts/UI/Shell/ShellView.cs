using R3;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 메뉴 셸(상점·라커룸·선수 카드·설정) 공유 베이스. 머리띠의 뒤로·제목·코인 칩을 맡는 얇은 바인더.
    /// 여는 쪽(FrontEndCoordinator)이 SetBackCallback으로 닫기 동작을 배선한다(MatchingWaitingView 패턴).
    /// </summary>
    public abstract class ShellView : UIView
    {
        //  코인 칩은 VM 없는 셸(설정)도 같은 길로 그리려고 필드 주입으로 받는다. 시험·미리보기처럼
        //  주입 없이 만들면 null이고, 그때는 칩을 숨긴다.
        [VContainer.Inject] private EconomyStore _economy;

        private Button _backButton;
        private System.Action _onBack;

        public override UILayer Layer => UILayer.Window;

        /// <summary>헤더에 표시할 셸 제목(서브클래스가 지정).</summary>
        protected abstract string Title { get; }

        public void SetBackCallback(System.Action onBack) => _onBack = onBack;

        /// <summary>코인 칩을 보일지 — 재화 스토어가 없으면(시험·미리보기) 숨긴다.</summary>
        public static bool CoinVisible(EconomyStore store) => store != null;

        public override void OnOpen()
        {
            base.OnOpen();

            var titleLabel = Root.Q<Label>("shell-title");
            if (titleLabel != null) titleLabel.text = Title;

            _backButton = Root.Q<Button>("back-button");
            _backButton.clicked += OnBackClicked;

            var coin = Root.Q<VisualElement>("shell-coin");
            if (coin != null)
            {
                bool visible = CoinVisible(_economy);
                coin.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (visible)
                {
                    var coinText = Root.Q<Label>("shell-coin-text");
                    //  로비 칩과 같은 표기 — 아직 못 받았으면 "-".
                    Disposables.Add(_economy.Progress.CombineLatest(_economy.Coins, LobbyHomeView.CoinChipText)
                        .Subscribe(text => coinText.text = $"🪙 {text}"));
                }
            }
        }

        public override void OnClose()
        {
            if (_backButton != null) _backButton.clicked -= OnBackClicked;
            base.OnClose();
        }

        private void OnBackClicked() => _onBack?.Invoke();
    }
}
