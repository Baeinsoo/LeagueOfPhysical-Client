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
        [Inject] private IRoomNetwork roomNetwork;
        [Inject] private LOPNetworkManager networkManager;
        [Inject] private IRoomDataContext roomDataContext;
        [Inject] private IGameDataContext gameDataContext;
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
            var getMatch = await WebAPI.GetMatch(roomDataContext.room.matchId);
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

            roomDataContext.Clear();
            gameDataContext.Clear();

            initialized = false;
        }

        private async Task ConnectRoomServerAsync()
        {
            networkManager.networkAddress = roomDataContext.room.ip;
            networkManager.port = roomDataContext.room.port;

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
            roomNetwork.SendToServer(new GameInfoRequest());

            await UniTask.WaitUntil(() => gameDataContext.gameInfo != null);
        }

        private async Task DisconnectRoomServerAsync()
        {
            networkManager.StopClient();

            await UniTask.WaitUntil(() => NetworkClient.ready == false);
        }

        public async Task StartGameAsync()
        {
            var gameInfo = gameDataContext.gameInfo;

            game.Run(gameInfo.Tick, gameInfo.Interval, gameInfo.ElapsedTime);
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
