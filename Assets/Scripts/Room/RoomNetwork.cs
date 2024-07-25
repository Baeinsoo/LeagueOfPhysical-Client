using GameFramework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class RoomNetwork : MonoSingleton<RoomNetwork>
    {
        private Dictionary<Type, Action<IMessage>> handlerMap;

        private INetwork networkImpl;

        protected override void Awake()
        {
            base.Awake();

            networkImpl = GetComponent<INetwork>() ?? gameObject.AddComponent<MirrorNetworkImpl>();
            networkImpl.onMessage += OnMessage;

            handlerMap = new Dictionary<Type, Action<IMessage>>();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            networkImpl.onMessage -= OnMessage;
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
            networkImpl.Send(message, 0/*0: serverId*/, reliable, instant);
        }

        public void RegisterHandler(Type type, Action<IMessage> handler)
        {
            if (handlerMap.ContainsKey(type) == true)
            {
                handlerMap[type] += handler;
            }
            else
            {
                handlerMap[type] = handler;
            }
        }

        public void UnregisterHandler(Type type, Action<IMessage> handler)
        {
            handlerMap[type] -= handler;
            if (handlerMap[type] == null)
            {
                handlerMap.Remove(type);
            }
        }
    }
}
