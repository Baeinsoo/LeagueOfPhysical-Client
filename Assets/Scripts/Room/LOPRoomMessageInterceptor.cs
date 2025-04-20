using GameFramework;
using System;
using UnityEngine;

namespace LOP
{
    public class LOPRoomMessageInterceptor : IMessageInterceptor
    {
        public static readonly LOPRoomMessageInterceptor Default = new LOPRoomMessageInterceptor();

        public void OnBeforeHandle<T>(T message) where T : IMessage
        {
            var dataContextManager = SceneLifetimeScope.Resolve<IDataContextManager>();

            dataContextManager.UpdateData(message);
        }

        public void OnAfterHandle<T>(T message) where T : IMessage { }

        public void OnError<T>(T message, string error) where T : IMessage
        {
            Debug.LogError($"error: {error}");
        }
    }
}
