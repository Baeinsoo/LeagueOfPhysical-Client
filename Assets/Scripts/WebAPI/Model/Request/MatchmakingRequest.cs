using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LOP
{
    public class MatchmakingRequest
    {
        public string userId;
        public GameMode matchType;
        public string subGameId;
        public string mapId;
    }
}
