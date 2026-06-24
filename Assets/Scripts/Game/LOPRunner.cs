using System.Collections;
using System.Collections.Generic;
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

        public new LOPEntityManager entityManager => base.entityManager as LOPEntityManager;

        protected override INetworkTime CreateNetworkTime() => new MirrorNetworkTime();

        public override void UpdateRunner()
        {
            BeginUpdate();

            ProcessNetworkMessage();

            ProcessInput();

            InterpolateEntity();

            UpdateEntity();

            UpdateAI();

            world.Tick(Runner.Time.tick, (float)tickUpdater.interval);

            SimulatePhysics();

            UpdateVisualEffect();

            ProcessEvent();

            EndUpdate();
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
            DispatchEvent<End>();

            entityManager.DestroyMarkedEntities();
        }
    }
}
