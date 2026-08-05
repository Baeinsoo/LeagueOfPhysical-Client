using GameFramework;
using MessagePipe;
using System;
using UnityEngine.Networking;

namespace LOP
{
    public class LOPWebRequestInterceptor : IWebRequestInterceptor
    {
        //  일반 요청용 — 토큰이 있으면 Authorization을 붙인다.
        public static LOPWebRequestInterceptor Default { get; } = new LOPWebRequestInterceptor(attachAuthorizationHeader: true);

        //  WebAPI.SignInAnonymous/Login 전용 — Authorization을 절대 붙이지 않는다. 갱신이 밀린
        //  상태에서 만료 임박/구 토큰이 로그인·가입 요청에 얹혀 나가면 서버가 401을 줄 수 있고,
        //  AuthenticationService가 그걸 "이 자격증명은 거부됐다"로 오판해 멀쩡한 계정을 지우고
        //  새로 가입해버린다(계정 유실). 어느 base URL을 쓰든(경로 접두사가 얼마나 붙든) 판단이
        //  흔들리지 않도록 URL 문자열이 아니라 호출부(WebAPI.cs)가 스스로 선언한다.
        public static LOPWebRequestInterceptor NoAuth { get; } = new LOPWebRequestInterceptor(attachAuthorizationHeader: false);

        //  static이라 DI가 안 된다 — RootLifetimeScope가 기동 시 공급자를 꽂아 준다.
        private static Func<string> accessTokenProvider;

        private readonly bool attachAuthorizationHeader;

        private LOPWebRequestInterceptor(bool attachAuthorizationHeader)
        {
            this.attachAuthorizationHeader = attachAuthorizationHeader;
        }

        public static void SetAccessTokenProvider(Func<string> provider)
        {
            accessTokenProvider = provider;
        }

        public void OnBeforeRequest(UnityWebRequest request)
        {
            if (attachAuthorizationHeader == false)
            {
                return;
            }

            string token = accessTokenProvider?.Invoke();
            if (string.IsNullOrEmpty(token))
            {
                return;
            }

            request.SetRequestHeader("Authorization", $"Bearer {token}");
        }

        public void OnSuccess<T>(UnityWebRequest request, T response)
        {
            // 정적 인터셉터라 DI 주입 불가 → GlobalMessagePipe로 타입별 발행(RootLifetimeScope가 SetProvider).
            GlobalMessagePipe.GetPublisher<T>().Publish(response);
        }

        public void OnError(UnityWebRequest request, string error) { }
    }
}
