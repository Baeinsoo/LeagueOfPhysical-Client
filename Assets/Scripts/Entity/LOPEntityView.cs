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
        // UpdateVisual 요청마다 하나씩 올라간다 — await 이후 "내가 아직 최신 요청인가"를 판단하는 표.
        private int visualRequestId;

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

            // 먼저 카운터부터 올린다 — 로딩 중인 UpdateVisual이 있다면 await에서 깨어났을 때
            // "더 이상 최신 요청이 아님"을 보고 자기 handle을 스스로 놓게 만들기 위함(이중 해제 방지).
            visualRequestId++;

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
            UpdatePostureTilt();
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

        // 자세는 속도만 바꿔서는 눈에 안 보인다 — 실루엣으로 읽히게 몸을 기울인다.
        // 젤다의 세 자세를 각도로 옮긴 것: 대자는 배를 살짝 아래로, 다이브는 머리부터 수직,
        // 패러세일은 매달린 것처럼 뒤로 눕는다. 셋이 서로 확실히 다른 각도여야 구분된다.
        private const float SpreadPitch = 25f;
        private const float DivePitch = 85f;
        private const float GlidePitch = -15f;

        // 기울기가 붙는 속도(초당 도). 자세는 즉시 바뀌어도 몸은 따라가는 데 시간이 걸리는 게
        // 자연스럽고, 입력이 튈 때 몸이 덜덜거리는 것도 막는다.
        private const float PitchDegreesPerSecond = 360f;

        // 지금 적용 중인 기울기. 트랜스폼에서 되읽지 않는 이유: localEulerAngles는 0~360으로
        // 정규화돼서 음수 각(GlidePitch=-15)이 345로 튀어나와 몸이 한 바퀴 돈다.
        private float currentPitch = SpreadPitch;

        // 자세(대자/다이브/패러세일)는 값만 바꿔서는 안 보인다 — 몸을 기울여야 실루엣으로 읽힌다.
        // Posture가 없는 엔티티(다른 게임 모드)는 조용히 아무 일도 안 한다.
        private void UpdatePostureTilt()
        {
            if (entityId == null || visualGameObject == null)
            {
                return;
            }

            var posture = entityRegistry.Get(entityId)?.Get<Posture>();
            if (posture == null)
            {
                return;
            }

            float targetPitch = posture.Gliding ? GlidePitch : Mathf.Lerp(SpreadPitch, DivePitch, posture.Axis);
            currentPitch = Mathf.MoveTowards(currentPitch, targetPitch, PitchDegreesPerSecond * Time.deltaTime);
            visualGameObject.transform.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
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

            //  빈 id는 "보여줄 몸이 없다"는 정당한 상태다(예: 아바타 없는 판치기 플레이어) —
            //  에러가 아니므로 로드를 시도하지 않고 조용히 끝낸다. id가 있는데 못 찾는 것과는 다르다:
            //  그건 진짜 에셋 누락 버그라 아래처럼 그대로 실패시켜 드러나게 둔다.
            if (string.IsNullOrEmpty(visualId))
            {
                // 지금은 아무도 비id→빈id로 갈아타지 않지만, 나중에 그런 전환이 생겨도
                // 이전 오브젝트가 참조를 잃고 떠돌지 않도록 미리 비워둔다.
                visualGameObject = null;
                return;
            }

            // 로컬 변수로 들고 있다가, await 이후 아직 유효한 요청일 때만 필드로 소유권을 넘긴다.
            int requestId = ++visualRequestId;
            var handle = Addressables.LoadAssetAsync<GameObject>(visualId);
            await handle.Task;

            // 로딩 도중 Cleanup(파괴)되었거나 더 새 visualId 요청이 들어와 밀렸으면, 이 handle은
            // 아무도 쓰지 않으므로 여기서 직접 놓는다 — handle 하나당 해제는 정확히 한 번.
            bool stillCurrent = this != null && requestId == visualRequestId;
            if (stillCurrent == false)
            {
                Addressables.Release(handle);
                return;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                Addressables.Release(handle);
                return;
            }

            // 여기부터가 실제 소유권 이전 지점 — 필드는 항상 completed + 유효한 handle만 들고 있다.
            asyncOperationHandle = handle;
            visualGameObject = Instantiate(handle.Result, transform);
            // 옛 모델의 기울기를 새 모델이 물려받으면 한 프레임 동안 엉뚱한 자세로 보인다.
            currentPitch = SpreadPitch;
            var worldEntity = entityRegistry.Get(entityId);
            if (worldEntity != null)
            {
                visualGameObject.transform.position = GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity);
                visualGameObject.transform.rotation = Quaternion.Euler(GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity));
            }
        }
    }
}
