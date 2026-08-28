using GameFramework.Runner;

namespace LOP.UI
{
    /// <summary>
    /// 내 차례인지와 남은 시간, 그리고 판이 몇 대 몇인지. 남은 시간은 서버가 보내 준 *마감 틱*에서
    /// 매 프레임 계산한다 — 초마다 메시지를 받을 필요가 없다. 뒤집힌 개수도 마찬가지로 매 프레임
    /// 동전 자세에서 직접 센다 — 동전 회전은 이미 스냅샷으로 들어오므로 따로 받을 것이 없다.
    /// </summary>
    public class PanchigiTurnViewModel
    {
        private const int AimingPhase = 1;

        private readonly PanchigiStateStore store;
        private readonly IPlayerContext playerContext;
        private readonly IRunner runner;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly LOP.MasterData.LOPMasterData masterData;

        public PanchigiTurnViewModel(PanchigiStateStore store, IPlayerContext playerContext, IRunner runner,
            GameFramework.World.EntityRegistry entityRegistry, LOP.MasterData.LOPMasterData masterData)
        {
            this.store = store;
            this.playerContext = playerContext;
            this.runner = runner;
            this.entityRegistry = entityRegistry;
            this.masterData = masterData;
        }

        public string Label()
        {
            if (store.IsEliminated(playerContext.entityId))
            {
                return "탈락 · 구경 중";
            }

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

        /// <summary>몇 개를 뒤집었나. 동전이 아직 안 왔으면 빈 줄로 둔다.</summary>
        public string FlipLabel()
        {
            PanchigiCoin.CountFlipped(entityRegistry.All, out int flipped, out int total);

            return total == 0 ? string.Empty : $"뒤집힘 {flipped} / {total}";
        }

        /// <summary>내가 몇 번 떨어뜨렸나. 벌칙이 꺼져 있으면(한도 0) 아예 안 보여준다.</summary>
        public string DropOutLabel()
        {
            int limit = masterData.Tables.TbPanchigiConfig.GetOrDefault(1)?.DropOutLimit ?? 0;
            if (limit <= 0)
            {
                return string.Empty;
            }

            //  판이 언제 끝나는지도 같이 보여준다 — 판치기는 시간이 아니라 턴 수로 끝난다.
            int turnLimit = masterData.Tables.TbPanchigiConfig.GetOrDefault(1)?.MatchTurnLimit ?? 0;
            string turns = turnLimit > 0 ? $" · 턴 {store.TurnCount.CurrentValue} / {turnLimit}" : string.Empty;

            return $"낙 {store.GetDropOutCount(playerContext.entityId)} / {limit}{turns}";
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
