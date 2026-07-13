using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>클라 키네마틱 물리 브릿지 — entityId로 LOPEntity를 잡아 Depenetrate/PushMotion. 클라 Simulated=내 캐릭만.</summary>
    public class LOPMotionBridge : IMotionBridge
    {
        [Inject] private IEntityManager entityManager;
        private readonly int _layerMask = LayerMask.GetMask("Default");

        public void SyncTransforms() => Physics.SyncTransforms();

        public void Depenetrate(string entityId)
            => entityManager.GetEntity<LOPEntity>(entityId)?.GetEntityComponent<PhysicsComponent>()?.Depenetrate(_layerMask);

        public void PushMotion(string entityId)
            => entityManager.GetEntity<LOPEntity>(entityId)?.PushMotionToPhysics();
    }
}
