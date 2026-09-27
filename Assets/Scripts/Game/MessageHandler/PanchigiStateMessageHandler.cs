using System.Collections.Generic;
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
            var players = new List<(string, IReadOnlyList<PanchigiRoll>)>(message.Players.Count);
            foreach (PanchigiPlayerRolls player in message.Players)
            {
                var list = new List<PanchigiRoll>(player.Rolls.Count);
                foreach (PanchigiRollInfo roll in player.Rolls) { list.Add(new PanchigiRoll(roll.Flipped, roll.Foul)); }
                players.Add((player.EntityId, list));
            }
            store.Set(message.Phase, message.CurrentEntityId, message.AimDeadlineTick, players);
        }
    }
}
