namespace LOP
{
    /// <summary>
    /// 맞음·무적을 눈에 보이게 하는 규칙(클라, 연출만). 판정은 서버가 하고 여기는 받은 상태를 읽기만 한다(스펙 §5.4).
    /// </summary>
    public static class DodgeHitFeedback
    {
        /// <summary>무적 동안 이 틱마다 켜고 끈다(0.1초).</summary>
        public const int BlinkTicks = 5;
        /// <summary>맞은 순간 화면 가장자리를 번쩍이는 길이(0.36초).</summary>
        public const int FlashTicks = 18;

        /// <summary>무적 끝 틱 전이면 깜빡인다. 끝 틱부터는 서버가 다시 맞힐 수 있으니 늘 보인다.</summary>
        public static bool BodyVisible(long renderTick, long invulnerableUntilTick)
        {
            long left = invulnerableUntilTick - renderTick;
            if (left <= 0)
            {
                return true;
            }
            return (left - 1) / BlinkTicks % 2 == 1;
        }

        /// <summary>목숨이 줄었으면 지금을 맞은 틱으로. 처음 알게 된 순간(모름 → 앎)은 맞음이 아니다.</summary>
        public static long NextHitTick(bool knewBefore, int livesBefore, int livesNow, long hitTick, long tick)
            => knewBefore && livesNow < livesBefore ? tick : hitTick;

        public static bool Flashing(long tick, long hitTick) => hitTick >= 0 && tick - hitTick < FlashTicks;

        /// <summary>무적 동안 담담하게 놀란 얼굴 — 캐릭터는 웃기려 하지 않는다(테마 스펙 §1).</summary>
        public static ChibiExpression ExpressionFor(long renderTick, long invulnerableUntilTick) =>
            renderTick < invulnerableUntilTick ? ChibiExpression.Surprise : ChibiExpression.Normal;
    }
}
