using GameFramework;
using LOP.Event.Entity;
using LOP.Event.LOPGameEngine.Update;
using UniRx;

namespace LOP
{
    public class LOPEntityController : MonoEntityController<LOPEntity>
    {
        protected virtual void Awake()
        {
            SceneLifetimeScope.Resolve<IGameEngine>().AddListener(this);
        }

        protected virtual void Start()
        {
            EventBus.Default.Subscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);

            SceneLifetimeScope.Resolve<IGameEngine>().RemoveListener(this);
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
