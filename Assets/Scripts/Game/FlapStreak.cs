using System.Globalization;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// 연속 날갯짓(spec §7) — 기절 없이 이어진 먹힌 날갯짓 수와 이번 판 최고. 순수 계산이라 EditMode로 고정한다.
    /// <para>같은 틱에 두 번 눌러도 하나다 — 시뮬은 한 틱에 한 번만 뛴다(Jump 입력 하나). 기절은 "아니었다 → 이다"일 때만 0으로 만들고,
    /// 처음 본 장은 기준만 잡는다(<see cref="FlappyCrashFx"/>와 같은 규칙).</para>
    /// </summary>
    public sealed class FlapStreak
    {
        private long _lastTick = long.MinValue;
        private bool _known;
        private bool _wasStunned;

        public int Count { get; private set; }

        public int Best { get; private set; }

        /// <summary>마지막으로 센 날갯짓 시각(초). "+N"이 이 뒤로 사라진다.</summary>
        public double LastFlapSeconds { get; private set; } = double.NegativeInfinity;

        /// <summary>먹힌 날갯짓 하나. 같은 틱의 두 번째는 안 센다(false).</summary>
        public bool Flap(long tick, double nowSeconds)
        {
            if (tick == _lastTick)
            {
                return false;
            }
            _lastTick = tick;
            Count++;
            if (Count > Best)
            {
                Best = Count;
            }
            LastFlapSeconds = nowSeconds;
            return true;
        }

        /// <summary>매 장 내 새의 기절 여부. 기절에 들어가는 장에 0으로 만들고 true.</summary>
        public bool ObserveStun(bool stunned)
        {
            bool crashed = _known && _wasStunned == false && stunned;
            _known = true;
            _wasStunned = stunned;
            if (crashed)
            {
                Count = 0;
                LastFlapSeconds = double.NegativeInfinity;
            }
            return crashed;
        }
    }

    /// <summary>머리 위 "+N" 시간표(spec §7). 마지막 날갯짓부터 잰다.</summary>
    public static class FlapStreakLook
    {
        public const float PopSeconds = 0.12f;
        public const float PopScale = 1.35f;
        public const float HoldSeconds = 0.7f;
        public const float FadeSeconds = 0.3f;

        //  합한 길이는 리터럴 — 사라짐 판정이 이 한 곳(t >= Total)이다.
        public const float Total = 1.0f;

        public static float Alpha(float since)
        {
            if (since < 0f || since >= Total)
            {
                return 0f;
            }
            return since <= HoldSeconds ? 1f : 1f - Mathf.Clamp01((since - HoldSeconds) / FadeSeconds);
        }

        public static float Scale(float since)
            => since <= 0f ? PopScale : Mathf.Lerp(PopScale, 1f, Mathf.Clamp01(since / PopSeconds));

        public static string Text(int count) => "+" + count.ToString(CultureInfo.InvariantCulture);
    }
}
