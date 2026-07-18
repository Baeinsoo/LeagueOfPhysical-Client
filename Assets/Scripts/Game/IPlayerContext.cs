using GameFramework;
using UnityEngine;

namespace LOP
{
    public interface IPlayerContext
    {
        ISession session { get; set; }
        LOPActor actor { get; set; }
        LOPEntityView entityView { get; set; }
    }
}
