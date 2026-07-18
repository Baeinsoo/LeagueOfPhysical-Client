using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
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
                // rotation 변경 이벤트는 소비자가 없어 발행하지 않는다(연속 상태는 pull). 값만 쓴다.
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
                // velocity 변경 이벤트는 소비자가 없어 발행하지 않는다(연속 상태는 pull). 값만 쓴다.
            }
        }

        public void RaisePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 엔티티 컴포넌트라 DI 주입 타이밍이 불확실 → GlobalMessagePipe로 keyed 발행(키=entityId).
            GlobalMessagePipe.GetPublisher<string, PropertyChange>().Publish(entityId, new PropertyChange(e.PropertyName));
        }

        public virtual void Initialize<TEntityCreationData>(TEntityCreationData creationData) where TEntityCreationData : struct, IEntityCreationData
        {
            entityId = creationData.entityId;
            // 모션(position/rotation/velocity)은 크리에이터가 World.Transform/Velocity(진실원본)에 직접 시드한다.
        }

        public override void UpdateEntity()
        {
        }

        public void SyncPhysics()
        {
            PhysicsFollower physicsFollower = GetComponent<PhysicsFollower>();

            if (physicsFollower == null)
            {
                return;
            }

            // kinematic 바디(원격 캐릭·아이템)는 World가 권위 — rb→World 되읽기는 rb.linearVelocity(=0)를
            // entity.velocity에 덮어 run 애니·smoothing을 망친다. 스킵한다(rb는 World를 따르는 follower).
            if (physicsFollower.entityRigidbody.isKinematic)
            {
                return;
            }

            position = physicsFollower.entityRigidbody.position;
            rotation = physicsFollower.entityRigidbody.rotation.eulerAngles;
            velocity = physicsFollower.entityRigidbody.linearVelocity;
        }

        /// <summary>World 모션(velocity·rotation)을 물리 바디에 밀어넣는다(Simulate 직전 호출). SyncPhysics(rb→World)의 역방향.</summary>
        public void PushMotionToPhysics()
        {
            PhysicsFollower physicsFollower = GetComponent<PhysicsFollower>();

            if (physicsFollower == null)
            {
                return;
            }

            Rigidbody rigidbody = physicsFollower.entityRigidbody;

            // kinematic 바디(캐릭·아이템)는 velocity를 못 받는다(Unity가 매 틱 경고). velocity는 스킵.
            // 내 캐릭 예측은 KinematicMoveSystem이 World.Transform을 직접 써서 facade 이벤트가 안 뜨므로,
            // World 위치·회전을 rb에 직접 밀어넣는다(원격은 World=스냅 위치라 idempotent).
            if (rigidbody.isKinematic)
            {
                rigidbody.position = position;
                rigidbody.rotation = Quaternion.Euler(rotation);
                return;
            }

            rigidbody.linearVelocity = velocity;
            rigidbody.rotation = Quaternion.Euler(rotation);
        }
    }
}
