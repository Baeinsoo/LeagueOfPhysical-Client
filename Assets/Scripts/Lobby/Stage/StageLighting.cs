using UnityEngine;

namespace LOP
{
    /// <summary>로비 터널 조명 값 — 일반은 밝은 터널에 노란 끝 빛, 랭크는 어두운 터널에 붉은 끝 빛과 발밑 스포트라이트(1부 spec §3.8).</summary>
    public readonly struct StageLighting
    {
        public readonly float SunIntensity;
        public readonly Color SunColor;
        public readonly Color Ambient;
        public readonly Color EndGlow;
        public readonly Color EndPointColor;
        public readonly float EndPointIntensity;
        public readonly float EndPointRange;
        public readonly float SpotIntensity;
        public readonly float SpotAngle;
        public readonly bool Spotlight;

        public StageLighting(float sunIntensity, Color sunColor, Color ambient, Color endGlow, Color endPointColor,
            float endPointIntensity, float endPointRange, float spotIntensity, float spotAngle, bool spotlight)
        {
            SunIntensity = sunIntensity;
            SunColor = sunColor;
            Ambient = ambient;
            EndGlow = endGlow;
            EndPointColor = endPointColor;
            EndPointIntensity = endPointIntensity;
            EndPointRange = endPointRange;
            SpotIntensity = spotIntensity;
            SpotAngle = spotAngle;
            Spotlight = spotlight;
        }

        public static StageLighting For(bool ranked) => ranked
            ? new StageLighting(0.35f, Hex("#8E8AC0"), Hex("#1F1D4A"), Hex("#E8322F"), Hex("#E8322F"), 3f, 14f, 6f, 30f, true)
            : new StageLighting(1.0f, Hex("#FFF8E8"), Hex("#3D3A86"), Hex("#FFF8E8"), Hex("#FFD23F"), 3f, 14f, 0f, 30f, false);

        /// <summary>두 값 사이. 스포트라이트는 세기로 섞고, 세기가 남아 있는 동안 켜 둔다(꺼질 때도 0으로 내려간 뒤 꺼지게).</summary>
        public static StageLighting Lerp(StageLighting a, StageLighting b, float t)
        {
            t = Mathf.Clamp01(t);
            float spot = Mathf.Lerp(a.Spotlight ? a.SpotIntensity : 0f, b.Spotlight ? b.SpotIntensity : 0f, t);
            return new StageLighting(
                Mathf.Lerp(a.SunIntensity, b.SunIntensity, t),
                Color.Lerp(a.SunColor, b.SunColor, t),
                Color.Lerp(a.Ambient, b.Ambient, t),
                Color.Lerp(a.EndGlow, b.EndGlow, t),
                Color.Lerp(a.EndPointColor, b.EndPointColor, t),
                Mathf.Lerp(a.EndPointIntensity, b.EndPointIntensity, t),
                Mathf.Lerp(a.EndPointRange, b.EndPointRange, t),
                spot,
                Mathf.Lerp(a.SpotAngle, b.SpotAngle, t),
                spot > 0.001f);
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
