using GameFramework;

namespace LOP
{
    public interface IMatchResultDataStore : IDataStore
    {
        MatchResult result { get; set; }
    }
}
