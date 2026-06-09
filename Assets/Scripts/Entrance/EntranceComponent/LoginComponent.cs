using GameFramework;
using LOP.UI;
using R3;
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

            var view = windowManager.Open<LoginView>();

            LoginType loginType = await view.ViewModel.OnLoginRequested.FirstAsync();

            LoginResult loginResult = LoginService.instance.Login(loginType);

            windowManager.Close(view);

            if (loginResult.success == false)
            {
                throw new Exception(loginResult.reason);
            }
        }
    }
}
