using GameFramework;
using System;
using UnityEngine;

namespace LOP
{
    public static partial class Extensions
    {
        public static void RegisterHandler<T>(this IRoomNetwork network, Action<T> handler, IMessageInterceptor interceptor = null) where T : IMessage
        {
            network.RegisterHandler<T>(message =>
            {
                try
                {
                    interceptor?.OnBeforeHandle(message);
                    handler(message);
                    interceptor?.OnAfterHandle(message);
                }
                catch (Exception e)
                {
                    interceptor?.OnError(message, e.Message);
                }
            });
        }
    }
}
