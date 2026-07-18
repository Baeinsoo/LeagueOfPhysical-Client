using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// World.Transform을 따라가는 물리 바디(Rigidbody/캡슐 콜라이더)를 소유하는 프레젠테이션 컴포넌트.
    /// rb 팔로우는 호스트 단일 패스(LOPRunner)가 MotionBridge.PushMotion으로 구동한다.
    /// </summary>
    public class PhysicsFollower : MonoBehaviour
    {
        private GameObject physicsGameObject;

        public Rigidbody entityRigidbody { get; private set; }
        public Collider[] entityColliders { get; private set; }

        public void Initialize(GameFramework.World.Entity worldEntity, bool isKinematic, bool isTrigger)
        {
            var worldTransform = worldEntity.Get<GameFramework.World.Transform>();
            var worldVelocity = worldEntity.Get<GameFramework.World.Velocity>();

            GameObject physics = transform.parent.Find("Physics").gameObject;
            physicsGameObject = physics.CreateChild("PhysicsGameObject");
            physicsGameObject.layer = LayerMask.NameToLayer("Character");

            entityRigidbody = physicsGameObject.AddComponent<Rigidbody>();
            entityRigidbody.linearDamping = 0f;
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
    }
}
