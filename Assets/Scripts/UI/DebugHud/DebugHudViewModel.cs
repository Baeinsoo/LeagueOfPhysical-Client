using GameFramework;
using VContainer;

namespace LOP.UI
{
    /// <summary>
    /// 디버그/유틸 HUD ViewModel. tick·경과시간·RTT·서버tick추정·lead·reconciliation은 변경을
    /// 통지하는 이벤트 소스가 없는 샘플링 값이라 R3(push) 대신 평범한 getter로 노출하고,
    /// View가 매 프레임 pull한다. reconciliation 값은 ReconciliationStats(SnapReconciler가 write)에서 읽는다.
    /// </summary>
    public class DebugHudViewModel
    {
        [Inject]
        private ReconciliationStats reconciliationStats;

        public bool IsRunning => GameEngine.current != null;

        public long Tick => GameEngine.Time.tick;

        public double ElapsedTime => GameEngine.Time.elapsedTime;

        public double RttMs => Mirror.NetworkTime.rtt * 1000;

        // 서버 현재 tick 추정 ≈ (predictedTime − 편도지연)/interval. Lead = Tick − 이것 = (AheadMargin + 편도지연)/interval = 진짜 lead.
        public long ServerTickEstimate => (long)((Mirror.NetworkTime.predictedTime - Mirror.NetworkTime.rtt * 0.5) / GameEngine.Time.tickInterval);

        public long Lead => GameEngine.Time.tick - ServerTickEstimate;

        public float ReconLast => reconciliationStats.Last;

        public float ReconAverage => reconciliationStats.Average;

        public float ReconMax => reconciliationStats.Max;
    }
}
