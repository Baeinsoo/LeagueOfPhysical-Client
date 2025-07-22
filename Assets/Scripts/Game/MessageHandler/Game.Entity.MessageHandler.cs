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
        }

        public void Unregister()
        {
            messageDispatcher.UnregisterHandler<EntitySnapsToC>(OnEntitySnapsToC);
            messageDispatcher.UnregisterHandler<EntitySpawnToC>(OnEntitySpawnToC);
            messageDispatcher.UnregisterHandler<EntityDespawnToC>(OnEntityDespawnToC);
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
    }
}
