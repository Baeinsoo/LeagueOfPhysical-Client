namespace LOP
{
    public enum RecordDirection
    {
        LowerIsBetter,
        HigherIsBetter,
    }

    public enum RecordFormat
    {
        /// <summary>초 — <c>m:ss.s</c>, 차이 <c>±0.00초</c>.</summary>
        Time,

        /// <summary>횟수 — <c>n회</c>, 차이 <c>±n회</c>.</summary>
        Count,
    }

    /// <summary>
    /// 종목이 선언하는 개인 기록 지표 하나(spec §7). 결과판의 "내 기록" 카드는 이것만 보고 그린다 — 종목을 모른다.
    /// 새 종목은 지표 하나를 선언하고 경기 중에 <c>MatchRecordSheet.Set</c> 한 줄로 값을 적으면 붙는다.
    /// <para><see cref="Id"/>는 기기 저장 키에 들어간다(<see cref="RecordKey"/>) — 한 번 정하면 바꾸지 않는다.</para>
    /// </summary>
    public sealed class RecordMetric
    {
        /// <summary>틱(0.02초)의 4분의 1. 같은 틱이면 같은 기록이고(계산 오차는 이보다 훨씬 작다), 한 틱이라도 빠르면 경신이다.</summary>
        public const double TimeMargin = 0.005d;

        public readonly string Id;
        public readonly string Label;
        public readonly RecordDirection Direction;
        public readonly RecordFormat Format;

        /// <summary>카드 안 순서(작을수록 위).</summary>
        public readonly int Order;

        public RecordMetric(string id, string label, RecordDirection direction, RecordFormat format, int order)
        {
            Id = id;
            Label = label;
            Direction = direction;
            Format = format;
            Order = order;
        }

        //  횟수는 정수라 여유가 필요 없다.
        public double Margin => Format == RecordFormat.Time ? TimeMargin : 0d;

        /// <summary><paramref name="value"/>가 <paramref name="best"/>보다 여유를 넘게 좋은가. 같으면 아니다.</summary>
        public bool IsBetter(double value, double best)
            => Direction == RecordDirection.LowerIsBetter ? value < best - Margin : value > best + Margin;
    }
}
