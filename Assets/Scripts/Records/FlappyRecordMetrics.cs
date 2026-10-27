namespace LOP
{
    /// <summary>플래피 레이스의 개인 기록 지표(spec §7). id는 저장 키에 들어가니 바꾸지 않는다.</summary>
    public static class FlappyRecordMetrics
    {
        /// <summary>출발 틱 → 내 결승 틱. 서버 등수가 와야 적는다(<c>FlappyFinishRecorder</c>).</summary>
        public static readonly RecordMetric FinishTime =
            new RecordMetric("flappy.finishTime", "완주 시간", RecordDirection.LowerIsBetter, RecordFormat.Time, 0);

        /// <summary>기절 없이 이어진 날갯짓의 이번 판 최고(<c>FlappyFlapStreak</c>).</summary>
        public static readonly RecordMetric FlapStreak =
            new RecordMetric("flappy.flapStreak", "연속 날갯짓 최고", RecordDirection.HigherIsBetter, RecordFormat.Count, 1);
    }
}
