namespace LOP
{
    /// <summary>
    /// 카드 러시의 개인 기록 지표(spec 2026-11-05 §10). id는 저장 키에 들어가니 바꾸지 않는다.
    /// 3차(장애물 레이스)는 옛 문 40칸과 다른 경기라 새 키로 시작한다 — 옛 키(cardrush.finishTime·streakBest)는 기기에 남지만 안 읽는다.
    /// </summary>
    public static class CardRushRecordMetrics
    {
        /// <summary>출발 틱 → 완주 틱. 서버 등수가 와야 적는다(<see cref="CardRushRecordReporter"/>).</summary>
        public static readonly RecordMetric FinishTime =
            new RecordMetric("cardrush.v3.finishTime", "완주 시간", RecordDirection.LowerIsBetter, RecordFormat.Time, 0);

        /// <summary>서버 확정 연속 무사 통과(장애물·구역을 맞지 않고 넘김)의 이번 판 최고.</summary>
        public static readonly RecordMetric CleanBest =
            new RecordMetric("cardrush.v3.cleanBest", "연속 무사 통과", RecordDirection.HigherIsBetter, RecordFormat.Count, 1);
    }
}
