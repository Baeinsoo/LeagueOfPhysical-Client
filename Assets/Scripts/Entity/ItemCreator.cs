using GameFramework;
using UnityEngine;

namespace LOP
{
    public class ItemCreator
    {
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public ItemCreator(GameFramework.World.EntityRegistry entityRegistry)
        {
            this.entityRegistry = entityRegistry;
        }

        public void Create(ItemCreationData creationData)
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
        }
    }
}
