using UnityEngine;

/// <summary>
/// 코스 고도 프로파일 — 생성기·플레이어 바닥/천장·봇이 모두 같은 공식을 쓰게 공유.
/// sharp=false: 사인(완만한 언덕). sharp=true: 삼각파(뾰족한 V·W, 급격한 느낌 — 경사는 선형이라 따라갈 수 있음).
/// </summary>
public static class FlappyElevation
{
    public static float Value(float x, float amp, float startX, float wavelength, bool sharp)
    {
        if (amp == 0f || wavelength <= 0f) return 0f;
        float ph = (x - startX) / wavelength;
        float w = sharp
            ? (2f / Mathf.PI) * Mathf.Asin(Mathf.Sin(2f * Mathf.PI * ph))   // 삼각파 [-1,1], 꼭짓점 뾰족
            : Mathf.Sin(2f * Mathf.PI * ph);
        return amp * w;
    }
}
