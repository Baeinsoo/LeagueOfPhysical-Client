using VContainer;
using VContainer.Unity;

namespace LOP
{
    public class LobbyLifetimeScope : SceneLifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<MatchStateMachine>(Lifetime.Transient);

            builder.Register<CheckMatch>(Lifetime.Transient);
            builder.Register<Idle>(Lifetime.Transient);
            builder.Register<RequestMatchmaking>(Lifetime.Transient);
            builder.Register<InWaitingRoom>(Lifetime.Transient);
            builder.Register<InGameRoom>(Lifetime.Transient);
            builder.Register<CancelMatchmaking>(Lifetime.Transient);
        }
    }
}
