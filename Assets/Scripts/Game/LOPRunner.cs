using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using GameFramework;
using GameFramework.Runner;
using GameFramework.Physics;
using LOP.Event.LOPRunner.Update;
using VContainer;
using GameFramework.Netcode;

namespace LOP
{
    [SceneInjectMonoBehaviour]
    public class LOPRunner : RunnerBase
    {
        [Inject] private GameFramework.World.IWorld world;

        [Inject] private IMapLoader mapLoader;
        [Inject] private INetworkTime networkTimeSource;

        // Slice 5-B: 파이프라인 스텝 — 순서대로 직접 호출(넷코드 순서 불변식이 코드에 명시).
        [Inject] private ReconcileSystem reconcileSystem;
        [Inject] private PhysicsSimulationSystem physicsSimulationSystem;
        [Inject] private WorldEventDrainSystem worldEventDrainSystem;
        [Inject] private WorldStateSaveSystem worldStateSaveSystem;
        [Inject] private DespawnFlushSystem despawnFlushSystem;

        [Inject] private IRoomDataStore roomDataStore;
        [Inject] private LOP.MasterData.LOPMasterData masterData;

        private readonly Restorer restorer = new Restorer();

        public override async Task InitializeAsync()
        {
            gameState = RunnerState.Initializing;

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

            // 맵 로딩과 베이스 초기화를 병렬로 — 둘 다 끝나길 기다린다.
            var mapLoadTask = mapLoader.LoadAsync(ResolveMapScenePath());

            await base.InitializeAsync();

            networkTime = networkTimeSource;
            ((LOPTickUpdater)tickUpdater).networkTime = networkTimeSource;

            await mapLoadTask;

            gameState = RunnerState.Initialized;
        }

        /// <summary>이 판에서 로드할 맵 씬. 매치의 이번 라운드가 가리키는 맵에서 온다.</summary>
        private string ResolveMapScenePath()
        {
            var rounds = roomDataStore.match?.rounds;
            var roundIndex = MatchSceneResolver.CurrentRoundIndex(rounds?.Length ?? 0);
            var round = rounds[roundIndex];
            var map = MatchSceneResolver.RequireRow(
                "TbMap", round.mapId, masterData.Tables.TbMap.GetOrDefault(round.mapId));

            return MatchSceneResolver.RequireScenePath("TbMap", round.mapId, map.ScenePath);
        }

        public override async Task DeinitializeAsync()
        {
            await base.DeinitializeAsync();

            restorer.Dispose();

            await mapLoader.UnloadAsync();
        }

        public override void Run(long tick, double interval, double elapsedTime)
        {
            base.Run(tick, interval, elapsedTime);

            gameState = RunnerState.Playing;
        }

        public override void Stop()
        {
            base.Stop();

            gameState = RunnerState.Paused;
        }

        /// <summary>서버의 MatchEndedToC를 받아 매치 종료 상태로 들어간다 — 판정은 서버 권위, 클라는 통보받을 뿐이다.</summary>
        public void EndMatch()
        {
            gameState = RunnerState.GameOver;
        }

        protected override void UpdateRunner()
        {
            reconcileSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            RunPhase<ProcessInput>(tickUpdater.tick, (float)tickUpdater.deltaTime);
            world.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            physicsSimulationSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            worldEventDrainSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            worldStateSaveSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
            RunPhase<End>(tickUpdater.tick, (float)tickUpdater.deltaTime);
            despawnFlushSystem.Tick(tickUpdater.tick, (float)tickUpdater.interval);
        }
    }
}
