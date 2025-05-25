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
        }

        public void Unregister()
        {
            messageDispatcher.UnregisterHandler<EntitySnapsToC>(OnEntitySnapsToC);
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
    }
}
