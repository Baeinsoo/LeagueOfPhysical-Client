using System.Collections.Generic;

namespace LOP
{
    /// <summary>라운드 결과 → 해설 종류. 연승과 지난 라운드 순위를 기억한다(호출 순서가 곧 라운드 순서).</summary>
    public sealed class ArcheryShootOffNarrator
    {
        private const float CloseMeters = 0.03f;

        private readonly Dictionary<string, int> streaks = new Dictionary<string, int>();
        private readonly Dictionary<string, int> lastRank = new Dictionary<string, int>();
        private int lastCount;

        //  지난 라운드에 아무도 못 맞혔으면 모두가 공동 꼴찌다 — 그걸 "꼴찌에서 1등"으로 읽지 않는다.
        private bool lastAnyHit;

        /// <param name="subjectId">해설의 주인공(1등, 또는 못 맞힌 나).</param>
        /// <param name="number">연승 수 또는 1·2등 차이(cm). 해당 없으면 0.</param>
        public ArcheryLine LineFor(ArcheryRoundResultEvent result, string myEntityId,
                                   out string subjectId, out int number)
        {
            var placements = result.placements;
            number = 0;
            subjectId = placements.Count > 0 ? placements[0].ShooterId : string.Empty;

            bool anyHit = placements.Count > 0 && placements[0].Hit;
            var winner = anyHit ? placements[0] : default;
            bool wasLast = anyHit && result.roundIndex > 0 && lastAnyHit && lastCount > 1
                        && lastRank.TryGetValue(winner.ShooterId, out int prev) && prev == lastCount - 1;

            foreach (var p in placements)
            {
                bool first = p.Hit && p.Rank == 0;
                streaks[p.ShooterId] = first ? (streaks.TryGetValue(p.ShooterId, out int s) ? s + 1 : 1) : 0;
            }
            int winnerStreak = anyHit ? streaks[winner.ShooterId] : 0;

            lastRank.Clear();
            foreach (var p in placements) { lastRank[p.ShooterId] = p.Rank; }
            lastCount = placements.Count;
            lastAnyHit = anyHit;

            if (wasLast)
            {
                return ArcheryLine.Comeback;
            }
            if (winnerStreak >= 3)
            {
                number = winnerStreak;
                return ArcheryLine.Streak;
            }
            //  거리가 같으면(공동 1등) 차이가 없는 것이다 — "단 1cm"로 부풀리지 않는다.
            if (anyHit && placements.Count > 1 && placements[1].Hit
                && placements[1].Distance > winner.Distance
                && placements[1].Distance - winner.Distance <= CloseMeters)
            {
                number = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.RoundToInt(
                    (placements[1].Distance - winner.Distance) * 100f));
                return ArcheryLine.Close;
            }
            foreach (var p in placements)
            {
                if (p.ShooterId == myEntityId && p.Hit == false)
                {
                    subjectId = myEntityId;
                    return ArcheryLine.NoHit;
                }
            }
            return ArcheryLine.Win;
        }
    }
}
