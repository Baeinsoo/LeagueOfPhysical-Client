namespace LOP
{
    public enum RankChange
    {
        None,
        /// <summary>순위 숫자가 작아졌다(추월).</summary>
        Up,
        /// <summary>순위 숫자가 커졌다(추월당함).</summary>
        Down,
    }

    /// <summary>
    /// 순위가 "바뀌었다"고 화면에 알릴지. 출발 직후엔 같은 x에서 줄을 서며 순위가 틱마다 뒤섞이고,
    /// 접전에선 두 새가 앞뒤를 주고받는다 — 그때마다 튕기면 숫자가 깜빡일 뿐 아무것도 안 알려 준다.
    /// <para>시제품(`flappy-applied.html`)과 같은 규칙: 출발 2.5초 유예, 직전 알림 0.8초 안의 변화는 삼킨다.
    /// 삼켜도 다음 비교 기준은 새 순위로 옮긴다(숫자는 이미 바뀌어 보이고 있다).</para>
    /// </summary>
    public class RankChangeDebounce
    {
        public const double Cooldown = 0.8;
        public const double StartGrace = 2.5;

        private int last;
        private double lastFired = double.NegativeInfinity;

        /// <param name="rank">지금 내 순위(1부터, 0 = 내 새가 없다).</param>
        /// <param name="raceSeconds">출발부터 흐른 초(<see cref="RaceClock.ElapsedSeconds"/>).</param>
        /// <param name="frozen">내가 결승했나. 결승 뒤의 변화는 추월이 아니다.</param>
        public RankChange Observe(int rank, double raceSeconds, bool frozen)
        {
            int before = last;
            last = rank;

            if (before == 0 || rank == 0 || rank == before || frozen)
            {
                return RankChange.None;
            }
            if (raceSeconds <= StartGrace || raceSeconds - lastFired < Cooldown)
            {
                return RankChange.None;
            }

            lastFired = raceSeconds;
            return rank < before ? RankChange.Up : RankChange.Down;
        }
    }
}
