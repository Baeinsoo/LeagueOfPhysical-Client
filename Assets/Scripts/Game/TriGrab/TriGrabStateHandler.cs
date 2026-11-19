using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>TriGrabStateToC → TriGrabClientState. DodgeStateHandler와 같은 모양이다.</summary>
    public class TriGrabStateHandler : MessageHandlerBase
    {
        private readonly TriGrabClientState state;
        private readonly ISubscriber<TriGrabStateToC> subscriber;

        public TriGrabStateHandler(TriGrabClientState state, ISubscriber<TriGrabStateToC> subscriber)
        {
            this.state = state;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(message => state.Apply(message)));
    }
}
