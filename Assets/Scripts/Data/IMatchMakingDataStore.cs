using GameFramework;
using UnityEngine;

namespace LOP
{
    public interface IMatchMakingDataStore : IDataStore
    {
        GameMode matchType { get; set; }
        string subGameId { get; set; }
        string mapId { get; set; }
    }
}
