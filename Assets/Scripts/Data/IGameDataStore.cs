using GameFramework;
using UnityEngine;

namespace LOP
{
    public interface IGameDataStore : IDataStore
    {
        GameInfo gameInfo { get; set; }
        string userEntityId { get; set; }
    }
}
