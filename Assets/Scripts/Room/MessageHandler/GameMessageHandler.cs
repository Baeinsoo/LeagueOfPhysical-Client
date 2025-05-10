using GameFramework;
using Mirror;
using VContainer;

namespace LOP
{
    public class GameMessageHandler : IRoomMessageHandler
    {
        [Inject]
        private IMessageDispatcher messageDispatcher;

        [Inject]
        private IUserDataContext userDataContext;

        [Inject]
        private IPlayerContext playerContext;

        [Inject]
        private IGameEngine gameEngine;

        public void Register()
        {
            messageDispatcher.RegisterHandler<GameInfoToC>(OnGameInfoToC, LOPRoomMessageInterceptor.Default);
        }

        public void Unregister()
        {
            messageDispatcher.UnregisterHandler<GameInfoToC>(OnGameInfoToC);
        }

        private void OnGameInfoToC(GameInfoToC gameInfoToC)
        {
            playerContext.session = new LOPSession
            (
                gameInfoToC.SessionId,
                userDataContext.user.id,
                NetworkClient.connection
            );

            foreach (var protoSnap in gameInfoToC.GameInfo.EntitySnaps)
            {
                EntitySnap entitySnap = MapperConfig.mapper.Map<EntitySnap>(protoSnap);
                entitySnap.tick = gameInfoToC.GameInfo.Tick;
                entitySnap.timestamp = gameInfoToC.GameInfo.ElapsedTime;

                bool isUserEntity = entitySnap.entityId == gameInfoToC.EntityId;

                LOPEntity entity = gameEngine.entityManager.CreateEntity<LOPEntity, LOPEntityCreationData>(new LOPEntityCreationData
                {
                    entityId = entitySnap.entityId,
                    visualId = "Assets/Art/Characters/Knight/Knight.prefab",
                    position = entitySnap.position,
                    rotation = entitySnap.rotation,
                    velocity = entitySnap.velocity,
                    isUserEntity = isUserEntity,
                });

                if (isUserEntity)
                {
                    playerContext.entity = entity;
                }
            }
        }
    }
}
