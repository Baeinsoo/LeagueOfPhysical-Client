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
        public MatchStatus status;
        public string[] playerList;
    }

    [Serializable]
    public enum MatchType
    {
        Friendly = 0,
        Rank = 1,
    }

    [Serializable]
    public enum MatchStatus
    {
        None = 0,
        MatchStart = 1,
        MatchEnd = 2,
    }
}
