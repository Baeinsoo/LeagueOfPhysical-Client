using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public partial class UserDataContext : IDataContext
    {
        public User user;
        public UserLocation userLocation;

        public UserDataContext()
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
