using GameFramework;
using VContainer;

namespace LOP
{
    public class Idle : State<MatchEvent>
    {
        private readonly IObjectResolver resolver;

        public Idle(IObjectResolver resolver)
        {
            this.resolver = resolver;
        }

        public override IState<MatchEvent> GetNextState(MatchEvent ev)
        {
            return ev switch
            {
                MatchEvent.PlayClicked => resolver.Resolve<RequestMatchmaking>(),
                _ => this,
            };
        }
    }
}
