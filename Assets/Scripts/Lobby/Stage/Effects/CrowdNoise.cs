namespace LOP
{
    /// <summary>
    /// 멀리서 웅성거리는 관중 소리를 만든다 — 낮게 출렁이는 잡음(갈색 잡음)을 사람 목소리 대역(520Hz 근처)으로 거른다.
    /// 반복 재생 때 이음매가 들리지 않게 끝 0.2초를 시작과 겹쳐 섞는다. 같은 씨앗이면 같은 소리.
    /// </summary>
    public static class CrowdNoise
    {
        public static float[] Generate(int sampleRate, float seconds, int seed)
        {
            int n = (int)(sampleRate * seconds);
            int fade = System.Math.Min(n / 4, (int)(sampleRate * 0.2f));
            var raw = new float[n + fade];
            var rnd = new System.Random(seed);
            float last = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                last = (last + 0.02f * ((float)rnd.NextDouble() * 2f - 1f)) / 1.02f;
                raw[i] = last * 3.5f;
            }

            //  띠 통과 필터(RBJ 쿡북): 가운데 520Hz, Q 0.6.
            double w0 = 2 * System.Math.PI * 520.0 / sampleRate, alpha = System.Math.Sin(w0) / (2 * 0.6);
            double b0 = alpha, b2 = -alpha, a0 = 1 + alpha, a1 = -2 * System.Math.Cos(w0), a2 = 1 - alpha;
            double x1 = 0, x2 = 0, y1 = 0, y2 = 0;
            float peak = 1e-6f;
            for (int i = 0; i < raw.Length; i++)
            {
                double x = raw[i];
                double y = (b0 * x + b2 * x2 - a1 * y1 - a2 * y2) / a0;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                raw[i] = (float)y;
                peak = System.Math.Max(peak, System.Math.Abs(raw[i]));
            }

            var outBuf = new float[n];
            for (int i = 0; i < n; i++) outBuf[i] = raw[i] / peak * 0.9f;
            for (int i = 0; i < fade; i++)
            {
                float t = i / (float)fade;
                outBuf[i] = outBuf[i] * t + raw[n + i] / peak * 0.9f * (1f - t);
            }
            return outBuf;
        }
    }
}
