using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public partial class MatchMakingDataContext : IDataContext
    {
        public Type[] subscribedTypes => new Type[] { };

        public MatchType matchType;
        public string subGameId;
        public string mapId;

        public void UpdateData<T>(T data)
        {
        }

        public void Clear()
        {
            matchType = default;
            subGameId = default;
            mapId = default;
        }
    }
}
