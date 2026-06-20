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

            Vector3 direction = new Vector3(horizontal, 0, vertical).normalized;

            if (direction.sqrMagnitude > 0)
            {
                //  Move
                var velocity = direction * characterComponent.masterData.Speed;
                entity.velocity = new Vector3(velocity.x, entity.velocity.y, velocity.z);

                // Rotate (deterministic snap — facing은 cosmetic, 부드러운 연출은 뷰/Stage④)
                float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                entity.rotation = new Vector3(0, angle, 0);
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
