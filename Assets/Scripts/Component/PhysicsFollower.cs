using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// World.Transform을 따라가는 물리 바디(Rigidbody/캡슐 콜라이더)를 소유·구동하는 프레젠테이션 컴포넌트.
    /// 엔티티-컴포넌트가 아닌 순수 MonoBehaviour — 시뮬(World.Transform)이 권위, Rigidbody는 팔로워.
    /// </summary>
    public class PhysicsFollower : MonoBehaviour, ICleanup
    {
        private System.IDisposable propertyChangeSubscription;
        private GameFramework.World.Entity worldEntity;

        private GameObject physicsGameObject;

        public Rigidbody entityRigidbody { get; private set; }
        public Collider[] entityColliders { get; private set; }

        public void Initialize(GameFramework.World.Entity worldEntity, bool isKinematic, bool isTrigger)
        {
            this.worldEntity = worldEntity;
            var worldTransform = worldEntity.Get<GameFramework.World.Transform>();
            var worldVelocity = worldEntity.Get<GameFramework.World.Velocity>();

            propertyChangeSubscription = GlobalMessagePipe.GetSubscriber<string, PropertyChange>().Subscribe(worldEntity.Id, OnPropertyChange);

            GameObject physics = transform.parent.Find("Physics").gameObject;
            physicsGameObject = physics.CreateChild("PhysicsGameObject");
            physicsGameObject.layer = LayerMask.NameToLayer("Character");

            entityRigidbody = physicsGameObject.AddComponent<Rigidbody>();
            entityRigidbody.linearDamping = 0f;   // 수평 정지는 이동 모터가 0으로 제동. 수직=순수 중력.
            entityRigidbody.angularDamping = 0.05f;
            entityRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            entityRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            entityRigidbody.position = worldTransform.Position.ToUnity();
            entityRigidbody.rotation = worldTransform.Rotation.ToUnity();
            entityRigidbody.linearVelocity = worldVelocity.Linear.ToUnity();
            entityRigidbody.isKinematic = isKinematic;

            CapsuleCollider capsuleCollider = physicsGameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = 0.35f;
            capsuleCollider.height = 1.5f;
            capsuleCollider.center = new Vector3(0, capsuleCollider.height * 0.5f, 0);
            capsuleCollider.isTrigger = isTrigger;
            entityColliders = new Collider[] { capsuleCollider };
        }

        public void Cleanup()
        {
            propertyChangeSubscription?.Dispose();
        }

        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(LOPEntity.position):
                    entityRigidbody.position = worldEntity.Get<GameFramework.World.Transform>().Position.ToUnity();
                    break;

                // velocity·rotation은 BeforePhysicsSimulation 브릿지(LOPEntity.PushMotionToPhysics)가 담당.
            }
        }
    }
}
