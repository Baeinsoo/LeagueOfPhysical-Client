using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework.Auth;
using GameFramework.Http;
using GameFramework.Threading;
using UnityEngine;

namespace LOP
{
    /// <summary>익명 계정 로그인과 세션 보관. 저장된 자격증명이 있으면 그것으로 로그인하고,
    /// 없으면 새 익명 계정을 만든다.</summary>
    public class AuthenticationService : IAccessTokenProvider
    {
        //  서버가 자격증명을 "거부"했다고 확신할 수 있는 유일한 HTTP 상태. 그 외(연결 실패·타임아웃·
        //  400·500·501 등)는 서버에 물어보지 못했거나 서버가 일시적으로 이상한 것뿐이라, 계정이
        //  잘못됐다는 근거가 아니다.
        private const long HttpStatusUnauthorized = 401;

        //  30초 근거: RoomConnector는 1초 간격으로 최대 60회 재시도한다 — 이 하한이 없으면 방 진입
        //  한 번이 재로그인 최대 60회로 번진다. 30초 바닥을 두면 최악의 경우도 최대 2회로 줄어든다.
        //  토큰 수명은 1시간, 정상 갱신은 만료 5분 전 마진에서만 도니까 30초 바닥이 그 주기를
        //  건드리지 않는다. 정말로 무효화된 토큰이라도 30초 안에는 다시 시도해 복구된다.
        private static readonly TimeSpan ForcedRefreshInterval = TimeSpan.FromSeconds(30);

        private readonly IAuthCredentialStore credentialStore;
        private readonly SingleFlight<string> refreshFlight = new SingleFlight<string>();
        private readonly Throttle forcedRefreshThrottle = new Throttle(ForcedRefreshInterval);

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

        /// <summary>요청에 실을 토큰을 준다. 만료가 임박했거나 강제 갱신이면 저장된 자격증명으로 다시
        /// 로그인해 갈아끼운다.</summary>
        public async UniTask<string> GetAccessTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
        {
            if (IsSignedIn == false)
            {
                return null;
            }

            if (forceRefresh == false &&
                Current.Token.NeedsRefresh(DateTimeOffset.UtcNow, AccessTokenInfo.DefaultRefreshMargin) == false)
            {
                return AccessToken;
            }

            //  동시에 들어온 요청들이 각자 로그인하지 않도록 한 번으로 접는다.
            //  cancellationToken을 갱신에 넘기지 않는 이유: 갱신은 여러 요청이 함께 기다리는 작업이라,
            //  한 요청이 취소됐다고 죽이면 나머지가 말려든다. 시간 상한은 로그인 호출의 HTTP 타임아웃이 준다.
            //  스로틀은 SingleFlight 바깥이 아니라 안쪽(RefreshAsync)에서 본다 — 바깥에서 막으면, 이미
            //  돌고 있는 갱신에 합류했어야 할 요청까지 옛 토큰을 받아 그냥 실패한다.
            return await refreshFlight.RunAsync(() => RefreshAsync(forceRefresh));
        }

        private async UniTask<string> RefreshAsync(bool forceRefresh)
        {
            //  방금 강제로 갱신한 참이면 다시 받아봐야 같은 결과다. 현재 토큰을 그대로 돌려주면
            //  호출자가 보낸 것과 같아져 BearerTokenHandler가 재전송을 접는다.
            if (forceRefresh && forcedRefreshThrottle.TryAcquire(DateTimeOffset.UtcNow) == false)
            {
                return AccessToken;
            }

            AuthCredential stored = credentialStore.Load();
            if (stored == null)
            {
                //  로그인된 상태인데 저장된 자격증명을 못 읽었다는 뜻이다 — 이후 갱신도 계속 이 분기를
                //  타 조용한 무동작이 반복되고, 서버가 토큰을 검사하기 시작하면 "언젠가 로그아웃됨"으로 보인다.
                Debug.LogWarning("[Auth] 저장된 자격증명을 읽지 못해 토큰을 갱신하지 못했습니다(다음 기회에 재시도).");
                return AccessToken;
            }

            try
            {
                AuthSession refreshed = await TryLoginAsync(stored);
                if (refreshed != null)
                {
                    Current = refreshed;
                }
            }
            catch (Exception exception)
            {
                //  갱신 실패는 자격증명이 죽었다는 뜻이 아니다(오프라인일 수도 있다). 계정을 건드리지 않고
                //  지금 토큰을 그대로 돌려준다 — 부르는 쪽은 토큰이 안 바뀐 것을 보고 재시도를 접는다.
                Debug.LogWarning($"[Auth] 토큰 갱신에 실패했습니다(다음 기회에 재시도): {exception.Message}");
            }

            return AccessToken;
        }

        private async UniTask<AuthSession> TryLoginAsync(AuthCredential credential)
        {
            try
            {
                LoginResponse response = await WebAPI.Login(new LoginRequest
                {
                    provider = credential.Provider,
                    providerUserId = credential.ProviderUserId,
                    secret = credential.Secret,
                });

                return new AuthSession(
                    response.userId,
                    AccessTokenInfo.FromExpiresIn(response.accessToken, response.expiresIn, DateTimeOffset.UtcNow));
            }
            catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusUnauthorized)
            {
                //  401만 "이 자격증명은 더 이상 못 쓴다"는 확답이다 — 호출자가 거부로 취급해
                //  계정을 새로 만들 수 있도록 null을 돌려준다.
                return null;
            }
        }

        private async UniTask<AuthSession> RegisterAnonymousAsync()
        {
            AnonymousSignInResponse response = await WebAPI.SignInAnonymous();

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
