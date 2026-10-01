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
        private readonly ServerGate serverGate;

        public LoginComponent(IWindowManager windowManager, AuthenticationService authenticationService, IUserDataStore userDataStore,
                              ServerGate serverGate)
        {
            this.serverGate = serverGate;
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

            while (true)
            {
                try
                {
                    return await authenticationService.SignInAsync(AuthProvider.Anonymous);
                }
                catch (Exception exception) when (ConnectionFailure.Is(exception))
                {
                    //  확인은 통과했는데 그 사이 서버가 내려갔다 — 로그인 팝업이 아니라 연결 팝업으로 돌아가 닿을 때까지 기다린 뒤 다시.
                    UnityEngine.Debug.LogWarning($"[Auth] 자동 로그인 중 서버 연결이 끊겼습니다: {exception.Message}");
                    await serverGate.EnsureReachableAsync();
                }
                catch (Exception exception)
                {
                    //  서버가 답했는데 실패(거부·서버 오류 등) — 팝업으로 넘겨 사용자가 다시 시도할 수 있게 한다.
                    UnityEngine.Debug.LogWarning($"[Auth] 조용한 자동 로그인 실패, 로그인 팝업으로 넘어갑니다: {exception}");
                    return null;
                }
            }
        }
    }
}
