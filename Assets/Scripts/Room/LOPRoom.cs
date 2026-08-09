using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Runner;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class LOPRoom : MonoBehaviour, IRoom
    {
        [Inject] private IGameFactory gameFactory;
        [Inject] private LOPNetworkManager networkManager;
        [Inject] private IRoomDataStore roomDataStore;
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IUserDataStore userDataStore;
        [Inject] private NetworkMessageDispatcher dispatcher;
        [Inject] private AppStateMachine appStateMachine;

        public IRunner runner { get; private set; }

        public bool initialized { get; private set; }

        private async void Awake()
        {
            try
            {
                await InitializeAsync();
                await ConnectRoomServerAsync();

                // 첫 측정은 접속하는 순간부터 오기 시작한다 — join 왕복과 겹쳐서 기다린다.
                // join 자체가 최소 1왕복이라 실제 추가 대기는 사실상 없다.
                var clockSample = WaitForFirstClockSampleAsync();
                await JoinRoomServerAsync();
                await clockSample;

                await StartGameAsync();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                appStateMachine.Fire(AppEvent.MatchEnded);
            }
        }

        private async void OnDestroy()
        {
            await DisconnectRoomServerAsync();
            await DeinitializeAsync();
        }

        public async Task InitializeAsync()
        {
            var getMatch = await WebAPI.GetMatch(roomDataStore.room.matchId);
            if (getMatch.code != ResponseCode.SUCCESS)
            {
                throw new Exception($"GetMatch Error. code: {getMatch.code}");
            }

            // 초기 엔티티 생성(GameInfoToC)이 JoinRoomServer에서 처리되기 전에 게임이 준비돼야 하므로 여기서 생성한다.
            runner = await gameFactory.CreateAsync();
            runner.onGameStateChanged += OnGameStateChanged;
            await runner.InitializeAsync();

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await runner.DeinitializeAsync();
            runner.onGameStateChanged -= OnGameStateChanged;

            await gameFactory.DestroyAsync();
            runner = null;

            roomDataStore.Clear();
            gameDataStore.Clear();

            initialized = false;
        }

        private async Task ConnectRoomServerAsync()
        {
            NetworkClient.RegisterHandler<CustomMirrorMessage>(message =>
            {
                dispatcher.Dispatch(message.payload);
            });

            if (EnvironmentSettings.active.UseLocalRoomInstance)
            {
                networkManager.networkAddress = EnvironmentSettings.active.LocalRoomHost;
                networkManager.port = EnvironmentSettings.active.LocalRoomPort;
            }
            else
            {
                networkManager.networkAddress = roomDataStore.room.ip;
                networkManager.port = roomDataStore.room.port;
            }

            //networkManager.onStartClient += () =>
            //{
            //};
            //networkManager.onStopClient += () =>
            //{
            //    SceneManager.LoadScene("Lobby");
            //};

            networkManager.StartClient();

            await UniTask.WaitUntil(() => NetworkClient.ready);
        }

        private async Task JoinRoomServerAsync()
        {
            CustomMirrorMessage message = new CustomMirrorMessage
            {
                payload = new GameInfoToS
                {
                    UserId = userDataStore.user.id
                },
            };
            NetworkClient.Send(message);

            await UniTask.WaitUntil(() => gameDataStore.gameInfo != null);
        }

        private async Task DisconnectRoomServerAsync()
        {
            networkManager.StopClient();

            await UniTask.WaitUntil(() => NetworkClient.ready == false);
        }

        //  시계가 첫 측정을 받기 전에는 시뮬을 시작하지 않는다.
        //  Mirror의 predictedTime은 "내 시계 + 서버가 알려준 오차"인데, pong이 하나도 안 온 상태에선
        //  그 오차가 0이라 사실상 내 프로세스 가동 시간이다(서버와 수 초 차이). 그대로 출발하면 첫
        //  pong이 오는 순간 시계가 통째로 점프하고, 그 격차를 틱이 건너뛰며 화면엔 위치 교정으로
        //  보인다(실측 313틱 = 6.3초 점프, 6m 움찔).
        //
        //  "값이 안정될 때까지"가 아니라 "첫 샘플이 올 때까지"만 기다리면 된다 — Mirror의 EMA는
        //  첫 샘플을 그대로 대입하므로(ExponentialMovingAverage.Add) pong 하나로 추정이 거의
        //  정확해진다. 안정될 때까지 기다렸더니 지터가 잦아들기를 기다리느라 2.8초가 걸렸다.
        //  rtt도 같은 EMA라 pong 전에는 0이다.
        private const int CLOCK_SAMPLE_POLL_INTERVAL_MS = 20;
        private const int CLOCK_SAMPLE_TIMEOUT_MS = 5000;

        private async Task WaitForFirstClockSampleAsync()
        {
            int waited = 0;

            while (NetworkTime.rtt <= 0 && waited < CLOCK_SAMPLE_TIMEOUT_MS)
            {
                await UniTask.Delay(CLOCK_SAMPLE_POLL_INTERVAL_MS);
                waited += CLOCK_SAMPLE_POLL_INTERVAL_MS;
            }

            if (NetworkTime.rtt <= 0)
            {
                //  못 받았어도 시작은 한다 — 여기서 막으면 매치가 영영 안 열린다.
                //  이 경우 시작 격차는 snap-forward가 건너뛴다.
                Debug.LogWarning($"[Room] 네트워크 시계 첫 측정이 {CLOCK_SAMPLE_TIMEOUT_MS}ms 안에 오지 않았다. 그대로 시작한다.");
            }
            else
            {
                Debug.Log($"[Room] 시계 첫 측정 대기 {waited}ms");
            }
        }

        public async Task StartGameAsync()
        {
            var gameInfo = gameDataStore.gameInfo;

            // 출발선을 제 위치(서버보다 앞)에 놓는다. gameInfo.Tick/ElapsedTime은 보낸 순간의 값이라
            // 받았을 땐 이미 과거다 — 지금 시계에서 유도한다.
            // 그 시계가 쓸 만한 값인지는 호출 전에 보장한다(WaitForFirstClockSampleAsync).
            double target = ((LOPTickUpdater)runner.tickUpdater).TargetTime;
            runner.Run((long)(target / gameInfo.Interval), gameInfo.Interval, target);
        }

        private void OnGameStateChanged(RunnerState gameState)
        {
            switch (gameState)
            {
                case RunnerState.GameOver:
                    Debug.Log("Game Over");
                    appStateMachine.Fire(AppEvent.MatchEnded);
                    break;
            }
        }
    }
}
