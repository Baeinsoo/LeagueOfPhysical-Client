namespace LOP
{
    /// <summary>
    /// 카드 러시의 서버 보정 — 맞힘·틀림 판정은 서버가 정한다. 내 몸은 클라가 예측하지만, 탭이 서버에 늦게 닿아
    /// 다른 틱에 판정되면 칸·멈춤이 갈린다. 자리는 상태에서 계산되므로 위치가 아직 같아도 상태가 다르면 되감는다.
    /// </summary>
    public class CardRushServerCorrectionHandler : IServerCorrectionHandler
    {
        private readonly CardRushWorld world;

        public CardRushServerCorrectionHandler(CardRushWorld world)
        {
            this.world = world;
        }

        //  같은 시점끼리 비교한다 — 그 틱에 내가 예측했던 상태 vs 서버가 그 틱에 가진 상태.
        //  다섯 칸 모두 본다. 하나라도 빠지면 그 칸만 다른 어긋남을 영영 못 고친다.
        public bool Matches(long tick, EntitySnap snap)
        {
            if (!world.TryGetSaved(tick, snap.entityId, out var predicted))
            {
                return true;
            }
            return predicted.Step == snap.cardStep
                && predicted.Attempt == snap.cardAttempt
                && predicted.MoveStartTick == snap.cardMoveStartTick
                && predicted.StunEndTick == snap.cardStunEndTick
                && predicted.Streak == snap.cardStreak;
        }

        public void ApplyAuthoritative(GameFramework.World.Entity entity, EntitySnap snap, float deltaTime)
        {
            var runner = entity.Get<CardRushRunner>();
            if (runner == null)
            {
                return;
            }
            ApplyCardState(ref runner.State, snap);
        }

        /// <summary>
        /// 스냅의 다섯 칸을 상태에 넣는다. 최고 연속·틀린 수는 서버가 안 보내므로 지금 값을 지킨다
        /// (최고 연속은 지금 연속보다 작을 수 없어 max로 맞춘다). 남의 몸에도 같은 식을 쓴다.
        /// 이 최고 연속은 <b>예측값</b>이다(되감기 앵커의 예측 정타가 남을 수 있다) — 기록은 <see cref="CardRushServerStreak"/>를 본다.
        /// </summary>
        public static void ApplyCardState(ref CardRushRunnerState state, EntitySnap snap)
        {
            state.Step = snap.cardStep;
            state.Attempt = snap.cardAttempt;
            state.MoveStartTick = snap.cardMoveStartTick;
            state.StunEndTick = snap.cardStunEndTick;
            state.Streak = snap.cardStreak;
            state.BestStreak = System.Math.Max(state.BestStreak, snap.cardStreak);
        }
    }
}
