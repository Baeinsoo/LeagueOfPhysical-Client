using GameFramework;
using UnityEngine;

namespace LOP
{
    public interface IUserDataContext : IDataContext
    {
        User user { get; set; }
        UserProfile userProfile { get; set; }
        UserLocation userLocation { get; set; }
        UserStats normalUserStats { get; set; }
        UserStats rankedUserStats { get; set; }
    }
}
