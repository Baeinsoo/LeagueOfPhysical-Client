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
        private IPlayerContext playerContext;

        [Inject]
        private IGameEngine gameEngine;

        [Inject]
        private PlayerInputManager playerInputManager;

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

            foreach (var entityCreationData in gameInfoToC.GameInfo.EntityCreationDatas)
            {
                bool isUserEntity = entityCreationData.LopEntityCreationData.BaseEntityCreationData.EntityId == gameInfoToC.EntityId;

                switch (entityCreationData.CreationDataCase)
                {
                    case EntityCreationData.CreationDataOneofCase.LopEntityCreationData:
                        LOPEntity entity = gameEngine.entityManager.CreateEntity<LOPEntity, LOPEntityCreationData>(new LOPEntityCreationData
                        {
                            entityId = entityCreationData.LopEntityCreationData.BaseEntityCreationData.EntityId,
                            position = MapperConfig.mapper.Map<Vector3>(entityCreationData.LopEntityCreationData.BaseEntityCreationData.Position),
                            rotation = MapperConfig.mapper.Map<Vector3>(entityCreationData.LopEntityCreationData.BaseEntityCreationData.Rotation),
                            velocity = MapperConfig.mapper.Map<Vector3>(entityCreationData.LopEntityCreationData.BaseEntityCreationData.Velocity),
                            characterCode = entityCreationData.LopEntityCreationData.CharacterCode,
                            visualId = entityCreationData.LopEntityCreationData.VisualId,
                        });
                        break;
                }

                playerInputManager.SetSequenceNumber(gameInfoToC.ExpectedNextSequence);
            }
        }
    }
}
