using GameFramework;
using LOP.Event.LOPRunner.Update;
using UniRx;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class LOPEntityController : MonoBehaviour, ICleanup
    {
        [Inject]
        private IRunner runner;

        public LOPEntity entity { get; private set; }

        public void SetEntity(LOPEntity entity)
        {
            this.entity = entity;
        }
        
        protected virtual void Start()
        {
            runner.AddListener(this);
        }

        public void Cleanup()
        {
            runner.RemoveListener(this);
            entity = null;
        }

        [RunnerListen(typeof(BeforePhysicsSimulation))]
        private void OnBeforePhysicsSimulation()
        {
            entity.PushMotionToPhysics();
        }

        [RunnerListen(typeof(AfterPhysicsSimulation))]
        private void OnUpdateAfterPhysicsSimulation()
        {
            entity.SyncPhysics();
        }
    }
}
