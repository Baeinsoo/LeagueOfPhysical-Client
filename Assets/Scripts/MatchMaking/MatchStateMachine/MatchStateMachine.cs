using GameFramework;
using VContainer;

namespace LOP
{
    public class MatchStateMachine : StateMachine<MatchEvent>
    {
        private readonly IObjectResolver resolver;

        public MatchStateMachine(IObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public override IState<MatchEvent> initState => resolver.Resolve<CheckMatch>();
    }
}
