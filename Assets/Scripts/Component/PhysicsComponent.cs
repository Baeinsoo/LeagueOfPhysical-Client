using GameFramework;
using LOP.Event.Entity;
using UnityEngine;

namespace LOP
{
    public class PhysicsComponent : LOPComponent
    {
        private GameObject _physicsGameObject;
        public GameObject physicsGameObject
        {
            get => _physicsGameObject;
            set
            {
                this.SetProperty(ref _physicsGameObject, value, entity.RaisePropertyChanged);
            }
        }

        public Rigidbody entityRigidbody { get; private set; }
        public Collider[] entityColliders { get; private set; }

        public void Initialize(bool isKinematic, bool isTrigger)
        {
            EventBus.Default.Subscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);

            GameObject physics = entity.transform.parent.Find("Physics").gameObject;
            physicsGameObject = physics.CreateChild("PhysicsGameObject");

            entityRigidbody = physicsGameObject.AddComponent<Rigidbody>();
            entityRigidbody.linearDamping = 0f;   // 수평 정지는 이동 모터가 0으로 제동(아래). 수직=순수 중력.
            entityRigidbody.angularDamping = 0.05f;
            entityRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
            entityRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            entityRigidbody.position = entity.position;
            entityRigidbody.rotation = Quaternion.Euler(entity.rotation);
            entityRigidbody.linearVelocity = entity.velocity;
            entityRigidbody.isKinematic = isKinematic;

            CapsuleCollider capsuleCollider = physicsGameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = 0.35f;
            capsuleCollider.height = 1.5f;
            capsuleCollider.center = new Vector3(0, capsuleCollider.height * 0.5f, 0);
            capsuleCollider.isTrigger = isTrigger;
            entityColliders = new Collider[] { capsuleCollider };
        }

        public override void OnDetach()
        {
            EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);

            base.OnDetach();
        }

        /// <summary>
        /// 겹친 지오메트리(스폰 시 지면과 붙음·끼임)에서 캡슐을 밖으로 밀어낸다.
        /// sweep 이동은 "시작부터 겹친" 콜라이더를 무시하므로, 겹친 채로는 지면을 못 잡아 통과한다 —
        /// 그래서 이동 전에 실제 콜라이더로 밀어내 겹침을 푼다(PhysX가 하던 일을 대신).
        /// </summary>
        public void Depenetrate(int layerMask)
        {
            var capsule = (CapsuleCollider)entityColliders[0];
            Vector3 ownPos = capsule.transform.position;
            Quaternion ownRot = capsule.transform.rotation;

            Vector3 center = capsule.transform.TransformPoint(capsule.center);
            float radius = capsule.radius;
            float halfSpan = Mathf.Max(capsule.height * 0.5f - radius, 0f);
            Vector3 up = capsule.transform.up;
            Vector3 p1 = center - up * halfSpan;
            Vector3 p2 = center + up * halfSpan;

            Collider[] overlaps = Physics.OverlapCapsule(p1, p2, radius, layerMask, QueryTriggerInteraction.Ignore);
            Vector3 total = Vector3.zero;
            foreach (var other in overlaps)
            {
                if (other == capsule)
                {
                    continue;   // 자기 콜라이더 제외
                }
                if (Physics.ComputePenetration(capsule, ownPos, ownRot,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 dir, out float dist))
                {
                    total += dir * dist;
                }
            }

            if (total.sqrMagnitude > 0f)
            {
                entity.position += total;   // 파사드 → World.Transform + reactive rb.position
            }
        }

        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(entity.position):
                    entityRigidbody.position = entity.position;
                    break;

                // velocity·rotation은 BeforePhysicsSimulation 브릿지(LOPEntity.PushMotionToPhysics)가 담당.
            }
        }
    }
}
