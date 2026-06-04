using System;
using GameFramework;

namespace LOP
{
    public class GameDataStore : IGameDataStore, IDisposable
    {
        public GameInfo gameInfo { get; set; }
        public string userEntityId { get; set; }

        public GameDataStore()
        {
            EventBus.Default.Subscribe<GameInfoToC>(nameof(IMessage), HandleGameInfo);
        }

        public void Dispose()
        {
            // 룸 스코프가 dispose될 때 전역 EventBus 구독을 해제한다.
            // (해제 안 하면 룸 재입장마다 죽은 GameDataStore가 EventBus.Default에 누적됨)
            EventBus.Default.Unsubscribe<GameInfoToC>(nameof(IMessage), HandleGameInfo);
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
