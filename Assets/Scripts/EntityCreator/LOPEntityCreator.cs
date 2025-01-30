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
            var lopEntity = new GameObject($"{nameof(LOPEntity)}_{lopEntityCreationData.entityId}").AddComponent<LOPEntity>();
            var lopEntityPresenter = lopEntity.gameObject.AddComponent<LOPEntityPresenter>();

            lopEntity.Initialize(lopEntityCreationData);
            lopEntityPresenter.Initialize(lopEntityCreationData);

            return lopEntity;
        }
    }
}
