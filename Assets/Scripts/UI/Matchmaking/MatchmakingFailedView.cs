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

        /// <summary>어떤 경로로든(확인 버튼 → 코디네이터의 Close 호출, 백드롭 클릭, Back()/ESC)
        /// 실제로 닫혔을 때 발화. 코디네이터가 이걸로 자기 참조를 정리해야, 코디네이터를
        /// 거치지 않는 닫기 경로가 생겨도 "닫힌 View를 계속 들고 있는" 상태가 안 남는다.</summary>
        public event Action Closed;

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
            Closed?.Invoke();
        }

        private void OnConfirmClicked() => Confirmed?.Invoke();
    }
}
