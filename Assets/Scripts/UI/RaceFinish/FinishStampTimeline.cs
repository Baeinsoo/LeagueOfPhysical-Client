using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 결승 도장 한 장의 시간표(spec §3, 시제품 <c>stamps</c>). 찍히는 순간 1.8배에서 0.12초에 1배로 "쾅", 1.8초까지 또렷하다가
    /// 0.4초에 걸쳐 사라진다. 순위 도장은 FINISH! 0.45초 뒤에 같은 시간표로 찍힌다.
    /// </summary>
    public static class FinishStampTimeline
    {
        public const float StampFrom = 1.8f;
        public const float StampInSeconds = 0.12f;
        public const float RankDelaySeconds = 0.45f;
        public const float FadeStartSeconds = 1.8f;
        public const float FadeEndSeconds = 2.2f;

        public static float Scale(float age)
            => StampFrom - (StampFrom - 1f) * Mathf.Clamp01(Mathf.Max(0f, age) / StampInSeconds);

        public static float Alpha(float age)
        {
            if (age < 0f)
            {
                return 0f;
            }
            if (age <= FadeStartSeconds)
            {
                return 1f;
            }
            return Mathf.Clamp01((FadeEndSeconds - age) / (FadeEndSeconds - FadeStartSeconds));
        }

        public static bool Gone(float age) => age >= FadeEndSeconds;
    }
}
