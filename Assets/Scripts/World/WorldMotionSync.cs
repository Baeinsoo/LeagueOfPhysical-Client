using GameFramework;
using LOP.Event.LOPGameEngine.Update;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 코어 밖(어댑터) 단방향 모션 미러. 매 틱 AfterPhysicsSimulation(= SyncPhysics 직후)에
    /// LOPEntity(진실원본)의 position/rotation/velocity를 변환해 순수 C#
    /// World.Transform/World.Velocity(미러)에 기록한다. 코어는 이 동기화를 모른다(Model A/DIP).
    /// </summary>
    public class WorldMotionSync : MonoBehaviour, ICleanup
    {
        [Inject]
        private IGameEngine gameEngine;

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        private LOPEntity entity;
        private GameFramework.World.Transform worldTransform;
        private GameFramework.World.Velocity worldVelocity;

        public void SetEntity(LOPEntity entity)
        {
            this.entity = entity;
        }

        protected virtual void Start()
        {
            GameFramework.World.Entity worldEntity = entityRegistry.Get(entity.entityId);
            worldTransform = worldEntity?.Get<GameFramework.World.Transform>();
            worldVelocity = worldEntity?.Get<GameFramework.World.Velocity>();
            gameEngine.AddListener(this);
        }

        public void Cleanup()
        {
            gameEngine.RemoveListener(this);
            worldTransform = null;
            worldVelocity = null;
            entity = null;
        }

        [GameEngineListen(typeof(AfterPhysicsSimulation))]
        private void OnAfterPhysicsSimulation()
        {
            if (worldTransform != null)
            {
                worldTransform.Position = entity.position.ToNumerics();
                worldTransform.Rotation = Quaternion.Euler(entity.rotation).ToNumerics();
            }

            if (worldVelocity != null)
            {
                worldVelocity.Linear = entity.velocity.ToNumerics();
            }
        }
    }
}
