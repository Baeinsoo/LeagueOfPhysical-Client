using GameFramework;
using UnityEngine;

namespace LOP
{
    public interface IGameDataContext : IDataContext
    {
        GameInfo gameInfo { get; set; }
    }
}
