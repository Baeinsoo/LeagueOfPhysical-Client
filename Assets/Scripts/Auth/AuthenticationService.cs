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
        //  서버가 자격증명을 "거부"했다고 확신할 수 있는 유일한 HTTP 상태. 그 외(연결 실패·타임아웃·
        //  400·500·501 등)는 서버에 물어보지 못했거나 서버가 일시적으로 이상한 것뿐이라, 계정이
        //  잘못됐다는 근거가 아니다.
        private const long HttpStatusUnauthorized = 401;

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

                //  여기 도달했다는 건 TryLoginAsync가 401(서버의 명시적 거부)로 null을 반환했다는
                //  뜻이다 — 그 외 실패(오프라인·타임아웃·5xx 등)는 TryLoginAsync가 예외로 던져
                //  이 지점에 오지 않는다(아래 참고). 그래서만 안전하게 자격증명을 버리고 새 계정을
                //  만든다 — 개발 중 DB 초기화처럼 계정 자체가 더 이상 없는 경우의 복구 경로다.
                Debug.LogWarning("[Auth] 저장된 자격증명이 거부되어(401) 새 익명 계정을 만듭니다.");
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

            AuthSession refreshed;
            try
            {
                refreshed = await TryLoginAsync(stored);
            }
            catch (Exception ex)
            {
                //  TryLoginAsync와 같은 기준(401만 거부)을 그대로 쓴다 — 여기선 거부든 일시
                //  실패든 아무것도 건드리지 않고 조용히 넘어간다. 갱신이 실패해도 지금 가진
                //  토큰이 당장 만료된 건 아니라서, 다음 요청이 실제로 401을 맞으면 그때
                //  SignInAsync 경로로 다시 로그인하면 된다. 계정을 여기서 지우지 않는다.
                Debug.LogWarning($"[Auth] 토큰 갱신 확인에 실패했습니다(다음 기회에 재시도): {ex.Message}");
                return;
            }

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

            if (request.isSuccess)
            {
                LoginResponse response = request.response;
                return new AuthSession(
                    response.userId,
                    AccessTokenInfo.FromExpiresIn(response.accessToken, response.expiresIn, DateTimeOffset.UtcNow));
            }

            //  401만 "이 자격증명은 더 이상 못 쓴다"는 확답이다 — 호출자가 거부로 취급해 계정을
            //  새로 만들 수 있도록 null을 돌려준다.
            if (request.responseCode == HttpStatusUnauthorized)
            {
                return null;
            }

            //  그 외(연결 실패로 responseCode=0, 타임아웃, 400, 500, 501 등)는 서버 응답을 못
            //  받았거나 서버가 일시적으로 이상한 것뿐이다. null을 돌려주면 호출자가 이걸 "거부"로
            //  착각해 멀쩡한 계정을 지워버리므로, 예외로 올려 "지금은 확인 못 함"을 분명히 한다.
            throw new Exception(
                $"로그인 확인에 실패했습니다(재시도 가능). httpStatus: {request.responseCode}, error: {request.error}");
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
