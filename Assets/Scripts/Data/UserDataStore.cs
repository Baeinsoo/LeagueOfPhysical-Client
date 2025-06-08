using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class UserDataStore : IUserDataStore
    {
        public User user { get; set; } = new User();
        public UserProfile userProfile { get; set; } = new UserProfile();
        public UserLocation userLocation { get; set; } = new UserLocation();
        public UserStats normalUserStats { get; set; }
        public UserStats rankedUserStats { get; set; }

        [DataListen(typeof(CreateUserResponse))]
        private void HandleCreateUser(CreateUserResponse response)
        {
            user = MapperConfig.mapper.Map<User>(response.user);
        }

        [DataListen(typeof(GetUserLocationResponse))]
        private void HandleGetUserLocation(GetUserLocationResponse response)
        {
            userLocation = MapperConfig.mapper.Map<UserLocation>(response.userLocation);
        }

        [DataListen(typeof(GetUserResponse))]
        private void HandleGetUser(GetUserResponse response)
        {
            if (response.user == null)
            {
                return;
            }

            user = MapperConfig.mapper.Map<User>(response.user);
        }

        [DataListen(typeof(GetUserStatsResponse))]
        private void HandleGetUserStats(GetUserStatsResponse response)
        {
            UserStats userStats = MapperConfig.mapper.Map<UserStats>(response.userStats);

            if (userStats.gameMode == GameMode.Normal)
            {
                normalUserStats = userStats;
            }
            else if (userStats.gameMode == GameMode.Ranked)
            {
                rankedUserStats = userStats;
            }
        }

        [DataListen(typeof(UpdateUserProfileResponse))]
        private void HandleUpdateUserProfile(UpdateUserProfileResponse response)
        {
            userProfile = MapperConfig.mapper.Map<UserProfile>(response.userProfile);
        }

        public void Clear()
        {
            user = new User();
            userProfile = new UserProfile();
            userLocation = new UserLocation();
            normalUserStats = null;
            rankedUserStats = null;
        }
    }
}
