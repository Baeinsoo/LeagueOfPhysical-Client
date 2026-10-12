using System;
using System.Runtime.CompilerServices;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 넘치는 글자를 잘라 "..."을 붙인다. USS text-overflow: ellipsis는 '…' 글자가 없는 글꼴(주아·Black Han Sans)을
    /// 만나면 에디터 메모리를 끝없이 잡아먹어서(10-12 재현) 쓰지 않는다. 두 글꼴 모두 마침표는 있다.
    /// 자를 폭은 라벨의 USS max-width — 레이아웃 폭이 아니라서 글자를 바꿔도 다시 줄어드는 고리가 없다.
    /// </summary>
    public static class KitEllipsis
    {
        public const string Dots = "...";
        public const string ClassName = "kit-ellipsis";

        private sealed class Binding { public string Full = ""; }
        private static readonly ConditionalWeakTable<Label, Binding> Bindings = new();

        /// <summary>폭을 모르면(0 이하·NaN) 그대로. 점 셋도 안 들어가면 점 셋만.</summary>
        public static string Fit(string full, float maxWidth, Func<string, float> measure)
        {
            if (string.IsNullOrEmpty(full) || !(maxWidth > 0f) || measure(full) <= maxWidth) return full ?? "";

            //  들어가는 가장 긴 앞부분을 이분 탐색.
            int lo = 0, hi = full.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (measure(full.Substring(0, mid) + Dots) <= maxWidth) lo = mid;
                else hi = mid - 1;
            }
            //  서로게이트 쌍(이모지)을 반으로 자르지 않는다.
            if (lo > 0 && char.IsHighSurrogate(full[lo - 1])) lo--;
            return full.Substring(0, lo) + Dots;
        }

        /// <summary>라벨 글자를 정한다. 이후 크기·스타일이 바뀌어도 원문에서 다시 자른다.</summary>
        public static void SetText(Label label, string full)
        {
            if (!Bindings.TryGetValue(label, out var b))
            {
                b = new Binding();
                Bindings.Add(label, b);
                label.RegisterCallback<GeometryChangedEvent>(_ => Refit(label));
            }
            b.Full = full ?? "";
            Refit(label);
        }

        /// <summary>root 아래 .kit-ellipsis 라벨을 지금 글자로 묶는다(UXML에 적힌 고정 글자용).</summary>
        public static void BindAll(VisualElement root) =>
            root.Query<Label>(className: ClassName).ForEach(l => SetText(l, l.text));

        private static void Refit(Label label)
        {
            if (!Bindings.TryGetValue(label, out var b)) return;
            string next = b.Full;
            if (label.panel != null)
            {
                var s = label.resolvedStyle;
                float max = s.maxWidth.value - s.paddingLeft - s.paddingRight;
                next = Fit(b.Full, max, t => label.MeasureTextSize(t, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined).x);
            }
            if (label.text != next) label.text = next;
        }
    }
}
