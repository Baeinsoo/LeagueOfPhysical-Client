using GameFramework;
using MessagePipe;
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

        [Inject]
        private ISubscriber<GameInfoToC> gameInfoSubscriber;

        private System.IDisposable subscription;

        public void Initialize()
        {
            subscription = gameInfoSubscriber.Subscribe(OnGameInfoToC);
        }

        public void Dispose()
        {
            subscription?.Dispose();
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
