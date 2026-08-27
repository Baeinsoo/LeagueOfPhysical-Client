using GameFramework;
using MessagePipe;

namespace LOP
{
    /// <summary>출발 예정 틱을 월드에 꽂고 화면용 상태를 갱신한다.</summary>
    public class MatchStartMessageHandler : MessageHandlerBase
    {
        private readonly GameFramework.World.IWorld world;
        private readonly MatchStartState matchStartState;
        private readonly ISubscriber<MatchStartToC> subscriber;

        public MatchStartMessageHandler(
            GameFramework.World.IWorld world,
            MatchStartState matchStartState,
            ISubscriber<MatchStartToC> subscriber)
        {
            this.world = world;
            this.matchStartState = matchStartState;
            this.subscriber = subscriber;
        }

        protected override void Subscribe() => Track(subscriber.Subscribe(OnMatchStartToC));

        private void OnMatchStartToC(MatchStartToC message)
        {
            // 와이어의 -1(미정)을 월드가 쓰는 표현(long.MaxValue)으로 바꾼다.
            long startTick = message.StartTick < 0 ? long.MaxValue : message.StartTick;

            world.GameplayStartTick = startTick;
            matchStartState.Update(startTick, message.ReadyCount, message.TotalCount);
        }
    }
}
