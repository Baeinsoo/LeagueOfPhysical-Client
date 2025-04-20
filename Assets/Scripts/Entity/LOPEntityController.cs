using GameFramework;
using LOP.Event.Entity;
using UnityEngine;
using UniRx;

namespace LOP
{
    public class LOPEntityController : MonoEntityController<LOPEntity>
    {
        protected virtual void Start()
        {
            entity.eventBus.Receive<PropertyChange>().Subscribe(OnPropertyChange).AddTo(this);
        }

        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(entity.visualGameObject):
                    break;
            }
        }
    }
}
