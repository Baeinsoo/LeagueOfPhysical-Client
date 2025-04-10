using System;

namespace LOP
{
    public class UserStats
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

    [Serializable]
    public enum GameMode
    {
        Normal = 0,
        Ranked = 1,
    }
}
