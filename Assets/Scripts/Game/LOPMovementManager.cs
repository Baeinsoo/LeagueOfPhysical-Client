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

                // Rotate
                float myFloat = 0;
                var angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                var smooth = Mathf.SmoothDampAngle(entity.rotation.y, angle, ref myFloat, 0.01f);
                entity.rotation = new Vector3(0, smooth, 0);
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
