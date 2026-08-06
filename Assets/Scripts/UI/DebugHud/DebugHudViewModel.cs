using GameFramework;
using GameFramework.Runner;
using UnityEngine;

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
        private readonly GameFramework.Netcode.SnapshotArrivalStats snapshotArrivalStats;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly RemoteInterpolationClock remoteInterpolationClock;

        public DebugHudViewModel(
            IRunner runner,
            ReconciliationStats reconciliationStats,
            InputTimingStats inputTimingStats,
            GameFramework.Netcode.SnapshotHistory snapshotHistory,
            GameFramework.Netcode.SnapshotArrivalStats snapshotArrivalStats,
            GameFramework.World.EntityRegistry entityRegistry,
            RemoteInterpolationClock remoteInterpolationClock)
        {
            this.runner = runner;
            this.reconciliationStats = reconciliationStats;
            this.inputTimingStats = inputTimingStats;
            this.snapshotHistory = snapshotHistory;
            this.snapshotArrivalStats = snapshotArrivalStats;
            this.entityRegistry = entityRegistry;
            this.remoteInterpolationClock = remoteInterpolationClock;
        }

        // tickUpdater 체크가 결합의 핵심: Deinitialize가 tickUpdater/networkTime를 null로 만들 때
        // gameState는 그대로라(GameOver 등) 종료 창에서 getter가 null 역참조하는 걸 막는다.
        public bool IsRunning => runner.tickUpdater != null && runner.gameState >= RunnerState.Playing;

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

        // Time.smoothDeltaTime = Unity가 평활한 프레임 간격. 한 프레임 튄 값에 숫자가 요동치지 않는다.
        public float Fps => Time.smoothDeltaTime > 0f ? 1f / Time.smoothDeltaTime : 0f;

        public float FrameMs => Time.smoothDeltaTime * 1000f;

        public int EntityCount => entityRegistry.Count;

        public double CushionMs => remoteInterpolationClock.Cushion * 1000;

        // 벽시계로 추정한 서버 tick − 실제로 받은 최신 스냅의 tick. 절대값엔 편도지연이 상수로
        // 깔려 있으니 보는 건 "자라는가"다. 자라면 서버가 자기 틱을 못 따라가고 있다는 뜻.
        // 아직 아무것도 안 받았을 때(LatestTick=-1, 리셋 직후 포함) 큰 값이 튀지 않도록 0으로.
        public long ServerTickLag => snapshotArrivalStats.LatestTick < 0 ? 0 : ServerTickEstimate - snapshotArrivalStats.LatestTick;

        public double SnapIntervalAvgMs => snapshotArrivalStats.AverageInterval * 1000;

        public double SnapIntervalMaxMs => snapshotArrivalStats.MaxInterval * 1000;

        public void ResetStats()
        {
            reconciliationStats.Reset();
            snapshotArrivalStats.Reset();
        }
    }
}
