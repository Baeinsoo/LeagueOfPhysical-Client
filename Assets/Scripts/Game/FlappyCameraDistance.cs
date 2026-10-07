using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// Flappy 레이스 카메라 거리는 20m로 고정한다 — 구간별로 줌을 바꾸면 눈대중(날아오는 틈·새 크기)이
    /// 흔들려서 뺐다(사용자 결정 2026-10-07). 20m는 새와 틈이 원조 물리와 같은 크기로 보이는 거리다.
    ///
    /// <para>내 새가 대시(패드 부스트·수동 대시) 중일 때만 카트 부스트처럼 3m 뒤로 빠졌다가,
    /// 대시가 끝나면 다시 부드럽게 돌아온다. <see cref="FlappyFallLookDown"/>과 같은 패턴 —
    /// 켜고 끄기 대신 목표 거리를 SmoothDamp로 따라가야 들어가고 나올 때 덜컹이지 않는다.</para>
    /// </summary>
    public class FlappyCameraDistance : ITickable, System.IDisposable
    {
        internal const float BaseDistance = 20f;   // m — 원조 물리에서 새·틈이 보이던 크기
        internal const float BoostPullBack = 3f;    // m — 대시 중 추가로 빠지는 거리
        private const float SmoothTime = 0.4f;      // s — 카트 부스트처럼 0.3~0.5초 사이로 들어가고 나온다

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        private float current = BaseDistance;
        private float velocity;

        //  테스트 seam — 실제로는 CameraController.DistanceOverride, 테스트는 가짜를 끼운다.
        internal System.Action<float?> applyDistance;

        public FlappyCameraDistance(IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry,
                                    CameraController cameraController)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            applyDistance = d => { if (cameraController != null) { cameraController.DistanceOverride = d; } };
        }

        public void Tick()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            bool dashing = false;
            if (string.IsNullOrEmpty(playerContext.entityId) == false)
            {
                float remaining = entityRegistry.Get(playerContext.entityId)?.Get<FlappyDash>()?.DashRemaining ?? 0f;
                dashing = remaining > 0f;
            }
            float target = TargetDistance(dashing);
            current = Mathf.SmoothDamp(current, target, ref velocity, SmoothTime, Mathf.Infinity, deltaTime);
            applyDistance(current);
        }

        //  계산만 떼어 테스트한다 — EntityRegistry·Tick 없이 바로 확인할 수 있다.
        internal static float TargetDistance(bool dashing) => BaseDistance + (dashing ? BoostPullBack : 0f);

        public void Dispose()
        {
            current = BaseDistance;
            velocity = 0f;
            applyDistance(null);
        }
    }
}
