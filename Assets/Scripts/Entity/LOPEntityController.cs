using GameFramework;
using LOP.Event.Entity;
using LOP.Event.LOPGameEngine.Update;
using UniRx;
using UnityEngine;
using VContainer;

namespace LOP
{
    public class LOPEntityController : MonoBehaviour, ICleanup
    {
        [Inject]
        private IGameEngine gameEngine;

        public LOPEntity entity { get; private set; }

        public void SetEntity(LOPEntity entity)
        {
            this.entity = entity;
        }
        
        protected virtual void Start()
        {
            gameEngine.AddListener(this);
            EventBus.Default.Subscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);
        }

        public void Cleanup()
        {
            EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);
            gameEngine.RemoveListener(this);
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

        [GameEngineListen(typeof(AfterPhysicsSimulation))]
        private void OnUpdateAfterPhysicsSimulation()
        {
            entity.SyncPhysics();
        }
    }
}
