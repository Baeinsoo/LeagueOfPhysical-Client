using GameFramework;
using System;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class LOPMovementManager : IMovementManager<LOPEntity>
    {
        private const float MaxAcceleration = 100f;   // 목표 속도로 따라붙는 빠르기(클수록 즉각 반응 — 튜닝값)

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        [Inject]
        private GameFramework.World.StatsSystem statsSystem;

        public void ProcessInput(LOPEntity entity, EntityTransform entityTransform, float horizontal, float vertical, bool jump)
        {
            if (entity.TryGetComponent<CharacterComponent>(out var characterComponent) == false)
            {
                throw new Exception("CharacterComponent does not exist. Cannot process input.");
            }

            // 대시 같은 이동 어빌리티가 Active면 입력 이동을 무시한다(대시가 방향·속도를 주도).
            if (AbilitySystem.HasActiveMotionEffect(entityRegistry.Get(entity.entityId)))
            {
                return;
            }

            var worldStats = entityRegistry.Get(entity.entityId).Get<GameFramework.World.Stats>();
            float speed = statsSystem.GetValue(worldStats, (int)GameFramework.World.EntityStatType.MoveSpeed);

            var result = MovementSystem.ProcessMovement(new MovementInput(
                entity.velocity, horizontal, vertical, speed,
                MaxAcceleration, (float)Runner.Time.tickInterval));

            // 계산된 새 속도를 World.velocity에 반영한다(좌우/앞뒤만; Y는 중력에 맡겨 보존).
            // 점프면 Y를 점프 속도로 세팅. World 쓰기 → PhysicsComponent 반응 동기 → Rigidbody.
            Vector3 velocity = entity.velocity;
            velocity.x = result.velocity.x;
            velocity.z = result.velocity.z;
            if (jump)
            {
                velocity.y = characterComponent.masterData.JumpPower;
            }
            entity.velocity = velocity;

            if (result.hasRotation)
            {
                entity.rotation = result.rotation;
            }
        }

        void IMovementManager.ProcessInput(IEntity entity, EntityTransform entityTransform, float horizontal, float vertical, bool jump)
        {
            if (entity is LOPEntity lopEntity)
            {
                ProcessInput(lopEntity, entityTransform, horizontal, vertical, jump);
            }
            else
            {
                throw new InvalidCastException("Entity must be of type LOPEntity.");
            }
        }
    }
}
