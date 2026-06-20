using GameFramework;
using Mirror;
using System;
using UnityEngine;

namespace LOP
{
    public class LOPSession : ISession
    {
        public string sessionId { get; }
        public string userId { get; }

        public bool isConnected => networkConnection != null && networkConnection.isReady;

        public NetworkConnection networkConnection { get; private set; }

        public LOPSession(string sessionId, string userId, NetworkConnection networkConnection)
        {
            this.sessionId = sessionId;
            this.userId = userId;
            this.networkConnection = networkConnection;
        }

        public void Send<T>(T message, bool reliable = true) where T : IMessage
        {
            if (isConnected == false)
            {
                return;
            }

            int channelId = reliable ? Channels.Reliable : Channels.Unreliable;
            networkConnection.Send(new CustomMirrorMessage
            {
                payload = message,
            }, channelId);
        }

        public IMessage Receive()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            if (networkConnection != null)
            {
                networkConnection.Disconnect();
                networkConnection = null;
            }
        }
    }
}
