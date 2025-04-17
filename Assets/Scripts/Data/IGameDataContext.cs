using GameFramework;
using UnityEngine;

namespace LOP
{
    public interface IGameDataContext : IDataContext
    {
        Player player { get; set; }
        GameInfo gameInfo { get; set; }
    }
}
