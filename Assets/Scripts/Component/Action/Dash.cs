using UnityEngine;

namespace LOP
{
    public class Dash : Action
    {
        protected override void OnActionUpdate()
        {
            Quaternion rotation = Quaternion.Euler(entity.rotation);
            Vector3 forward = rotation * Vector3.forward;

            entity.entityRigidbody.AddForce(forward * 7, ForceMode.Impulse);
        }
    }
}
