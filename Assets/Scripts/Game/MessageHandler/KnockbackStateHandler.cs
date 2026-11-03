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
                ApplyCracks(world.Floor, state.Cracks);
            }
        }));

        /// <summary>
        /// 서버가 정한 무너지는 틱을 바닥에 넣는다. 상태는 전체 목록이라 먼저 비운다 —
        /// 안 비우면 서버가 지운 칸이 남는다. 예고(1초)가 왕복보다 길어 내 예측도 같은 틱에 떨어진다.
        /// </summary>
        public static void ApplyCracks(KnockbackFloor floor, System.Collections.Generic.IReadOnlyList<KnockbackCrackInfo> cracks)
        {
            floor.ClearCollapses();
            for (int i = 0; i < cracks.Count; i++)
            {
                var c = cracks[i];
                if (c.CollapseTick < 0 || c.Tile < 0 || c.Tile >= floor.Tiles.Count) continue;
                floor.SetCollapseTick(c.Tile, c.CollapseTick);
            }
        }
    }
}
