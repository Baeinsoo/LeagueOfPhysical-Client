using GameFramework;
using UnityEngine;

namespace LOP
{
    public class PlayerContext : IPlayerContext
    {
        public ISession session { get; set; }
        public LOPEntity entity { get; set; }
        public LOPEntityView entityView { get; set; }
    }
}
