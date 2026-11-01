using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.EditorTools.Automation
{
    /// <summary>
    /// 글자 칸에 진짜 키보드처럼 친다(spec §8.5) — EventSystem 아래 패널이 IMGUI 이벤트에서 만드는 것과 같은 KeyDownEvent를
    /// 지금 초점 요소에 보낸다. 다 친 뒤 칸 글자가 기대와 다르면 실패한다 — 조용히 value를 넣어 통과시키지 않는다.
    /// </summary>
    internal static class UiTyper
    {
        public static bool Type(VisualElement target, string value, bool clear, bool submit, out string why)
        {
            var field = FieldOf(target) ?? target.Q<TextField>();
            if (field == null)
            {
                why = $"글자 칸(TextField)이 아니다: {target.name}";
                return false;
            }

            string expected = clear ? value : (field.text ?? string.Empty) + value;
            field.Focus();
            if (clear)
            {
                //  전부 고른 뒤 지운다 — 사람이 전체 선택하고 지우는 것과 같은 길(maxLength·검사가 그대로 걸린다).
                field.SelectAll();
                Send(field, new UnityEngine.Event { type = EventType.KeyDown, keyCode = KeyCode.Backspace });
            }
            foreach (char c in value)
            {
                Send(field, new UnityEngine.Event { type = EventType.KeyDown, character = c, keyCode = KeyCode.None });
            }

            if (field.text != expected)
            {
                why = $"글자가 안 들어갔다 — 기대 '{expected}', 칸 '{field.text}'(spec §8.5)";
                return false;
            }
            //  이름 바꾸기 모달처럼 KeyCode.Return을 확인으로 받는 칸이 있다.
            if (submit) Send(field, new UnityEngine.Event { type = EventType.KeyDown, keyCode = KeyCode.Return });
            why = null;
            return true;
        }

        private static TextField FieldOf(VisualElement element)
        {
            for (var v = element; v != null; v = v.hierarchy.parent)
            {
                if (v is TextField field) return field;
            }
            return null;
        }

        private static void Send(TextField field, UnityEngine.Event imgui)
        {
            //  TextField는 초점을 안쪽 글자 입력 요소에 넘긴다 — 키 이벤트는 그 초점 요소가 받아야 한다.
            var target = field.panel?.focusController?.focusedElement as VisualElement ?? field;
            using (var evt = KeyDownEvent.GetPooled(imgui))
            {
                target.SendEvent(evt);
            }
        }
    }
}
