using GameFramework;
using UnityEngine;

namespace LOP
{
    public interface IRoomDataStore : IDataStore
    {
        Room room { get; set; }
        Match match { get; set; }
    }
}
