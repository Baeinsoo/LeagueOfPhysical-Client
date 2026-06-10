using Cysharp.Threading.Tasks;
using GameFramework;
using LOP.UI;
using System;
using System.Threading.Tasks;
using VContainer;

namespace LOP
{
    public class LoginComponent : IEntranceComponent
    {
        [Inject] private IWindowManager windowManager;

        public async Task Execute()
        {
            var autoLoginResult = await LoginService.instance.TryAutoLogin();
            if (autoLoginResult.success)
            {
                return;
            }

            LoginResult loginResult = await windowManager.OpenModalAsync<LoginView, LoginResult>();

            if (loginResult.success == false)
            {
                throw new Exception(loginResult.reason);
            }
        }
    }
}
