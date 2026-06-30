using Cysharp.Threading.Tasks;
using GameFramework;
using LOP.Event.Entity;
using UniRx;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace LOP
{
    public class LOPEntityView : MonoBehaviour, ICleanup
    {
        public LOPEntity entity { get; private set; }

        public void SetEntity(LOPEntity entity)
        {
            this.entity = entity;
        }

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
            EventBus.Default.Subscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);
            EventBus.Default.Subscribe<AbilityActivated>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnAbilityActivated);
            EventBus.Default.Subscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnEntityDamage);

            if (entity.TryGetEntityComponent<AppearanceComponent>(out var appearanceComponent))
            {
                UpdateVisual(appearanceComponent.visualId);
            }
        }

        public void Cleanup()
        {
            EventBus.Default.Unsubscribe<PropertyChange>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnPropertyChange);
            EventBus.Default.Unsubscribe<AbilityActivated>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnAbilityActivated);
            EventBus.Default.Unsubscribe<EntityDamage>(EventTopic.EntityId<LOPEntity>(entity.entityId), OnEntityDamage);

            if (asyncOperationHandle.IsValid())
            {
                Addressables.Release(asyncOperationHandle);
            }

            if (_visualGameObject != null)
            {
                Destroy(_visualGameObject);
            }

            entity = null;
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
     
        // 어빌리티 발동 연출 cue → 애니 트리거. 한 곳에서 매핑(cue 늘면 dict에 추가, if 누적 없음).
        // 캐릭터별 컨트롤러가 쓰는 트리거 이름이 달라 cue 하나에 후보 트리거를 다 친다(없는 건 no-op).
        private static readonly System.Collections.Generic.Dictionary<string, string[]> CueTriggers =
            new System.Collections.Generic.Dictionary<string, string[]>
            {
                ["attack"] = new[] { "Attack 01", "Attack", "Melee Attack" },
            };

        private void OnAbilityActivated(AbilityActivated abilityActivated)
        {
            if (visualGameObject == null)
            {
                return;
            }

            if (CueTriggers.TryGetValue(abilityActivated.cue, out var triggers))
            {
                Animator animator = visualGameObject.GetComponent<Animator>();
                foreach (var trigger in triggers)
                {
                    animator.SetTrigger(trigger);
                }
            }
        }

        private void OnEntityDamage(EntityDamage entityDamage)
        {
            if (visualGameObject == null || entityDamage.isDodged)
            {
                return;
            }

            Animator animator = visualGameObject.GetComponent<Animator>();
            if (animator != null)
            {
                animator.SetTrigger("Hit");
            }
        }

        private async void UpdateVisual(string visualId)
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
            await asyncOperationHandle.Task;

            GameObject visual = transform.parent.Find("Visual").gameObject;

            visualGameObject = Instantiate(asyncOperationHandle.Task.Result, visual.transform);
            visualGameObject.transform.position = entity.position;
            visualGameObject.transform.rotation = Quaternion.Euler(entity.rotation);
        }
    }
}
