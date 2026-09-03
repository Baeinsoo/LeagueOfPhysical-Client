using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 구름 층의 높이와, 층 안에서 안개가 얼마나 짙어지는지.
    ///
    /// 선반은 깊이 단서가 못 된다 — 위 선반이 아래를 완전히 가려서 겹겹이 멀어지는 그림이
    /// 안 나온다. 그래서 낙하 속도감은 구름이 전담한다.
    ///
    /// 굽는 쪽(에디터 빌더)과 보는 쪽(런타임)이 같은 표를 봐야 하므로 여기 하나만 둔다.
    /// </summary>
    public static class SkydiveCloudLayers
    {
        // 60m/s로 떨어지면 층 사이가 약 2.8초. 층을 지나는 데는 약 1.3초 걸린다.
        private const float Spacing = 170f;
        private const float Lowest = 150f;
        private const float Highest = 2900f;

        /// <summary>층의 반두께(m). 이 범위 안에 있으면 구름 속이다.</summary>
        public const float HalfThickness = 40f;

        /// <summary>층 밖에서 쓰는 기준 밀도. 400m에서 24%, 1000m에서 81% 씻긴다.</summary>
        public const float BaseFogDensity = 0.0013f;

        // 한가운데에서 기준의 몇 배가 되는가. 너무 올리면 발밑 선반까지 사라져
        // "윤곽은 비쳐 보임"이 아니라 "완전히 가림"이 된다.
        private const float PeakMultiplier = 3.4f;

        public static readonly float[] Altitudes = Build();

        private static float[] Build()
        {
            int count = Mathf.FloorToInt((Highest - Lowest) / Spacing) + 1;
            var list = new float[count];
            for (int i = 0; i < count; i++)
            {
                list[i] = Highest - i * Spacing;
            }
            return list;
        }

        /// <summary>그 고도에서 써야 할 안개 밀도. 고도만 보고 답하므로 호출해도 상태가 안 남는다.</summary>
        public static float DensityAt(float altitude)
        {
            float nearest = 0f;
            for (int i = 0; i < Altitudes.Length; i++)
            {
                float d = Mathf.Abs(altitude - Altitudes[i]);
                if (i == 0 || d < nearest)
                {
                    nearest = d;
                }
            }

            if (nearest >= HalfThickness)
            {
                return BaseFogDensity;
            }

            // 가장자리에서 한가운데로 갈수록 부드럽게 짙어진다(경계에서 툭 끊기면 눈에 띈다).
            float t = 1f - nearest / HalfThickness;
            float eased = t * t * (3f - 2f * t);
            return BaseFogDensity * Mathf.Lerp(1f, PeakMultiplier, eased);
        }
    }
}
