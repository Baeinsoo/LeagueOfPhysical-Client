using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LOP
{
    public class LOPNetworkManager : NetworkManager
    {
        #region Start & Stop Callbacks
        /// <summary>
        /// This is invoked when the client is started.
        /// </summary>
        public override void OnStartClient()
        {
            base.OnStartClient();

            Debug.Log("[OnStartClient]");
        }

        /// <summary>
        /// This is called when a client is stopped.
        /// </summary>
        public override void OnStopClient()
        {
            base.OnStopClient();

            Debug.Log("[OnStopClient]");
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
