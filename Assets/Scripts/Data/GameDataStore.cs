using GameFramework;

namespace LOP
{
    public class GameDataStore : IGameDataStore
    {
        public GameInfo gameInfo { get; set; }
        public string userEntityId { get; set; }

        public GameDataStore()
        {
            EventBus.Default.Subscribe<GameInfoToC>(nameof(IMessage), HandleGameInfo);
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
