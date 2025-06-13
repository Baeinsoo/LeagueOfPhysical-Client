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
            entity.eventBus.Receive<PropertyChange>().Subscribe(OnPropertyChange).AddTo(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            SceneLifetimeScope.Resolve<IGameEngine>().RemoveListener(this);
        }

        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(entity.visualId):
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
