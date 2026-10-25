using System;

namespace LOP
{
    /// <summary>
    /// 기록 시간. 출발 틱부터 잰다. 내가 결승하면 그 틱에서 멈춘다 — 등수(0.2초 늦게 오는 서버 값)가 아니라
    /// 내 시뮬이 결승선을 넘은 틱이라, 넘는 순간 숫자가 선다.
    /// <para>조각 5의 "완주 시간"(spec §7)은 서버 등수가 처음 생긴 틱으로 재기로 했다 — 둘이 한두 틱 다를 수 있다.</para>
    /// </summary>
    public static class RaceClock
    {
        public static double ElapsedSeconds(double nowSeconds, long startTick, double secondsPerTick, long myFinishedTick)
        {
            if (startTick == long.MaxValue || secondsPerTick <= 0d)
            {
                return 0d;   //  출발이 아직 안 정해졌다(대기 중) — 0:00.0
            }
            double start = startTick * secondsPerTick;
            double end = myFinishedTick != FinishState.NotFinished ? myFinishedTick * secondsPerTick : nowSeconds;
            return Math.Max(0d, end - start);
        }

        public static string Format(double seconds)
        {
            //  10분의 1초까지 버린다. 반올림하면 59.96초가 "0:60.0"이 된다.
            //  작은 여유(1e-6)는 5.3×10 = 52.999…가 52로 버려지는 것을 막는다.
            long tenths = (long)Math.Floor(Math.Max(0d, seconds) * 10d + 1e-6);
            long minutes = tenths / 600;
            long rest = tenths % 600;
            return $"{minutes}:{rest / 10:00}.{rest % 10}";
        }
    }
}
