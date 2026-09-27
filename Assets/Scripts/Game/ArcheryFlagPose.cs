using UnityEngine;

namespace LOP
{
    public readonly struct ArcheryFlagPoseValue
    {
        /// <summary>천이 향하는 쪽: +1 = 사수 기준 오른쪽, −1 = 왼쪽, 0 = 처짐.</summary>
        public readonly int Side;
        /// <summary>0 = 막대에 늘어짐, 1 = 옆으로 다 펴짐.</summary>
        public readonly float Extend;
        public readonly float FlapDegrees;

        public ArcheryFlagPoseValue(int side, float extend, float flapDegrees)
        {
            Side = side;
            Extend = extend;
            FlapDegrees = flapDegrees;
        }
    }

    /// <summary>관중석 깃발이 바람(m/s², 양수 = 사수 기준 오른쪽)을 보여 준다 — 관중이 풍향계다.</summary>
    public static class ArcheryFlagPose
    {
        public const float CalmBelow = 0.3f;
        public const float FullAt = 5f;

        public static ArcheryFlagPoseValue At(float wind, float time, float phase)
        {
            float strength = Mathf.Abs(wind);
            if (strength < CalmBelow)
            {
                return new ArcheryFlagPoseValue(0, 0f, 0f);
            }
            float extend = Mathf.Clamp01(strength / FullAt);
            float flap = Mathf.Sin(time * (6f + strength) + phase) * 15f * extend;
            return new ArcheryFlagPoseValue(wind > 0f ? 1 : -1, extend, flap);
        }
    }
}
