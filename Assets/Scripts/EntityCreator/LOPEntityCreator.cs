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

            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            entity.Initialize(lopEntityCreationData);

            LOPEntityController controller = root.CreateChildWithComponent<LOPEntityController>();
            controller.SetEntity(entity);

            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            view.SetEntity(entity);
            view.SetEntityController(controller);

            bool isUserEntity = SceneLifetimeScope.Resolve<IGameDataContext>().userEntityId == lopEntityCreationData.entityId;

            if (isUserEntity)
            {
                SnapReconciler snapReconciler = entity.gameObject.AddComponent<SnapReconciler>();
                snapReconciler.entity = entity;
            }
            else
            {
                SnapInterpolator snapInterpolator = entity.gameObject.AddComponent<SnapInterpolator>();
                snapInterpolator.entity = entity;
            }

            return entity;
        }
    }
}
