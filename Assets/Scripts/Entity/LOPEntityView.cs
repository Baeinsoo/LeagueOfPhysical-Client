using GameFramework;
using UniRx;
using UnityEngine;
using LOP.Event.Entity;

namespace LOP
{
    public class LOPEntityView : MonoEntityView<LOPEntity, LOPEntityController>
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
