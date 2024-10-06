using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    [Serializable]
    public class MatchDto
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
