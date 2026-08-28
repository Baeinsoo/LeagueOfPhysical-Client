using GameFramework;
using MessagePipe;

namespace LOP
{
    public class PanchigiStateMessageHandler : MessageHandlerBase
    {
        private readonly PanchigiStateStore store;
        private readonly ISubscriber<PanchigiStateToC> subscriber;

        public PanchigiStateMessageHandler(PanchigiStateStore store, ISubscriber<PanchigiStateToC> subscriber)
        {
            this.store = store;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(OnState));

        private void OnState(PanchigiStateToC message)
        {
            store.Set(message.Phase, message.CurrentEntityId, message.AimDeadlineTick, message.TurnCount,
                message.DropOutCounts, message.EliminatedEntityIds);
        }
    }
}
