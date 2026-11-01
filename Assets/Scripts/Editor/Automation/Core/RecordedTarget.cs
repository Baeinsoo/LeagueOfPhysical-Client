using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LOP.EditorTools.Automation
{
    /// <summary>
    /// 녹화 때 눌린 요소 → 그것을 다시 정확히 가리키는 선택자(spec §14.3). 맞은 요소에서 위로 올라가 첫 "누를 것"만 본다 —
    /// 그것을 못 가리키면 더 올라가지 않고 null(좌표로 남긴다): 바깥 상자의 가운데를 누르면 엉뚱한 것이 눌린다.
    /// </summary>
    public static class RecordedTarget
    {
        public const int MaxTextLength = 40;
        /// <summary>이 프로젝트 UI 키트의 "누르는 것" 클래스 — Button이 아닌 누를 수 있는 판(카드·명패)에 붙는다.</summary>
        public const string PressClass = "kit-press";

        public static UiSelector Choose(VisualElement hit, Func<UiSelector, VisualElement> resolve)
        {
            for (var v = hit; v != null; v = v.hierarchy.parent)
            {
                if (!IsPressable(v)) continue;
                foreach (var selector in SelectorsFor(v))
                {
                    if (IsSameOrInside(resolve(selector), v)) return selector;
                }
                return null;
            }
            return null;
        }

        public static TextField FieldOf(VisualElement element)
        {
            for (var v = element; v != null; v = v.hierarchy.parent)
            {
                if (v is TextField field) return field;
            }
            return null;
        }

        //  글자 선택자는 버튼과 그 안쪽 Label을 함께 맞춘다 — 운전수는 뒤의 것(Label)을 고르고 그 가운데를 누르는데, 그것도 버튼 안이다.
        private static bool IsSameOrInside(VisualElement resolved, VisualElement target)
        {
            for (var v = resolved; v != null; v = v.hierarchy.parent)
            {
                if (v == target) return true;
            }
            return false;
        }

        private static bool IsPressable(VisualElement v) => v is Button || v is Toggle || v is TextField || v.ClassListContains(PressClass);

        private static IEnumerable<UiSelector> SelectorsFor(VisualElement v)
        {
            string name = OwnName(v);
            //  글자 칸은 이름으로만 — 칸의 글자는 치는 대로 바뀐다.
            string text = v is TextField ? null : ShortText(v);
            if (name != null) yield return new UiSelector { Name = name };
            if (name != null && text != null) yield return new UiSelector { Name = name, Text = text };
            if (name == null && text != null) yield return new UiSelector { Text = text };
        }

        //  unity-로 시작하는 이름은 유니티가 붙인 안쪽 이름 — 우리 UXML 이름이 아니다.
        private static string OwnName(VisualElement v) =>
            string.IsNullOrEmpty(v.name) || v.name.StartsWith("unity-", StringComparison.Ordinal) ? null : v.name;

        private static string ShortText(VisualElement v)
        {
            string text = UiLocator.TextOf(v);
            return string.IsNullOrEmpty(text) || text.Length > MaxTextLength || text.Contains("\n") ? null : text;
        }
    }
}
