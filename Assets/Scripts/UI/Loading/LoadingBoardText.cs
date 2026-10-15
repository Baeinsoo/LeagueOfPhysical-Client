namespace LOP.UI
{
    /// <summary>로딩판 글자 — 태그(일반/리그), 종목, 맵, 한 줄 설명. 종목을 아직 모르면(랭크 추첨 전·재접속) 짧은 안내.</summary>
    public readonly struct LoadingBoardText
    {
        public const string CasualTag = "일반 경기";
        public const string RankedTag = "리그 경기 · 랭크";
        public const string Drawing = "종목 추첨 중…";
        public const string Entering = "경기장 입장 중…";

        public readonly string Tag;
        public readonly string Title;
        public readonly string Map;
        public readonly string Description;
        public readonly bool Ranked;

        private LoadingBoardText(bool ranked, string title, string map, string description)
        {
            Ranked = ranked;
            Tag = ranked ? RankedTag : CasualTag;
            Title = title;
            Map = map ?? string.Empty;
            Description = description ?? string.Empty;
        }

        public static LoadingBoardText From(bool ranked, string modeName, string mapName, string description) =>
            string.IsNullOrEmpty(modeName)
                ? new LoadingBoardText(ranked, ranked ? Drawing : Entering, null, null)
                : new LoadingBoardText(ranked, modeName, mapName, description);
    }

    /// <summary>로딩판이 쓸 큐·종목·맵. 경기 정보(match)가 오면 그것을, 아니면 플레이 때 고른 값을 쓴다.</summary>
    public static class LoadingBoardSource
    {
        public static (bool ranked, int gameModeId, int mapId) Resolve(int storedQueueId, int storedGameModeId, int storedMapId, Match match, int rankedQueueId)
        {
            if (match == null) return (storedQueueId == rankedQueueId, storedGameModeId, storedMapId);
            bool ranked = match.queueId == rankedQueueId;
            if (match.rounds == null || match.rounds.Length == 0) return (ranked, ranked ? 0 : storedGameModeId, ranked ? 0 : storedMapId);
            var round = match.rounds[MatchSceneResolver.CurrentRoundIndex(match.rounds.Length)];
            return (ranked, round.gameModeId, round.mapId);
        }
    }
}
