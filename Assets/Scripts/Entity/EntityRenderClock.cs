namespace LOP
{
    /// <summary>
    /// 이 엔티티의 연출을 "몇 틱 시점"으로 그릴지 답한다.
    /// 내 캐릭은 예측 틱(지금), 남은 보간 재생 시계(조금 과거) — 위치와 애니가 같은 시점을 보게 맞춘다.
    /// </summary>
    public class EntityRenderClock
    {
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.Runner.IRunner runner;
        private readonly RemoteInterpolationClock remoteClock;
        private readonly IGameDataStore gameDataStore;

        public EntityRenderClock(IPlayerContext playerContext, GameFramework.Runner.IRunner runner,
                                 RemoteInterpolationClock remoteClock, IGameDataStore gameDataStore)
        {
            this.playerContext = playerContext;
            this.runner = runner;
            this.remoteClock = remoteClock;
            this.gameDataStore = gameDataStore;
        }

        /// <summary>틱 하나가 몇 초인지. 아직 게임 정보를 못 받았으면 0.</summary>
        public double SecondsPerTick => gameDataStore.gameInfo != null ? gameDataStore.gameInfo.Interval : 0d;

        /// <summary>
        /// 이 엔티티의 연출을 "몇 초 시점"으로 그릴지. <see cref="TickFor"/>와 같은 시간축의
        /// <b>연속값</b>이다.
        ///
        /// <para>매 프레임 조금씩 움직여야 하는 것(추격자 벽 등)은 이쪽을 쓴다. 틱으로 자르면
        /// 0.02초 단위로 끊겨서, 60fps 화면에서는 여섯 프레임 중 하나가 제자리에 선다 —
        /// 10m/s로 오는 물체라면 0.2m씩 점프한다. 반대로 애니메이션처럼 "그 사건이 몇 틱 전에
        /// 일어났나"를 묻는 쪽은 <see cref="TickFor"/>가 맞다.</para>
        /// </summary>
        public double TimeFor(string entityId)
        {
            if (entityId != null && entityId == playerContext.entityId)
            {
                return runner.tickUpdater?.elapsedTime ?? 0d;
            }

            if (remoteClock.HasSnapshot == false || SecondsPerTick <= 0d)
            {
                return runner.tickUpdater?.elapsedTime ?? 0d;
            }
            return remoteClock.RenderTime;
        }

        public long TickFor(string entityId)
        {
            if (entityId != null && entityId == playerContext.entityId)
            {
                return runner.tickUpdater?.tick ?? 0;
            }

            // 재생 시계는 초 단위(서버 타임라인)라 틱 간격으로 나눠 틱으로 되돌린다.
            double interval = SecondsPerTick;
            if (remoteClock.HasSnapshot == false || interval <= 0d)
            {
                return runner.tickUpdater?.tick ?? 0;
            }
            return (long)(remoteClock.RenderTime / interval);
        }
    }
}
