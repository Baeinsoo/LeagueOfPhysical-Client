using System;

namespace LOP
{
    /// <summary>이번 시즌의 내 보이는 랭크(로비 GET /user/:id/rank). 배치 전이면 divisionIndex -1.</summary>
    [Serializable]
    public class RankDto
    {
        public int season;
        public int split;
        public int divisionIndex;
        public string tierCode;
        public int division;
        public int lp;
        public int placementPlayed;
        public int placementGames;
        public int peakDivisionIndex;
        public int gamesPlayed;
    }

    /// <summary>한 판이 보이는 랭크를 어떻게 바꿨나. 첫 배치 판이면 divisionBefore가 null.</summary>
    [Serializable]
    public class RankChangeDto
    {
        public int? divisionBefore;
        public int lpBefore;
        public int divisionAfter;
        public int lpAfter;
        public int placementPlayed;
        public int placementGames;
        public bool promoted;
        public bool demoted;
        /// <summary>이번 판이 배치 판이었나(서버가 싣는다). 마지막 배치 판 = 이것 && placementPlayed == placementGames.</summary>
        public bool wasPlacement;
    }
}
