using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class MatchmakingDataStore : IMatchmakingDataStore
    {
        public int queueId { get; set; }
        public int gameModeId { get; set; }
        public int mapId { get; set; }

        public void Clear()
        {
            queueId = default;
            gameModeId = default;
            mapId = default;
        }
    }
}
