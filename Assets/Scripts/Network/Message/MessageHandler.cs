using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;

namespace LOP
{
    public abstract class MessageHandlerBase
    {
        public abstract void Invoke(IMessage message);
        public abstract bool IsEmpty { get; }
    }

    public class MessageHandler<T> : MessageHandlerBase where T : IMessage
    {
        private Action<T> handlers;

        public override void Invoke(IMessage message)
        {
            if (message is T typedMessage)
            {
                handlers.Invoke(typedMessage);
            }
            else
            {
                Debug.LogError($"Invalid message type: Expected {typeof(T)}, but got {message.GetType()}");
            }
        }

        public void AddHandler(Action<T> handler)
        {
            handlers += handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public void RemoveHandler(Action<T> handler)
        {
            handlers -= handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public override bool IsEmpty => handlers == null || handlers.GetInvocationList().Length == 0;
    }
}
