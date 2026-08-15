using System;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>매칭 실패 안내 팝업. 확인을 누르면 닫힌다.</summary>
    public class MatchmakingFailedView : UIPopup
    {
        private Button _confirmButton;

        /// <summary>확인 클릭. 코디네이터가 닫기를 배선한다(화면 교체는 View 책임이 아니다).</summary>
        public event Action Confirmed;

        public override void OnOpen()
        {
            base.OnOpen();

            _confirmButton = Root.Q<Button>("mmf-confirm");
            _confirmButton.clicked += OnConfirmClicked;
        }

        public override void OnClose()
        {
            if (_confirmButton != null)
            {
                _confirmButton.clicked -= OnConfirmClicked;
            }

            base.OnClose();
        }

        private void OnConfirmClicked() => Confirmed?.Invoke();
    }
}
