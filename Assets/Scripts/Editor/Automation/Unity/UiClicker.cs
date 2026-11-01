using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.EditorTools.Automation
{
    /// <summary>
    /// 진짜 마우스처럼 요소 가운데에 왼쪽 버튼 누름·뗌을 보낸다. 패널 뿌리에 보내면 패널이 그 자리 맨 위 요소를 골라 전달한다 —
    /// 모달이 덮고 있으면 모달이 받는다(누르기 전에 <see cref="UiLocator.IsOnTop"/>도 따로 본다).
    /// </summary>
    internal static class UiClicker
    {
        public static void Click(VisualElement element)
        {
            Vector2 at = element.worldBound.center;
            var root = element.panel.visualTree;

            var down = new UnityEngine.Event { type = EventType.MouseDown, mousePosition = at, button = 0, clickCount = 1 };
            using (var evt = PointerDownEvent.GetPooled(down))
            {
                root.SendEvent(evt);
            }

            var up = new UnityEngine.Event { type = EventType.MouseUp, mousePosition = at, button = 0, clickCount = 1 };
            using (var evt = PointerUpEvent.GetPooled(up))
            {
                root.SendEvent(evt);
            }
        }
    }
}
