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
        [Inject] private RoomNetwork roomNetwork;
        [Inject] private LOPNetworkManager networkManager;
        [Inject] private IDataContextManager dataManager;

        public bool initialized { get; private set; }

        public float latency => (float)Mirror.NetworkTime.rtt * 0.5f;

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
            await game.InitializeAsync();

#if UNITY_EDITOR
            if (Blackboard.Contain<RoomDto>() == false)
            {
                var roomMeta = new RoomDto
                {
                    id = "test",
                    matchId = "",
                    ip = "localhost",
                    port = 7777,
                };
                dataManager.Get<RoomDataContext>().room = roomMeta;
            }
#endif
            dataManager.Get<RoomDataContext>().room = Blackboard.Read<RoomDto>(erase: true);

            var getMatch = await WebAPI.GetMatch(dataManager.Get<RoomDataContext>().room.matchId);
            if (getMatch.response.code != ResponseCode.SUCCESS)
            {
                throw new Exception($"GetMatch Error. code: {getMatch.response.code}");
            }

            dataManager.Get<RoomDataContext>().match = getMatch.response.match;

            roomNetwork.RegisterHandler<GameInfoResponse>(OnGameInfoResponse);

            game.onGameStateChanged += OnGameStateChanged;

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await game.DeinitializeAsync();

            dataManager.Get<RoomDataContext>().Clear();

            try
            {
                roomNetwork.UnregisterHandler<GameInfoResponse>(OnGameInfoResponse);
            }
            catch
            {
                //  Maybe roomNetwork was already destroyed.
            }

            game.onGameStateChanged -= OnGameStateChanged;

            initialized = false;
        }

        private async Task ConnectRoomServerAsync()
        {
            networkManager.networkAddress = dataManager.Get<RoomDataContext>().room.ip;
            networkManager.port = dataManager.Get<RoomDataContext>().room.port;

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

            await UniTask.WaitUntil(() => Blackboard.Contain<GameInfoResponse>());
        }

        private async Task DisconnectRoomServerAsync()
        {
            networkManager.StopClient();

            await UniTask.WaitUntil(() => NetworkClient.ready == false);
        }

        public async Task StartGameAsync()
        {
            var gameInfoResponse = Blackboard.Read<GameInfoResponse>(erase: true);

            game.Run(gameInfoResponse.GameInfo.Tick, gameInfoResponse.GameInfo.Interval, gameInfoResponse.GameInfo.ElapsedTime);

            dataManager.Get<RoomDataContext>().player.entityId = gameInfoResponse.EntityId;
        }

        private void OnGameInfoResponse(GameInfoResponse gameInfoResponse)
        {
            Blackboard.Write(gameInfoResponse);
        }

        private void OnGameStateChanged(GameState gameState)
        {
            switch (gameState)
            {
                case GameState.GameOver:
                    SceneManager.LoadScene("Lobby");
                    break;
            }
        }
    }
}
