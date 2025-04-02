using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class User
    {
        public string id;
        public string username;
        public string email;

        public User()
        {
            username = SystemInfo.deviceUniqueIdentifier;
            email = $"{username}@email";
        }
    }
}
