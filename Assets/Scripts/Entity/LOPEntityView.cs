using Cysharp.Threading.Tasks;
using GameFramework;
using LOP.Event.Entity;
using UniRx;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

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
        private AsyncOperationHandle<GameObject> asyncOperationHandle;

        protected virtual void Start()
        {
            entity.eventBus.Receive<PropertyChange>().Subscribe(OnPropertyChange).AddTo(this);
            entity.eventBus.Receive<ActionStart>().Subscribe(OnActionStart).AddTo(this);

            if (entity.TryGetEntityComponent<AppearanceComponent>(out var appearanceComponent))
            {
                UpdateVisual(appearanceComponent.visualId);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (asyncOperationHandle.IsValid())
            {
                Addressables.Release(asyncOperationHandle);
            }

            if (_visualGameObject != null)
            {
                Destroy(_visualGameObject);
            }
        }

        private void OnPropertyChange(PropertyChange propertyChange)
        {
            switch (propertyChange.propertyName)
            {
                case nameof(AppearanceComponent.visualId):
                    UpdateVisual(entity.GetEntityComponent<AppearanceComponent>().visualId);
                    break;

                case nameof(entity.velocity):
                case nameof(entity.position):
                    float walkThreshold = 0.01f;
                    float walkThresholdSquared = walkThreshold * walkThreshold;
                    float horizontalSpeedSquared = entity.velocity.x * entity.velocity.x + entity.velocity.z * entity.velocity.z;
                    if (horizontalSpeedSquared > walkThresholdSquared && entity.IsGrounded())
                    {
                        Animator animator = visualGameObject?.GetComponent<Animator>();
                        if (animator != null)
                        {
                            animator.SetBool("Run", true);
                        }
                    }
                    else
                    {
                        Animator animator = visualGameObject?.GetComponent<Animator>();
                        if (animator != null)
                        {
                            animator.SetBool("Run", false);
                        }
                    }
                    break;
            }
        }
     
        private void OnActionStart(ActionStart actionStart)
        {
            if (visualGameObject == null)
            {
                return;
            }

            if (actionStart.actionCode == "attack_001")
            {
                visualGameObject.GetComponent<Animator>().SetTrigger("Attack 01");
                visualGameObject.GetComponent<Animator>().SetTrigger("Attack");
                visualGameObject.GetComponent<Animator>().SetTrigger("Melee Attack");
            }
        }

        private void UpdateVisual(string visualId)
        {
            if (this.visualId == visualId)
            {
                return;
            }

            this.visualId = visualId;

            if (asyncOperationHandle.IsValid())
            {
                Addressables.Release(asyncOperationHandle);
            }

            asyncOperationHandle = Addressables.LoadAssetAsync<GameObject>(visualId);
            asyncOperationHandle.Completed += (prefab) =>
            {
                GameObject visual = transform.parent.Find("Visual").gameObject;

                visualGameObject = Instantiate(prefab.Result, visual.transform);
                visualGameObject.transform.position = entity.position;
                visualGameObject.transform.rotation = Quaternion.Euler(entity.rotation);
            };
        }
    }
}
