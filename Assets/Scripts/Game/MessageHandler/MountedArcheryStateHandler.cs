using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>MountedArcheryStateToC → MountedArcheryClientState. DodgeStateHandler와 같은 모양이다.</summary>
    public class MountedArcheryStateHandler : MessageHandlerBase
    {
        private readonly MountedArcheryClientState state;
        private readonly ISubscriber<MountedArcheryStateToC> subscriber;

        public MountedArcheryStateHandler(MountedArcheryClientState state, ISubscriber<MountedArcheryStateToC> subscriber)
        {
            this.state = state;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(message => state.Apply(message)));
    }
}
