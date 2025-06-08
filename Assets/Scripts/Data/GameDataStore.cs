using GameFramework;

namespace LOP
{
    public class GameDataStore : IGameDataStore
    {
        public GameInfo gameInfo { get; set; }
        public string userEntityId { get; set; }

        public GameDataStore(IDataUpdater dataUpdater)
        {
            dataUpdater.AddListener(this);
        }

        [DataListen(typeof(GameInfoToC))]
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
