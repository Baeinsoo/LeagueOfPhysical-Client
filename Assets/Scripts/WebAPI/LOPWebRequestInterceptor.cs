using GameFramework;
using MessagePipe;
using System;
using UnityEngine.Networking;

namespace LOP
{
    public class LOPWebRequestInterceptor : IWebRequestInterceptor
    {
        public static LOPWebRequestInterceptor Default { get; private set; } = new LOPWebRequestInterceptor();

        //  static이라 DI가 안 된다 — RootLifetimeScope가 기동 시 공급자를 꽂아 준다.
        private static Func<string> accessTokenProvider;

        public static void SetAccessTokenProvider(Func<string> provider)
        {
            accessTokenProvider = provider;
        }

        public void OnBeforeRequest(UnityWebRequest request)
        {
            if (IsAuthEndpoint(request.url))
            {
                //  /auth/* (로그인/익명가입) 자체엔 절대 붙이지 않는다. 갱신이 밀린 상태에서
                //  Current가 아직 non-null이면 만료 임박/구 토큰이 이 요청에 얹혀 나갈 수 있고,
                //  서버가 그걸로 401을 주면 AuthenticationService가 "자격증명이 거부됐다"로
                //  오판해 멀쩡한 계정을 지우고 새로 가입해버린다. /auth/*는 애초에 토큰이 필요
                //  없는 엔드포인트라 아예 안 붙이는 게 안전하다.
                return;
            }

            string token = accessTokenProvider?.Invoke();
            if (string.IsNullOrEmpty(token))
            {
                return;
            }

            request.SetRequestHeader("Authorization", $"Bearer {token}");
        }

        private static bool IsAuthEndpoint(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            try
            {
                return new Uri(url).AbsolutePath.StartsWith("/auth/", StringComparison.Ordinal);
            }
            catch (UriFormatException)
            {
                return false;
            }
        }

        public void OnSuccess<T>(UnityWebRequest request, T response)
        {
            // 정적 인터셉터라 DI 주입 불가 → GlobalMessagePipe로 타입별 발행(RootLifetimeScope가 SetProvider).
            GlobalMessagePipe.GetPublisher<T>().Publish(response);
        }

        public void OnError(UnityWebRequest request, string error) { }
    }
}
