using GameFramework;
using System;
using UnityEngine;

namespace LOP
{
    public static partial class Extensions
    {
        public static void RegisterHandler<T>(this IMessageDispatcher messageDispatcher, Action<T> handler, IMessageInterceptor interceptor = null) where T : IMessage
        {
            messageDispatcher.RegisterHandler<T>(message =>
            {
                try
                {
                    interceptor?.OnBeforeHandle(message);
                    handler(message);
                    interceptor?.OnAfterHandle(message);
                }
                catch (Exception e)
                {
                    interceptor?.OnError(message, e);
                }
            });
        }
    }
}
