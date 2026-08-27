namespace LOP
{
    /// <summary>
    /// Flappy Race의 서버 보정 — 스턴이 서버 권위다.
    ///
    /// 남의 새까지 클라가 굴리게 되면서 "남이 벽에 부딪혀 멈췄나"도 클라가 예측하게 됐다.
    /// 그 판정이 서버와 갈리면 0.8초 얼음이 통째로 어긋나므로, 위치가 맞아도 되돌린다.
    /// </summary>
    public class FlappyServerCorrectionHandler : IServerCorrectionHandler
    {
        private readonly FlappyWorld world;       // 같은 게임 안이므로 구체를 직접 본다
        private readonly GameFramework.Runner.IRunner runner;

        public FlappyServerCorrectionHandler(FlappyWorld world, GameFramework.Runner.IRunner runner)
        {
            this.world = world;
            this.runner = runner;
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

        public void ApplyAuthoritative(GameFramework.World.Entity entity, EntitySnap snap)
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
            float interval = (float)runner.tickUpdater.interval;
            stun.StunRemaining = RemainingSeconds(snap.stunEndTick, tick: snap.tick, interval);
            stun.InvulnRemaining = RemainingSeconds(snap.invulnEndTick, tick: snap.tick, interval);
        }

        /// <summary>끝나는 절대 틱에서 지금 틱을 빼 남은 시간(초)으로 바꾼다. 이미 지났거나(0 포함) 같으면 0.</summary>
        public static float RemainingSeconds(long endTick, long tick, float interval)
        {
            long remainingTicks = endTick - tick;
            return remainingTicks > 0 ? remainingTicks * interval : 0f;
        }
    }
}
