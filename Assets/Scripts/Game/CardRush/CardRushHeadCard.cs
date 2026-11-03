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
    }
}
