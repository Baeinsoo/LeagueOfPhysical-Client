using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 맵별 분위기(<see cref="SkydiveMood"/>)를 높이로 읽는다 — 가장 가까운 위아래 두 점을 섞고, 끝 밖은 끝 값.
    /// 순수 함수라 그림 없이 시험한다.
    /// </summary>
    public static class SkydiveMoodCurve
    {
        public static bool TryEvaluate(SkydiveMoodKey[] keys, float altitude, out SkydiveMoodKey mood)
        {
            if (keys == null || keys.Length == 0)
            {
                mood = default;
                return false;
            }
            mood = Evaluate(keys, altitude);
            return true;
        }

        public static SkydiveMoodKey Evaluate(SkydiveMoodKey[] keys, float altitude)
        {
            //  순서에 기대지 않는다 — 위로 가장 가까운 점과 아래로 가장 가까운 점을 찾는다.
            int above = -1, below = -1;
            for (int i = 0; i < keys.Length; i++)
            {
                float a = keys[i].Altitude;
                if (a >= altitude && (above < 0 || a < keys[above].Altitude)) { above = i; }
                if (a <= altitude && (below < 0 || a > keys[below].Altitude)) { below = i; }
            }
            if (above < 0) { return keys[below]; }   // 맨 위보다 높다
            if (below < 0) { return keys[above]; }   // 맨 아래보다 낮다
            if (above == below) { return keys[above]; }

            var hi = keys[above];
            var lo = keys[below];
            float t = Mathf.InverseLerp(lo.Altitude, hi.Altitude, altitude);   // 0 = 아래 점, 1 = 위 점
            return new SkydiveMoodKey
            {
                Altitude = altitude,
                Fog = Color.Lerp(lo.Fog, hi.Fog, t),
                FogDensity = Mathf.Lerp(lo.FogDensity, hi.FogDensity, t),
                Ambient = Color.Lerp(lo.Ambient, hi.Ambient, t),
                Sun = Color.Lerp(lo.Sun, hi.Sun, t),
                SunIntensity = Mathf.Lerp(lo.SunIntensity, hi.SunIntensity, t),
                Bloom = Mathf.Lerp(lo.Bloom, hi.Bloom, t),
                Exposure = Mathf.Lerp(lo.Exposure, hi.Exposure, t),
            };
        }
    }
}
