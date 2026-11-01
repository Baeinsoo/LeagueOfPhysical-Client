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
            if (submit) SendSubmit(field);
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
            //  TextField는 초점을 안쪽 글자 입력 요소에 넘긴다 — 키 이벤트는 그 초점 요소가 받아야 한다(글자가 거기서 들어간다).
            var target = field.panel?.focusController?.focusedElement as VisualElement ?? field;
            using (var evt = KeyDownEvent.GetPooled(imgui))
            {
                target.SendEvent(evt);
            }
        }

        private static void SendSubmit(TextField field)
        {
            //  Return/KeypadEnter는 안쪽 글자 입력 요소(TextElement, 초점 요소)의 KeyboardTextEditorEventHandler가
            //  항상 StopPropagation으로 삼킨다(Unity 6000.3 — 실제 Enter도 같다, KeyboardTextEditor.cs의 single-line
            //  분기가 조건 없이 그렇다) — 그 요소를 target으로 보내면 TextField 자신에 등록된
            //  RegisterCallback<KeyDownEvent>(확인 동작, ChangeDisplayNameView.OnKeyDown과 같은 등록)까지 절대 안 번진다.
            //  TextField 자신을 target으로 지정하면 전파 경로가 안쪽 요소(자손)를 거치지 않고 바로 TextField에서
            //  AtTarget으로 핸들러를 깨운다 — EventBase.target은 public 세터라 여기서 직접 지정할 수 있다.
            var imgui = new UnityEngine.Event { type = EventType.KeyDown, keyCode = KeyCode.Return };
            using (var evt = KeyDownEvent.GetPooled(imgui))
            {
                evt.target = field;
                field.SendEvent(evt);
            }
        }
    }
}
