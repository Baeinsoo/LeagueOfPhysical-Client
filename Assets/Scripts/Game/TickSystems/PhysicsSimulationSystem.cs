namespace LOP
{
    /// <summary>구 LOPRunner.SimulatePhysics 이동. World.Transform → rb 팔로우 후 PhysX 스텝.</summary>
    public class PhysicsSimulationSystem : GameFramework.Runner.ITickSystem
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.IMotionBridge motionBridge;
        private readonly GameFramework.Physics.IPhysicsSimulator physicsSimulator;

        public PhysicsSimulationSystem(GameFramework.World.EntityRegistry entityRegistry, GameFramework.World.IMotionBridge motionBridge, GameFramework.Physics.IPhysicsSimulator physicsSimulator)
        {
            this.entityRegistry = entityRegistry;
            this.motionBridge = motionBridge;
            this.physicsSimulator = physicsSimulator;
        }

        public void Tick(long tick, float deltaTime)
        {
            // World.Transform → rb 팔로우: PhysicsBody 가진 모든 엔티티(내 캐릭=예측, 남·아이템=보간).
            // Simulated는 world.Tick서 이미 밀렸으나 idempotent. per-entity LOPEntityController 대체.
            // 단 다이나믹 몸은 제외한다 — 그건 물리 엔진이 굴리므로 rb가 진실원본이고,
            // 밀어넣으면 한 틱 낡은 World 값으로 속도·회전을 매 틱 덮어써 제대로 구르지 못한다.
            foreach (var entity in entityRegistry.All)
            {
                var body = entity.Get<GameFramework.World.PhysicsBody>();
                if (body != null && body.IsKinematic == false)
                {
                    continue;
                }
                motionBridge.PushMotion(entity);
            }

            physicsSimulator.Simulate(deltaTime);

            // 물리 엔진이 굴린 결과를 World로 되읽는다 — 스냅샷은 World만 보기 때문이다.
            foreach (var entity in entityRegistry.All)
            {
                var body = entity.Get<GameFramework.World.PhysicsBody>();
                if (body == null || body.IsKinematic)
                {
                    continue;
                }
                var transform = entity.Get<GameFramework.World.Transform>();
                var velocity = entity.Get<GameFramework.World.Velocity>();
                if (transform == null || velocity == null)
                {
                    continue;
                }
                transform.Position = body.GetPosition();
                transform.Rotation = body.GetRotation();
                velocity.Linear = body.GetVelocity();
            }
        }
    }
}
