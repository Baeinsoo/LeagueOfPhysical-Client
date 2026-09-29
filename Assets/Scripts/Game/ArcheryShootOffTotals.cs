using System.Collections.Generic;

namespace LOP
{
    /// <summary>
    /// 한 발 승부 총점을 라운드 결과로 직접 센다. 결과 이벤트(신뢰 채널)가 점수 스냅샷(비신뢰, 뒤에 옴)보다 먼저 와서
    /// 마지막 라운드 결과 순간의 <see cref="ArcheryScore"/>는 아직 그 전 점수다 — "꼴찌 확정"을 거기서 고르면 딴 사람이 된다.
    /// 처음 보는 사람만 그때의 스냅샷 점수에서 시작한다(판 중간에 들어온 경우).
    /// </summary>
    public sealed class ArcheryShootOffTotals
    {
        private readonly Dictionary<string, int> totals = new Dictionary<string, int>();

        /// <param name="currentScore">처음 보는 사람의 시작 점수(스냅샷). 이미 아는 사람에겐 부르지 않는다.</param>
        public void Add(IReadOnlyList<ArcheryRoundPlacement> placements, System.Func<string, int> currentScore)
        {
            foreach (var p in placements)
            {
                if (totals.TryGetValue(p.ShooterId, out int total) == false)
                {
                    total = currentScore(p.ShooterId);
                }
                totals[p.ShooterId] = total + p.Points;
            }
        }

        /// <summary>총점 꼴찌. 동점이면 id 서수가 뒤인 사람. 아무도 없으면 null.</summary>
        public string Bottom()
        {
            string worst = null;
            int worstScore = int.MaxValue;
            foreach (var pair in totals)
            {
                if (pair.Value < worstScore || (pair.Value == worstScore && string.CompareOrdinal(pair.Key, worst) > 0))
                {
                    worst = pair.Key;
                    worstScore = pair.Value;
                }
            }
            return worst;
        }
    }
}
