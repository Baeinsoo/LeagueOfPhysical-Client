using GameFramework;
using UnityEngine;

namespace LOP
{
    /// <summary>
    /// World.Transform을 따라가는 물리 바디(Rigidbody/캡슐 콜라이더)를 소유하는 프레젠테이션 컴포넌트.
    /// 앵커 루트(LOPActor)와 같은 GameObject에 rb+콜라이더를 둔다(시뮬 바디 = 루트).
    /// rb 팔로우는 호스트 단일 패스(LOPRunner)가 MotionBridge.PushMotion으로 구동한다.
    /// </summary>
    public class PhysicsFollower : MonoBehaviour
    {
        public Rigidbody entityRigidbody { get; private set; }
        public Collider[] entityColliders { get; private set; }

        public void Initialize(GameFramework.World.Entity worldEntity, bool isKinematic, bool isTrigger)
        {
            var worldTransform = worldEntity.Get<GameFramework.World.Transform>();
            var worldVelocity = worldEntity.Get<GameFramework.World.Velocity>();

            gameObject.layer = LayerMask.NameToLayer("Character");

            // 루트(시뮬 바디)를 스폰 위치에 즉시 배치한다. kinematic rb의 rb.position은 다음 물리 스텝에야
            // 트랜스폼에 반영돼, 루트가 한 틱 원점에 머물다 스폰 지점으로 점프하면 자식 모델이 끌려가
            // 첫 틱에 순간이동한다 — 트랜스폼을 바로 세팅해 그 점프를 없앤다.
            transform.SetPositionAndRotation(worldTransform.Position.ToUnity(), worldTransform.Rotation.ToUnity());

            entityRigidbody = gameObject.AddComponent<Rigidbody>();
            entityRigidbody.linearDamping = 0f;
            entityRigidbody.angularDamping = 0.05f;
            entityRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            entityRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            entityRigidbody.position = worldTransform.Position.ToUnity();
            entityRigidbody.rotation = worldTransform.Rotation.ToUnity();
            entityRigidbody.linearVelocity = worldVelocity.Linear.ToUnity();
            entityRigidbody.isKinematic = isKinematic;

            CapsuleCollider capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = 0.35f;
            capsuleCollider.height = 1.5f;
            capsuleCollider.center = new Vector3(0, capsuleCollider.height * 0.5f, 0);
            capsuleCollider.isTrigger = isTrigger;
            entityColliders = new Collider[] { capsuleCollider };
        }
    }
}
