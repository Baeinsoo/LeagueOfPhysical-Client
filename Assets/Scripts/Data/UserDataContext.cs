using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public partial class UserDataContext : IDataContext
    {
        public Type[] subscribedTypes => new Type[] { };

        public User user;
        public UserLocation userLocation;

        public UserDataContext()
        {
            user = new User();
            userLocation = new UserLocation();
        }

        public void UpdateData<T>(T data)
        {
        }

        public void Clear()
        {
            user = new User();
            userLocation = new UserLocation();
        }
    }
}
