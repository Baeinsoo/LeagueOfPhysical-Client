using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 대시처럼 motion_speed가 있는 이동 어빌리티의 움직임을 매 틱 적용한다.
    /// 어빌리티가 Active 페이즈인 동안 캐릭터를 바라보는 방향으로 그 속도로 민다(플레이어 입력 무시 = 조종 불가).
    /// world.Tick(페이즈 전진) 다음, 물리 시뮬 전에 호출. 클라이언트·서버 동일.
    /// </summary>
    public class AbilityMotionSystem
    {
        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private LOP.MasterData.LOPMasterData md;

        public void ApplyMotion(IEnumerable<LOPEntity> entities)
        {
            foreach (var entity in entities)
            {
                if (TryGetActiveMotionSpeed(entityRegistry.Get(entity.entityId), md, out float speed) == false)
                {
                    continue;
                }
                if (entity.TryGetComponent<PhysicsComponent>(out var physicsComponent) == false)
                {
                    continue;
                }

                // 바라보는 방향(수평)으로 speed만큼 가도록 속도를 맞춘다. 일반 이동과 같은 방식(차이만큼 힘).
                Vector3 forward = Quaternion.Euler(entity.rotation) * Vector3.forward;
                Vector3 target = new Vector3(forward.x, 0f, forward.z).normalized * speed;
                var rb = physicsComponent.entityRigidbody;
                Vector3 delta = target - new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                rb.AddForce(new Vector3(delta.x, 0f, delta.z), ForceMode.VelocityChange);
            }
        }

        /// <summary>엔티티가 지금 Active 페이즈인 이동 어빌리티(motion_speed &gt; 0)를 쓰고 있으면 그 속도를 돌려준다.</summary>
        public static bool TryGetActiveMotionSpeed(GameFramework.World.Entity worldEntity, LOP.MasterData.LOPMasterData md, out float speed)
        {
            speed = 0f;
            var active = worldEntity?.Get<Abilities>()?.ActiveAbility;
            if (active == null || active.Value.Phase != AbilityPhase.Active)
            {
                return false;
            }

            float motionSpeed = md.Tables.TbAbility.GetOrDefault(active.Value.AbilityId)?.MotionSpeed ?? 0f;
            if (motionSpeed <= 0f)
            {
                return false;
            }

            speed = motionSpeed;
            return true;
        }
    }
}
