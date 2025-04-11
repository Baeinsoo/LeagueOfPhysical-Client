using System;
using UnityEngine;

namespace LOP
{
    public class Match
    {
        public string id;
        public MatchType matchType;
        public string subGameId;
        public string mapId;
        public int targetRating;
        public string[] playerList;
    }

    [Serializable]
    public enum MatchType
    {
        Friendly = 0,
        Rank = 1,
    }
}
