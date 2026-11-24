using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>TriGrabCardsToC(카드 자리, 비신뢰) → TriGrabCardTrack.</summary>
    public class TriGrabCardsHandler : MessageHandlerBase
    {
        private readonly TriGrabCardTrack track;
        private readonly ISubscriber<TriGrabCardsToC> subscriber;

        public TriGrabCardsHandler(TriGrabCardTrack track, ISubscriber<TriGrabCardsToC> subscriber)
        {
            this.track = track;
            this.subscriber = subscriber;
        }

        protected override void Subscribe()
            => Track(subscriber.Subscribe(message => track.Push(message.Tick, message.Ids, message.Xs, message.Zs)));
    }
}
