using GameFramework;
using VContainer;

namespace LOP.UI
{
    /// <summary>
    /// 디버그/유틸 HUD ViewModel. tick·경과시간·RTT·서버tick추정·lead·reconciliation은 변경을
    /// 통지하는 이벤트 소스가 없는 샘플링 값이라 R3(push) 대신 평범한 getter로 노출하고,
    /// View가 매 프레임 pull한다. reconciliation 값은 ReconciliationStats(Reconciler가 write)에서 읽는다.
    /// </summary>
    public class DebugHudViewModel
    {
        [Inject]
        private ReconciliationStats reconciliationStats;

        [Inject]
        private InputTimingStats inputTimingStats;

        [Inject]
        private GameFramework.Netcode.SnapshotHistory snapshotHistory;

        public bool IsRunning => Runner.current != null;

        public long Tick => Runner.Time.tick;

        public double ElapsedTime => Runner.Time.elapsedTime;

        public double RttMs => Runner.NetworkTime.rtt * 1000;

        // 서버 현재 tick 추정 ≈ (predictedTime − 편도지연)/interval. Lead = Tick − 이것 = (AheadMargin + 편도지연)/interval = 진짜 lead.
        public long ServerTickEstimate => (long)(Runner.NetworkTime.serverNow / Runner.Time.tickInterval);

        public long Lead => Runner.Time.tick - ServerTickEstimate;

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
