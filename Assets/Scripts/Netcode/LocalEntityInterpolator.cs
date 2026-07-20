using System.Collections.Generic;
using GameFramework;
using GameFramework.Netcode;
using LOP.Event.LOPRunner.Update;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 내 캐릭터의 지연 렌더링. 진짜 위치(sim)는 <see cref="Reconciler"/>가 하드 보정하고,
    /// 이 컴포넌트는 보이는 메시(visualGameObject)만 저장된 틱 스냅 사이를 보간해 부드럽게 그린다
    /// (틱/프레임 주기차 흡수). 게임 로직·물리는 건드리지 않는다.
    /// <para>연속 renderTime을 감싸는 두 스냅을 <see cref="SnapshotInterpolation"/>으로 브래킷 탐색해 보간
    /// (Fiedler snapshot interpolation). 절대 틱 키 조회가 아니라 브래킷이라 "그 틱이 없어서 스킵"이 불가능.</para>
    /// </summary>
    public class LocalEntityInterpolator : MonoBehaviour, ICleanup
    {
        [Inject] private IRunner runner;
        [Inject] private GameFramework.Netcode.RenderCorrectionSmoother renderCorrectionSmoother;
        [Inject] private GameFramework.World.EntityRegistry entityRegistry;

        public LOPActor actor { get; set; }

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
            runner.AddListener(this);
        }

        public void Cleanup()
        {
            runner.RemoveListener(this);
            renderCorrectionSmoother.Reset();
        }

        [RunnerListen(typeof(End))]
        private void OnEnd()
        {
            // renderTarget = 시뮬 위치 + 감쇠 중인 보정 offset. offset이 시뮬 스텝과 상쇄되어
            // 이 스트림은 보정 순간에도 연속 → 아래 LateUpdate 보간이 튀지 않는다(걷기 지연도 없음).
            var worldEntity = entityRegistry.Get(actor.entityId);
            if (worldEntity == null)
            {
                return;
            }
            samples.Add(new RenderSample
            {
                time = Runner.Time.tick * Runner.Time.tickInterval,
                position = renderCorrectionSmoother.Target(GameFramework.World.EntityMotionExtensions.GetPosition(worldEntity).ToNumerics()).ToUnity(),
                rotation = GameFramework.World.EntityMotionExtensions.GetRotation(worldEntity),
            });
            renderCorrectionSmoother.DecayTick((float)Runner.Time.tickInterval);
        }

        private void LateUpdate()
        {
            if (actor.visualGameObject == null || samples.Count == 0)
            {
                return;
            }

            // 한 틱 뒤 연속 시각에서 그린다(외삽 대신 과거 두 샘플 사이 보간).
            double renderTime = Runner.Time.elapsedTime - Runner.Time.tickInterval;

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
