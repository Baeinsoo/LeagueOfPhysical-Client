using GameFramework.Runner;

namespace LOP.UI
{
    /// <summary>
    /// 내 차례인지와 남은 시간. 남은 시간은 서버가 보내 준 *마감 틱*에서 매 프레임 계산한다 —
    /// 초마다 메시지를 받을 필요가 없다.
    /// </summary>
    public class PanchigiTurnViewModel
    {
        private const int AimingPhase = 1;

        private readonly PanchigiStateStore store;
        private readonly IPlayerContext playerContext;
        private readonly IRunner runner;

        public PanchigiTurnViewModel(PanchigiStateStore store, IPlayerContext playerContext, IRunner runner)
        {
            this.store = store;
            this.playerContext = playerContext;
            this.runner = runner;
        }

        public string Label()
        {
            if (store.Phase.CurrentValue != AimingPhase)
            {
                return "동전이 멈추는 중";
            }

            if (store.CurrentEntityId.CurrentValue != playerContext.entityId)
            {
                return "다른 사람 차례";
            }

            return $"내 차례 · {RemainingSeconds()}";
        }

        private int RemainingSeconds()
        {
            double interval = runner.tickUpdater?.interval ?? 0;
            if (interval <= 0)
            {
                return 0;
            }

            long left = store.AimDeadlineTick.CurrentValue - runner.tickUpdater.tick;
            return left <= 0 ? 0 : (int)System.Math.Ceiling(left * interval);
        }
    }
}
