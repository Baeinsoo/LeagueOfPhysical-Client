using GameFramework;

namespace LOP
{
    public class PlayerContext : IPlayerContext
    {
        public ISession session { get; set; }
        public string entityId { get; set; }
        public LOPEntityView entityView { get; set; }
    }
}
