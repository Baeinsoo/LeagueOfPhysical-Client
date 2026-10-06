using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 내 새가 빠르게 떨어지는 동안 카메라 중심을 아래로 옮겨 바닥이 어디인지 먼저 보여 준다(절벽·샤프트).
    /// 플랫포머 카메라의 세로 앞보기(vertical look-ahead). 판정과 무관하다.
    /// </summary>
    public class FlappyFallLookDown : ITickable, System.IDisposable
    {
        //  옛 값은 고정 22~30 m/s였다(맥스 낙하 30일 때 얘기). 그 비율을 그대로(22f/30f) 고정해
        //  맥스 낙하에 곱하는 쪽으로 바꿨다. 미네 코어로 맥스 낙하가 30→11.25로 줄어도
        //  (22/30×11.25=8.25) 같은 느낌이 옮겨진다. 비율 밑에서 켜면 날갯짓 뒤 0.6초만에 닿아
        //  평소 비행에서도 카메라가 출렁인다(2026-09-30).
        private const float StartRatio = 22f / 30f;  // 오늘의 22/30 — 맥스 낙하 속도에 대한 비율
        private const float MaxDrop = 5f;           // m
        //  켜고 끄기 대신 속도에 비례한 목표를 부드럽게 따라간다 — 일정 속도로 출발·정지하면 덜컹인다.
        private const float SmoothTime = 0.3f;      // s

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly FlappyConfig config;
        private float offsetVelocity;

        internal System.Action<Vector3> applyPivot;
        internal float Offset { get; private set; }

        public FlappyFallLookDown(IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry,
                                  CameraController cameraController, FlappyConfig config)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.config = config;
            applyPivot = p => { if (cameraController != null) { cameraController.PivotOffset = p; } };
        }

        public void Tick()
        {
            Tick(Time.deltaTime);
        }

        internal void Tick(float deltaTime)
        {
            float vy = 0f;
            if (string.IsNullOrEmpty(playerContext.entityId) == false)
            {
                var velocity = entityRegistry.Get(playerContext.entityId)?.Get<GameFramework.World.Velocity>();
                if (velocity != null) { vy = velocity.Linear.Y; }
            }
            float target = TargetDrop(vy, config.MaxFallSpeed);
            Offset = Mathf.SmoothDamp(Offset, target, ref offsetVelocity, SmoothTime, Mathf.Infinity, deltaTime);
            applyPivot(Vector3.up * Offset);
        }

        //  계산만 떼어 테스트한다 — maxFall이 30이든(옛 값) 11.25든(미네 코어) 같은 비율로 맞는지
        //  EntityRegistry·Tick 없이 바로 확인할 수 있다.
        internal static float TargetDrop(float vy, float maxFall)
        {
            float start = StartRatio * maxFall;
            return -MaxDrop * Mathf.Clamp01((-vy - start) / (maxFall - start));
        }

        public void Dispose()
        {
            Offset = 0f;
            offsetVelocity = 0f;
            applyPivot(Vector3.zero);
        }
    }
}
