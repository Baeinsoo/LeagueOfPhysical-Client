using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class MatchMakingDataStore : IMatchMakingDataStore
    {
        public GameMode matchType { get; set; }
        public string subGameId { get; set; }
        public string mapId { get; set; }

        public void Clear()
        {
            matchType = default;
            subGameId = default;
            mapId = default;
        }
    }
}
