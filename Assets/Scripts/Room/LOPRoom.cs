using GameFramework;
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

        public bool initialized { get; private set; }

        public float latency => (float)Mirror.NetworkTime.rtt * 0.5f;

        private async void Awake()
        {
            await InitializeAsync();

            StartGame();
        }

        private async void OnDestroy()
        {
            StopGame();

            await DeinitializeAsync();
        }

        public async Task InitializeAsync()
        {
            Data.Room.room = Blackboard.Read<RoomDto>(erase: true);

            var getMatch = await WebAPI.GetMatch(Data.Room.room.matchId);
            if (getMatch.response.code != ResponseCode.SUCCESS)
            {
                throw new System.Exception($"GetMatch Error. code: {getMatch.response.code}");
            }
            Data.Room.match = getMatch.response.match;

            networkManager.networkAddress = Data.Room.room.ip;
            networkManager.port = Data.Room.room.port;

            networkManager.onStartClient += () =>
            {
            };
            networkManager.onStopClient += () =>
            {
                SceneManager.LoadScene("Lobby");
            };

            roomNetwork.RegisterHandler<GameInfoResponse>(OnGameInfoResponse);

            await game.InitializeAsync();

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await game.DeinitializeAsync();

            try
            {
                roomNetwork.UnregisterHandler<GameInfoResponse>(OnGameInfoResponse);
            }
            catch
            {
                //  Maybe roomNetwork was destroyed already
            }

            Data.Room.Clear();

            initialized = false;
        }

        public void StartGame()
        {
            networkManager.StartClient();
        }

        public void StopGame()
        {
            networkManager.StopClient();
        }

        private void OnGameInfoResponse(GameInfoResponse gameInfoResponse)
        {
            game.Run(gameInfoResponse.GameInfo.Tick, gameInfoResponse.GameInfo.Interval, gameInfoResponse.GameInfo.ElapsedTime);
        }
    }
}
