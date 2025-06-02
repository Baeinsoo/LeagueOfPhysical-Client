using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class LOPNetworkManager : NetworkManager
    {
        public event System.Action onStartClient;
        public event System.Action onStopClient;

        private PortTransport _portTransport;
        public PortTransport portTransport
        {
            get
            {
                return _portTransport ??= (transport is LatencySimulation latencySimulation ? latencySimulation.wrap : transport) as PortTransport;
            }
        }

        public ushort port
        {
            set => portTransport.Port = value;
            get => portTransport.Port;
        }

        #region Start & Stop Callbacks
        /// <summary>
        /// This is invoked when the client is started.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            onStartClient?.Invoke();
        }

        /// <summary>
        /// This is called when a client is stopped.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();

            onStopClient?.Invoke();
        }
        #endregion

        public override void OnDestroy()
        {
            base.OnDestroy();

            if (NetworkClient.isConnected)
            {
                StopClient();
            }
            ResetStatics();
        }
    }
}
