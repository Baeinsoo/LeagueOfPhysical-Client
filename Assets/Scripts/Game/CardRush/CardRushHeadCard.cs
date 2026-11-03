namespace LOP
{
    /// <summary>머리 위 카드 한 장의 모습(spec 2026-11-03 §5.1). 뷰는 이것을 그리기만 한다.</summary>
    public readonly struct CardRushHeadCardFace
    {
        public readonly bool Visible;       // 거짓이면 숨김 — 이동·멈춤(카드가 손을 떠났다)·완주
        public readonly bool FaceUp;        // 참이면 돌고 있다(색이 보인다). 거짓이면 뒷면(출발 전·대기)
        public readonly CardColor Color;
        public readonly float Remaining01;  // 자동 던짐까지 남은 몫(1 → 0). 돌 때만 의미

        public CardRushHeadCardFace(bool visible, bool faceUp, CardColor color, float remaining01)
        {
            Visible = visible; FaceUp = faceUp; Color = color; Remaining01 = remaining01;
        }
    }

    public static class CardRushHeadCard
    {
        /// <summary>
        /// 내 카드는 <c>PlayerInputManager.NextInputTick</c>으로, 남의 카드는 그림 틱으로 부른다 —
        /// 내 카드를 다른 틱으로 칠하면 색 경계에서 "보고 누른 색 ≠ 판정 색"이 된다.
        /// </summary>
        public static CardRushHeadCardFace Compute(in CardRushRunnerState s, long tick, long gameplayStartTick,
                                                   CardRushCourse course, in CardRushTiming timing)
        {
            if (course == null || s.IsFinished(course.StepCount)) return default;
            //  출발 전엔 뒷면으로 띄워 무엇을 누를지 보여 준다.
            if (gameplayStartTick == long.MaxValue || tick < gameplayStartTick) return new CardRushHeadCardFace(true, false, default, 1f);
            if (tick < CardRushCard.ReadyTick(s, gameplayStartTick, timing.MoveTicks)) return default;
            if (CardRushCard.TryColorAt(s, tick, course, out var color))
            {
                float remaining = 1f - (float)(tick - s.SpinStartTick) / CardRushCard.WindowTicks;
                return new CardRushHeadCardFace(true, true, color, UnityEngine.Mathf.Clamp01(remaining));
            }
            return new CardRushHeadCardFace(true, false, default, 1f);
        }

        /// <summary>
        /// 남의 카드(그림 틱). 남의 상태는 최신 스냅이라 그려지는 몸보다 앞선다 — 그림 틱이 마지막 던짐보다 이르면 아직 손에 있다.
        /// 그때는 던지기 전 상태(칸·시도는 던짐에서 되짚고, 시작 틱은 <paramref name="memory"/>가 그 던짐의 것일 때만)로 칠한다.
        /// 시작을 모르면 사라지지 않고 뒷면으로 남는다(최종 리뷰 m3 — 보간 지연만큼 카드가 먼저 증발했다).
        /// </summary>
        public static CardRushHeadCardFace ComputeDrawn(in CardRushRunnerState s, long drawnTick, long gameplayStartTick,
                                                        CardRushCourse course, in CardRushTiming timing, in CardRushSpinMemory memory)
        {
            if (course == null)
            {
                return default;
            }
            bool right = CardRushThrowFx.IsRightThrow(s, timing);
            long throwAt = right ? s.MoveStartTick : s.StunEndTick == 0 ? 0 : s.StunEndTick - timing.StunTicks;
            if (throwAt == 0 || drawnTick >= throwAt)
            {
                return Compute(s, drawnTick, gameplayStartTick, course, timing);
            }

            int step = right ? s.Step - 1 : s.Step;
            bool known = memory.Tick != 0 && memory.Step == step && (right || memory.Attempt == s.Attempt - 1)
                         && memory.Tick <= throwAt && throwAt - memory.Tick <= CardRushCard.WindowTicks
                         && (right || memory.BoundThrow == 0 || memory.BoundThrow == throwAt);
            if (known == false || drawnTick < memory.Tick)
            {
                return new CardRushHeadCardFace(true, false, default, 1f);
            }
            var before = new CardRushRunnerState { Step = step, Attempt = memory.Attempt, SpinStartTick = memory.Tick };
            if (CardRushCard.TryColorAt(before, drawnTick, course, out var color))
            {
                float remaining = 1f - (float)(drawnTick - memory.Tick) / CardRushCard.WindowTicks;
                return new CardRushHeadCardFace(true, true, color, UnityEngine.Mathf.Clamp01(remaining));
            }
            return new CardRushHeadCardFace(true, false, default, 1f);
        }
    }
}
