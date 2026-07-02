using UnityEngine;

namespace LOP
{
    /// <summary>
    /// <see cref="MotionEffect"/> 핸들러(side). Active 동안 매 틱 캐릭터를 바라보는 방향으로 그 속도로 민다(대시).
    /// executor가 Active 매 틱 호출 → 입력 무시(조종 불가)는 입력/이동 쪽 게이트(<see cref="AbilitySystem.HasActiveMotionEffect"/>)가 담당.
    /// 속도는 World.velocity(진실원본)에 쓴다 → PhysicsComponent 반응 동기가 Rigidbody에 반영.
    /// entityManager는 ctx로 받는다(DI 주입하면 world-graph↔entity-manager 순환) — LOPEntity 파사드 접근용, 후속에 World 직접 쓰기로 제거 예정.
    /// </summary>
    public class MotionEffectHandler : AbilityEffectHandler<MotionEffect>
    {
        protected override void OnActiveTick(AbilityEffectContext ctx, MotionEffect effect)
        {
            var entity = ctx.EntityManager.GetEntity<LOPEntity>(ctx.Caster.Id);
            if (entity == null)
            {
                return;
            }

            // 바라보는 방향(수평)으로 speed만큼 가도록 World.velocity를 맞춘다(Y 보존).
            // World 쓰기 → PhysicsComponent 반응 동기 → Rigidbody.
            Vector3 forward = Quaternion.Euler(entity.rotation) * Vector3.forward;
            Vector3 target = new Vector3(forward.x, 0f, forward.z).normalized * effect.Speed;

            Vector3 velocity = entity.velocity;
            velocity.x = target.x;
            velocity.z = target.z;
            entity.velocity = velocity;
        }
    }
}
