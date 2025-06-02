using GameFramework;
using UnityEngine;

namespace LOP
{
    public class LOPActionManager : IActionManager<LOPEntity>
    {
        public bool TryExecuteAction(LOPEntity entity, int actionId)
        {
            if (entity == null)
            {
                Debug.LogWarning("Entity is null. Cannot execute action.");
                return false;
            }

            if (actionId <= 0)
            {
                Debug.LogWarning($"Invalid action ID. Cannot execute action. actionId: {actionId}");
                return false;
            }

            //  Dash (Temporary Skill Example)
            if (actionId == 1)
            {
                Quaternion rotation = Quaternion.Euler(entity.rotation);
                Vector3 forward = rotation * Vector3.forward;

                entity.entityRigidbody.AddForce(forward * 7, ForceMode.Impulse);
            }

            return true;
        }

        bool IActionManager.TryExecuteAction(IEntity entity, int actionId)
        {
            if (entity is LOPEntity lopEntity)
            {
                return TryExecuteAction(lopEntity, actionId);
            }
            else
            {
                Debug.LogWarning("Entity must be of type LOPEntity.");
                return false;
            }
        }
    }
}
