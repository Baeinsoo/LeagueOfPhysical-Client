using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace LOP
{
    [DontDestroyMonoSingleton]
    public class LoginService : MonoSingleton<LoginService>
    {
        private const string LOGIN_TYPE_KEY = "LOGIN_TYPE_KEY";

        private GuestLogin guestLogin;

        protected override void Awake()
        {
            base.Awake();

            guestLogin = this.GetOrAddComponent<GuestLogin>();
        }

        public async Task<bool> TryAutoLogin()
        {
            if (!PlayerPrefs.HasKey(LOGIN_TYPE_KEY))
            {
                return false;
            }

            var loginType = PlayerPrefs.GetString(LOGIN_TYPE_KEY).Parse<LoginType>();

            Login(loginType);

            return true;
        }

        public void Login(LoginType loginType)
        {
            LoginResult loginResult = null;

            switch (loginType)
            {
                case LoginType.Guest:
                    loginResult = guestLogin.Login();
                    break;

                case LoginType.GooglePlayGame:
                    throw new NotImplementedException();

                case LoginType.GameCenter:
                    throw new NotImplementedException();
            }

            if (loginResult.success)
            {
                PlayerPrefs.SetString(LOGIN_TYPE_KEY, loginType.ToString());
                Data.User.user.id = loginResult.id;
            }
            else
            {
                Debug.LogWarning(loginResult.reason);
            }
        }

        public void Logout()
        {
            if (!PlayerPrefs.HasKey(LOGIN_TYPE_KEY))
            {
                Debug.LogWarning($"There is no login data. Logout is ignored.");
                return;
            }

            LogoutResult logoutResult = null;
            var loginType = PlayerPrefs.GetString(LOGIN_TYPE_KEY).Parse<LoginType>();
            switch (loginType)
            {
                case LoginType.Guest:
                    logoutResult = guestLogin.Logout();
                    break;

                case LoginType.GooglePlayGame:
                    throw new NotImplementedException();

                case LoginType.GameCenter:
                    throw new NotImplementedException();
            }

            if (logoutResult.success)
            {
                PlayerPrefs.DeleteKey(LOGIN_TYPE_KEY);
                Data.User.Clear();
            }
            else
            {
                Debug.LogWarning(logoutResult.reason);
            }
        }
    }
}
