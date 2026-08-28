using System.Collections.Generic;
using GameFramework;
using GameFramework.Runner;
using GameFramework.Netcode;
using LOP.Event.LOPRunner.Update;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 예측된 엔티티의 지연 렌더링(내 것이든 남의 것이든). 진짜 위치(sim)는 <see cref="Reconciler"/>가 하드 보정하고,
    /// 이 컴포넌트는 보이는 메시(visualGameObject)만 저장된 틱 스냅 사이를 보간해 부드럽게 그린다
    /// (틱/프레임 주기차 흡수). 게임 로직·물리는 건드리지 않는다.
    /// <para>연속 renderTime을 감싸는 두 스냅을 <see cref="SnapshotInterpolation"/>으로 브래킷 탐색해 보간
    /// (Fiedler snapshot interpolation). 절대 틱 키 조회가 아니라 브래킷이라 "그 틱이 없어서 스킵"이 불가능.</para>
    /// </summary>
    public class PredictedEntityInterpolator : MonoBehaviour, ICleanup, ITickSystem
    {
        [Inject] private IRunner runner;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        public LOPActor actor { get; set; }

        // 내 것이냐 남의 것이냐에 따라 설정이 달라서 EntityBinder가 만들어 건넨다
        // (<see cref="RenderCorrectionSmootherFactory"/>에 그 이유가 적혀 있다).
        public GameFramework.Netcode.RenderCorrectionSmoother renderCorrectionSmoother { get; set; }

        // 캐릭터가 아니면(아이템 등) null — StunAppearance는 캐릭터에만 붙는다.
        public StunAppearance stunAppearance { get; set; }

        private struct RenderSample
        {
            public double time;
            public Vector3 position;
            public Vector3 rotation;
        }

        private BoundedList<RenderSample> samples;
        private readonly List<double> sampleTimes = new List<double>(20);

        private void Awake()
        {
            samples = new BoundedList<RenderSample>(20);
        }

        private void Start()
        {
            runner.RegisterSystem<End>(this);
        }

        public void Cleanup()
        {
            runner.UnregisterSystem(this);
            renderCorrectionSmoother.Reset();
        }

        public void Tick(long tick, float deltaTime)
        {
            // renderTarget = 시뮬 위치 + 0으로 수렴 중인 보정 offset(3차 에르미트 블렌드, 감쇠 아님).
            // offset이 시뮬 스텝과 상쇄되어 이 스트림은 보정 순간에도 연속
            // → 아래 LateUpdate 보간이 튀지 않는다(걷기 지연도 없음).
            var worldEntity = entityRegistry.Get(actor.entityId);
            if (worldEntity == null)
            {
                return;
            }

            // 예측 대상은 스냅을 기다리지 않고 시뮬 결과를 그 자리에서 읽는다 — FlappyStun이 없는
            // 엔티티(FlapWang 등)는 항상 null이라 자연히 스턴 표시가 안 켜진다.
            stunAppearance?.SetState(StunVisuals.Of(worldEntity.Get<FlappyStun>()));

            samples.Add(new RenderSample
            {
                time = tick * runner.tickUpdater.interval,
                position = renderCorrectionSmoother.Target(GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity).ToNumerics()).ToUnity(),
                rotation = GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity),
            });
            renderCorrectionSmoother.Advance(deltaTime);
        }

        /// <summary>
        /// 시뮬 위치가 하드 보정으로 튀었음을 알린다. 보이는 메시가 그 차이를 부드럽게 흡수한다
        /// (시뮬에는 영향 없음). 크기별로 스냅/무시를 판단하는 것은 스무더 몫이다.
        /// <paramref name="authoritativeVelocity"/>는 이음매에서 렌더 속도를 잇는 데 쓴다.
        /// <paramref name="deltaTime"/>는 이 보정 직후 곧바로 이어질 이번 틱의 시간폭이다 — 호출
        /// 순서상(Reconcile→world.Tick→여기 Target) sim이 이미 한 틱 더 진행된 뒤에 첫 Target이
        /// 불리므로, 그만큼을 스무더가 미리 셈해 두게 한다(안 그러면 이음매가 한 틱 늦게 체결된다).
        /// </summary>
        public void OnCorrection(System.Numerics.Vector3 before, System.Numerics.Vector3 after,
                                 System.Numerics.Vector3 authoritativeVelocity, float deltaTime)
        {
            renderCorrectionSmoother.OnCorrection(before, after, authoritativeVelocity, deltaTime);
        }

        private void LateUpdate()
        {
            if (actor.visualGameObject == null || samples.Count == 0)
            {
                return;
            }

            // 한 틱 뒤 연속 시각에서 그린다(외삽 대신 과거 두 샘플 사이 보간).
            double renderTime = runner.tickUpdater.elapsedTime - runner.tickUpdater.interval;

            sampleTimes.Clear();
            for (int i = 0; i < samples.Count; i++)
            {
                sampleTimes.Add(samples[i].time);
            }

            BracketIndices bracket = SnapshotInterpolation.Solve(sampleTimes, renderTime);
            RenderSample lower = samples[bracket.Lower];
            RenderSample upper = samples[bracket.Upper];

            // lower/upper는 이미 스무딩된 renderTarget이라 그대로 보간하면 된다.
            actor.visualGameObject.transform.position = Vector3.Lerp(lower.position, upper.position, bracket.Alpha);
            actor.visualGameObject.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(lower.rotation), Quaternion.Euler(upper.rotation), bracket.Alpha);
        }
    }
}
