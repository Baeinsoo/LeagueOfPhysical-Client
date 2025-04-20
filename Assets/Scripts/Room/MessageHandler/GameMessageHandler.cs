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
            roomNetwork.RegisterHandler<GameInfoResponse>(OnGameInfoResponse, LOPRoomMessageInterceptor.Default);
        }

        public void Unregister()
        {
            roomNetwork.UnregisterHandler<GameInfoResponse>(OnGameInfoResponse);
        }

        private void OnGameInfoResponse(GameInfoResponse response)
        {
            game.gameEngine.entityManager.CreateEntity<LOPEntity, LOPEntityCreationData>(new LOPEntityCreationData
            {
                entityId = response.EntityId,
                visualId = "Assets/Art/Characters/Knight/Knight.prefab",
            });
        }
    }
}
