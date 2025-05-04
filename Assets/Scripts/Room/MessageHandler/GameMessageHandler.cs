using GameFramework;
using Mirror;
using UnityEngine;
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
        private IGameDataContext gameDataContext;

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
            gameDataContext.entity = gameEngine.entityManager.CreateEntity<LOPEntity, LOPEntityCreationData>(new LOPEntityCreationData
            {
                userId = userDataContext.user.id,
                entityId = gameInfoToC.EntityId,
                visualId = "Assets/Art/Characters/Knight/Knight.prefab",
            });

            gameDataContext.session = new LOPSession
            (
                gameInfoToC.SessionId,
                userDataContext.user.id,
                NetworkClient.connection
            );
        }
    }
}
