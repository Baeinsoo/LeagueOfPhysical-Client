using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>진행 막대(.kit-progress) 채움 폭. 비율은 0~1로 자르고, 숫자가 아니면 0으로 본다.</summary>
    public static class KitProgress
    {
        public static float Clamp01(float ratio)
        {
            if (float.IsNaN(ratio) || ratio < 0f) return 0f;
            return ratio > 1f ? 1f : ratio;
        }

        public static void SetRatio(VisualElement fill, float ratio)
        {
            fill.style.width = Length.Percent(Clamp01(ratio) * 100f);
        }
    }
}
