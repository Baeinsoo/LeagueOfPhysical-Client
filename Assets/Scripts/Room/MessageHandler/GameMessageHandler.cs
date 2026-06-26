using GameFramework;
using Mirror;
using VContainer;

namespace LOP
{
    public class GameMessageHandler : IRoomMessageHandler
    {
        [Inject]
        private IUserDataStore userDataStore;

        [Inject]
        private IPlayerContext playerContext;

        public void Initialize()
        {
            EventBus.Default.Subscribe<GameInfoToC>(nameof(IMessage), OnGameInfoToC);
        }

        public void Dispose()
        {
            EventBus.Default.Unsubscribe<GameInfoToC>(nameof(IMessage), OnGameInfoToC);
        }

        private void OnGameInfoToC(GameInfoToC gameInfoToC)
        {
            playerContext.session = new LOPSession
            (
                gameInfoToC.SessionId,
                userDataStore.user.id,
                NetworkClient.connection
            );
        }
    }
}
