using GameFramework;

namespace LOP
{
    public interface IMatchmakingDataStore : IDataStore
    {
        int queueId { get; set; }
        int gameModeId { get; set; }
        int mapId { get; set; }
    }
}
