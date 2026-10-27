namespace LOP
{
    /// <summary>
    /// 한 지표의 이번 판 결과(spec §7) — 이번 값과 이 판 전의 저장된 최고. 기록지(<c>MatchRecordSheet</c>)가 판마다 한 번 만들고
    /// 결과(<c>MatchResult.myRecords</c>)에 실어 결과판으로 나른다. 순수 값이라 시험이 기기 저장 없이 돈다.
    /// </summary>
    public readonly struct RecordOutcome
    {
        public readonly RecordMetric Metric;
        public readonly double Value;

        /// <summary>종목·맵을 알아 저장된 최고와 비교했나. 아니면 값만 보이고 저장하지 않는다.</summary>
        public readonly bool Compared;

        public readonly bool HasPrevious;

        /// <summary>이 판 전의 최고. <see cref="HasPrevious"/>가 거짓이면 뜻이 없다.</summary>
        public readonly double Previous;

        public RecordOutcome(RecordMetric metric, double value, bool compared, bool hasPrevious, double previous)
        {
            Metric = metric;
            Value = value;
            Compared = compared;
            HasPrevious = compared && hasPrevious;
            Previous = previous;
        }

        public bool IsFirst => Compared && HasPrevious == false;

        //  저장값이 없으면(첫 판) 경신이 아니다 — 첫 판마다 NEW BEST가 뜨면 의미가 없다(연출 절제).
        public bool IsNewBest => Compared && HasPrevious && Metric.IsBetter(Value, Previous);

        public bool ShouldSave => IsFirst || IsNewBest;

        public string ValueText => RecordText.Value(Metric, Value);

        /// <summary>최고와의 차이, 처음이면 "첫 기록", 비교 못 했으면 빈 문자열.</summary>
        public string DiffText
        {
            get
            {
                if (Compared == false)
                {
                    return string.Empty;
                }
                return IsFirst ? RecordText.FirstRecord : RecordText.Diff(Metric, Value, Previous);
            }
        }
    }
}
