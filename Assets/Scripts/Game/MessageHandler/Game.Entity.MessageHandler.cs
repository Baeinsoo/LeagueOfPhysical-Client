using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameEntityMessageHandler : IGameMessageHandler
    {
        [Inject] private IMessageDispatcher messageDispatcher;
        [Inject] private IPlayerContext playerContext;
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IGameEngine gameEngine;

        public void Register()
        {
            messageDispatcher.RegisterHandler<EntitySnapsToC>(OnEntitySnapsToC, LOPRoomMessageInterceptor.Default);
            messageDispatcher.RegisterHandler<EntitySpawnToC>(OnEntitySpawnToC, LOPRoomMessageInterceptor.Default);
            messageDispatcher.RegisterHandler<EntityDespawnToC>(OnEntityDespawnToC, LOPRoomMessageInterceptor.Default);
            messageDispatcher.RegisterHandler<ActionStartToC>(OnActionStartToC, LOPRoomMessageInterceptor.Default);
            messageDispatcher.RegisterHandler<UserEntitySnapToC>(OnUserEntitySnapToC, LOPRoomMessageInterceptor.Default);
        }

        public void Unregister()
        {
            messageDispatcher.UnregisterHandler<EntitySnapsToC>(OnEntitySnapsToC);
            messageDispatcher.UnregisterHandler<EntitySpawnToC>(OnEntitySpawnToC);
            messageDispatcher.UnregisterHandler<EntityDespawnToC>(OnEntityDespawnToC);
            messageDispatcher.UnregisterHandler<ActionStartToC>(OnActionStartToC);
            messageDispatcher.UnregisterHandler<UserEntitySnapToC>(OnUserEntitySnapToC);
        }

        private void OnEntitySnapsToC(EntitySnapsToC entitySnapsToC)
        {
            foreach (var serverEntitySnap in entitySnapsToC.EntitySnaps.OrEmpty())
            {
                if (gameEngine.entityManager.TryGetEntity<LOPEntity>(serverEntitySnap.EntityId, out var entity) == false)
                {
                    Debug.LogWarning($"Entity {serverEntitySnap.EntityId} not found");
                    continue;
                }

                EntitySnap entitySnap = MapperConfig.mapper.Map<EntitySnap>(serverEntitySnap);
                entitySnap.tick = entitySnapsToC.Tick;
                entitySnap.timestamp = entitySnapsToC.Tick * gameDataStore.gameInfo.Interval;

                if (playerContext.entity.entityId == entity.entityId)
                {
                    entity.GetComponent<SnapReconciler>().AddServerEntitySnap(entitySnap);
                }
                else
                {
                    entity.GetComponent<ServerStateReconciler>().AddServerEntitySnap(entitySnap);
                }
            }
        }

        private void OnEntitySpawnToC(EntitySpawnToC entitySpawnToC)
        {
            switch (entitySpawnToC.EntityCreationData.CreationDataCase)
            {
                case EntityCreationData.CreationDataOneofCase.CharacterCreationData:
                    string entityId = entitySpawnToC.EntityCreationData.CharacterCreationData.BaseEntityCreationData.EntityId;

                    if (gameEngine.entityManager.TryGetEntity<LOPEntity>(entityId, out var entity))
                    {
                        Debug.LogWarning($"Entity {entityId} already exists");
                        return;
                    }

                    gameEngine.entityManager.CreateEntity<LOPEntity, CharacterCreationData>(new CharacterCreationData
                    {
                        entityId = entityId,
                        position = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.CharacterCreationData.BaseEntityCreationData.Position),
                        rotation = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.CharacterCreationData.BaseEntityCreationData.Rotation),
                        velocity = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.CharacterCreationData.BaseEntityCreationData.Velocity),
                        characterCode = entitySpawnToC.EntityCreationData.CharacterCreationData.CharacterCode,
                        visualId = entitySpawnToC.EntityCreationData.CharacterCreationData.VisualId,

                        maxHP = entitySpawnToC.EntityCreationData.CharacterCreationData.MaxHP,
                        currentHP = entitySpawnToC.EntityCreationData.CharacterCreationData.CurrentHP,
                        maxMP = entitySpawnToC.EntityCreationData.CharacterCreationData.MaxMP,
                        currentMP = entitySpawnToC.EntityCreationData.CharacterCreationData.CurrentMP,
                        level = entitySpawnToC.EntityCreationData.CharacterCreationData.Level,
                        currentExp = entitySpawnToC.EntityCreationData.CharacterCreationData.CurrentExp,
                    });
                    break;

                case EntityCreationData.CreationDataOneofCase.ItemCreationData:
                    string itemEntityId = entitySpawnToC.EntityCreationData.ItemCreationData.BaseEntityCreationData.EntityId;

                    if (gameEngine.entityManager.TryGetEntity<LOPEntity>(itemEntityId, out var item))
                    {
                        Debug.LogWarning($"Entity {itemEntityId} already exists");
                        return;
                    }

                    gameEngine.entityManager.CreateEntity<LOPEntity, ItemCreationData>(new ItemCreationData
                    {
                        entityId = itemEntityId,
                        position = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.ItemCreationData.BaseEntityCreationData.Position),
                        rotation = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.ItemCreationData.BaseEntityCreationData.Rotation),
                        velocity = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.ItemCreationData.BaseEntityCreationData.Velocity),
                        itemCode = entitySpawnToC.EntityCreationData.ItemCreationData.ItemCode,
                        visualId = entitySpawnToC.EntityCreationData.ItemCreationData.VisualId,
                    });
                    break;
            }
        }

        private void OnEntityDespawnToC(EntityDespawnToC entityDespawnToC)
        {
            if (gameEngine.entityManager.TryGetEntity<LOPEntity>(entityDespawnToC.EntityId, out var entity))
            {
                gameEngine.entityManager.DeleteEntityById(entityDespawnToC.EntityId);
            }
            else
            {
                Debug.LogWarning($"Entity {entityDespawnToC.EntityId} not found for despawn");
            }
        }

        private void OnActionStartToC(ActionStartToC actionStartToC)
        {
            if (playerContext.entity != null && playerContext.entity.entityId == actionStartToC.EntityId)
            {
                return;
            }

            if (gameEngine.entityManager.TryGetEntity<LOPEntity>(actionStartToC.EntityId, out var entity))
            {
                entity.eventBus.Publish(new Event.Entity.ActionStart(actionStartToC.ActionCode));
            }
        }

        private void OnUserEntitySnapToC(UserEntitySnapToC userEntitySnapToC)
        {
            if (playerContext.entity == null)
            {
                return;
            }

            playerContext.entity.GetComponent<HealthComponent>().currentHP = userEntitySnapToC.CurrentHP;
            playerContext.entity.GetComponent<HealthComponent>().maxHP = userEntitySnapToC.MaxHP;
            playerContext.entity.GetComponent<ManaComponent>().currentMP = userEntitySnapToC.CurrentMP;
            playerContext.entity.GetComponent<ManaComponent>().maxMP = userEntitySnapToC.MaxMP;
            playerContext.entity.GetComponent<LevelComponent>().currentExp = userEntitySnapToC.CurrentExp;
            playerContext.entity.GetComponent<LevelComponent>().level = userEntitySnapToC.Level;
        }
    }
}
