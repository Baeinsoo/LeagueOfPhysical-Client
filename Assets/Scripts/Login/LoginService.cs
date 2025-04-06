using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace LOP
{
    [DontDestroyMonoSingleton]
    [DIMonoBehaviour]
    public class LoginService : MonoSingleton<LoginService>
    {
        private const string LOGIN_TYPE_KEY = "LOGIN_TYPE_KEY";

        private GuestLogin guestLogin;

        [Inject]
        private IDataContextManager dataManager;

        protected override void Awake()
        {
            base.Awake();

            guestLogin = this.GetOrAddComponent<GuestLogin>();
        }

        public async Task<LoginResult> TryAutoLogin()
        {
            if (!PlayerPrefs.HasKey(LOGIN_TYPE_KEY))
            {
                return new LoginResult(false, "No cached login data.", "");
            }

            var loginType = PlayerPrefs.GetString(LOGIN_TYPE_KEY).Parse<LoginType>();

            return Login(loginType);
        }

        public LoginResult Login(LoginType loginType)
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
            }

            return loginResult;
        }

        public LogoutResult Logout()
        {
            if (!PlayerPrefs.HasKey(LOGIN_TYPE_KEY))
            {
                Debug.LogWarning($"There is no login data. Logout is ignored.");
                return new LogoutResult(false, "There is no login data.");
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
                dataManager.Get<UserDataContext>().Clear();
            }
            else
            {
                Debug.LogWarning(logoutResult.reason);
            }

            return logoutResult;
        }
    }
}
