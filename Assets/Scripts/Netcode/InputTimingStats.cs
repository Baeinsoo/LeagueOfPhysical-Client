namespace LOP
{
    /// <summary>
    /// netcode 측정용 입력 타이밍 피드백 홀더(클라). InputTimingToC 핸들러가 최신 요약을 write하고
    /// DebugHud가 pull해 표시한다. 게임 스코프 Singleton이라 게임마다 리셋된다. (ReconciliationStats 패턴)
    ///
    /// 최신 창(0.5초) 값과 리셋 이후 누적치를 함께 둔다. 창 값만 보면 사건이 지나간 뒤엔 0이라
    /// "실패가 없었다"로 읽히는데, 실제로 그렇게 오독해 수정이 먹혔다고 판단할 뻔했다.
    /// </summary>
    public class InputTimingStats
    {
        public double AvgD { get; private set; }
        public int MaxD { get; private set; }
        public int PruneCount { get; private set; }
        public int SeqGapCount { get; private set; }
        public bool HasData { get; private set; }

        public int TotalPruneCount { get; private set; }
        public int TotalSeqGapCount { get; private set; }

        // 리셋 이후 가장 늦게 도착한 입력(틱). 음수면 이르게, 양수면 지각.
        // 아직 창을 하나도 못 받았으면 NoWorstMaxD.
        public const int NoWorstMaxD = int.MinValue;
        public int WorstMaxD { get; private set; } = NoWorstMaxD;

        public void Update(double avgD, int maxD, int pruneCount, int seqGapCount)
        {
            AvgD = avgD;
            MaxD = maxD;
            PruneCount = pruneCount;
            SeqGapCount = seqGapCount;
            HasData = true;

            TotalPruneCount += pruneCount;
            TotalSeqGapCount += seqGapCount;

            if (maxD > WorstMaxD)
            {
                WorstMaxD = maxD;
            }
        }

        public void Reset()
        {
            TotalPruneCount = 0;
            TotalSeqGapCount = 0;
            WorstMaxD = NoWorstMaxD;
        }
    }
}
