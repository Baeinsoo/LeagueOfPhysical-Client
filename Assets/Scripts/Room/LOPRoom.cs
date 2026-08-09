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

        public async Task StartGameAsync()
        {
            var gameInfo = gameDataStore.gameInfo;

            // 출발선을 제 위치(서버보다 앞)에 놓는다. gameInfo.Tick/ElapsedTime은 보낸 순간의 값이라
            // 받았을 땐 이미 과거다.
            //
            // 접속 직후라 Mirror의 시각 추정(predictedTime)은 아직 수렴 전이다 — 여기서 잡은 출발선은
            // 몇 초 뒤 제자리를 찾으며 크게 움직인다. 그 격차는 기다려서 없애지 않고(로딩 2.8초를
            // 물어야 했다) 틱 쪽 snap-forward가 건너뛴다(LOPTickUpdater.MaxCatchUpTicks).
            // Mirror의 방식 자체가 "추정치를 계속 따라가되 크게 틀리면 점프"다.
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
