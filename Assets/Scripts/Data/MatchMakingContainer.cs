using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public partial class MatchMakingContainer : IDataContainer
    {
        public MatchType matchType;
        public string subGameId;
        public string mapId;

        public void Clear()
        {
            matchType = default;
            subGameId = default;
            mapId = default;
        }
    }
}
