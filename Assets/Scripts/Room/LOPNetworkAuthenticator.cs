using Cysharp.Threading.Tasks;
using GameFramework;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using VContainer;

namespace LOP
{
    [SceneInjectMonoBehaviour]
    public class LOPNetworkAuthenticator : NetworkAuthenticator
    {
        [Inject]
        private IUserDataStore userDataStore;

        [Inject]
        private GameFramework.Http.IAccessTokenProvider accessTokenProvider;

        private string preparedAccessToken;

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

        /// <summary>접속 직전에 토큰을 준비한다. OnClientAuthenticate는 동기 Mirror 콜백이라
        /// 그 안에서 갱신을 기다릴 수 없으므로, StartClient() 전에 반드시 이것을 await해야 한다.</summary>
        public async UniTask PrepareCredentialAsync(CancellationToken cancellationToken)
        {
            //  강제 갱신(true)을 쓰지 않는다 — 게임서버는 접속 시점에 한 번만 검사하므로 남은 수명이
            //  짧아도 문제가 없고, 강제로 부르면 1a의 30초 스로틀과 얽혀 접속만 늦어진다.
            preparedAccessToken = await accessTokenProvider.GetAccessTokenAsync(false, cancellationToken);
        }

        /// <summary>
        /// Called on client from OnClientAuthenticateInternal when a client needs to authenticate
        /// </summary>
        public override void OnClientAuthenticate()
        {
            var customProperties = new CustomProperties
            {
                userId = userDataStore.user.id,
                accessToken = preparedAccessToken,
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
