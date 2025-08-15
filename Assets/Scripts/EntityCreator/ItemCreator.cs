using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    [EntityCreatorRegistration]
    public class ItemCreator : IEntityCreator<LOPEntity, ItemCreationData>
    {
        [Inject]
        private IObjectResolver objectResolver;

        public ItemCreator()
        {
            SceneLifetimeScope.Inject(this);
        }

        public LOPEntity Create(ItemCreationData creationData)
        {
            GameObject root = new GameObject($"Item_{creationData.entityId}");
            GameObject visual = root.CreateChild("Visual");
            GameObject physics = root.CreateChild("Physics");

            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            objectResolver.Inject(entity);
            entity.Initialize(creationData);

            EntityTypeComponent entityTypeComponent = entity.AddEntityComponent<EntityTypeComponent>();
            objectResolver.Inject(entityTypeComponent);
            entityTypeComponent.Initialize(EntityType.Item);

            ItemComponent itemComponent = entity.AddEntityComponent<ItemComponent>();
            objectResolver.Inject(itemComponent);
            itemComponent.Initialize(creationData.itemCode);

            AppearanceComponent appearanceComponent = entity.AddEntityComponent<AppearanceComponent>();
            objectResolver.Inject(appearanceComponent);
            appearanceComponent.Initialize(creationData.visualId);

            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            objectResolver.Inject(physicsComponent);
            physicsComponent.Initialize(true, true);

            LOPEntityController controller = root.CreateChildWithComponent<LOPEntityController>();
            objectResolver.Inject(controller);
            controller.SetEntity(entity);

            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(entity);
            view.SetEntityController(controller);

            ServerStateReconciler serverStateReconciler = entity.gameObject.AddComponent<ServerStateReconciler>();
            objectResolver.Inject(serverStateReconciler);
            serverStateReconciler.entity = entity;
            serverStateReconciler.entityView = view;

            return entity;
        }
    }
}
