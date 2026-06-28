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

            if (entity.TryGetComponent<PhysicsComponent>(out var physicsComponent) == false)
            {
                throw new Exception("PhysicsComponent does not exist. Cannot process input.");
            }

            var worldStats = entityRegistry.Get(entity.entityId).Get<GameFramework.World.Stats>();
            float speed = statsSystem.GetValue(worldStats, (int)GameFramework.World.EntityStatType.MoveSpeed);

            var result = MovementSystem.ProcessMovement(new MovementInput(
                entity.velocity, horizontal, vertical, speed,
                MaxAcceleration, (float)Runner.Time.tickInterval));

            // 계산된 새 속도와 지금 속도의 차이만큼 힘을 줘서 속도를 맞춘다(좌우/앞뒤만).
            Vector3 delta = result.velocity - entity.velocity;
            physicsComponent.entityRigidbody.AddForce(new Vector3(delta.x, 0f, delta.z), ForceMode.VelocityChange);
            if (result.hasRotation)
            {
                entity.rotation = result.rotation;
            }

            // 점프: 위쪽 속도를 점프 속도로 맞춘다(땅에 있는지 체크는 아직 없음).
            if (jump)
            {
                var rb = physicsComponent.entityRigidbody;
                float jumpSpeed = characterComponent.masterData.JumpPower;
                rb.AddForce(Vector3.up * (jumpSpeed - rb.linearVelocity.y), ForceMode.VelocityChange);
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
