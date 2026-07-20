using GameFramework;
using MessagePipe;
using UnityEngine.Networking;

namespace LOP
{
    public class LOPWebRequestInterceptor : IWebRequestInterceptor
    {
        public static LOPWebRequestInterceptor Default { get; private set; } = new LOPWebRequestInterceptor();

        public void OnBeforeRequest(UnityWebRequest request) { }

        public void OnSuccess<T>(UnityWebRequest request, T response)
        {
            // 정적 인터셉터라 DI 주입 불가 → GlobalMessagePipe로 타입별 발행(RootLifetimeScope가 SetProvider).
            GlobalMessagePipe.GetPublisher<T>().Publish(response);
        }

        public void OnError(UnityWebRequest request, string error) { }
    }
}
