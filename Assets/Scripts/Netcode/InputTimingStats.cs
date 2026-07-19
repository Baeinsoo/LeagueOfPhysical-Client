namespace LOP
{
    /// <summary>
    /// netcode 측정용 입력 타이밍 피드백 홀더(클라). InputTimingToC 핸들러가 최신 요약을 write하고
    /// DebugHud가 pull해 표시한다. 게임 스코프 Singleton이라 게임마다 리셋된다. (ReconciliationStats 패턴)
    /// </summary>
    public class InputTimingStats
    {
        public double AvgD { get; private set; }
        public int MaxD { get; private set; }
        public int PruneCount { get; private set; }
        public int SeqGapCount { get; private set; }
        public bool HasData { get; private set; }

        public void Update(double avgD, int maxD, int pruneCount, int seqGapCount)
        {
            AvgD = avgD;
            MaxD = maxD;
            PruneCount = pruneCount;
            SeqGapCount = seqGapCount;
            HasData = true;
        }
    }
}
