using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 고도에 따라 대기 색을 정한다. 높이에 따라 하늘이 변하면 HUD 숫자 없이도 얼마나
    /// 내려왔는지가 읽힌다 — 레퍼런스에서 가장 인상적이었던 부분이다.
    /// </summary>
    public static class SkydiveSkyGradient
    {
        public const float TopAltitude = 3000f;

        // 위는 공기가 얇은 느낌의 서늘한 하늘색, 아래는 지면의 따뜻한 아지랑이.
        private static readonly Color Top = new Color(0.72f, 0.80f, 0.90f);
        private static readonly Color Bottom = new Color(0.94f, 0.89f, 0.78f);

        public readonly struct Colors
        {
            public readonly Color fog;
            public readonly Color skyTint;

            public Colors(Color fog, Color skyTint)
            {
                this.fog = fog;
                this.skyTint = skyTint;
            }
        }

        public static Colors Evaluate(float altitude)
        {
            float t = Mathf.Clamp01(1f - altitude / TopAltitude);   // 0=꼭대기, 1=지면
            Color fog = Color.Lerp(Top, Bottom, t);

            // 하늘은 안개보다 덜 변한다 — 같이 움직이면 화면이 통째로 색만 바뀐 것처럼 보인다.
            Color sky = Color.Lerp(Top, Bottom, t * 0.55f);
            return new Colors(fog, sky);
        }
    }
}
