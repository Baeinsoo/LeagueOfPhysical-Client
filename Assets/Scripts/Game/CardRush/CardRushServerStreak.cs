namespace LOP
{
    /// <summary>
    /// 스냅으로 받은 연속 무사 통과(card_clean_streak)의 최댓값 — 서버가 확정한 값만 모은다(클라 전용, 시뮬이 안 읽는다).
    /// <see cref="CardRushRunnerState.BestCleanStreak"/>는 와이어에 없는 예측값이라, 서버가 버린 탭의 통과가 되감기 앵커에서
    /// 살아남을 수 있다(2차 최종 리뷰 I3). 개인 기록 "연속 무사 통과"는 이 값을 적는다.
    /// </summary>
    public sealed class CardRushServerStreak : GameFramework.World.Component
    {
        public int Best;

        /// <summary>스냅 하나를 본다. 몸에 이 부품이 없으면(다른 종목) 아무것도 안 한다.</summary>
        public static void Observe(GameFramework.World.Entity entity, int serverStreak)
        {
            var tracked = entity?.Get<CardRushServerStreak>();
            if (tracked != null && serverStreak > tracked.Best)
            {
                tracked.Best = serverStreak;
            }
        }
    }
}
