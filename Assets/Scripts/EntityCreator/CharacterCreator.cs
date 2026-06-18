using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class CharacterCreator : IEntityCreator<LOPEntity, CharacterCreationData>
    {
        [Inject]
        private IGameDataStore gameDataStore;

        [Inject]
        private IPlayerContext playerContext;

        [Inject]
        private IObjectResolver objectResolver;

        [Inject]
        private GameFramework.World.EntityRegistry entityRegistry;

        public LOPEntity Create(CharacterCreationData creationData)
        {
            GameObject root = new GameObject($"Character_{creationData.entityId}");
            GameObject visual = root.CreateChild("Visual");
            GameObject physics = root.CreateChild("Physics");

            LOPEntity entity = root.CreateChildWithComponent<LOPEntity>();
            objectResolver.Inject(entity);
            entity.Initialize(creationData);

            EntityTypeComponent entityTypeComponent = entity.AddEntityComponent<EntityTypeComponent>();
            objectResolver.Inject(entityTypeComponent);
            entityTypeComponent.Initialize(EntityType.Character);

            CharacterComponent characterComponent = entity.AddEntityComponent<CharacterComponent>();
            objectResolver.Inject(characterComponent);
            characterComponent.Initialize(creationData.characterCode);

            AppearanceComponent appearanceComponent = entity.AddEntityComponent<AppearanceComponent>();
            objectResolver.Inject(appearanceComponent);
            appearanceComponent.Initialize(creationData.visualId);

            PhysicsComponent physicsComponent = entity.AddEntityComponent<PhysicsComponent>();
            objectResolver.Inject(physicsComponent);
            physicsComponent.Initialize(false, false);

            ManaComponent manaComponent = entity.AddEntityComponent<ManaComponent>();
            objectResolver.Inject(manaComponent);
            manaComponent.Initialize(creationData.maxMP, creationData.currentMP);

            StatsComponent statsComponent = entity.AddEntityComponent<StatsComponent>();
            objectResolver.Inject(statsComponent);
            statsComponent.Initialize(creationData.characterCode);

            LevelComponent levelComponent = entity.AddEntityComponent<LevelComponent>();
            objectResolver.Inject(levelComponent);
            levelComponent.Initialize(creationData.level, creationData.currentExp);

            LOPEntityController controller = root.CreateChildWithComponent<LOPEntityController>();
            objectResolver.Inject(controller);
            controller.SetEntity(entity);

            LOPEntityView view = root.CreateChildWithComponent<LOPEntityView>();
            objectResolver.Inject(view);
            view.SetEntity(entity);

            bool isUserEntity = gameDataStore.userEntityId == creationData.entityId;

            if (isUserEntity)
            {
                UserComponent userComponent = entity.AddEntityComponent<UserComponent>();
                objectResolver.Inject(userComponent);

                playerContext.entity = entity;
                playerContext.entityView = view;

                SnapReconciler snapReconciler = entity.gameObject.AddComponent<SnapReconciler>();
                objectResolver.Inject(snapReconciler);
                snapReconciler.entity = entity;
                snapReconciler.entityView = view;
            }
            else
            {
                ServerStateReconciler serverStateReconciler = entity.gameObject.AddComponent<ServerStateReconciler>();
                objectResolver.Inject(serverStateReconciler);
                serverStateReconciler.entity = entity;
                serverStateReconciler.entityView = view;
            }

            // --- World Core (병렬·추가) — Slice 1: Health, Slice B: Transform/Velocity ---
            var worldEntity = new GameFramework.World.Entity(creationData.entityId);
            var worldHealth = new GameFramework.World.Health(creationData.maxHP) { Current = creationData.currentHP };
            worldEntity.Add(worldHealth);
            worldEntity.Add(new GameFramework.World.Transform
            {
                Position = entity.position.ToNumerics(),
                Rotation = Quaternion.Euler(entity.rotation).ToNumerics(),
            });
            worldEntity.Add(new GameFramework.World.Velocity { Linear = entity.velocity.ToNumerics() });
            entityRegistry.Add(worldEntity);
            Debug.Log($"[World] Registered entity {worldEntity.Id} Health={worldHealth.Current}/{worldHealth.Max}");
            // --- end World Core slice 1+B ---

            return entity;
        }
    }
}
