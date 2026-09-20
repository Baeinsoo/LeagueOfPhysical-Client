namespace FlappyRace
{
    /// <summary>
    /// 코스 고도 프로파일 — 생성기·플레이어 바닥/천장·봇이 모두 같은 공식을 쓰게 공유.
    /// sharp=false: 사인(완만한 언덕). sharp=true: 삼각파(뾰족한 V·W, 급격한 느낌 — 경사는
    /// 선형이라 따라갈 수 있음).
    ///
    /// <para><b>Logic 어셈블리에 산다</b>: 맵 빌더(에디터, <c>LOP.MapTools</c>)와 런타임이 둘 다
    /// 쓸 수 있어야 한다. 이 어셈블리는 엔진을 참조하지 않으므로 <c>Mathf</c> 대신
    /// <c>System.Math</c>를 쓴다 — 결과는 같고, 플레이어 빌드에서만 깨지는 일이 없다.</para>
    /// </summary>
    public static class FlappyElevation
    {
        public static float Value(float x, float amp, float startX, float wavelength, bool sharp)
        {
            if (amp == 0f || wavelength <= 0f)
            {
                return 0f;
            }
            double ph = (x - startX) / wavelength;
            double w = sharp
                //  삼각파 [-1,1], 꼭짓점 뾰족
                ? (2.0 / System.Math.PI) * System.Math.Asin(System.Math.Sin(2.0 * System.Math.PI * ph))
                : System.Math.Sin(2.0 * System.Math.PI * ph);
            return amp * (float)w;
        }

        /// <summary>
        /// 사인과 삼각파를 <paramref name="sharpness"/>(0~1)로 섞는다.
        ///
        /// <para>구간마다 모양을 <i>갈아 끼우면</i> 경계에서 값이 튀어 회랑이 끊긴다. 섞으면
        /// "뒤로 갈수록 뾰족해진다"를 연속적으로 얻는다.</para>
        /// </summary>
        public static float Blend(float x, float amp, float startX, float wavelength, float sharpness)
        {
            float soft = Value(x, amp, startX, wavelength, sharp: false);
            float hard = Value(x, amp, startX, wavelength, sharp: true);
            float t = sharpness < 0f ? 0f : (sharpness > 1f ? 1f : sharpness);
            return soft + (hard - soft) * t;
        }
    }
}
