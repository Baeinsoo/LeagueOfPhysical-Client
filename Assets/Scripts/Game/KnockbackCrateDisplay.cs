using UnityEngine;

namespace LOP
{
    /// <summary>보급 상자 그림의 크기·높이·시간 식. 화면(뷰)과 시험이 같은 식을 쓴다.</summary>
    public static class KnockbackCrateDisplay
    {
        public const float DropHeight = 8f;
        public const float ShadowStartRadius = 0.4f;
        public const float ShadowEndRadius = 1.0f;
        public const float CrateSize = 0.7f;
        public const float VanishSeconds = 0.15f;
        public const float HoverAmplitude = 0.12f;
        public const float HoverSpeed = 2.5f;
        public const float SpinDegreesPerSecond = 40f;

        /// <summary>예고가 시작된 순간 0, 상자가 떨어지는 틱에 1. 예고가 0틱이면 이미 떨어진 것으로 본다.</summary>
        public static float WarnProgress01(double renderTick, long landTick, int warnTicks)
        {
            if (warnTicks <= 0) return 1f;
            double start = landTick - warnTicks;
            return (float)System.Math.Max(0d, System.Math.Min(1d, (renderTick - start) / warnTicks));
        }

        public static float ShadowRadius(float progress01)
            => Mathf.Lerp(ShadowStartRadius, ShadowEndRadius, Mathf.Clamp01(progress01));

        // 제곱으로 내려오면 처음엔 천천히, 땅에 닿을 때 가장 빠르다(중력으로 떨어지는 모양).
        public static float FallHeight(double renderTick, long landTick, int warnTicks)
        {
            float p = WarnProgress01(renderTick, landTick, warnTicks);
            return DropHeight * (1f - p * p);
        }

        /// <summary>떨어진 뒤 위아래로 살짝 떠다니는 높이 변화(m).</summary>
        public static float HoverOffset(float seconds) => HoverAmplitude * Mathf.Sin(seconds * HoverSpeed);

        /// <summary>주워진 상자의 크기 비율. 0.15초 동안 1에서 0으로 줄어든다.</summary>
        public static float VanishScale(float elapsedSeconds) => 1f - Mathf.Clamp01(elapsedSeconds / VanishSeconds);
    }
}
