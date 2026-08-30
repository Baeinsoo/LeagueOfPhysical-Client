namespace LOP
{
    /// <summary>
    /// Flappy Race의 서버 보정 — 스턴이 서버 권위다.
    ///
    /// 클라는 내 새를 직접 굴리므로 "내가 벽에 부딪혔나"도 예측한다. 그 판정이 서버와 갈리면
    /// 0.8초 얼음이 통째로 어긋나므로, 위치가 맞아도 되돌린다.
    /// (남의 새는 굴리지 않는다 — 보간으로 그리고 스턴 겉모습도 스냅에서 온다.)
    /// </summary>
    public class FlappyServerCorrectionHandler : IServerCorrectionHandler
    {
        private readonly FlappyWorld world;       // 같은 게임 안이므로 구체를 직접 본다

        public FlappyServerCorrectionHandler(FlappyWorld world)
        {
            this.world = world;
        }

        //  비교는 반드시 같은 시점끼리 한다 — 앵커 틱에 "내가 그때 예측했던" 스턴 vs 서버가 그 틱에
        //  갖고 있던 스턴. 지금 살아있는 값과 비교하면 클라가 앞서 달리는 리드 구간 내내 시점이
        //  어긋나 보여, 스턴이 걸리거나 풀릴 때마다 불필요한 되돌리기가 난다.
        public bool Matches(long tick, EntitySnap snap)
        {
            //  앵커 틱 기록이 없으면(정상 경로엔 없는 엣지) 비교 불가 — 불일치로 단정하지 않고
            //  위치 판정에 맡긴다.
            if (!world.TryGetSavedStun(tick, snap.entityId, out var predicted))
            {
                return true;
            }
            return StunMatches(predicted.StunRemaining, predicted.InvulnRemaining,
                               snap.stunEndTick, snap.invulnEndTick, tick);
        }

        /// <summary>켜짐/꺼짐만 본다. 남은 시간의 미세한 차이는 다음 틱 시뮬이 알아서 좁힌다.</summary>
        public static bool StunMatches(float predictedStun, float predictedInvuln,
                                       long snapStunEnd, long snapInvulnEnd, long tick)
        {
            return (predictedStun > 0f) == (snapStunEnd > tick)
                && (predictedInvuln > 0f) == (snapInvulnEnd > tick);
        }

        public void ApplyAuthoritative(GameFramework.World.Entity entity, EntitySnap snap, float deltaTime)
        {
            var stun = entity.Get<FlappyStun>();
            if (stun == null)
            {
                return;
            }
            //  끝나는 틱에서 남은 시간을 되계산한다. 불리언이었다면 여기서 전체 시간을 새로 채울
            //  수밖에 없어, 서버가 이미 절반쯤 지난 스턴을 처음부터 다시 시작하게 만든다.
            //  snap.tick을 기준 시점으로 쓴다 — 한 배치의 스냅은 전부 같은 틱이라고 Reconciler가
            //  보장해 주므로(위 Matches의 tick과 같은 값), 여기서 따로 받지 않아도 된다.
            //  틱 간격은 되감기를 구동하는 쪽이 넘겨 준다(러너에서 직접 꺼내면 DI에 고리가 생긴다).
            //  변환은 "끝났다"를 아는 곳(FlappyStunSystem)에 있다. 여기에 따로 두면 시뮬이 끝으로
            //  보는 기준과 와이어가 세는 기준이 갈려, 클라의 새만 한 틱 더 얼어 있게 된다(실측).
            stun.StunRemaining = FlappyStunSystem.RemainingSeconds(snap.stunEndTick, snap.tick, deltaTime);
            stun.InvulnRemaining = FlappyStunSystem.RemainingSeconds(snap.invulnEndTick, snap.tick, deltaTime);
        }
    }
}
