using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public partial class UserDataContext : IDataContext
    {
        public Type[] subscribedTypes => new Type[]
        {
            typeof(CreateUserResponse),
            typeof(GetUserLocationResponse),
            typeof(GetUserResponse),
            typeof(GetUserStatsResponse),
            typeof(UpdateUserProfileResponse)
        };

        private Dictionary<Type, Action<object>> updateHandlers;

        public User user;
        public UserProfile userProfile;
        public UserLocation userLocation;
        public UserStats normalUserStats;
        public UserStats rankedUserStats;

        public UserDataContext()
        {
            user = new User();
            userProfile = new UserProfile();
            userLocation = new UserLocation();

            updateHandlers = new Dictionary<Type, Action<object>>
            {
                { typeof(CreateUserResponse), data => HandleCreateUser((CreateUserResponse)data) },
                { typeof(GetUserLocationResponse), data => HandleGetUserLocation((GetUserLocationResponse)data) },
                { typeof(GetUserResponse), data => HandleGetUser((GetUserResponse)data) },
                { typeof(GetUserStatsResponse), data => HandleGetUserStats((GetUserStatsResponse)data) },
                { typeof(UpdateUserProfileResponse), data => HandleUpdateUserProfile((UpdateUserProfileResponse)data) }
            };
        }

        public void UpdateData<T>(T data)
        {
            if (updateHandlers.TryGetValue(data.GetType(), out var handler))
            {
                handler(data);
            }
        }

        private void HandleCreateUser(CreateUserResponse response)
        {
            user = MapperConfig.mapper.Map<User>(response.user);
        }

        private void HandleGetUserLocation(GetUserLocationResponse response)
        {
            userLocation = MapperConfig.mapper.Map<UserLocation>(response.userLocation);
        }

        private void HandleGetUser(GetUserResponse response)
        {
            if (response.user == null)
            {
                return;
            }

            user = MapperConfig.mapper.Map<User>(response.user);
        }

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
            updateHandlers.Clear();
        }
    }
}
