namespace LOP
{
    public enum CardRushThrowKind { None, ToDoor, BackToFace }

    /// <summary>
    /// 그리는 쪽이 기억한 돌기 시작 — 어느 (칸, 시도)에서 돌기 시작했나와 그 틱. Tick 0 = 못 봤다.
    /// <see cref="BoundThrow"/>는 그 시작 뒤 처음 본 틀린 던짐 틱(0 = 아직) — 보정이 던짐을 다른 틱으로 바꾸면 그 시작은 이 던짐의 것이
    /// 아니다(예측의 시작이 서버에서 버려졌다, 최종 리뷰 m1).
    /// </summary>
    public readonly struct CardRushSpinMemory
    {
        public readonly int Step;
        public readonly int Attempt;
        public readonly long Tick;
        public readonly long BoundThrow;
        public CardRushSpinMemory(int step, int attempt, long tick, long boundThrow = 0) { Step = step; Attempt = attempt; Tick = tick; BoundThrow = boundThrow; }
        public static CardRushSpinMemory Of(in CardRushRunnerState s) => new CardRushSpinMemory(s.Step, s.Attempt, s.SpinStartTick);

        /// <summary>
        /// 이 프레임의 상태로 기억을 고친다. 돌고 있으면 그 시작을 기억하고, 틀린 던짐을 처음 보면 그 던짐에 묶고,
        /// 묶인 던짐과 다른 틀린 던짐이 보이면(보정) 버린다.
        /// </summary>
        public static CardRushSpinMemory Observe(in CardRushSpinMemory memory, in CardRushRunnerState s, in CardRushTiming timing)
        {
            if (s.SpinStartTick != 0)
            {
                return Of(s);
            }
            if (memory.Tick == 0 || s.StunEndTick == 0 || CardRushThrowFx.IsRightThrow(s, timing))
            {
                return memory;
            }
            long wrongAt = s.StunEndTick - timing.StunTicks;
            if (memory.BoundThrow == 0)
            {
                return memory.Tick <= wrongAt ? new CardRushSpinMemory(memory.Step, memory.Attempt, memory.Tick, wrongAt) : memory;
            }
            return memory.BoundThrow == wrongAt ? memory : default;
        }
    }

    /// <summary>
    /// 던진 카드의 그림 시간표(spec 2026-11-03 §5.3) — 상태의 던짐 틱(맞음 = MoveStartTick, 틀림 = StunEndTick − StunTicks)에서 잰다.
    /// 그림만이다: 몸 이동·멈춤은 시뮬대로 그 틱부터 간다.
    /// </summary>
    public static class CardRushThrowFx
    {
        public const int FlightTicks = 3;   // 손 → 문
        public const int BounceTicks = 4;   // 틀림: 문 → 얼굴
        public const int SlapTick = FlightTicks + BounceTicks;
        //  철썩 뒤 얼굴에 붙어 있다가 떨어져 사라지기까지. 몸이 뒤로 벌러덩(멈춤 0.30~0.45)하며 얼굴이 하늘을 볼 때까지 붙어 있어야
        //  뒤 위 카메라에 "얼굴에 카드" 가 보인다 — 5틱이면 얼굴이 앞(문 쪽)을 보는 동안 다 떨어져 몸에 가렸다(실제 판 스크린샷).
        public const int DropTicks = 14;

        /// <summary>
        /// 날아가는 카드가 지금 어디쯤인가. <c>BackToFace</c>에서 <c>since &lt; SlapTick</c>이면 문 → 얼굴 비행,
        /// 그 뒤 <see cref="DropTicks"/>는 얼굴에 붙었다 떨어짐 — 뷰가 since로 나눈다.
        /// </summary>
        public static (CardRushThrowKind kind, double since) Phase(in CardRushRunnerState s, double drawnTick, in CardRushTiming timing)
        {
            long wrongAt = WrongThrowTick(s, timing);
            long rightAt = s.MoveStartTick == 0 ? long.MinValue : s.MoveStartTick;
            if (wrongAt > rightAt)
            {
                double since = drawnTick - wrongAt;
                if (since >= 0 && since < FlightTicks) return (CardRushThrowKind.ToDoor, since);
                if (since >= FlightTicks && since < SlapTick + DropTicks) return (CardRushThrowKind.BackToFace, since);
            }
            else if (rightAt != long.MinValue)
            {
                double since = drawnTick - rightAt;
                if (since >= 0 && since < FlightTicks) return (CardRushThrowKind.ToDoor, since);
            }
            return (CardRushThrowKind.None, 0d);
        }

        /// <summary>칸 step의 문에 카드가 꽂혀 있나 — 건넌 문, 방금 맞힌 문은 날아간 카드가 닿은 뒤부터.</summary>
        public static bool IsStuckIn(in CardRushRunnerState s, double drawnTick, int step)
        {
            if (step >= s.Step) return false;
            if (step < s.Step - 1 || s.MoveStartTick == 0) return true;
            return drawnTick >= s.MoveStartTick + FlightTicks;
        }

        /// <summary>
        /// 마지막으로 던진 카드의 색(원장 판정: 틀린 카드도 던진 색으로 그린다). 맞음이면 그 칸의 요구 색,
        /// 틀림이면 던지기 전 상태(같은 칸·시도 −1·돌기 시작 틱)로 던진 틱의 색을 다시 센다 —
        /// 던지면 상태의 <c>SpinStartTick</c>이 0으로 지워지므로 돌기 시작 틱은 그리는 쪽이 기억해 넘긴다.
        /// 모르면(0, 던짐보다 늦음) false — 뒷면으로 그린다.
        /// </summary>
        public static bool TryThrownColor(in CardRushRunnerState s, long spinStartTick, CardRushCourse course,
                                          in CardRushTiming timing, out CardColor color)
        {
            color = default;
            if (course == null) return false;
            long wrongAt = WrongThrowTick(s, timing);
            long rightAt = s.MoveStartTick == 0 ? long.MinValue : s.MoveStartTick;
            if (wrongAt > rightAt)
            {
                if (s.Attempt < 1 || spinStartTick == 0 || spinStartTick > wrongAt) return false;
                var before = new CardRushRunnerState { Step = s.Step, Attempt = s.Attempt - 1, SpinStartTick = spinStartTick };
                return CardRushCard.TryColorAt(before, wrongAt, course, out color);
            }
            if (rightAt == long.MinValue || s.Step < 1) return false;
            color = course.RequiredColor(s.Step - 1);
            return true;
        }

        /// <summary>
        /// 날아가는 카드의 앞면 색(뷰가 부르는 길). 맞음이면 그 칸 요구 색. 틀림이면 기억한 시작이 바로 그 던짐의 것일 때만
        /// (같은 칸, 시도 = 지금 − 1, 던짐 틱에서 창 <see cref="CardRushCard.WindowTicks"/> 안) 던진 색을 센다 — 남의 몸에서 시작을 못 보고
        /// 앞 시도의 시작이 남았으면 거짓 색 대신 뒷면(false).
        /// </summary>
        public static bool TryThrownFace(in CardRushRunnerState s, in CardRushSpinMemory memory, CardRushCourse course,
                                         in CardRushTiming timing, out CardColor color)
        {
            long spin = 0;
            if (IsRightThrow(s, timing) == false)
            {
                long wrongAt = WrongThrowTick(s, timing);
                bool sameThrow = memory.Tick != 0 && memory.Step == s.Step && memory.Attempt == s.Attempt - 1
                                 && (memory.BoundThrow == 0 || memory.BoundThrow == wrongAt)
                                 && memory.Tick <= wrongAt && wrongAt - memory.Tick <= CardRushCard.WindowTicks;
                spin = sameThrow ? memory.Tick : 0;
            }
            return TryThrownColor(s, spin, course, timing, out color);
        }

        /// <summary>
        /// 맞힌 카드가 문에 꽂혀 보이기 시작했나 — 꽂힌 판은 코스 그림 틱(<paramref name="courseTick"/>)으로 켜진다.
        /// 날아가는 판을 그 틱에 숨겨, 두 그림 틱 기준이 1틱 달라도 두 장이 겹치지 않게 한다.
        /// </summary>
        public static bool FlyingHiddenByStuck(in CardRushRunnerState s, long courseTick, in CardRushTiming timing)
            => IsRightThrow(s, timing) && s.Step >= 1 && IsStuckIn(s, courseTick, s.Step - 1);

        /// <summary>마지막 던짐이 맞음인가(맞음이면 날아간 문은 칸 Step − 1, 틀림이면 칸 Step).</summary>
        public static bool IsRightThrow(in CardRushRunnerState s, in CardRushTiming timing)
            => s.MoveStartTick != 0 && s.MoveStartTick > WrongThrowTick(s, timing);

        //  둘 다 있으면 더 늦은 던짐이 지금 것이다(맞음과 틀림은 같은 틱에 날 수 없다).
        private static long WrongThrowTick(in CardRushRunnerState s, in CardRushTiming timing)
            => s.StunEndTick == 0 ? long.MinValue : s.StunEndTick - timing.StunTicks;
    }
}
