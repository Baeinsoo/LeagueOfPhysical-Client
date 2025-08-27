using GameFramework;
using LOP.Event.Entity;
using LOP.Event.LOPGameEngine.Update;
using UniRx;
using VContainer;

namespace LOP
{
    public class LOPEntityController : MonoEntityController<LOPEntity>
    {
        [Inject]
        private IGameEngine gameEngine;
        
        protected virtual void Start()
        {
            gameEngine.AddListener(this);
            EventBus.Default.Subscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);
        }

        public override void Cleanup()
        {
            EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);
            gameEngine.RemoveListener(this);

            base.Cleanup();
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
