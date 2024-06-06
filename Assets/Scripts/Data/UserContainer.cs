using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public partial class UserContainer
    {
        public User user;

        public UserContainer()
        {
            user = new User();
        }

        public void Clear()
        {
            user = new User();
        }
    }
}
