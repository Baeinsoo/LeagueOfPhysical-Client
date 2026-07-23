using GameFramework;
using System;

namespace LOP
{
    public class MatchStateMachine : StateMachine<MatchEvent>
    {
        private readonly Func<CheckMatch> checkMatch;

        public MatchStateMachine(Func<CheckMatch> checkMatch)
        {
            this.checkMatch = checkMatch;
        }

        public override IState<MatchEvent> initState => checkMatch();
    }
}
