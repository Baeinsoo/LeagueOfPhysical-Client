using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 일반→랭크 순간의 조명 쇼. 0.15초에 걸쳐 거의 꺼지고(8%), 0.45초부터 끝 기둥부터 0.12초 간격으로
    /// 기둥 조명 띠가 켜지며(켜질 때 잠깐 더 밝게), 1.5~2.0초에 전체 밝기가 돌아온다.
    /// t는 쇼 시작 뒤 초. 쇼가 아닐 땐 +∞.
    /// </summary>
    public static class LightShowCurve
    {
        public const float Duration = 2f;
        public const float StripIdle = 0.75f;
        private const float DimFloor = 0.08f;

        public static float Dim(float t)
        {
            if (t < 0f || t >= Duration) return 1f;
            if (t < 0.15f) return 1f - t / 0.15f * (1f - DimFloor);
            if (t < 1.5f) return DimFloor;
            return DimFloor + (t - 1.5f) / 0.5f * (1f - DimFloor);
        }

        public static float Strip(float z, float lastPillarZ, float spacing, float t)
        {
            if (t >= Duration) return StripIdle;
            float onAt = 0.45f + (lastPillarZ - z) / spacing * 0.12f;
            if (t < onAt) return 0f;
            return Mathf.Min(1.4f, StripIdle + Mathf.Max(0f, 0.65f - (t - onAt) * 2f));
        }
    }
}
