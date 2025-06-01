using Cysharp.Threading.Tasks;
using GameFramework;
using LOP.Event.Entity;
using UniRx;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace LOP
{
    public class LOPEntityView : MonoEntityView<LOPEntity, LOPEntityController>
    {
        private GameObject _visualGameObject;
        public GameObject visualGameObject
        {
            get => _visualGameObject;
            private set
            {
                if (_visualGameObject != value)
                {
                    Destroy(_visualGameObject);
                }

                _visualGameObject = value;
            }
        }

        private string visualId;

        protected virtual void Start()
        {
            entity.eventBus.Receive<PropertyChange>().Subscribe(OnPropertyChange).AddTo(this);

            UpdateVisual(entity.visualId);

            bool isUserEntity = SceneLifetimeScope.Resolve<IGameDataContext>().userEntityId == entity.entityId;
            if (isUserEntity)
            {
                SceneLifetimeScope.Resolve<IPlayerContext>().entityView = this;
            }
        }

        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(entity.visualId):
                    UpdateVisual(entity.visualId);
                    break;
            }
        }

        private void UpdateVisual(string visualId)
        {
            if (this.visualId == visualId)
            {
                return;
            }

            this.visualId = visualId;
         
            var handle = Addressables.LoadAssetAsync<GameObject>(visualId);
            handle.Completed += (prefab) =>
            {
                GameObject visual = transform.parent.Find("Visual").gameObject;

                var instance = Instantiate(prefab.Result, visual.transform);

                visualGameObject = instance;
            };
        }
    }
}
