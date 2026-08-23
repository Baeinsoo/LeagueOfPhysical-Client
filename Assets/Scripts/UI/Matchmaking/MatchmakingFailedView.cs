using System;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>매칭 실패 안내 팝업. 확인을 누르면 닫힌다.</summary>
    public class MatchmakingFailedView : UIPopup
    {
        private Button _confirmButton;
        private Label _messageLabel;

        //  WindowManager.Open이 OnOpen까지 마치고 돌아오므로 SetMessage는 보통 그 *뒤*에 불린다.
        //  그래서 여기 담아 두고, 라벨이 이미 잡혀 있으면 바로 갈아 끼운다.
        private string _message;

        /// <summary>확인 클릭. 코디네이터가 닫기를 배선한다(화면 교체는 View 책임이 아니다).</summary>
        public event Action Confirmed;

        /// <summary>어떤 경로로든(확인 버튼 → 코디네이터의 Close 호출, 백드롭 클릭, Back()/ESC)
        /// 실제로 닫혔을 때 발화. 코디네이터가 이걸로 자기 참조를 정리해야, 코디네이터를
        /// 거치지 않는 닫기 경로가 생겨도 "닫힌 View를 계속 들고 있는" 상태가 안 남는다.</summary>
        public event Action Closed;

        /// <summary>안내 문구. 열기 전에 코디네이터가 사유에 맞춰 정한다.</summary>
        public void SetMessage(string message)
        {
            _message = message;

            //  이미 열려 있으면 바로 반영한다(열기 전에 부르는 게 정상이지만 순서에 안 기대게).
            if (_messageLabel != null && string.IsNullOrEmpty(message) == false)
            {
                _messageLabel.text = message;
            }
        }

        public override void OnOpen()
        {
            base.OnOpen();

            _confirmButton = Root.Q<Button>("mmf-confirm");
            _confirmButton.clicked += OnConfirmClicked;

            _messageLabel = Root.Q<Label>("mmf-message");
            if (string.IsNullOrEmpty(_message) == false)
            {
                _messageLabel.text = _message;
            }
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
