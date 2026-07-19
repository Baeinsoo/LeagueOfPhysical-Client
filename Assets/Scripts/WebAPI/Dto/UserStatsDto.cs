using System;

namespace LOP
{
    [Serializable]
    public class UserStatsDto
    {
        public string userId;
        public GameMode gameMode;
        public int gamesPlayed;
        public int wins;
        public int losses;
        public int draws;
        public int eloRating;
        public string tier;
    }
}
