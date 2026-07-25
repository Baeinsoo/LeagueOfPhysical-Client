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
            foreach (var entity in entityRegistry.All)
            {
                motionBridge.PushMotion(entity);
            }

            physicsSimulator.Simulate(deltaTime);
        }
    }
}
