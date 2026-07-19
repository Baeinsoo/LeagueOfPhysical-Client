using GameFramework;

namespace LOP
{
    public interface IPlayerContext
    {
        ISession session { get; set; }
        string entityId { get; set; }
        LOPEntityView entityView { get; set; }
    }
}
