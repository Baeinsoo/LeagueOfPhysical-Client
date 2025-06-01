using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameEntityMessageHandler : IGameMessageHandler
    {
        [Inject] private IMessageDispatcher messageDispatcher;
        [Inject] private IPlayerContext playerContext;
        [Inject] private IGameDataContext gameDataContext;
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
                entitySnap.timestamp = entitySnapsToC.Tick * gameDataContext.gameInfo.Interval;

                if (playerContext.entity.entityId == entity.entityId)
                {
                    entity.GetComponent<SnapReconciler>().AddServerEntitySnap(entitySnap);
                }
                else
                {
                    entity.GetComponent<SnapInterpolator>().AddServerEntitySnap(entitySnap);
                }
            }
        }

        private void OnEntitySpawnToC(EntitySpawnToC entitySpawnToC)
        {
            switch (entitySpawnToC.EntityCreationData.CreationDataCase)
            {
                case EntityCreationData.CreationDataOneofCase.LopEntityCreationData:

                    string entityId = entitySpawnToC.EntityCreationData.LopEntityCreationData.BaseEntityCreationData.EntityId;

                    if (gameEngine.entityManager.TryGetEntity<LOPEntity>(entityId, out var entity))
                    {
                        Debug.LogWarning($"Entity {entityId} already exists");
                        return;
                    }

                    gameEngine.entityManager.CreateEntity<LOPEntity, LOPEntityCreationData>(new LOPEntityCreationData
                    {
                        entityId = entityId,
                        visualId = entitySpawnToC.EntityCreationData.LopEntityCreationData.VisualId,
                        position = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.LopEntityCreationData.BaseEntityCreationData.Position),
                        rotation = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.LopEntityCreationData.BaseEntityCreationData.Rotation),
                        velocity = MapperConfig.mapper.Map<Vector3>(entitySpawnToC.EntityCreationData.LopEntityCreationData.BaseEntityCreationData.Velocity),
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
