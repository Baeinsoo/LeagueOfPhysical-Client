using System;
using UnityEngine;

namespace LOP.UI
{
    public enum CountdownKind
    {
        /// <summary>출발이 3초보다 멀거나 시계를 아직 모른다 — "n / m 대기 중".</summary>
        Waiting,

        /// <summary>3·2·1.</summary>
        Number,

        /// <summary>출발 뒤 0.8초.</summary>
        Go,

        /// <summary>그 뒤 — 아무것도 안 띄운다.</summary>
        Hidden,
    }

    public readonly struct CountdownFrame
    {
        public readonly CountdownKind Kind;
        public readonly int Number;

        /// <summary>이 숫자(또는 GO)가 뜬 지 몇 초 — 팝·사라짐이 여기서 나온다.</summary>
        public readonly double SecondsIn;

        public CountdownFrame(CountdownKind kind, int number, double secondsIn)
        {
            Kind = kind;
            Number = number;
            SecondsIn = secondsIn;
        }
    }

    /// <summary>
    /// 카운트다운 계산(spec §3, 시제품 카운트다운): 숫자 구간 3초(올림), 숫자마다 1.5배 → 1배 0.25초 팝, GO!는 0.8초에 걸쳐 사라짐.
    /// 시각은 러너의 연속 시각(<c>elapsedTime</c>)으로 잰다 — 틱(0.02초 계단)으로 재면 팝이 계단진다.
    /// </summary>
    public static class RaceCountdown
    {
        public const double CountSeconds = 3.0;
        public const double GoSeconds = 0.8;
        public const float PopFrom = 1.5f;
        public const float PopSeconds = 0.25f;

        //  startTick × secondsPerTick의 곱셈이 부동소수 오차(2e-16 수준)를 남긴다 — 정확히 GoSeconds(0.8초) 경계에서
        //  그 오차가 since < GoSeconds 비교를 뒤집어 GO!가 0.8초를 넘기고도 한 틱 더 보인다. 여유를 조금 둔다.
        private const double BoundaryEpsilon = 1e-6;

        public static CountdownFrame At(long startTick, double secondsPerTick, double nowSeconds, bool running)
        {
            //  runner.Run()이 불리기 전엔 tick도 interval도 0이다. 그대로 셈하면 남은 초가 0이 돼 "GO!"가 뜬다.
            if (startTick == long.MaxValue || running == false || secondsPerTick <= 0d)
            {
                return new CountdownFrame(CountdownKind.Waiting, 0, 0d);
            }
            double remaining = startTick * secondsPerTick - nowSeconds;
            if (remaining > CountSeconds)
            {
                //  서버가 전원 준비 뒤 한 박자 쉬고 출발선을 긋는다 — 그 여유까지 세면 5부터 세는 꼴이다.
                return new CountdownFrame(CountdownKind.Waiting, 0, 0d);
            }
            if (remaining > 0d)
            {
                int n = (int)Math.Ceiling(remaining);
                return new CountdownFrame(CountdownKind.Number, n, n - remaining);
            }
            double since = -remaining;
            return since < GoSeconds - BoundaryEpsilon
                ? new CountdownFrame(CountdownKind.Go, 0, since)
                : new CountdownFrame(CountdownKind.Hidden, 0, since);
        }

        /// <summary>팝: 뜨는 순간 1.5배, 0.25초에 걸쳐 1배로.</summary>
        public static float Scale(double secondsIn)
            => PopFrom - (PopFrom - 1f) * Mathf.Clamp01((float)(secondsIn / PopSeconds));

        /// <summary>GO!의 투명도: 1 → 0.8초에 0.</summary>
        public static float GoAlpha(double secondsIn)
            => Mathf.Clamp01(1f - (float)(secondsIn / GoSeconds));
    }

    public enum CountdownSound { None, Beat, Go }

    /// <summary>박자 소리를 언제 낼지(시제품: 숫자가 바뀔 때마다 박자, 0이 되면 GO). 프레임마다 같은 숫자를 다시 봐도 한 번만.</summary>
    public sealed class CountdownCue
    {
        private CountdownKind lastKind = CountdownKind.Waiting;
        private int lastNumber;

        public CountdownSound Observe(CountdownFrame frame)
        {
            var sound = CountdownSound.None;
            if (frame.Kind == CountdownKind.Number && (lastKind != CountdownKind.Number || lastNumber != frame.Number))
            {
                sound = CountdownSound.Beat;
            }
            else if (frame.Kind == CountdownKind.Go && lastKind != CountdownKind.Go && lastKind != CountdownKind.Hidden)
            {
                sound = CountdownSound.Go;
            }
            lastKind = frame.Kind;
            lastNumber = frame.Number;
            return sound;
        }
    }
}
