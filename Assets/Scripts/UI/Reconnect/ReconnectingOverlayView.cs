using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>경기 중 끊겼을 때 화면을 덮는다. 뒤에서 혼자 도는 경기를 가리고 입력을 막는다. 재접속 감시가 연다.</summary>
    public class ReconnectingOverlayView : UIPopup
    {
        public const string Title = "연결이 끊겼습니다";
        public const string Message = "다시 연결하는 중…";

        public override bool AutoClose => false;

        public override void OnOpen()
        {
            base.OnOpen();
            Root.Q<Label>("reconnecting-title").text = Title;
            Root.Q<Label>("reconnecting-message").text = Message;
        }
    }
}
