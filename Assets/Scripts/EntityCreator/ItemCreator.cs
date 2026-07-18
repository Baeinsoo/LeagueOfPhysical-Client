using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class ItemCreator : IEntityCreator<LOPActor, ItemCreationData>
    {
        [Inject] private IObjectResolver objectResolver;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        public LOPActor Create(ItemCreationData creationData)
        {
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = creationData.position.ToNumerics(),
                Rotation = Quaternion.Euler(creationData.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = creationData.velocity.ToNumerics() });
            worldEntity.Add(new EntityKind(EntityType.Item));
            worldEntity.Add(new MasterDataRef(creationData.itemCode));
            worldEntity.Add(new Appearance(creationData.visualId));
            entityRegistry.Add(worldEntity);

            // 앵커: 뷰(물리/모델/보간)는 EntityBinder가 붙인다.
            GameObject root = new GameObject($"Actor_{creationData.entityId}");
            LOPActor entity = root.AddComponent<LOPActor>();
            objectResolver.Inject(entity);
            entity.Initialize(creationData);
            return entity;
        }
    }
}
