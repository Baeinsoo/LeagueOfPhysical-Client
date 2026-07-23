using GameFramework;
using System;

namespace LOP
{
    public class Idle : State<MatchEvent>
    {
        private readonly Func<RequestMatchmaking> requestMatchmaking;

        public Idle(Func<RequestMatchmaking> requestMatchmaking)
        {
            this.requestMatchmaking = requestMatchmaking;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.PlayClicked => requestMatchmaking(),
                _ => this,
            };
        }
    }
}
