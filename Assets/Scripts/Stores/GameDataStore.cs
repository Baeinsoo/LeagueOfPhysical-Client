using System;
using GameFramework;
using MessagePipe;

namespace LOP
{
    public class GameDataStore : IGameDataStore, IDisposable
    {
        public GameInfo gameInfo { get; set; }
        public string userEntityId { get; set; }

        private readonly IDisposable subscription;

        public GameDataStore(ISubscriber<GameInfoToC> gameInfoSubscriber)
        {
            subscription = gameInfoSubscriber.Subscribe(HandleGameInfo);
        }

        public void Dispose()
        {
            subscription.Dispose();
        }

        private void HandleGameInfo(GameInfoToC gameInfoToC)
        {
            gameInfo = gameInfoToC.GameInfo;
            userEntityId = gameInfoToC.EntityId;
        }

        public void Clear()
        {
            gameInfo = null;
        }
    }
}
