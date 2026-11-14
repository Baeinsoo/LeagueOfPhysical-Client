namespace LOP
{
    /// <summary>
    /// 카드 러시의 개인 기록 지표(spec 2026-11-05 §10). id는 저장 키에 들어가니 바꾸지 않는다.
    /// 4.2차(달리기 4/3배, 굴러오는 것·빨간 바닥 없음)는 4차 시간과 비교할 수 없어 새 키로 시작한다 — 옛 키(cardrush.finishTime·streakBest·v3.*·v4.*)는 기기에 남지만 안 읽는다.
    /// </summary>
    public static class CardRushRecordMetrics
    {
        /// <summary>출발 틱 → 완주 틱. 서버 등수가 와야 적는다(<see cref="CardRushRecordReporter"/>).</summary>
        public static readonly RecordMetric FinishTime =
            new RecordMetric("cardrush.v42.finishTime", "완주 시간", RecordDirection.LowerIsBetter, RecordFormat.Time, 0);

        /// <summary>서버 확정 연속 무사 통과(장애물·구역을 맞지 않고 넘김)의 이번 판 최고.</summary>
        public static readonly RecordMetric CleanBest =
            new RecordMetric("cardrush.v42.cleanBest", "연속 무사 통과", RecordDirection.HigherIsBetter, RecordFormat.Count, 1);
    }
}
