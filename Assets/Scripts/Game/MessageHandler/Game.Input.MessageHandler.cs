using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameInputMessageHandler : IGameMessageHandler
    {
        [Inject]
        private IGameEngine gameEngine;

        [Inject]
        private IMessageDispatcher messageDispatcher;

        [Inject]
        private IPlayerContext playerContext;

        [Inject]
        private IGameDataContext gameDataContext;

        public void Register()
        {
            messageDispatcher.RegisterHandler<EntitySnapsToC>(OnEntitySnapsToC, LOPRoomMessageInterceptor.Default);
            messageDispatcher.RegisterHandler<InputSequenceToC>(OnInputSequenceToC, LOPRoomMessageInterceptor.Default);
        }

        public void Unregister()
        {
            messageDispatcher.UnregisterHandler<EntitySnapsToC>(OnEntitySnapsToC);
            messageDispatcher.UnregisterHandler<InputSequenceToC>(OnInputSequenceToC);
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

        private void OnInputSequenceToC(InputSequenceToC inputSequenceToC)
        {
            InputSequence inputSequence = new InputSequence
            {
                Tick = inputSequenceToC.InputSequence.Tick,
                Sequence = inputSequenceToC.InputSequence.Sequence,
            };

            LOPEntity lopEntity = gameEngine.entityManager.GetEntity<LOPEntity>(inputSequenceToC.EntityId);

            lopEntity.GetComponent<SnapReconciler>().AddServerInputSequence(inputSequence);
        }
    }
}
