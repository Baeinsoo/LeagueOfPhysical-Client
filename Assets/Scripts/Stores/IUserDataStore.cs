using GameFramework;
using R3;
using UnityEngine;

namespace LOP
{
    public interface IUserDataStore : IDataStore
    {
        User user { get; set; }
        UserProfile userProfile { get; set; }
        //  위치는 바뀌는 걸 알아야 하는 소비자가 있어 관찰 가능하게 노출한다. 쓰기는 스토어 안에서만.
        ReadOnlyReactiveProperty<UserLocation> userLocation { get; }
        System.Collections.Generic.IReadOnlyDictionary<int, UserRating> userRatingByQueueId { get; }
    }
}
