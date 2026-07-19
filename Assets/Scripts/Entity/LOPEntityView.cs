using Cysharp.Threading.Tasks;
using GameFramework;
using LOP.Event.Entity;
using MessagePipe;
using UniRx;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using VContainer;

namespace LOP
{
    public class LOPEntityView : MonoBehaviour, ICleanup
    {
        public LOPActor actor { get; private set; }

        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        public void SetEntity(LOPActor actor)
        {
            this.actor = actor;
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

        private System.IDisposable subscriptions;

        protected virtual void Start()
        {
            var bag = DisposableBag.CreateBuilder();
            GlobalMessagePipe.GetSubscriber<string, AbilityActivated>().Subscribe(actor.entityId, OnAbilityActivated).AddTo(bag);
            GlobalMessagePipe.GetSubscriber<string, EntityDamage>().Subscribe(actor.entityId, OnEntityDamage).AddTo(bag);
            subscriptions = bag.Build();

            var appearance = entityRegistry.Get(actor.entityId)?.Get<Appearance>();
            if (appearance != null)
            {
                UpdateVisual(appearance.VisualId);
            }
        }

        public void Cleanup()
        {
            subscriptions?.Dispose();

            if (asyncOperationHandle.IsValid())
            {
                Addressables.Release(asyncOperationHandle);
            }

            if (_visualGameObject != null)
            {
                Destroy(_visualGameObject);
            }

            actor = null;
        }

        private void Update()
        {
            UpdateRunAnimation();
        }

        // 걷기 애니는 연속 상태(속도)라 매 프레임 읽어 갱신한다(pull).
        // 변경 알림(PropertyChange)에 기대면 이동이 World에 직접 쓴 변화(제동→0 등)를 놓쳐 애니가 옛 상태에 머문다.
        private void UpdateRunAnimation()
        {
            if (actor == null || visualGameObject == null)
            {
                return;
            }

            Animator animator = visualGameObject.GetComponent<Animator>();
            if (animator == null)
            {
                return;
            }

            const float walkThreshold = 0.01f;
            var worldEntity = entityRegistry.Get(actor.entityId);
            Vector3 v = worldEntity != null ? GameFramework.World.EntityMotionExtensions.GetVelocity(worldEntity) : Vector3.zero;
            float horizontalSpeedSquared = v.x * v.x + v.z * v.z;
            bool grounded = worldEntity != null && IsGrounded(GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity));
            animator.SetBool("Run", horizontalSpeedSquared > walkThreshold * walkThreshold && grounded);
        }

        // TODO: 고도화 필요! (접지 판정 — 구 LOPActor에서 이전)
        private static bool IsGrounded(Vector3 position)
        {
            Vector3 checkPosition = position + Vector3.down * 0.2f;
            Collider[] colliders = Physics.OverlapSphere(checkPosition, 0.4f);
            return System.Linq.Enumerable.Any(colliders, col => col.gameObject.name == "Plane");
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

            visualGameObject = Instantiate(asyncOperationHandle.Task.Result, transform);
            var worldEntity = entityRegistry.Get(actor.entityId);
            if (worldEntity != null)
            {
                visualGameObject.transform.position = GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity);
                visualGameObject.transform.rotation = Quaternion.Euler(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity));
            }
        }
    }
}
