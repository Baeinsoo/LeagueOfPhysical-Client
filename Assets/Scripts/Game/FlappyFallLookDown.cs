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
        //  문턱은 날갯짓 속도 위에 둔다 — 날갯짓 한 주기는 날갯짓 속도(아래로)까지 떨어지고 다시 뜨므로,
        //  그보다 빨라야 "평소 비행이 아닌 낙하"다. 날갯짓~맥스 낙하 사이의 같은 몫(K)에서 켠다.
        //  옛 물리(날갯짓 18.6 · 맥스 낙하 30)에선 정확히 옛 문턱 22가 나온다. 맥스 낙하의 비율(22/30)로
        //  잡으면 미네 코어(10.125 · 11.25)에선 8.25가 되어 날갯짓 속도 밑이라 평소 비행에도 카메라가 출렁인다.
        private const float K = (22f - 18.6f) / (30f - 18.6f);
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
            float target = TargetDrop(vy, config.FlapImpulse, config.MaxFallSpeed);
            Offset = Mathf.SmoothDamp(Offset, target, ref offsetVelocity, SmoothTime, Mathf.Infinity, deltaTime);
            applyPivot(Vector3.up * Offset);
        }

        //  계산만 떼어 테스트한다 — 옛 물리든 미네 코어든 문턱이 날갯짓 속도 위에 서는지
        //  EntityRegistry·Tick 없이 바로 확인할 수 있다.
        internal static float TargetDrop(float vy, float flapImpulse, float maxFall)
        {
            float start = flapImpulse + K * (maxFall - flapImpulse);
            float full = maxFall;
            return -MaxDrop * Mathf.Clamp01((-vy - start) / (full - start));
        }

        public void Dispose()
        {
            Offset = 0f;
            offsetVelocity = 0f;
            applyPivot(Vector3.zero);
        }
    }
}
