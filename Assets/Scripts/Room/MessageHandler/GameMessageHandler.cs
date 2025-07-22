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
        private IUserDataStore userDataStore;

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
                userDataStore.user.id,
                NetworkClient.connection
            );

            foreach (var entityCreationData in gameInfoToC.GameInfo.EntityCreationDatas)
            {
                switch (entityCreationData.CreationDataCase)
                {
                    case EntityCreationData.CreationDataOneofCase.CharacterCreationData:
                        gameEngine.entityManager.CreateEntity<LOPEntity, CharacterCreationData>(new CharacterCreationData
                        {
                            entityId = entityCreationData.CharacterCreationData.BaseEntityCreationData.EntityId,
                            position = MapperConfig.mapper.Map<Vector3>(entityCreationData.CharacterCreationData.BaseEntityCreationData.Position),
                            rotation = MapperConfig.mapper.Map<Vector3>(entityCreationData.CharacterCreationData.BaseEntityCreationData.Rotation),
                            velocity = MapperConfig.mapper.Map<Vector3>(entityCreationData.CharacterCreationData.BaseEntityCreationData.Velocity),
                            characterCode = entityCreationData.CharacterCreationData.CharacterCode,
                            visualId = entityCreationData.CharacterCreationData.VisualId,
                        });
                        break;

                    case EntityCreationData.CreationDataOneofCase.ItemCreationData:
                        gameEngine.entityManager.CreateEntity<LOPEntity, ItemCreationData>(new ItemCreationData
                        {
                            entityId = entityCreationData.ItemCreationData.BaseEntityCreationData.EntityId,
                            position = MapperConfig.mapper.Map<Vector3>(entityCreationData.ItemCreationData.BaseEntityCreationData.Position),
                            rotation = MapperConfig.mapper.Map<Vector3>(entityCreationData.ItemCreationData.BaseEntityCreationData.Rotation),
                            velocity = MapperConfig.mapper.Map<Vector3>(entityCreationData.ItemCreationData.BaseEntityCreationData.Velocity),
                            itemCode = entityCreationData.ItemCreationData.ItemCode,
                            visualId = entityCreationData.ItemCreationData.VisualId,
                        });
                        break;
                }

                playerInputManager.SetSequenceNumber(gameInfoToC.ExpectedNextSequence);
            }
        }
    }
}
