using UnityEngine;

namespace LOP
{
    /// <summary>바닥 런웨이 조명의 밝기 — 빛의 물결이 발밑에서 출구(+z) 쪽으로 흐른다.</summary>
    public static class RunwayPulse
    {
        public const float Speed = 6f;
        public const float Spacing = 9f;
        public const float Tail = 2.2f;
        public const float Floor = 0.18f;

        public static float Brightness(float z, float time)
        {
            float d = Mathf.Repeat(z - time * Speed, Spacing);
            float peak = Mathf.Max(0f, 1f - d / Tail);
            return Floor + (1f - Floor) * peak;
        }
    }
}
