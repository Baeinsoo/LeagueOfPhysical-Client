using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>키트 토스트 — 크림 스티커 한 장. 언제 닫을지는 여는 쪽(코디네이터)이 정한다.</summary>
    public class KitToastView : UIView
    {
        public override UILayer Layer => UILayer.Notification;

        public void Show(string text) => Root.Q<Label>("toast-text").text = text;
    }
}
