using GameFramework;
using MessagePipe;
using Mirror;
using VContainer;

namespace LOP
{
    public class RoomSessionMessageHandler : MessageHandlerBase
    {
        [Inject]
        private IUserDataStore userDataStore;

        [Inject]
        private IPlayerContext playerContext;

        [Inject]
        private ISubscriber<GameInfoToC> gameInfoSubscriber;

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
