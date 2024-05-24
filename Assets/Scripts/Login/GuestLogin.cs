using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class GuestLogin : MonoBehaviour
    {
        public LoginResult Login()
        {
            return new LoginResult(true, "always success", SystemInfo.deviceUniqueIdentifier);
        }

        public LogoutResult Logout()
        {
            return new LogoutResult(true, "always success");
        }
    }
}
