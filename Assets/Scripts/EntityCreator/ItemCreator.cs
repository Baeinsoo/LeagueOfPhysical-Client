using GameFramework;
using UnityEngine;

namespace LOP
{
    [EntityCreatorRegistration]
    public class ItemCreator : IEntityCreator<LOPEntity, ItemCreationData>
    {
        public LOPEntity Create(ItemCreationData creationData)
        {
            GameObject root = new GameObject($"Item_{creationData.entityId}");
            GameObject visual = root.CreateChild("Visual");
            GameObject physics = root.CreateChild("Physics");

            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            entity.Initialize(creationData);

            EntityTypeComponent entityTypeComponent = entity.AddEntityComponent<EntityTypeComponent>();
            entityTypeComponent.Initialize(EntityType.Item);

            ItemComponent itemComponent = entity.AddEntityComponent<ItemComponent>();
            itemComponent.Initialize(creationData.itemCode);

            AppearanceComponent appearanceComponent = entity.AddEntityComponent<AppearanceComponent>();
            appearanceComponent.Initialize(creationData.visualId);

            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            physicsComponent.Initialize(true, true);

            LOPEntityController controller = root.CreateChildWithComponent<LOPEntityController>();
            controller.SetEntity(entity);

            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            view.SetEntity(entity);
            view.SetEntityController(controller);

            ServerStateReconciler serverStateReconciler = entity.gameObject.AddComponent<ServerStateReconciler>();
            serverStateReconciler.entity = entity;
            serverStateReconciler.entityView = view;

            return entity;
        }
    }
}
