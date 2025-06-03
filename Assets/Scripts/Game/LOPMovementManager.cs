using GameFramework;
using System;
using UnityEngine;

namespace LOP
{
    public class LOPMovementManager : IMovementManager<LOPEntity>
    {
        public void ProcessInput(LOPEntity entity, float horizontal, float vertical, bool jump)
        {
            Vector3 direction = new Vector3(horizontal, 0, vertical).normalized;

            if (direction.sqrMagnitude > 0)
            {
                //  Move
                var velocity = direction * entity.masterData.speed;
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
                entity.entityRigidbody.linearVelocity -= new Vector3(0, entity.entityRigidbody.linearVelocity.y, 0);
                entity.entityRigidbody.AddForce(Vector3.up * entity.masterData.jump_power, ForceMode.Impulse);
            }
        }

        void IMovementManager.ProcessInput(IEntity entity, float horizontal, float vertical, bool jump)
        {
            if (entity is LOPEntity lopEntity)
            {
                ProcessInput(lopEntity, horizontal, vertical, jump);
            }
            else
            {
                throw new InvalidCastException("Entity must be of type LOPEntity.");
            }
        }
    }
}
