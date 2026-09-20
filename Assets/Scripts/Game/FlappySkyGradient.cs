using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 코스 진행률(0~1) 하나로 하늘이 정해진다. <see cref="SkydiveSkyGradient"/>의 짝이며,
    /// 축만 고도(y)가 아니라 진행(x)이다.
    ///
    /// <para><b>안개 하나가 세 층을 가른다</b>: ExponentialSquared는 거리의 제곱으로 먹으므로
    /// (씻김 = 1 − exp(−(밀도·거리)²)), 층이 20 / 34 / 82m로 떨어져 있으면 한 값이 세 층을
    /// 다르게 씻긴다 — 0.009에서 3% / 9% / 42%, 0.016에서 10% / 26% / 82%.
    /// 그래서 층마다 재질 채도를 따로 만들지 않는다.</para>
    /// </summary>
    public static class FlappySkyGradient
    {
        //  구간 1 — 서늘한 아침빛. 배경이 42% 씻긴다.
        //  <b>밝기를 낮춰 둔 이유</b>: 안개색이 게임 평면 재질(휘도 0.77)만큼 밝으면 씻긴
        //  배경이 파이프와 같은 값이 되어 장애물이 묻힌다. 처음엔 (0.62,0.68,0.76)이었는데
        //  안개를 켜자마자 그렇게 됐다. 구간 3의 먼지빛(휘도 0.43)과 깊이를 맞춘다.
        private static readonly Color StartFog = new Color(0.42f, 0.50f, 0.62f);
        private static readonly Color StartSky = new Color(0.55f, 0.66f, 0.80f);
        private const float StartDensity = 0.009f;

        //  구간 3 — 주황 먼지빛. 배경이 82% 씻겨 거의 사라진다.
        private static readonly Color EndFog = new Color(0.58f, 0.40f, 0.28f);
        private static readonly Color EndSky = new Color(0.62f, 0.42f, 0.28f);
        private const float EndDensity = 0.016f;

        public static (Color fog, Color skyTint, float density) Evaluate(float progress)
        {
            float t = progress < 0f ? 0f : (progress > 1f ? 1f : progress);
            return (Color.Lerp(StartFog, EndFog, t),
                    Color.Lerp(StartSky, EndSky, t),
                    Mathf.Lerp(StartDensity, EndDensity, t));
        }
    }
}
