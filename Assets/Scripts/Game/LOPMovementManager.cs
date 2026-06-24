using GameFramework;
using System;
using UnityEngine;

namespace LOP
{
    public class LOPMovementManager : IMovementManager<LOPEntity>
    {
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

            var result = MovementSystem.ProcessMovement(new MovementInput(
                entity.velocity, horizontal, vertical, characterComponent.masterData.Speed));

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
