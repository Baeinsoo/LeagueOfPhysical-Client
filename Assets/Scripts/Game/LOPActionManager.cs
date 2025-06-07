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

        public bool TryExecuteAction(LOPEntity entity, string actionCode)
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

            action.TryActionStart();

            return true;
        }

        bool IActionManager.TryExecuteAction(IEntity entity, string actionCode)
        {
            if (entity is LOPEntity lopEntity)
            {
                return TryExecuteAction(lopEntity, actionCode);
            }
            else
            {
                Debug.LogWarning("Entity must be of type LOPEntity.");
                return false;
            }
        }

        private void GetOrAddAction(LOPEntity entity, string actionCode, out Action action)
        {
            action = entity.actions.FirstOrDefault(x => x.actionCode == actionCode);
            if (action == null)
            {
                var actionMasterData = masterDataManager.GetMasterData<MasterData.Action>(actionCode);
                var actionType = System.Type.GetType($"LOP.{actionMasterData.Class}");

                action = entity.gameObject.AddComponent(actionType) as Action;
                entity.AttachComponent(action);

                action.Initialize(actionCode);
            }
        }
    }
}
