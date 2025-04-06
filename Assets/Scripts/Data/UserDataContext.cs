using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public partial class UserDataContext : IDataContext
    {
        public Type[] subscribedTypes => new Type[] { typeof(UserDto), typeof(UserProfileDto), typeof(UserLocationDto) };

        public User user;
        public UserProfile userProfile;
        public UserLocation userLocation;

        public UserDataContext()
        {
            user = new User();
            userProfile = new UserProfile();
            userLocation = new UserLocation();
        }

        public void UpdateData<T>(T data)
        {
            if (data is UserDto userDto)
            {
                user = MapperConfig.mapper.Map<User>(userDto);
            }
            else if (data is UserProfileDto userProfileDto)
            {
                userProfile = MapperConfig.mapper.Map<UserProfile>(userProfileDto);
            }
            else if (data is UserLocationDto userLocationDto)
            {
                userLocation = MapperConfig.mapper.Map<UserLocation>(userLocationDto);
            }
        }

        public void Clear()
        {
            user = new User();
            userProfile = new UserProfile();
            userLocation = new UserLocation();
        }
    }
}
