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

        // 이 핸들러는 GameInfoMessageHandler(같은 메시지로 엔티티를 스폰한다)보다 먼저 불려야 한다 —
        // 스폰 도중 EntityBinder가 userEntityId를 읽어 내 캐릭터를 가려내기 때문이다. 이 스토어는 룸
        // 스코프라 게임 스코프보다 먼저 구독하고, 버스가 구독 순서를 보장한다(OrderedMessageBroker).
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
