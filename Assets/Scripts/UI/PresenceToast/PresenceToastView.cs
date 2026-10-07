using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 위쪽 가운데 한 줄 알림("2P 선수 연결 끊김"). 줄마다 잠깐 보이고 사라진다 — 게임을 가리지 않게
    /// 입력은 통과시키고, 한꺼번에 여럿 오면 오래된 줄부터 밀어낸다.
    /// </summary>
    public class PresenceToastView : UIView
    {
        private const long ShowMs = 3000;
        private const int MaxLines = 3;

        private VisualElement list;

        public override UILayer Layer => UILayer.Notification;

        public override void OnOpen()
        {
            base.OnOpen();
            list = Root.Q<VisualElement>("presence-toast-list");
        }

        public void Show(string text)
        {
            if (list == null) return;
            var line = new Label(text) { pickingMode = PickingMode.Ignore };
            line.AddToClassList("presence-toast-line");
            list.Add(line);
            while (list.childCount > MaxLines) list.RemoveAt(0);
            line.schedule.Execute(() => line.RemoveFromHierarchy()).StartingIn(ShowMs);
        }
    }
}
