using UnityEngine;

namespace LOP
{
    /// <summary>
    /// <see cref="MotionEffect"/> 핸들러(side). Active 동안 매 틱 캐릭터를 바라보는 방향으로 그 속도로 민다(대시).
    /// executor가 Active 매 틱 호출 → 입력 무시(조종 불가)는 입력/이동 쪽 게이트(<see cref="AbilitySystem.HasActiveMotionEffect"/>)가 담당.
    /// 물리(Rigidbody)는 side 개념이라 코어가 아닌 여기서 적용한다.
    /// entityManager는 ctx로 받는다(DI 주입하면 world-graph↔entity-manager 순환).
    /// </summary>
    public class MotionEffectHandler : AbilityEffectHandler<MotionEffect>
    {
        protected override void OnActiveTick(AbilityEffectContext ctx, MotionEffect effect)
        {
            var entity = ctx.EntityManager.GetEntity<LOPEntity>(ctx.Caster.Id);
            if (entity == null || entity.TryGetComponent<PhysicsComponent>(out var physicsComponent) == false)
            {
                return;
            }

            // 바라보는 방향(수평)으로 speed만큼 가도록 속도를 맞춘다. 일반 이동과 같은 방식(차이만큼 힘).
            Vector3 forward = Quaternion.Euler(entity.rotation) * Vector3.forward;
            Vector3 target = new Vector3(forward.x, 0f, forward.z).normalized * effect.Speed;
            var rb = physicsComponent.entityRigidbody;
            Vector3 delta = target - new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(new Vector3(delta.x, 0f, delta.z), ForceMode.VelocityChange);
        }
    }
}
