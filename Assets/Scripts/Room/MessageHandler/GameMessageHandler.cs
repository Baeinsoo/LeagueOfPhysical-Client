using GameFramework;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class GameMessageHandler : IRoomMessageHandler
    {
        [Inject]
        private IGame game;

        [Inject]
        private IRoomNetwork roomNetwork;

        public void Register()
        {
            roomNetwork.RegisterHandler<GameInfoToC>(OnGameInfoToC, LOPRoomMessageInterceptor.Default);
        }

        public void Unregister()
        {
            roomNetwork.UnregisterHandler<GameInfoToC>(OnGameInfoToC);
        }

        private void OnGameInfoToC(GameInfoToC gameInfoToC)
        {
            game.gameEngine.entityManager.CreateEntity<LOPEntity, LOPEntityCreationData>(new LOPEntityCreationData
            {
                entityId = gameInfoToC.EntityId,
                visualId = "Assets/Art/Characters/Knight/Knight.prefab",
            });
        }
    }
}
