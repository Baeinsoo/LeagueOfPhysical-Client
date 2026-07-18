using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class ItemCreator : IEntityCreator<LOPEntity, ItemCreationData>
    {
        [Inject]
        private IObjectResolver objectResolver;

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        public LOPEntity Create(ItemCreationData creationData)
        {
            GameObject root = new GameObject($"Item_{creationData.entityId}");
            GameObject visual = root.CreateChild("Visual");
            GameObject physics = root.CreateChild("Physics");

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

            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            objectResolver.Inject(entity);
            entity.LinkWorldMotion(
                worldEntity.Get<GameFramework.World.Transform>(),
                worldEntity.Get<GameFramework.World.Velocity>());
            entity.Initialize(creationData);

            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            objectResolver.Inject(physicsComponent);
            physicsComponent.Initialize(true, true);

            LOPEntityController controller = root.CreateChildWithComponent<LOPEntityController>();
            objectResolver.Inject(controller);
            controller.SetEntity(entity);

            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(entity);

            RemoteEntityInterpolator interpolator = entity.gameObject.AddComponent<RemoteEntityInterpolator>();
            objectResolver.Inject(interpolator);
            interpolator.entity = entity;
            interpolator.entityView = view;

            entityRegistry.Add(worldEntity);

            return entity;
        }
    }
}
