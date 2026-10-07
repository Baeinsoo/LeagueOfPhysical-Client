namespace LOP.UI
{
    /// <summary>
    /// 랭크 표기는 여기 한 곳에서만 만든다 — 로비·대기·프로필·결과가 같은 말을 쓰게.
    /// 데이터(DTO)는 사실만 싣고, 문자열은 여기서 만든다.
    /// </summary>
    public static class RankFormat
    {
        private static readonly string[] Roman = { "", "I", "II", "III", "IV" };

        /// <summary>"골드 II", 마스터는 "마스터". 표에 없는 단계는 "?".</summary>
        public static string DivisionName(int divisionIndex, LOP.MasterData.TbRankDivision divisions)
        {
            var d = divisions.GetOrDefault(divisionIndex);
            if (d == null) return "?";
            return d.Division == 0 ? d.TierName : $"{d.TierName} {Roman[d.Division]}";
        }

        /// <summary>"배치 전" / "배치 2/5" / "골드 II · 45 LP".</summary>
        public static string Summary(RankDto rank, LOP.MasterData.TbRankDivision divisions)
        {
            if (rank.placementPlayed == 0) return "배치 전";
            if (rank.placementPlayed < rank.placementGames) return $"배치 {rank.placementPlayed}/{rank.placementGames}";
            return $"{DivisionName(rank.divisionIndex, divisions)} · {rank.lp} LP";
        }

        /// <summary>단계를 넘은 것까지 친 순 증감. 첫 배치 판은 앞 단계가 없어 얻은 LP 그대로.</summary>
        public static int NetLp(RankChangeDto c) =>
            c.divisionBefore.HasValue ? (c.divisionAfter - c.divisionBefore.Value) * 100 + c.lpAfter - c.lpBefore : c.lpAfter;

        /// <summary>"+18 LP" / "-7 LP" / "±0 LP".</summary>
        public static string LpDelta(RankChangeDto c) => $"{Signed(NetLp(c))} LP";

        /// <summary>결과 화면 한 줄 — 배치 / 배치 완료 / 승급 / 강등 / 평소.</summary>
        public static string ResultLine(RankChangeDto c, LOP.MasterData.TbRankDivision divisions)
        {
            string after = DivisionName(c.divisionAfter, divisions);
            if (c.wasPlacement && c.placementPlayed >= c.placementGames)
            {
                return $"배치 완료: {after}";
            }
            if (c.wasPlacement || !c.divisionBefore.HasValue)
            {
                return $"배치 {c.placementPlayed}/{c.placementGames} · {after} {c.lpAfter} LP";
            }

            string before = DivisionName(c.divisionBefore.Value, divisions);
            if (c.promoted) return $"승급! {before} → {after} ({LpDelta(c)})";
            if (c.demoted) return $"강등 {before} → {after} ({LpDelta(c)})";

            string sign = Signed(NetLp(c));
            return before == after
                ? $"{after} {c.lpBefore} → {c.lpAfter} LP ({sign})"
                : $"{before} {c.lpBefore} → {after} {c.lpAfter} LP ({sign})";
        }

        /// <summary>티어 색 USS 클래스("rank-tier--gold"). 표에 없으면 빈 문자열.</summary>
        public static string TierClass(int divisionIndex, LOP.MasterData.TbRankDivision divisions)
        {
            var d = divisions.GetOrDefault(divisionIndex);
            return d == null ? string.Empty : $"rank-tier--{d.TierCode}";
        }

        private static string Signed(int n) => n > 0 ? $"+{n}" : n < 0 ? n.ToString() : "±0";
    }
}
