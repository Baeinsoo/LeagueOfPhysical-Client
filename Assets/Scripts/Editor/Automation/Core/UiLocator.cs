using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace LOP.EditorTools.Automation
{
    /// <summary>UI Toolkit 요소 찾기·보임·맨 위 판정(spec §4.2·§8.1). 패널에 안 붙은 요소(시험)는 보이지 않는 것으로 친다.</summary>
    public static class UiLocator
    {
        /// <summary>자기 글자, 없으면 안쪽 글자들을 줄바꿈으로 — 이 프로젝트 버튼은 글자를 안쪽 Label에 둔다.</summary>
        public static string TextOf(VisualElement element)
        {
            if (element is TextElement self && !string.IsNullOrWhiteSpace(self.text)) return self.text.Trim();
            var parts = new List<string>();
            element.Query<TextElement>().ForEach(t =>
            {
                if (t != element && !string.IsNullOrWhiteSpace(t.text)) parts.Add(t.text.Trim());
            });
            return string.Join("\n", parts);
        }

        public static bool Matches(VisualElement element, UiSelector selector)
        {
            if (!string.IsNullOrEmpty(selector.Name) && element.name != selector.Name) return false;
            if (string.IsNullOrEmpty(selector.Text) && string.IsNullOrEmpty(selector.TextContains)) return true;

            string text = TextOf(element);
            if (!string.IsNullOrEmpty(selector.Text) && text != selector.Text) return false;
            //  문화권 비교는 이모지를 "무시해도 되는 글자"로 보고 아무 데서나 찾았다고 한다 — 있는 그대로 비교한다.
            if (!string.IsNullOrEmpty(selector.TextContains) && text.IndexOf(selector.TextContains, StringComparison.Ordinal) < 0) return false;
            return true;
        }

        public static List<VisualElement> FindAll(VisualElement root, UiSelector selector)
        {
            var found = new List<VisualElement>();
            root.Query<VisualElement>().ForEach(e =>
            {
                if (Matches(e, selector)) found.Add(e);
            });
            return found;
        }

        public static bool IsVisible(VisualElement element)
        {
            if (element.panel == null) return false;
            for (var v = element; v != null; v = v.hierarchy.parent)
            {
                var style = v.resolvedStyle;
                if (style.display == DisplayStyle.None || style.visibility == Visibility.Hidden || style.opacity < 0.01f) return false;
            }
            var rect = element.worldBound;
            return !float.IsNaN(rect.width) && rect.width > 0.5f && rect.height > 0.5f;
        }

        /// <summary>요소 가운데를 패널이 집으면 그 요소나 그 안쪽이 나오나 — 모달 백드롭·다른 창에 가려졌으면 false.</summary>
        public static bool IsOnTop(VisualElement element)
        {
            if (element.panel == null) return false;
            var picked = element.panel.Pick(element.worldBound.center);
            for (var v = picked; v != null; v = v.hierarchy.parent)
            {
                if (v == element) return true;
            }
            return false;
        }

        public static UiElementInfo Describe(VisualElement element)
        {
            bool visible = IsVisible(element);
            return new UiElementInfo
            {
                Name = element.name,
                Type = element.GetType().Name,
                Text = TextOf(element),
                Visible = visible,
                Enabled = element.enabledInHierarchy,
                OnTop = visible && IsOnTop(element),
            };
        }

        /// <summary>누를 수 있는 것 > 보이는 것 > 나머지, 같으면 뒤의 것(그리는 순서가 뒤 = 위 밴드·안쪽).</summary>
        public static int PickBest(IReadOnlyList<UiElementInfo> infos)
        {
            int best = -1;
            int bestScore = -1;
            for (int i = 0; i < infos.Count; i++)
            {
                int score = infos[i].Clickable ? 2 : infos[i].Visible ? 1 : 0;
                if (score >= bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }
            return best;
        }
    }
}
