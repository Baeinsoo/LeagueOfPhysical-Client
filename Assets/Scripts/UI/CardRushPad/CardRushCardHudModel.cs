namespace LOP.UI
{
    /// <summary>카드 HUD 한 장. 지금 색(뒷면 여부 포함)·다음 칩·연속 수의 순수 함수(spec §7.3).</summary>
    public readonly struct CardRushCardFace
    {
        /// <summary>참이면 앞면(색이 보인다). 거짓이면 뒷면(이동·멈춤·출발 전·완주).</summary>
        public readonly bool FaceUp;

        /// <summary>앞면일 때의 색. 뒷면이면 의미 없다(default).</summary>
        public readonly CardColor Color;

        /// <summary>다음 몇 칸의 요구 색(미리 읽기). 남은 칸이 모자라면 그만큼만, 완주면 비어 있다.</summary>
        public readonly CardColor[] Next;

        public readonly int Streak;

        public CardRushCardFace(bool faceUp, CardColor color, CardColor[] next, int streak)
        {
            FaceUp = faceUp;
            Color = color;
            Next = next;
            Streak = streak;
        }
    }

    /// <summary>
    /// 카드 HUD를 (선수 상태, 그림 틱)에서 계산한다. 색 판정은 <see cref="CardRushCard.TryColorAt"/>를 그대로 쓴다 —
    /// 여기서 틱을 건드리지 않는다.
    ///
    /// <para><paramref name="displayTick"/>은 반드시 <c>PlayerInputManager.NextInputTick</c>(다음 입력이 달고 갈 틱)이어야
    /// 한다(spec §7.3). 보간 틱 등 다른 틱을 쓰면 "보고 누른 색"과 "판정 색"이 색 경계에서 한 틱 어긋난다 —
    /// 이것이 §2.5의 핵심 전제("판정 틱 = 내가 본 틱")가 화면에서 지켜지는 자리다.</para>
    /// </summary>
    public static class CardRushCardHudModel
    {
        public static CardRushCardFace Compute(in CardRushRunnerState s, long displayTick, long gameplayStartTick,
            CardRushCourse course, int moveTicks, int nextCount = 3)
        {
            bool faceUp = CardRushCard.TryColorAt(s, displayTick, course, out var color);

            int count = course == null ? 0 : System.Math.Clamp(course.StepCount - s.Step, 0, nextCount);
            var next = count == 0 ? System.Array.Empty<CardColor>() : new CardColor[count];
            for (int i = 0; i < count; i++)
            {
                next[i] = course.RequiredColor(s.Step + i);
            }

            return new CardRushCardFace(faceUp, color, next, s.Streak);
        }
    }
}
