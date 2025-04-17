using GameFramework;
using UnityEngine;

namespace LOP
{
    public interface IRoomDataContext : IDataContext
    {
        Room room { get; set; }
        Match match { get; set; }
    }
}
