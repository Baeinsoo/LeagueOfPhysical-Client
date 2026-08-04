using Cysharp.Threading.Tasks;
using LOP.UI;
using System;
using System.Threading.Tasks;

namespace LOP
{
    public class LoginComponent : IEntranceComponent
    {
        private readonly IWindowManager windowManager;
        private readonly AuthenticationService authenticationService;
        private readonly IUserDataStore userDataStore;

        public LoginComponent(IWindowManager windowManager, AuthenticationService authenticationService, IUserDataStore userDataStore)
        {
            this.windowManager = windowManager;
            this.authenticationService = authenticationService;
            this.userDataStore = userDataStore;
        }

        public async Task Execute()
        {
            AuthSession session = await TrySilentSignIn();

            if (session == null)
            {
                //  저장된 자격증명이 없다 — 사용자가 로그인 방식을 고르게 한다.
                session = await windowManager.OpenModalAsync<LoginView, AuthSession>();
            }

            userDataStore.user.id = session.UserId;
        }

        private async UniTask<AuthSession> TrySilentSignIn()
        {
            if (authenticationService.HasStoredCredential == false)
            {
                return null;
            }

            try
            {
                return await authenticationService.SignInAsync(AuthProvider.Anonymous);
            }
            catch (Exception exception)
            {
                //  네트워크 실패 등 — 팝업으로 넘겨 사용자가 재시도할 수 있게 한다. 원인이
                //  오프라인/서버다운/진짜 거부 중 무엇인지 콘솔에 남겨 나중에 구분할 수 있게 한다.
                UnityEngine.Debug.LogWarning($"[Auth] 조용한 자동 로그인 실패, 로그인 팝업으로 넘어갑니다: {exception}");
                return null;
            }
        }
    }
}
