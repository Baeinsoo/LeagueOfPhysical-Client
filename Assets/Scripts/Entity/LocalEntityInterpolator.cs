using GameFramework;
using LOP.Event.LOPRunner.Update;
using UnityEngine;
using VContainer;

namespace LOP
{
    /// <summary>
    /// 내 캐릭터의 지연 렌더링. 진짜 위치(sim)는 <see cref="Reconciler"/>가 하드 보정하고,
    /// 이 컴포넌트는 보이는 메시(visualGameObject)만 저장된 틱 스냅 사이를 보간해 부드럽게 그린다
    /// (틱/프레임 주기차 흡수). 게임 로직·물리는 건드리지 않는다.
    /// </summary>
    public class LocalEntityInterpolator : MonoBehaviour, ICleanup
    {
        [Inject] private IRunner runner;
        [Inject] private GameFramework.Netcode.RenderCorrectionSmoother renderCorrectionSmoother;

        public LOPEntity entity { get; set; }
        public LOPEntityView entityView { get; set; }

        private BoundedDictionary<long, EntityTransform> entityTransformSnaps;

        private void Awake()
        {
            entityTransformSnaps = new BoundedDictionary<long, EntityTransform>(20);
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
            entityTransformSnaps[Runner.Time.tick] = new EntityTransform
            {
                position = entity.position,
                rotation = entity.rotation,
                velocity = entity.velocity,
            };
        }

        private void LateUpdate()
        {
            if (entityView.visualGameObject == null || entityTransformSnaps.Count < 2)
            {
                return;
            }

            double tickInterval = Runner.Time.tickInterval;
            double renderTime = Runner.Time.elapsedTime - tickInterval;

            long tickPrev = (long)(renderTime / tickInterval);
            long tickNext = tickPrev + 1;
            float t = (float)((renderTime % tickInterval) / tickInterval);

            // 역산 틱이 버퍼에 없으면 이번 프레임 보간 스킵(직전 위치 유지).
            if (entityTransformSnaps.TryGetValue(tickPrev, out var prev) == false ||
                entityTransformSnaps.TryGetValue(tickNext, out var next) == false)
            {
                return;
            }

            Vector3 target = Vector3.Lerp(prev.position, next.position, t);
            entityView.visualGameObject.transform.position =
                renderCorrectionSmoother.Smooth(target.ToNumerics(), Time.deltaTime).ToUnity();
            entityView.visualGameObject.transform.rotation = Quaternion.Slerp(
                Quaternion.Euler(prev.rotation), Quaternion.Euler(next.rotation), t);
        }
    }
}
