using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using System.Threading.Tasks;

namespace LOP
{
    public class LOPGame : MonoBehaviour, IGame
    {
        public IGameEngine gameEngine { get; private set; }
        [Inject] private RoomNetwork roomNetwork;

        private float originalFixedDeltaTime;
        private bool originalAutoSyncTransforms;

        public bool initialized { get; private set; }

        public async Task InitializeAsync()
        {
            gameEngine = GetComponentInChildren<IGameEngine>();

            await gameEngine.InitializeAsync();

            roomNetwork.RegisterHandler<GameInfoResponse>(OnGameInfoResponse);

            Physics.simulationMode = SimulationMode.Script;

            originalAutoSyncTransforms = Physics.autoSyncTransforms;
            Physics.autoSyncTransforms = true;

            originalFixedDeltaTime = UnityEngine.Time.fixedDeltaTime;

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await gameEngine.DeinitializeAsync();

            roomNetwork.UnregisterHandler<GameInfoResponse>(OnGameInfoResponse);

            Physics.simulationMode = SimulationMode.FixedUpdate;
            Physics.autoSyncTransforms = originalAutoSyncTransforms;

            UnityEngine.Time.fixedDeltaTime = originalFixedDeltaTime;

            initialized = false;
        }

        public void Run(long tick, double interval, double elapsedTime)
        {
            gameEngine.Run(tick, interval, elapsedTime);
        }

        public void Stop()
        {
            gameEngine.Stop();
        }

        private void OnGameInfoResponse(GameInfoResponse gameInfoResponse)
        {
            Debug.Log($"OnGameInfoResponse. EntityId: {gameInfoResponse.EntityId}");
        }
    }
}
