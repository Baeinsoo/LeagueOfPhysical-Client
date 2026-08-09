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
                await JoinRoomServerAsync();
                await WaitForClockSyncAsync();
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

        //  시계가 맞기 전에는 시뮬을 시작하지 않는다.
        //  Mirror의 predictedTime은 "내 시계 + 서버가 알려준 오차"인데, 그 오차가 20샘플(≈2초)에 걸쳐
        //  수렴한다. 접속 직후엔 아직 내 에디터 가동 시간에 가까워서, 그대로 출발하면 시계가 뒤늦게
        //  제자리를 찾으면서 그만큼 틱이 질주한다(실측 284틱 = 5.7초를 8배속으로 갈아 넘김).
        //  DOTS도 시계가 동기되기 전에는 시뮬을 돌리지 않는다.
        private const double CLOCK_SYNC_STABLE_THRESHOLD = 0.010;   //  sec. 수렴 후 잔떨림(수 ms)보다 크고 초기 전이(수 초)보다 훨씬 작다
        private const int CLOCK_SYNC_STABLE_SAMPLES = 3;
        private const int CLOCK_SYNC_SAMPLE_INTERVAL_MS = 100;      //  Mirror ping 간격과 같게
        private const int CLOCK_SYNC_TIMEOUT_MS = 5000;

        private async Task WaitForClockSyncAsync()
        {
            double previous = NetworkTime.predictionErrorUnadjusted;
            int stable = 0;
            int waited = 0;

            while (stable < CLOCK_SYNC_STABLE_SAMPLES && waited < CLOCK_SYNC_TIMEOUT_MS)
            {
                await UniTask.Delay(CLOCK_SYNC_SAMPLE_INTERVAL_MS);
                waited += CLOCK_SYNC_SAMPLE_INTERVAL_MS;

                double current = NetworkTime.predictionErrorUnadjusted;
                stable = Math.Abs(current - previous) < CLOCK_SYNC_STABLE_THRESHOLD ? stable + 1 : 0;
                previous = current;
            }

            if (stable < CLOCK_SYNC_STABLE_SAMPLES)
            {
                //  못 기다렸어도 시작은 한다 — 여기서 막으면 매치가 영영 안 열린다.
                Debug.LogWarning($"[Room] 네트워크 시계가 {CLOCK_SYNC_TIMEOUT_MS}ms 안에 수렴하지 않았다. 그대로 시작한다.");
            }
        }

        public async Task StartGameAsync()
        {
            var gameInfo = gameDataStore.gameInfo;

            // 출발선을 제 위치(서버보다 앞)에 놓는다. gameInfo.Tick/ElapsedTime은 보낸 순간의 값이라
            // 받았을 땐 이미 과거다. 속도 보정(ClockDilator)은 달리는 중 드리프트를 잡는 장치이지
            // 잘못된 출발점을 메우는 장치가 아니다 — 0.5초 미만 오차는 5%씩만 좁혀 수 초가 걸린다.
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
