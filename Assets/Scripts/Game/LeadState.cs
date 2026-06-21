namespace LOP
{
    /// <summary>
    /// 동적 lead 상태 홀더(클라). 입력 타이밍 핸들러가 LeadController로 AheadMargin을 갱신(0.5초 1회),
    /// LOPTickUpdater가 매 프레임 읽는다. Enabled로 고정/동적 A/B 토글. 게임 스코프 Singleton.
    /// </summary>
    public class LeadState
    {
        public const double DefaultMargin = 0.030;

        public double AheadMargin { get; set; } = DefaultMargin;
        public bool Enabled { get; set; } = true;  // 동적 조정 on/off (A/B 비교)
    }
}
