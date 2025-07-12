using UnityEngine;
using System;
using GameFramework;

namespace LOP
{
    public class Dash : Action
    {
        protected override void OnActionUpdate()
        {
            Quaternion rotation = Quaternion.Euler(entity.rotation);
            Vector3 forward = rotation * Vector3.forward;

            if (entity.TryGetEntityComponent<PhysicsComponent>(out var physicsComponent) == false)
            {
                throw new Exception("PhysicsComponent does not exist. Cannot apply force.");
            }

            physicsComponent.entityRigidbody.AddForce(forward * 10, ForceMode.Impulse);
        }
    }
}
