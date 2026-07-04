using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using GameFramework;
using LOP.Event.LOPRunner.Update;
using VContainer;

namespace LOP
{
    [DIMonoBehaviour]
    public class LOPRunner : RunnerBase
    {
        [Inject] private GameFramework.World.WorldEventBuffer worldEventBuffer;
        [Inject] private GameFramework.World.IEventSink eventSink;
        [Inject] private IPhysicsSimulator physicsSimulator;
        [Inject] private GameFramework.World.IWorld world;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private AbilityEffectExecutor abilityEffectExecutor;

        [Inject] private IMapLoader mapLoader;
        [Inject] private IPlayerContext playerContext;
        [Inject] private GameFramework.Netcode.SnapshotHistory snapshotHistory;

        private const string MapId = "Assets/Art/Scenes/FlapWangMap.unity";

        private readonly Restorer restorer = new Restorer();

        public new LOPEntityManager entityManager => base.entityManager as LOPEntityManager;

        protected override INetworkTime CreateNetworkTime() => new MirrorNetworkTime();

        public override async Task InitializeAsync()
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

            // 맵 로딩과 베이스 초기화를 병렬로 — 둘 다 끝나길 기다린다.
            var mapLoadTask = mapLoader.LoadAsync(MapId);

            await base.InitializeAsync();

            await mapLoadTask;

            gameState = Initialized.State;
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

            gameState = Playing.State;
        }

        public override void Stop()
        {
            base.Stop();

            gameState = Paused.State;
        }

        public override void UpdateRunner()
        {
            BeginUpdate();

            ProcessNetworkMessage();

            ProcessInput();

            InterpolateEntity();

            UpdateEntity();

            UpdateAI();

            world.Tick(Runner.Time.tick, (float)tickUpdater.interval);
            DriveAbilityEffects();

            SimulatePhysics();

            UpdateVisualEffect();

            ProcessEvent();

            EndUpdate();
        }

        // world.Tick이 페이즈를 전진시킨 뒤, 진행 중 어빌리티의 Active 창 effect를 executor로 구동(대시 push 등).
        // 핸들러가 side 자원(Rigidbody)을 ctx의 entityManager로 잡도록 host가 구동 — DI 순환 회피.
        private void DriveAbilityEffects()
        {
            long tick = Runner.Time.tick;
            foreach (var entity in entityManager.GetEntities<LOPEntity>())
            {
                abilityEffectExecutor.DriveActiveEntity(entityRegistry.Get(entity.entityId), entityManager, tick);
            }
        }

        private void BeginUpdate()
        {
            DispatchEvent<Begin>();
        }

        private void ProcessNetworkMessage()
        {

        }

        private void ProcessInput()
        {
            DispatchEvent<ProcessInput>();
        }

        private void InterpolateEntity()
        {
        }

        private void UpdateEntity()
        {
            DispatchEvent<BeforeEntityUpdate>();

            entityManager.UpdateEntities();

            DispatchEvent<AfterEntityUpdate>();
        }

        private void UpdateAI()
        {
        }

        private void SimulatePhysics()
        {
            DispatchEvent<BeforePhysicsSimulation>();

            physicsSimulator.Simulate((float)tickUpdater.interval);

            DispatchEvent<AfterPhysicsSimulation>();
        }

        private void UpdateVisualEffect()
        {
        }

        private void ProcessEvent()
        {
            // --- World Core — 슬라이스 3: 이벤트 버퍼 드레인 ---
            var snapshot = worldEventBuffer.Snapshot;
            if (snapshot.Count == 0) return;

            eventSink.Emit(snapshot);
            worldEventBuffer.Clear();
            // --- end World Core slice 3 ---
        }

        private void EndUpdate()
        {
            RecordLocalSnapshot();

            DispatchEvent<End>();

            entityManager.DestroyMarkedEntities();
        }

        // 내 캐릭의 이번 틱 최종 시뮬 상태를 스냅샷에 남긴다. End 디스패치(=SnapReconciler 스무딩) 전에
        // 찍어 스무딩이 얹히기 전 원본 예측 상태를 포착한다. 되돌리기는 슬라이스 ③.
        private void RecordLocalSnapshot()
        {
            LOPEntity local = playerContext.entity;
            if (local == null)
            {
                return;
            }

            GameFramework.World.Entity worldEntity = entityRegistry.Get(local.entityId);
            if (worldEntity == null)
            {
                return;
            }

            var transform = worldEntity.Get<GameFramework.World.Transform>();
            var velocity = worldEntity.Get<GameFramework.World.Velocity>();
            if (transform == null || velocity == null)
            {
                return;
            }

            snapshotHistory.Record(new GameFramework.Netcode.EntitySnapshot(
                Runner.Time.tick,
                transform.Position,
                transform.Rotation,
                velocity.Linear));
        }
    }
}
