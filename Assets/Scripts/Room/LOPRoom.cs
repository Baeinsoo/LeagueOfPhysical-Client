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
        [Inject] public IGame game { get; private set; }
        [Inject] private LOPNetworkManager networkManager;
        [Inject] private IRoomDataStore roomDataStore;
        [Inject] private IGameDataStore gameDataStore;
        [Inject] private IUserDataStore userDataStore;
        [Inject] private IEnumerable<IRoomMessageHandler> roomMessageHandlers;

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

            game.onGameStateChanged += OnGameStateChanged;

            await game.InitializeAsync();

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await game.DeinitializeAsync();

            game.onGameStateChanged -= OnGameStateChanged;

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

            networkManager.networkAddress = roomDataStore.room.ip;
            networkManager.port = roomDataStore.room.port;

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

            game.Run(gameInfo.Tick + 1, gameInfo.Interval, gameInfo.ElapsedTime);
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
