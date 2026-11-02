namespace LOP
{
    /// <summary>카드 러시의 개인 기록 지표(spec §7). id는 저장 키에 들어가니 바꾸지 않는다.</summary>
    public static class CardRushRecordMetrics
    {
        /// <summary>출발 틱 → 마지막 정타(완주) 틱. 서버 등수가 와야 적는다(<see cref="CardRushRecordReporter"/>).</summary>
        public static readonly RecordMetric FinishTime =
            new RecordMetric("cardrush.finishTime", "완주 시간", RecordDirection.LowerIsBetter, RecordFormat.Time, 0);

        /// <summary>서버 확정 연속 정타 수의 이번 판 최고.</summary>
        public static readonly RecordMetric StreakBest =
            new RecordMetric("cardrush.streakBest", "연속 정타 최고", RecordDirection.HigherIsBetter, RecordFormat.Count, 1);
    }
}
