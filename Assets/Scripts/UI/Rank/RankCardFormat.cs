namespace LOP.UI
{
    public readonly struct RankCard
    {
        public readonly bool Loaded;
        public readonly string TierCode;
        public readonly string Headline;
        public readonly string Subline;

        public RankCard(bool loaded, string tierCode, string headline, string subline)
        {
            Loaded = loaded; TierCode = tierCode; Headline = headline; Subline = subline;
        }
    }

    /// <summary>로비 랭크 모드 티어 카드의 문구. 상대 티어 범위는 약속하지 않는다(매칭은 숨은 점수로 한다).</summary>
    public static class RankCardFormat
    {
        public const string LoadFailed = "랭크 정보를 불러오지 못했습니다";

        public static RankCard Failed() => new RankCard(false, null, LoadFailed, string.Empty);

        public static RankCard From(RankDto rank, LOP.MasterData.TbRankDivision divisions)
        {
            var d = divisions.GetOrDefault(rank.divisionIndex);
            string tier = d?.TierCode;
            string current = d == null ? null : $"{RankFormat.DivisionName(rank.divisionIndex, divisions)} · {rank.lp} LP";

            if (rank.placementPlayed < rank.placementGames)
            {
                string head = rank.placementPlayed == 0 || current == null ? "배치 전" : current;
                return new RankCard(true, tier, head, $"배치 {rank.placementPlayed}/{rank.placementGames}");
            }

            bool top = divisions.GetOrDefault(rank.divisionIndex + 1) == null;
            string peak = divisions.GetOrDefault(rank.peakDivisionIndex) == null
                ? null
                : $"시즌 최고 {RankFormat.DivisionName(rank.peakDivisionIndex, divisions)}";
            string promote = top ? null : $"승급까지 {100 - rank.lp} LP";
            string sub = promote != null && peak != null ? $"{promote} · {peak}" : promote ?? peak ?? string.Empty;

            return new RankCard(true, tier, current ?? "배치 전", sub);
        }
    }
}
