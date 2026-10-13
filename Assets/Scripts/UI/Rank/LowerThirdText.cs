namespace LOP.UI
{
    /// <summary>방송 자막의 랭크 줄과 그 아래 줄. 받기 전·못 받음에도 빈칸이나 "★ "만 남기지 않고 짧게 쓴다.</summary>
    public readonly struct LowerThirdText
    {
        public const string Loading = "★ 랭크 확인 중";
        public const string Unavailable = "★ 랭크 정보 없음";

        public readonly string Rank;
        public readonly string Sub;

        private LowerThirdText(string rank, string sub) { Rank = rank; Sub = sub; }

        public static LowerThirdText From(RankCard card)
        {
            if (!card.Loaded) return new LowerThirdText(card.Headline == null ? Loading : Unavailable, string.Empty);
            if (string.IsNullOrEmpty(card.Headline)) return new LowerThirdText(Loading, string.Empty);
            return new LowerThirdText($"★ {card.Headline}", card.Subline ?? string.Empty);
        }
    }
}
