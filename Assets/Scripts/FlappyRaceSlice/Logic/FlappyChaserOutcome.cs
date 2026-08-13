namespace FlappyRace
{
    /// <summary>
    /// 시뮬 충돌 분포를 시간축으로 풀어 "이 실력이면 완주하는가"를 판정한다.
    /// 추격자 곡선 튜닝의 통과 조건(고수 완주율 100%)을 계산하는 도구.
    ///
    /// 단독 주행 모델이라 "선두 한 화면 뒤" 규칙은 적용하지 않는다 —
    /// 혼자 달리면 자기가 곧 선두이고, 그 규칙은 항상 자기보다 뒤를 가리켜 절대 잡지 못한다.
    /// 따라서 가속선만 비교하면 되고, 그것이 곧 최악 조건이다.
    /// </summary>
    public static class FlappyChaserOutcome
    {
        /// <param name="bucketClips">구간별 평균 충돌 횟수(런당). 인덱스 i = startX + i*bucketWidth 구간.</param>
        /// <param name="caughtAtTime">잡힌 시각(초). 완주했으면 -1.</param>
        /// <returns>완주하면 true.</returns>
        public static bool Survives(
            float[] bucketClips, float bucketWidth,
            float startX, float finishX,
            float forwardSpeed, float dismountTime,
            FlappyChaserCurve curve, float chaserStartX,
            out float caughtAtTime)
        {
            caughtAtTime = -1f;
            if (bucketClips == null || bucketClips.Length == 0) return true;

            const float dt = 0.05f;
            float t = 0f;
            float x = startX;
            float chaser = chaserStartX;
            float pause = 0f;
            int bucket = -1;
            int guard = 0;

            while (x < finishX && guard++ < 2000000)
            {
                chaser += curve.SpeedAt(t) * dt;

                if (pause > 0f)
                {
                    pause -= dt;   // 낙마 중 — 전진 정지
                }
                else
                {
                    float nx = x + forwardSpeed * dt;
                    int nb = (int)((nx - startX) / bucketWidth);
                    if (nb > bucket)
                    {
                        bucket = nb;
                        if (nb >= 0 && nb < bucketClips.Length)
                            pause += bucketClips[nb] * dismountTime;
                    }
                    x = nx;
                }

                t += dt;

                if (x <= chaser) { caughtAtTime = t; return false; }
            }
            return true;
        }
    }
}
