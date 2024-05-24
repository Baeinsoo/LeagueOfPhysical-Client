using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public partial class UserData
    {
        public User user;

        public UserData()
        {
            user = new User();
        }

        public void Clear()
        {
            user = new User();
        }
    }
}
