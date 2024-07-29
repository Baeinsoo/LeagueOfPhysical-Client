using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class LOPNetworkAuthenticator : NetworkAuthenticator
    {
        #region Messages
        public struct AuthRequestMessage : NetworkMessage
        {
            public CustomProperties customProperties;
        }

        public struct AuthResponseMessage : NetworkMessage
        {
            public int code;
            public string message;
        }
        #endregion

        #region Client
        /// <summary>
        /// Called on client from StartClient to initialize the Authenticator
        /// <para>Client message handlers should be registered in this method.</para>
        /// </summary>
        public override void OnStartClient()
        {
            // register a handler for the authentication response we expect from server
            NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponseMessage, false);
        }

        /// <summary>
        /// Called on client from StopClient to reset the Authenticator
        /// <para>Client message handlers should be unregistered in this method.</para>
        /// </summary>
        public override void OnStopClient()
        {
            // unregister the handler for the authentication response
            NetworkClient.UnregisterHandler<AuthResponseMessage>();
        }

        /// <summary>
        /// Called on client from OnClientAuthenticateInternal when a client needs to authenticate
        /// </summary>
        public override void OnClientAuthenticate()
        {
            var customProperties = new CustomProperties
            {
                userId = SystemInfo.deviceUniqueIdentifier,
                token = "token",
                characterId = 0,
            };

            NetworkClient.Send(new AuthRequestMessage { customProperties = customProperties });
        }

        /// <summary>
        /// Called on client when the server's AuthResponseMessage arrives
        /// </summary>
        /// <param name="msg">The message payload</param>
        public void OnAuthResponseMessage(AuthResponseMessage msg)
        {
            if (msg.code == 200)
            {
                // Authentication has been accepted
                ClientAccept();
            }
            else
            {
                Debug.LogError($"Authentication Response: {msg.message}");

                // Authentication has been rejected
                ClientReject();
            }
        }
        #endregion
    }
}
