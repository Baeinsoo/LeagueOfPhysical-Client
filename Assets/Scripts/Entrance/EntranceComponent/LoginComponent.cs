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
        [Inject] private IObjectResolver resolver;
        [Inject] private IUIManager uiManager;

        public async Task Execute()
        {
            var autoLoginResult = await LoginService.instance.TryAutoLogin();
            if (autoLoginResult.success)
            {
                return;
            }

            var view = resolver.Resolve<LoginView>();
            uiManager.Open(view, UILayer.Popup);

            LoginType loginType = await view.ViewModel.OnLoginRequested.FirstAsync();

            LoginResult loginResult = LoginService.instance.Login(loginType);

            uiManager.Close(view);

            if (loginResult.success == false)
            {
                throw new Exception(loginResult.reason);
            }
        }
    }
}
