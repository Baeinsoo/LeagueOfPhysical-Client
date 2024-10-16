using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class RoomNetwork : MonoBehaviour
    {
        private const int SERVER_ID = 0;

        private Dictionary<Type, MessageHandlerBase> handlerMap;
        private INetwork networkImpl;

        private void Awake()
        {
            handlerMap = new Dictionary<Type, MessageHandlerBase>();
            networkImpl = GetComponent<INetwork>();
            networkImpl.onMessage += OnMessage;
        }

        private void OnDestroy()
        {
            handlerMap.Clear();
            handlerMap = null;

            networkImpl.onMessage -= OnMessage;
            networkImpl = null;
        }

        private void OnMessage(int connectionId/*0: serverId*/, IMessage message)
        {
            if (handlerMap.TryGetValue(message.GetType(), out var handler))
            {
                handler?.Invoke(message);
            };
        }

        public void SendToServer(IMessage message, bool reliable = true, bool instant = false)
        {
            networkImpl.Send(message, SERVER_ID, reliable, instant);
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
    }
}
