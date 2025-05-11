using GameFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VContainer;
using System.Threading.Tasks;
using System;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

namespace LOP
{
    public class LOPGame : MonoBehaviour, IGame
    {
        public event Action<IGameState> onGameStateChanged;

        [Inject]
        public IGameEngine gameEngine { get; private set; }

        [Inject]
        private IEnumerable<IGameMessageHandler> gameMessageHandlers;

        [Inject]
        private IRoomDataContext roomDataContext;

        private IGameState _gameState;
        public IGameState gameState
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
            gameState = Initializing.State;

            var oldSimulationMode = Physics.simulationMode;
            var oldAutoSyncTransforms = Physics.autoSyncTransforms;

            restorer.action += () =>
            {
                Physics.simulationMode = oldSimulationMode;
                Physics.autoSyncTransforms = oldAutoSyncTransforms;
            };

            Physics.simulationMode = SimulationMode.Script;
            Physics.autoSyncTransforms = false;

            foreach (var gameMessageHandler in gameMessageHandlers.OrEmpty())
            {
                gameMessageHandler.Register();
            }

            var sceneLoadOperation = SceneManager.LoadSceneAsync(roomDataContext.match.mapId, LoadSceneMode.Additive);  //  gamedata? addressable?

            await gameEngine.InitializeAsync();

            await UniTask.WaitUntil(() => sceneLoadOperation.isDone);

            gameState = Initialized.State;

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await gameEngine.DeinitializeAsync();

            foreach (var gameMessageHandler in gameMessageHandlers.OrEmpty())
            {
                gameMessageHandler.Unregister();
            }

            restorer.Dispose();

            initialized = false;
        }

        public void Run(long tick, double interval, double elapsedTime)
        {
            gameState = Playing.State;

            gameEngine.Run(tick, interval, elapsedTime);
        }
    }
}
