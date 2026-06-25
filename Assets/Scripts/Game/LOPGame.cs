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
        public event Action<IGameState> onGameStateChanged;

        [Inject]
        public IRunner runner { get; private set; }

        [Inject]
        private IEnumerable<IGameMessageHandler> gameMessageHandlers;

        [Inject]
        private IMapLoader mapLoader;

        private const string MapId = "Assets/Art/Scenes/FlapWangMap.unity";

        private IGameState _gameState;
        public IGameState gameState
        {
            get => _gameState;
            private set
            {
                if (_gameState == value)
                {
                    return;
                }

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
            Physics.gravity = new Vector3(0, -9.81f * 2, 0);

            foreach (var gameMessageHandler in gameMessageHandlers.OrEmpty())
            {
                gameMessageHandler.Register();
            }

            // 맵 로딩과 runner 초기화를 병렬로 — 둘 다 끝나길 기다린다.
            var mapLoadTask = mapLoader.LoadAsync(MapId);

            await runner.InitializeAsync();

            await mapLoadTask;

            gameState = Initialized.State;

            initialized = true;
        }

        public async Task DeinitializeAsync()
        {
            await runner.DeinitializeAsync();

            foreach (var gameMessageHandler in gameMessageHandlers.OrEmpty())
            {
                gameMessageHandler.Unregister();
            }

            restorer.Dispose();

            await mapLoader.UnloadAsync();

            initialized = false;
        }

        public void Run(long tick, double interval, double elapsedTime)
        {
            runner.Run(tick, interval, elapsedTime);

            gameState = Playing.State;
        }

        public void Stop()
        {
            runner.Stop();

            gameState = Paused.State;
        }
    }
}
