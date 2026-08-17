using System;

namespace LOP
{
    [Serializable]
    public class UserRatingDto
    {
        public string userId;
        public int queueId;
        public int mmr;
        public int gamesPlayed;
        public int firstPlaces;
        public int placementSum;
    }
}
