using GameFramework;
using UnityEngine;
using UnityEngine.Networking;

namespace LOP
{
    public class LOPWebRequestInterceptor : IWebRequestInterceptor
    {
        public static LOPWebRequestInterceptor Default { get; private set; } = new LOPWebRequestInterceptor();

        public void OnBeforeRequest(UnityWebRequest request) { }

        public void OnSuccess<T>(UnityWebRequest request, T response)
        {
            AppEventBus.Publish(response);
            RoomEventBus.Publish(response);
        }

        public void OnError(UnityWebRequest request, string error) { }
    }
}
