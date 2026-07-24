using GameFramework;

namespace LOP.UI
{
    /// <summary>
    /// 디버그/유틸 HUD ViewModel. tick·경과시간·RTT·서버tick추정·lead·reconciliation은 변경을
    /// 통지하는 이벤트 소스가 없는 샘플링 값이라 R3(push) 대신 평범한 getter로 노출하고,
    /// View가 매 프레임 pull한다. reconciliation 값은 ReconciliationStats(Reconciler가 write)에서 읽는다.
    /// </summary>
    public class DebugHudViewModel
    {
        private readonly IRunner runner;
        private readonly ReconciliationStats reconciliationStats;
        private readonly InputTimingStats inputTimingStats;
        private readonly GameFramework.Netcode.SnapshotHistory snapshotHistory;

        public DebugHudViewModel(
            IRunner runner,
            ReconciliationStats reconciliationStats,
            InputTimingStats inputTimingStats,
            GameFramework.Netcode.SnapshotHistory snapshotHistory)
        {
            this.runner = runner;
            this.reconciliationStats = reconciliationStats;
            this.inputTimingStats = inputTimingStats;
            this.snapshotHistory = snapshotHistory;
        }

        public bool IsRunning => runner.gameState >= RunnerState.Playing;

        public long Tick => runner.tickUpdater.tick;

        public double ElapsedTime => runner.tickUpdater.elapsedTime;

        public double RttMs => runner.networkTime.Rtt * 1000;

        // 서버 현재 tick 추정 ≈ (predictedTime − 편도지연)/interval. Lead = Tick − 이것 = (AheadMargin + 편도지연)/interval = 진짜 lead.
        public long ServerTickEstimate => (long)(runner.networkTime.ServerNow / runner.tickUpdater.interval);

        public long Lead => runner.tickUpdater.tick - ServerTickEstimate;

        public float ReconLast => reconciliationStats.Last;

        public float ReconAverage => reconciliationStats.Average;

        public float ReconMax => reconciliationStats.Max;

        public double TimingAvgD => inputTimingStats.AvgD;

        public int TimingMaxD => inputTimingStats.MaxD;

        public int TimingPrune => inputTimingStats.PruneCount;

        public int TimingSeqGap => inputTimingStats.SeqGapCount;

        public int SnapshotCount => snapshotHistory.Count;

        public long SnapshotLatestTick => snapshotHistory.Latest?.Tick ?? -1;
    }
}
