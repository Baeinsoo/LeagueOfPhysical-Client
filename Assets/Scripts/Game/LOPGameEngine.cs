using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using GameFramework;
using LOP.Event.LOPGameEngine.Update;

namespace LOP
{
    public class LOPGameEngine : GameEngineBase
    {
        public new LOPEntityManager entityManager => base.entityManager as LOPEntityManager;

        public override void UpdateEngine()
        {
            BeginUpdate();

            ProcessNetworkMessage();

            ProcessInput();

            InterpolateEntity();

            UpdateEntity();

            UpdateAI();

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

            Physics.Simulate((float)tickUpdater.interval);

            DispatchEvent<AfterPhysicsSimulation>();
        }

        private void UpdateVisualEffect()
        {
        }

        private void ProcessEvent()
        {
        }

        private void EndUpdate()
        {
            DispatchEvent<End>();

            entityManager.DestroyMarkedEntities();
        }
    }
}
