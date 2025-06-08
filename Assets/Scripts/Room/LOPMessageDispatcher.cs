using GameFramework;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class LOPMessageDispatcher : IMessageDispatcher
    {
        private Dictionary<Type, MessageHandlerBase> handlerMap = new Dictionary<Type, MessageHandlerBase>();

        public LOPMessageDispatcher()
        {
            NetworkClient.RegisterHandler<CustomMirrorMessage>(message =>
            {
                EnqueueMessage(message.payload);

                RoomEventBus.Publish(message.payload);
            });
        }

        public void RegisterHandler<T>(Action<T> handler) where T : IMessage
        {
            if (handlerMap.TryGetValue(typeof(T), out var baseMessageHandler))
            {
                if (baseMessageHandler is MessageHandler<T> messageHandler)
                {
                    messageHandler.AddHandler(handler);
                }
                else
                {
                    Debug.LogWarning($"MessageHandler for {typeof(T)} is of a different type.");
                }
            }
            else
            {
                var messageHandler = new MessageHandler<T>();
                messageHandler.AddHandler(handler);
                handlerMap[typeof(T)] = messageHandler;
            }
        }

        public void UnregisterHandler<T>(Action<T> handler) where T : IMessage
        {
            if (handlerMap.TryGetValue(typeof(T), out var baseMessageHandler))
            {
                if (baseMessageHandler is MessageHandler<T> messageHandler)
                {
                    messageHandler.RemoveHandler(handler);

                    if (messageHandler.IsEmpty)
                    {
                        handlerMap.Remove(typeof(T));
                    }
                }
                else
                {
                    Debug.LogWarning($"MessageHandler for {typeof(T)} is of a different type.");
                }
            }
        }

        public void EnqueueMessage(IMessage message)
        {
            if (handlerMap.TryGetValue(message.GetType(), out var handler))
            {
                handler?.Invoke(message);
            };
        }

        public void Dispose()
        {
            handlerMap.Clear();
            NetworkClient.UnregisterHandler<CustomMirrorMessage>();
        }
    }
}
