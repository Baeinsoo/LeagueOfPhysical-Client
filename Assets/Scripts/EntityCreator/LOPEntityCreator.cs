using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [EntityCreatorRegistration]
    public class LOPEntityCreator : IEntityCreator<LOPEntity, LOPEntityCreationData>
    {
        public LOPEntity Create(LOPEntityCreationData lopEntityCreationData)
        {
            GameObject root = new GameObject($"{nameof(LOPEntity)}_{lopEntityCreationData.entityId}");
            GameObject visual = root.CreateChild("Visual");
            GameObject physics = root.CreateChild("Physics");

            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            entity.Initialize(lopEntityCreationData);

            LOPEntityController controller = root.CreateChildWithComponent<LOPEntityController>();
            controller.SetEntity(entity);

            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            view.SetEntity(entity);
            view.SetEntityController(controller);

            bool isUserEntity = SceneLifetimeScope.Resolve<IGameDataStore>().userEntityId == lopEntityCreationData.entityId;

            if (isUserEntity)
            {
                SnapReconciler snapReconciler = entity.gameObject.AddComponent<SnapReconciler>();
                snapReconciler.entity = entity;
                snapReconciler.entityView = view;
            }
            else
            {
                SnapInterpolator snapInterpolator = entity.gameObject.AddComponent<SnapInterpolator>();
                snapInterpolator.entity = entity;
                snapInterpolator.entityView = view;
            }

            return entity;
        }
    }
}
