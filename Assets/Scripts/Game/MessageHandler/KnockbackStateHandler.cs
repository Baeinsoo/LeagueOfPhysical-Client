using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>KnockbackStateToC → KnockbackClientState. 원 단계는 바닥(월드)에도 넣어 칸이 같은 틱에 무너지게 한다.</summary>
    public class KnockbackStateHandler : MessageHandlerBase
    {
        private readonly KnockbackClientState state;
        private readonly KnockbackWorld world;
        private readonly ISubscriber<KnockbackStateToC> subscriber;

        public KnockbackStateHandler(KnockbackClientState state, KnockbackWorld world, ISubscriber<KnockbackStateToC> subscriber)
        {
            this.state = state;
            this.world = world;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(message =>
        {
            if (state.Apply(message))
            {
                world.Floor.SetZoneStages(state.ZoneStages);
            }
        }));
    }
}
