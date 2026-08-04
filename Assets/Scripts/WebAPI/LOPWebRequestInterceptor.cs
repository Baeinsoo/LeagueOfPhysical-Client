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
