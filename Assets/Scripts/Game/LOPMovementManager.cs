using GameFramework;
using System;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class LOPMovementManager : IMovementManager<LOPEntity>
    {
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
                entity.velocity, horizontal, vertical, speed));

            if (result.hasMove)
            {
                entity.velocity = result.velocity;   // EventBus 연출은 여기(host)
                entity.rotation = result.rotation;
            }

            //  Jump
            if (jump)
            {
                physicsComponent.entityRigidbody.linearVelocity -= new Vector3(0, physicsComponent.entityRigidbody.linearVelocity.y, 0);
                physicsComponent.entityRigidbody.AddForce(Vector3.up * characterComponent.masterData.JumpPower, ForceMode.Impulse);
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
