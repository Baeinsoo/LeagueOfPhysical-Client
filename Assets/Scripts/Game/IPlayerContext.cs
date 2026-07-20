using GameFramework;

namespace LOP
{
    public interface IPlayerContext
    {
        ISession session { get; set; }
        string entityId { get; set; }
        LOPActor actor { get; set; }
    }
}
