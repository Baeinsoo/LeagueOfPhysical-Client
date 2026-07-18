using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class ItemCreator : IEntityCreator<LOPActor, ItemCreationData>
    {
        [Inject]
        private IObjectResolver objectResolver;

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        public LOPActor Create(ItemCreationData creationData)
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

            LOPActor entity = root.CreateChildWithComponent<LOPActor>();
            objectResolver.Inject(entity);
            entity.LinkWorldMotion(
                worldEntity.Get<GameFramework.World.Transform>(),
                worldEntity.Get<GameFramework.World.Velocity>());
            entity.Initialize(creationData);

            PhysicsFollower physicsFollower = entity.gameObject.AddComponent<PhysicsFollower>();
            objectResolver.Inject(physicsFollower);
            physicsFollower.Initialize(worldEntity, true, true);
            worldEntity.Add(new PhysicsBody(physicsFollower.entityRigidbody, (CapsuleCollider)physicsFollower.entityColliders[0]));

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
