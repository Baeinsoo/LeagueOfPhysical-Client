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

        /// <summary>
        /// 지연 시뮬레이터가 감싸고 있으면 벗겨 실제 트랜스포트를 돌려준다. 안 감싸고 있으면 그대로.
        /// </summary>
        public static Transport Unwrap(Transport configured)
        {
            return configured is LatencySimulation simulation && simulation.wrap != null
                ? simulation.wrap
                : configured;
        }

        /// <summary>
        /// 플레이어 빌드에서는 지연 시뮬레이터를 쓰지 않는다 — 에디터에서 켜 둔 채로 빌드하면
        /// 그 지연이 그대로 앱에 실려 나간다. 실제로 편도 100ms가 실린 APK가 폰에 깔려서,
        /// 폰에서 잰 RTT 140이 전부 가짜였다(2026-09-01).
        ///
        /// <para>씬은 그대로 두고 여기서만 갈아탄다 — 그래야 에디터에서 지연을 켜 두는 평소
        /// 작업 방식이 안 바뀌고, 빌드에는 절대 안 실린다.</para>
        ///
        /// <para><c>base.Awake()</c>가 <c>InitializeSingleton()</c>에서 <c>Transport.active</c>를
        /// 굳히므로 반드시 그 전에 바꿔야 한다.</para>
        /// </summary>
        public override void Awake()
        {
#if !UNITY_EDITOR
            //  시뮬레이터를 끄지는 않는다 — LatencySimulation.OnDisable()이 자기가 감싸던
            //  트랜스포트까지 같이 꺼 버려서, 끄는 순간 진짜 통신이 죽는다.
            //  Transport.active가 아니면 ClientSend가 불릴 일이 없으므로 그냥 두면 된다.
            transport = Unwrap(transport);
#endif
            base.Awake();
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
