using GameFramework;
using UnityEngine;

namespace LOP
{
    public interface IMatchmakingDataStore : IDataStore
    {
        GameMode matchType { get; set; }
        string subGameId { get; set; }
        string mapId { get; set; }
    }
}
