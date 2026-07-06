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
            }
        }

        private void Update()
        {
            UpdateRunAnimation();
        }

        // 걷기 애니는 연속 상태(속도)라 매 프레임 읽어 갱신한다(pull).
        // 변경 알림(PropertyChange)에 기대면 이동이 World에 직접 쓴 변화(제동→0 등)를 놓쳐 애니가 옛 상태에 머문다.
        private void UpdateRunAnimation()
        {
            if (entity == null || visualGameObject == null)
            {
                return;
            }

            Animator animator = visualGameObject.GetComponent<Animator>();
            if (animator == null)
            {
                return;
            }

            const float walkThreshold = 0.01f;
            float horizontalSpeedSquared = entity.velocity.x * entity.velocity.x + entity.velocity.z * entity.velocity.z;
            bool fast = horizontalSpeedSquared > walkThreshold * walkThreshold;
            bool grounded = entity.IsGrounded();
            bool run = fast && grounded;

            // [RUN-DIAG 임시] velocity=0 깜빡임(가짜 무입력) 소멸 확인용. Task 2 후 제거.
            if (run != _lastRunDiag)
            {
                UnityEngine.Debug.Log($"[RUN] {(run ? "RUN" : "IDLE")} spd={Mathf.Sqrt(horizontalSpeedSquared):F3} fast={fast} grounded={grounded}");
                _lastRunDiag = run;
            }

            animator.SetBool("Run", run);
        }

        private bool _lastRunDiag;
     
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
