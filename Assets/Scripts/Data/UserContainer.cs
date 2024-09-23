using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public partial class UserContainer : IDataContainer
    {
        public User user;
        public UserLocation userLocation;

        public UserContainer()
        {
            user = new User();
            userLocation = new UserLocation();
        }

        public void Clear()
        {
            user = new User();
            userLocation = new UserLocation();
        }
    }
}
