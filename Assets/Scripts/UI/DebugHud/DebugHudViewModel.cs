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
        private readonly LeadState leadState;

        // 멈춤 횟수는 틱 업데이터가 세션 내내 누적한다. 측정 창의 답("이 창에 멈춤이 있었나")을
        // 얻으려면 리셋 시점 값을 기억해 두고 차이를 본다.
        private int catchUpBaseline;

        public DebugHudViewModel(
            IRunner runner,
            ReconciliationStats reconciliationStats,
            InputTimingStats inputTimingStats,
            GameFramework.Netcode.SnapshotHistory snapshotHistory,
            GameFramework.Netcode.SnapshotArrivalStats snapshotArrivalStats,
            GameFramework.World.EntityRegistry entityRegistry,
            RemoteInterpolationClock remoteInterpolationClock,
            LeadState leadState)
        {
            this.leadState = leadState;
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

        public int CorrectionCount => reconciliationStats.CorrectionCount;

        public double TimingAvgD => inputTimingStats.AvgD;

        public int TimingMaxD => inputTimingStats.MaxD;

        public int TimingPrune => inputTimingStats.PruneCount;

        public int TimingSeqGap => inputTimingStats.SeqGapCount;

        // 위 네 값은 최신 0.5초 창이라 사건이 지나가면 0으로 돌아간다. 판정은 아래 누적치로 한다.
        public int TimingTotalPrune => inputTimingStats.TotalPruneCount;

        public int TimingTotalSeqGap => inputTimingStats.TotalSeqGapCount;

        public int TimingWorstD => inputTimingStats.WorstMaxD == InputTimingStats.NoWorstMaxD ? 0 : inputTimingStats.WorstMaxD;

        // 동적 lead가 실제로 쥐고 있는 여유(ms). 이 값이 바닥에 붙어 있는지가 lead 정책의 진단점이다.
        public double AheadMarginMs => leadState.AheadMargin * 1000;

        public int CatchUpCapped => runner.tickUpdater == null ? 0 : runner.tickUpdater.catchUpCappedCount - catchUpBaseline;

        // 세션 전체 기준 최대 뒤처짐(리셋으로 안 지워진다). 크기만 참고하고, "이 창에 있었나"는 CatchUpCapped로 본다.
        public long MaxTicksBehind => runner.tickUpdater == null ? 0 : runner.tickUpdater.maxTicksBehind;

        // 갈아서 따라잡는 대신 번호만 옮긴 횟수. 멈춤이 여기로 흡수되면 CatchUpCapped는 0으로 보이므로 함께 봐야 한다.
        public int SnapForward => runner.tickUpdater == null ? 0 : runner.tickUpdater.snapForwardCount;

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
            inputTimingStats.Reset();

            if (runner.tickUpdater != null)
            {
                catchUpBaseline = runner.tickUpdater.catchUpCappedCount;
            }
        }

        // HUD 값이 많아 눈으로 옮겨 적기 어렵다. 한 줄로 찍어 두면 콘솔에서 그대로 가져갈 수 있다.
        public void DumpStats()
        {
            Debug.Log($"[HudDump] elapsed={ElapsedTime:F1} tick={Tick} fps={Fps:F0} frameMs={FrameMs:F1}" +
                      $" entities={EntityCount} reconMax={ReconMax:F3} reconAvg={ReconAverage:F3} reconLast={ReconLast:F3}" +
                      $" corrections={CorrectionCount}" +
                      $" snapLag={ServerTickLag} snapGapAvg={SnapIntervalAvgMs:F1} snapGapMax={SnapIntervalMaxMs:F1}" +
                      $" cushion={CushionMs:F1} rtt={RttMs:F0} lead={Lead} margin={AheadMarginMs:F0}" +
                      $" stalls={CatchUpCapped} snaps={SnapForward} behindMax={MaxTicksBehind}" +
                      $" dAvg={TimingAvgD:F1} dMax={TimingMaxD} prune={TimingPrune} seqGap={TimingSeqGap}" +
                      $" | worstD={TimingWorstD} pruneTot={TimingTotalPrune} seqGapTot={TimingTotalSeqGap}");
        }
    }
}
