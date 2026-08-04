using System;
using Cysharp.Threading.Tasks;
using GameFramework.Auth;
using UnityEngine;

namespace LOP
{
    /// <summary>익명 계정 로그인과 세션 보관. 저장된 자격증명이 있으면 그것으로 로그인하고,
    /// 없으면 새 익명 계정을 만든다.</summary>
    public class AuthenticationService
    {
        private readonly IAuthCredentialStore credentialStore;

        public AuthSession Current { get; private set; }
        public bool IsSignedIn => Current != null;
        public string AccessToken => Current?.Token.Token;

        /// <summary>기기에 쓸 수 있는 자격증명이 남아 있는지. 있으면 팝업 없이 자동 로그인한다.</summary>
        public bool HasStoredCredential => credentialStore.Load() != null;

        //  생성자를 하나만 둔다 — VContainer가 여러 생성자 중 무엇을 쓸지 헷갈리지 않게.
        public AuthenticationService(IAuthCredentialStore credentialStore)
        {
            this.credentialStore = credentialStore;
        }

        public async UniTask<AuthSession> SignInAsync(AuthProvider provider)
        {
            if (provider != AuthProvider.Anonymous)
            {
                throw new NotSupportedException($"{provider} 로그인은 아직 준비 중입니다.");
            }

            AuthCredential stored = credentialStore.Load();

            if (stored != null)
            {
                AuthSession session = await TryLoginAsync(stored);
                if (session != null)
                {
                    Current = session;
                    return session;
                }

                //  서버가 자격증명을 거부했다(개발 중 DB 초기화 등). 들고 있어봐야 영영 못 쓰므로
                //  버리고 새 계정을 만든다 — 사용자는 새 계정으로 그냥 진입한다.
                Debug.LogWarning("[Auth] 저장된 자격증명이 거부되어 새 익명 계정을 만듭니다.");
                credentialStore.Clear();
            }

            Current = await RegisterAnonymousAsync();
            return Current;
        }

        public void SignOut()
        {
            credentialStore.Clear();
            Current = null;
        }

        /// <summary>만료가 임박했으면 저장된 자격증명으로 다시 로그인해 토큰을 갈아끼운다.</summary>
        public async UniTask RefreshIfNeededAsync()
        {
            if (IsSignedIn == false)
            {
                return;
            }

            if (Current.Token.NeedsRefresh(DateTimeOffset.UtcNow, AccessTokenInfo.DefaultRefreshMargin) == false)
            {
                return;
            }

            AuthCredential stored = credentialStore.Load();
            if (stored == null)
            {
                return;
            }

            AuthSession refreshed = await TryLoginAsync(stored);
            if (refreshed != null)
            {
                Current = refreshed;
            }
        }

        private async UniTask<AuthSession> TryLoginAsync(AuthCredential credential)
        {
            var request = WebAPI.Login(new LoginRequest
            {
                provider = credential.Provider,
                providerUserId = credential.ProviderUserId,
                secret = credential.Secret,
            });

            await request;

            if (request.isSuccess == false)
            {
                return null;
            }

            LoginResponse response = request.response;
            return new AuthSession(
                response.userId,
                AccessTokenInfo.FromExpiresIn(response.accessToken, response.expiresIn, DateTimeOffset.UtcNow));
        }

        private async UniTask<AuthSession> RegisterAnonymousAsync()
        {
            var request = WebAPI.SignInAnonymous();
            await request;

            if (request.isSuccess == false)
            {
                throw new Exception($"익명 계정 생성에 실패했습니다. error: {request.error}");
            }

            AnonymousSignInResponse response = request.response;

            credentialStore.Save(new AuthCredential
            {
                Provider = response.credential.provider,
                ProviderUserId = response.credential.providerUserId,
                Secret = response.credential.secret,
            });

            return new AuthSession(
                response.userId,
                AccessTokenInfo.FromExpiresIn(response.accessToken, response.expiresIn, DateTimeOffset.UtcNow));
        }
    }
}
