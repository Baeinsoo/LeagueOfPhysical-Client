using Cysharp.Threading.Tasks;
using GameFramework;
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        [Inject] private IEnumerable<IRoomMessageHandler> roomMessageHandlers;

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
                SceneManager.LoadScene("Lobby");
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
            if (getMatch.response.code != ResponseCode.SUCCESS)
            {
                throw new Exception($"GetMatch Error. code: {getMatch.response.code}");
            }

            foreach (var roomMessageHandler in roomMessageHandlers.OrEmpty())
            {
                roomMessageHandler.Register();
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

            foreach (var roomMessageHandler in roomMessageHandlers.OrEmpty())
            {
                roomMessageHandler.Unregister();
            }

            roomDataStore.Clear();
            gameDataStore.Clear();

            initialized = false;
        }

        private async Task ConnectRoomServerAsync()
        {
            NetworkClient.RegisterHandler<CustomMirrorMessage>(message =>
            {
                EventBus.Default.Publish(nameof(IMessage), message.payload);
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

            runner.Run(gameInfo.Tick + 1, gameInfo.Interval, gameInfo.ElapsedTime);
        }

        private void OnGameStateChanged(IGameState gameState)
        {
            switch (gameState)
            {
                case GameOver:
                    Debug.Log("Game Over");
                    SceneManager.LoadScene("Lobby");
                    break;
            }
        }
    }
}
