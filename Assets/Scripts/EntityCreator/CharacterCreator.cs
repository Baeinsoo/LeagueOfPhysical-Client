using GameFramework;
using UnityEngine;

namespace LOP
{
    [EntityCreatorRegistration]
    public class CharacterCreator : IEntityCreator<LOPEntity, CharacterCreationData>
    {
        public LOPEntity Create(CharacterCreationData creationData)
        {
            GameObject root = new GameObject($"Character_{creationData.entityId}");
            GameObject visual = root.CreateChild("Visual");
            GameObject physics = root.CreateChild("Physics");

            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            entity.Initialize(creationData);

            EntityTypeComponent entityTypeComponent = entity.AddEntityComponent<EntityTypeComponent>();
            entityTypeComponent.Initialize(EntityType.Character);

            CharacterComponent characterComponent = entity.AddEntityComponent<CharacterComponent>();
            characterComponent.Initialize(creationData.characterCode);

            AppearanceComponent appearanceComponent = entity.AddEntityComponent<AppearanceComponent>();
            appearanceComponent.Initialize(creationData.visualId);

            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            physicsComponent.Initialize();

            LOPEntityController controller = root.CreateChildWithComponent<LOPEntityController>();
            controller.SetEntity(entity);

            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            view.SetEntity(entity);
            view.SetEntityController(controller);

            bool isUserEntity = SceneLifetimeScope.Resolve<IGameDataStore>().userEntityId == creationData.entityId;

            if (isUserEntity)
            {
                SceneLifetimeScope.Resolve<IPlayerContext>().entity = entity;
                SceneLifetimeScope.Resolve<IPlayerContext>().entityView = view;

                SnapReconciler snapReconciler = entity.gameObject.AddComponent<SnapReconciler>();
                snapReconciler.entity = entity;
                snapReconciler.entityView = view;
            }
            else
            {
                ServerStateReconciler serverStateReconciler = entity.gameObject.AddComponent<ServerStateReconciler>();
                serverStateReconciler.entity = entity;
                serverStateReconciler.entityView = view;
            }

            return entity;
        }
    }
}
