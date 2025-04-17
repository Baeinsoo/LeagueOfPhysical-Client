using GameFramework;
using UnityEngine;

namespace LOP
{
    public interface IMatchMakingDataContext : IDataContext
    {
        GameMode matchType { get; set; }
        string subGameId { get; set; }
        string mapId { get; set; }
    }
}
