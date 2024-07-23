using GameFramework;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System;

namespace LOP
{
    public class LoginComponent : IEntranceComponent
    {
        public async Task Execute()
        {
            var autoLoginResult = await LoginService.instance.TryAutoLogin();
            if (autoLoginResult.success == false)
            {
                LoginResult loginResult = null;
                var loginPopup = PopupManager.instance.GetPopup<LoginPopup>();
                loginPopup.onGuestLoginClick += () =>
                {
                    loginResult = LoginService.instance.Login(LoginType.Guest);
                };
                loginPopup.Show();

                await Cysharp.Threading.Tasks.UniTask.WaitUntil(() => loginResult != null);

                loginPopup.Close();

                if (loginResult.success == false)
                {
                    throw new Exception(loginResult.reason);
                }
            }
        }
    }
}
