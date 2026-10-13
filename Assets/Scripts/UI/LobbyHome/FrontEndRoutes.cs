using System.Collections.Generic;

namespace LOP.UI
{
    /// <summary>로비 버튼 → 목적지 표. 아직 화면이 없는 곳(라커룸·미션·시즌)은 토스트로 알린다.</summary>
    public static class FrontEndRoutes
    {
        public const string ComingSoonText = "곧 열려요";

        public static readonly IReadOnlyDictionary<string, FrontEndDestination> LobbyButtons = new Dictionary<string, FrontEndDestination>
        {
            ["player-plate"] = FrontEndDestination.Profile,
            ["nav-shop"] = FrontEndDestination.Shop,
            ["nav-locker"] = FrontEndDestination.Locker,
            ["nav-records"] = FrontEndDestination.Profile,
            ["nav-missions"] = FrontEndDestination.Missions,
            ["nav-season"] = FrontEndDestination.Season,
            ["nav-settings"] = FrontEndDestination.Settings,
            ["game-chip"] = FrontEndDestination.GameSelect,
        };

        public static bool IsComingSoon(FrontEndDestination d) =>
            d == FrontEndDestination.Locker || d == FrontEndDestination.Missions || d == FrontEndDestination.Season;
    }
}
