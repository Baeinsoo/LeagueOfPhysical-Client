using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using System.Threading.Tasks;
using System;

namespace LOP
{
    public class LOPGame : MonoBehaviour, IGame
    {
        public event Action<GameState> onGameStateChanged;

        [Inject]
        private RoomNetwork roomNetwork;

        public IGameEngine gameEngine { get; private set; }

        private GameState _gameState;
        public GameState gameState
        {
            get => _gameState;
            private set
            {
                _gameState = value;
                onGameStateChanged?.Invoke(value);
            }
        }

        public bool initialized { get; private set; }

        private Restorer restorer = new Restorer();

        public async Task InitializeAsync()
        {
            gameState = GameState.Initializing;

            var oldSimulationMode = Physics.simulationMode;
            var oldAutoSyncTransforms = Physics.autoSyncTransforms;

            restorer.action += () =>
            {
                Physics.simulationMode = oldSimulationMode;
                Physics.autoSyncTransforms = oldAutoSyncTransforms;
            };

            Physics.simulationMode = SimulationMode.Script;
            Physics.autoSyncTransforms = false;

            //var sceneLoadOperation = SceneManager.LoadSceneAsync(Data.Room.match.mapId, LoadSceneMode.Additive);

            gameEngine = GetComponentInChildren<IGameEngine>();
            await gameEngine.InitializeAsync();
            //await UniTask.WaitUntil(() => sceneLoadOperation.isDone && gameEngine.initialized);

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await gameEngine.DeinitializeAsync();

            restorer.Dispose();

            initialized = false;
        }

        public void Run(long tick, double interval, double elapsedTime)
        {
            gameEngine.Run(tick, interval, elapsedTime);
        }
    }
}
