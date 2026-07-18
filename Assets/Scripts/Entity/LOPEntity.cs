using GameFramework;
using System.Linq;
using UnityEngine;

namespace LOP
{
    public class LOPEntity : MonoBehaviour, IEntity
    {
        public string entityId { get; private set; }

        private GameFramework.World.Transform worldTransform;
        private GameFramework.World.Velocity worldVelocity;

        /// <summary>크리에이터가 이 엔티티의 World.Entity 모션 컴포넌트를 연결한다(파사드 백킹). Initialize 전에 호출.</summary>
        public void LinkWorldMotion(GameFramework.World.Transform transform, GameFramework.World.Velocity velocity)
        {
            this.worldTransform = transform;
            this.worldVelocity = velocity;
        }

        public Vector3 position
        {
            get => worldTransform.Position.ToUnity();
            set
            {
                var current = worldTransform.Position.ToUnity();
                if (Vector3EqualityComparer.instance.Equals(current, value)) return;
                worldTransform.Position = value.ToNumerics();
            }
        }

        public Vector3 rotation
        {
            get => worldTransform.Rotation.ToUnity().eulerAngles;
            set
            {
                var current = worldTransform.Rotation.ToUnity().eulerAngles;
                if (Vector3EqualityComparer.instance.Equals(current, value)) return;
                worldTransform.Rotation = Quaternion.Euler(value).ToNumerics();
                // rotation 변경 이벤트는 소비자가 없어 발행하지 않는다(연속 상태는 pull). 값만 쓴다.
            }
        }

        public Vector3 velocity
        {
            get => worldVelocity.Linear.ToUnity();
            set
            {
                var current = worldVelocity.Linear.ToUnity();
                if (Vector3EqualityComparer.instance.Equals(current, value)) return;
                worldVelocity.Linear = value.ToNumerics();
                // velocity 변경 이벤트는 소비자가 없어 발행하지 않는다(연속 상태는 pull). 값만 쓴다.
            }
        }

        public virtual void Initialize<TEntityCreationData>(TEntityCreationData creationData) where TEntityCreationData : struct, IEntityCreationData
        {
            entityId = creationData.entityId;
            // 모션(position/rotation/velocity)은 크리에이터가 World.Transform/Velocity(진실원본)에 직접 시드한다.
        }

        // TODO: 고도화 필요! (구 GameFramework IsGrounded 확장에서 이전)
        public bool IsGrounded()
        {
            Vector3 checkPosition = position + Vector3.down * 0.2f;
            Collider[] colliders = Physics.OverlapSphere(checkPosition, 0.4f);
            return colliders.Any(col => col.gameObject.name == "Plane");
        }
    }
}
