using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>DodgeStateToC → DodgeClientState. ArcheryStateHandler와 같은 모양이다.</summary>
    public class DodgeStateHandler : MessageHandlerBase
    {
        private readonly DodgeClientState state;
        private readonly ISubscriber<DodgeStateToC> subscriber;

        public DodgeStateHandler(DodgeClientState state, ISubscriber<DodgeStateToC> subscriber)
        {
            this.state = state;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(message => state.Apply(message)));
    }
}
