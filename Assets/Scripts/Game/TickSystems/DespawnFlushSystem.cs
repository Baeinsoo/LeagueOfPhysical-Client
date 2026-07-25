namespace LOP
{
    /// <summary>구 LOPRunner.EndUpdate(디스폰 플러시 부분) 이동. 파이프라인 맨 마지막 — 확정된 디스폰을 실제로 반영.</summary>
    public class DespawnFlushSystem : GameFramework.Runner.ITickSystem
    {
        private readonly EntitySpawner entitySpawner;

        public DespawnFlushSystem(EntitySpawner entitySpawner)
        {
            this.entitySpawner = entitySpawner;
        }

        public void Tick(long tick, float deltaTime)
        {
            entitySpawner.FlushDespawns();
        }
    }
}
