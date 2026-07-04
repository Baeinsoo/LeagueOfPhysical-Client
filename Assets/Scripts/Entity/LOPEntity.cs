using GameFramework;
using LOP.Event.Entity;
using System.ComponentModel;
using UnityEngine;

namespace LOP
{
    public class LOPEntity : MonoEntity
    {
        private GameFramework.World.Transform worldTransform;
        private GameFramework.World.Velocity worldVelocity;

        /// <summary>크리에이터가 이 엔티티의 World.Entity 모션 컴포넌트를 연결한다(파사드 백킹). Initialize 전에 호출.</summary>
        public void LinkWorldMotion(GameFramework.World.Transform transform, GameFramework.World.Velocity velocity)
        {
            this.worldTransform = transform;
            this.worldVelocity = velocity;
        }

        public override Vector3 position
        {
            get => worldTransform.Position.ToUnity();
            set
            {
                var current = worldTransform.Position.ToUnity();
                if (Vector3EqualityComparer.instance.Equals(current, value)) return;
                worldTransform.Position = value.ToNumerics();
                RaisePropertyChanged(this, new PropertyChangedEventArgs(nameof(position)));
            }
        }

        public override Vector3 rotation
        {
            get => worldTransform.Rotation.ToUnity().eulerAngles;
            set
            {
                var current = worldTransform.Rotation.ToUnity().eulerAngles;
                if (Vector3EqualityComparer.instance.Equals(current, value)) return;
                worldTransform.Rotation = Quaternion.Euler(value).ToNumerics();
                RaisePropertyChanged(this, new PropertyChangedEventArgs(nameof(rotation)));
            }
        }

        public override Vector3 velocity
        {
            get => worldVelocity.Linear.ToUnity();
            set
            {
                var current = worldVelocity.Linear.ToUnity();
                if (Vector3EqualityComparer.instance.Equals(current, value)) return;
                worldVelocity.Linear = value.ToNumerics();
                RaisePropertyChanged(this, new PropertyChangedEventArgs(nameof(velocity)));
            }
        }

        public void RaisePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            EventBus.Default.Publish(EventTopic.EntityId<LOPEntity>(entityId), new PropertyChange(e.PropertyName));
        }

        public virtual void Initialize<TEntityCreationData>(TEntityCreationData creationData) where TEntityCreationData : struct, IEntityCreationData
        {
            entityId = creationData.entityId;
            // 모션(position/rotation/velocity)은 크리에이터가 World.Transform/Velocity(진실원본)에 직접 시드한다.
        }

        public override void UpdateEntity()
        {
            UpdateStatuses();
        }

        private void UpdateStatuses()
        {
            foreach (var status in this.GetEntityComponents<Status>().OrEmpty())
            {
                status.UpdateStatus();
            }
        }

        public void SyncPhysics()
        {
            PhysicsComponent physicsComponent = this.GetEntityComponent<PhysicsComponent>();

            if (physicsComponent == null)
            {
                return;
            }

            // kinematic 바디(원격 캐릭·아이템)는 World가 권위 — rb→World 되읽기는 rb.linearVelocity(=0)를
            // entity.velocity에 덮어 run 애니·smoothing을 망친다. 스킵한다(rb는 World를 따르는 follower).
            if (physicsComponent.entityRigidbody.isKinematic)
            {
                return;
            }

            position = physicsComponent.entityRigidbody.position;
            rotation = physicsComponent.entityRigidbody.rotation.eulerAngles;
            velocity = physicsComponent.entityRigidbody.linearVelocity;
        }

        /// <summary>World 모션(velocity·rotation)을 물리 바디에 밀어넣는다(Simulate 직전 호출). SyncPhysics(rb→World)의 역방향.</summary>
        public void PushMotionToPhysics()
        {
            PhysicsComponent physicsComponent = this.GetEntityComponent<PhysicsComponent>();

            if (physicsComponent == null)
            {
                return;
            }

            physicsComponent.entityRigidbody.linearVelocity = velocity;
            physicsComponent.entityRigidbody.rotation = Quaternion.Euler(rotation);
        }
    }
}
