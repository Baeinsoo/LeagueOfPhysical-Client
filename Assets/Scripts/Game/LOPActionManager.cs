using GameFramework;
using UnityEngine;
using System.Linq;
using VContainer;

namespace LOP
{
    public class LOPActionManager : IActionManager<LOPEntity>
    {
        [Inject]
        private IMasterDataManager masterDataManager;

        [Inject]
        private IObjectResolver objectResolver;

        public bool TryStartAction(LOPEntity entity, string actionCode)
        {
            if (entity == null)
            {
                Debug.LogWarning("Entity is null. Cannot execute action.");
                return false;
            }

            if (string.IsNullOrEmpty(actionCode))
            {
                Debug.LogWarning($"Invalid action Code. Cannot execute action. actionCode: {actionCode}");
                return false;
            }

            GetOrAddAction(entity, actionCode, out Action action);

            return action.TryStartAction();
        }

        bool IActionManager.TryStartAction(IEntity entity, string actionCode)
        {
            if (entity is LOPEntity lopEntity)
            {
                return TryStartAction(lopEntity, actionCode);
            }
            else
            {
                Debug.LogWarning("Entity must be of type LOPEntity.");
                return false;
            }
        }

        public bool TryEndAction(LOPEntity entity, string actionCode)
        {
            if (entity == null)
            {
                Debug.LogWarning("Entity is null. Cannot end action.");
                return false;
            }
            if (string.IsNullOrEmpty(actionCode))
            {
                Debug.LogWarning($"Invalid action Code. Cannot end action. actionCode: {actionCode}");
                return false;
            }

            Action action = entity.FindEntityComponent<Action>(x => x.actionCode == actionCode);
            if (action == null)
            {
                Debug.LogWarning($"Action {actionCode} not found on entity {entity.entityId}. Cannot end action.");
                return false;
            }

            return action.TryEndAction();
        }

        bool IActionManager.TryEndAction(IEntity entity, string actionCode)
        {
            if (entity is LOPEntity lopEntity)
            {
                return TryEndAction(lopEntity, actionCode);
            }
            else
            {
                Debug.LogWarning("Entity must be of type LOPEntity.");
                return false;
            }
        }

        private void GetOrAddAction(LOPEntity entity, string actionCode, out Action action)
        {
            action = entity.FindEntityComponent<Action>(x => x.actionCode == actionCode);
            if (action == null)
            {
                var actionMasterData = masterDataManager.GetMasterData<MasterData.Action>(actionCode);
                var actionType = System.Type.GetType($"LOP.{actionMasterData.Class}");

                action = entity.gameObject.AddComponent(actionType) as Action;
                objectResolver.Inject(action);
                entity.AttachEntityComponent(action);

                action.Initialize(actionCode);
            }
        }
    }
}
