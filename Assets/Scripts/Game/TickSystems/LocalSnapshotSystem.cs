namespace LOP
{
    /// <summary>
    /// 구 LOPRunner.RecordLocalSnapshot 이동. 내 캐릭의 이번 틱 최종 시뮬 상태를 스냅샷에 남긴다.
    /// End 디스패치(=LocalEntityInterpolator의 지연 렌더링용 틱 기록) 전에 찍어, 뷰 보간이 얹히기 전
    /// 원본 예측 상태를 포착한다. 되돌리기(하드 복원+재생)는 Reconciler.Reconcile이 다음 틱 앞에서 수행.
    /// </summary>
    public class LocalSnapshotSystem : GameFramework.Runner.ITickSystem
    {
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.Netcode.SnapshotHistory snapshotHistory;
        private readonly GameFramework.Netcode.SequenceBuffer<PredictedAbilityState> predictedAbilityStateHistory;

        public LocalSnapshotSystem(
            IPlayerContext playerContext,
            GameFramework.World.EntityRegistry entityRegistry,
            GameFramework.Netcode.SnapshotHistory snapshotHistory,
            GameFramework.Netcode.SequenceBuffer<PredictedAbilityState> predictedAbilityStateHistory)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.snapshotHistory = snapshotHistory;
            this.predictedAbilityStateHistory = predictedAbilityStateHistory;
        }

        public void Tick(long tick, float deltaTime)
        {
            string entityId = playerContext.entityId;
            if (entityId == null)
            {
                return;
            }

            GameFramework.World.Entity worldEntity = entityRegistry.Get(entityId);
            if (worldEntity == null)
            {
                return;
            }

            var transform = worldEntity.Get<GameFramework.World.Transform>();
            var velocity = worldEntity.Get<GameFramework.World.Velocity>();
            if (transform == null || velocity == null)
            {
                return;
            }

            snapshotHistory.Record(new GameFramework.Netcode.EntitySnapshot(
                tick,
                transform.Position,
                transform.Rotation,
                velocity.Linear));

            predictedAbilityStateHistory.Record(tick, PredictedAbilityState.Capture(worldEntity));
        }
    }
}
