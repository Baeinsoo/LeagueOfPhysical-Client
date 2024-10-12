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

        private Dictionary<Type, Action<IMessage>> handlerMap;
        private INetwork networkImpl;

        private void Awake()
        {
            handlerMap = new Dictionary<Type, Action<IMessage>>();
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
            if (handlerMap.ContainsKey(typeof(T)) == true)
            {
                handlerMap[typeof(T)] += handler as Action<IMessage>;
            }
            else
            {
                handlerMap[typeof(T)] = handler as Action<IMessage>;
            }
        }

        public void UnregisterHandler<T>(Action<T> handler) where T : IMessage
        {
            handlerMap[typeof(T)] -= handler as Action<IMessage>;
            if (handlerMap[typeof(T)] == null)
            {
                handlerMap.Remove(typeof(T));
            }
        }
    }
}
