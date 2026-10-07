using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>PlayerPresenceToC → PlayerPresenceStore. DodgeStateHandler와 같은 모양이다.</summary>
    public class PresenceMessageHandler : MessageHandlerBase
    {
        private readonly PlayerPresenceStore store;
        private readonly ISubscriber<PlayerPresenceToC> subscriber;

        public PresenceMessageHandler(PlayerPresenceStore store, ISubscriber<PlayerPresenceToC> subscriber)
        {
            this.store = store;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(message => store.Apply(message)));
    }
}
