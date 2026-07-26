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
        public string entityId { get; private set; }

        [Inject] private GameFramework.World.EntityRegistry entityRegistry;
        [Inject] private EntityRenderClock renderClock;
        [Inject] private AbilityDataProvider abilityDataProvider;
        [Inject] private LOP.MasterData.LOPMasterData masterData;

        public void SetEntityId(string entityId)
        {
            this.entityId = entityId;
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
            GlobalMessagePipe.GetSubscriber<string, EntityDamage>().Subscribe(entityId, OnEntityDamage).AddTo(bag);
            subscriptions = bag.Build();

            var appearance = entityRegistry.Get(entityId)?.Get<Appearance>();
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

            entityId = null;
        }

        private void Update()
        {
            UpdateRunAnimation();
            UpdateAbilityAnimation();
        }

        // 걷기 애니는 연속 상태(속도)라 매 프레임 읽어 갱신한다(pull).
        // 변경 알림(PropertyChange)에 기대면 이동이 World에 직접 쓴 변화(제동→0 등)를 놓쳐 애니가 옛 상태에 머문다.
        private void UpdateRunAnimation()
        {
            if (entityId == null || visualGameObject == null)
            {
                return;
            }

            Animator animator = visualGameObject.GetComponent<Animator>();
            if (animator == null)
            {
                return;
            }

            const float walkThreshold = 0.01f;
            var worldEntity = entityRegistry.Get(entityId);
            Vector3 v = worldEntity != null ? GameFramework.World.EntityMotionExtensions.GetVelocity(worldEntity) : Vector3.zero;
            float horizontalSpeedSquared = v.x * v.x + v.z * v.z;
            bool grounded = worldEntity?.Get<GameFramework.World.GroundState>()?.IsGrounded ?? false;
            animator.SetBool("Run", horizontalSpeedSquared > walkThreshold * walkThreshold && grounded);
        }

        // 이미 그리기 시작한 발동. 같은 스킬을 연타해도 발동마다 값이 달라지도록 종료 틱까지 함께 본다.
        private (int abilityId, long endTick) playingActivation;

        // 직전 동작(서 있기·달리기)에서 시전 동작으로 넘어가며 섞는 시간. 초 단위라 출발 동작이 뭐든 일정하다.
        private const float AbilityBlendSeconds = 0.1f;

        // 시전 애니는 "지금 시전 중"이라는 지속 상태에서 파생한다. 발동 순간의 이벤트를 놓쳤거나
        // 늦게 접속한 클라도 스냅샷만 보고 애니를 *중간부터* 이어 붙일 수 있다(트리거로는 불가능).
        private void UpdateAbilityAnimation()
        {
            if (entityId == null || visualGameObject == null)
            {
                return;
            }

            var activation = entityRegistry.Get(entityId)?.Get<Abilities>()?.Activation;
            if (activation == null || abilityDataProvider.TryGet(activation.Value.AbilityId, out AbilityData data) == false)
            {
                playingActivation = default;
                return;
            }

            long totalTicks = data.StartupTicks + data.ActiveTicks + data.RecoveryTicks;
            long tick = renderClock.TickFor(entityId);
            if (AbilityPlayback.Solve(activation.Value, tick, totalTicks, out _, out _) == false)
            {
                playingActivation = default;
                return;
            }

            var abilityView = masterData.Tables.TbAbilityView.GetOrDefault(activation.Value.AbilityId);
            if (abilityView == null || string.IsNullOrEmpty(abilityView.AnimState))
            {
                playingActivation = default;
                return;   // 연출을 정해두지 않은 어빌리티(대시·헤이스트 등)
            }

            // 시작 지점만 잡아주고 뒤는 애니메이터에 맡긴다. 매 프레임 밀어넣으면 클립 길이와
            // 어빌리티 길이가 다를 때 끝에서 계속 되감겨 덜덜거린다.
            var current = (activation.Value.AbilityId, activation.Value.RecoveryEndTick);
            if (playingActivation == current)
            {
                return;
            }

            Animator animator = visualGameObject.GetComponent<Animator>();
            if (animator == null)
            {
                return;
            }

            // 발동 뒤 "몇 초 지났나"에서 이어 붙인다 — 비율(진행도)로 넣으면 클립 길이가 어빌리티
            // 길이와 다를 때, 발동을 본 클라와 놓친 클라가 서로 다른 포즈를 그리게 된다.
            long elapsedTicks = tick - (activation.Value.RecoveryEndTick - totalTicks);
            float elapsedSeconds = (float)(elapsedTicks * renderClock.SecondsPerTick);
            animator.CrossFadeInFixedTime(abilityView.AnimState, AbilityBlendSeconds, abilityView.AnimLayer, elapsedSeconds);
            playingActivation = current;
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
            var worldEntity = entityRegistry.Get(entityId);
            if (worldEntity != null)
            {
                visualGameObject.transform.position = GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity);
                visualGameObject.transform.rotation = Quaternion.Euler(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity));
            }
        }
    }
}
