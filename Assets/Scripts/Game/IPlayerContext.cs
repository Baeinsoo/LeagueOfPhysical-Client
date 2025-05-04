using GameFramework;
using UnityEngine;

namespace LOP
{
    public interface IPlayerContext
    {
        ISession session { get; set; }
        LOPEntity entity { get; set; }
    }
}
