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
            AppEventBus.Publish(message);
            RoomEventBus.Publish(message);
        }

        public void OnAfterHandle<T>(T message) where T : IMessage { }

        public void OnError<T>(T message, Exception e) where T : IMessage
        {
            Debug.LogError(e);
        }
    }
}
