using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>TriGrabRingStateToC → TriGrabClientState. DodgeStateHandler와 같은 모양이다.</summary>
    public class TriGrabStateHandler : MessageHandlerBase
    {
        private readonly TriGrabClientState state;
        private readonly ISubscriber<TriGrabRingStateToC> subscriber;

        public TriGrabStateHandler(TriGrabClientState state, ISubscriber<TriGrabRingStateToC> subscriber)
        {
            this.state = state;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(message => state.Apply(message)));
    }
}
