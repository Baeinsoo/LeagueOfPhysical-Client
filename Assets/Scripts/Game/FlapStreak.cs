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

        /// <summary>먹힌 날갯짓 하나. 같은 틱의 두 번째는 안 센다(false).</summary>
        public bool Flap(long tick)
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
            }
            return crashed;
        }
    }
}
