using UnityEngine;

namespace LOP
{
    /// <summary>컷인 0.95초 — 오른쪽에서 밀려 들어와(0.12) 머물다(처음 0.1초 흔들림) 왼쪽으로 빠진다(0.13). 그동안 화면은 흑백 정지.</summary>
    public static class ArcheryCutInTimeline
    {
        public const float Duration = 0.95f;
        private const float In = 0.12f;
        private const float Out = 0.13f;
        private const float ShakeSeconds = 0.1f;
        private const float ShakePixels = 4f;

        public static bool IsActive(float t) => t >= 0f && t < Duration;

        public static float SlideX(float t)
        {
            if (t < In) return 1f - EaseOut(Mathf.Clamp01(t / In));
            if (t < Duration - Out) return 0f;
            return -EaseIn(Mathf.Clamp01((t - (Duration - Out)) / Out));
        }

        public static Vector2 Shake(float t)
        {
            float s = t - In;
            if (s < 0f || s >= ShakeSeconds) return Vector2.zero;
            float k = 1f - s / ShakeSeconds;
            return new Vector2(Mathf.Sin(s * 157f), Mathf.Cos(s * 131f)) * (ShakePixels * k);
        }

        /// <summary>흑백 정지 가중치 — 들어오는 동안 켜지고, 나가는 동안 꺼진다.</summary>
        public static float Freeze(float t)
        {
            if (IsActive(t) == false) return 0f;
            if (t < In) return t / In;
            if (t < Duration - Out) return 1f;
            return 1f - (t - (Duration - Out)) / Out;
        }

        private static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x);
        private static float EaseIn(float x) => x * x * x;
    }
}
