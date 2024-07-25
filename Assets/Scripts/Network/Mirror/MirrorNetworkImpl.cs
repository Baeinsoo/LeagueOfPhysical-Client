using GameFramework;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class MirrorNetworkImpl : MonoBehaviour, INetwork
    {
        public event Action<int, IMessage> onMessage;

        private void Awake()
        {
            RegisterMessage();
        }

        private void OnDestroy()
        {
            onMessage = null;

            UnregisterMessage();
        }

        private void RegisterMessage()
        {
            NetworkClient.RegisterHandler<CustomMirrorMessage>(message =>
            {
                onMessage?.Invoke(0/*0: serverId*/, message.payload);
            });
        }

        private void UnregisterMessage()
        {
            NetworkClient.UnregisterHandler<CustomMirrorMessage>();
        }

        public void Send(IMessage message, int targetId, bool reliable = true, bool instant = false)
        {
            if (!NetworkClient.isConnected)
            {
                Debug.LogWarning($"NetworkClient.isConnected is false.");
                return;
            }

            if (!NetworkClient.connection.isAuthenticated)
            {
                Debug.LogWarning($"Not yet authenticated.");
                return;
            }

            var customMirrorMessage = new CustomMirrorMessage
            {
                payload = message,
            };

            NetworkClient.Send(customMirrorMessage);
        }

        void INetwork.SendToAll(IMessage message, bool reliable = true, bool instant = false)
        {
            throw new NotImplementedException();
        }

        void INetwork.SendToNear(IMessage message, Vector3 center, float radius, bool reliable = true, bool instant = false)
        {
            throw new NotImplementedException();
        }
    }
}
