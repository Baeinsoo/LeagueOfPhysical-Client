using GameFramework;
using LOP.Event.Entity;
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
            EventBus.Default.Subscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);
        }

        public void Cleanup()
        {
            EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);
            runner.RemoveListener(this);
            entity = null;
        }

        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(AppearanceComponent.visualId):
                    break;
            }
        }

        [RunnerListen(typeof(BeforePhysicsSimulation))]
        private void OnBeforePhysicsSimulation()
        {
            entity.PushVelocityToPhysics();
        }

        [RunnerListen(typeof(AfterPhysicsSimulation))]
        private void OnUpdateAfterPhysicsSimulation()
        {
            entity.SyncPhysics();
        }
    }
}
