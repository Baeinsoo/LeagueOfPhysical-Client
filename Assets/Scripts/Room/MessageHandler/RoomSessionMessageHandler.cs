using GameFramework;
using MessagePipe;
using Mirror;

namespace LOP
{
    public class RoomSessionMessageHandler : MessageHandlerBase
    {
        private readonly IUserDataStore userDataStore;
        private readonly IPlayerContext playerContext;
        private readonly ISubscriber<GameInfoToC> gameInfoSubscriber;

        public RoomSessionMessageHandler(IUserDataStore userDataStore, IPlayerContext playerContext, ISubscriber<GameInfoToC> gameInfoSubscriber)
        {
            this.userDataStore = userDataStore;
            this.playerContext = playerContext;
            this.gameInfoSubscriber = gameInfoSubscriber;
        }

        protected override void Subscribe() => Track(gameInfoSubscriber.Subscribe(OnGameInfoToC));

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
