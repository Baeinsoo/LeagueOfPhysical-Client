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
        //  날갯짓 뒤 0.6초면 −15 m/s에 닿아, 문턱을 거기 두면 평소 비행에서도 켜져 카메라가 출렁였다(2026-09-30).
        //  −22는 날갯짓 없이 0.7초 넘게 떨어져야 닿는다 — 절벽·샤프트 낙하에서만 켜진다.
        private const float StartSpeed = 22f;       // m/s — 이보다 빨리 떨어지면 내려다보기 시작
        private const float FullSpeed = 30f;        // m/s — 최대 낙하 속도, 여기서 MaxDrop
        private const float MaxDrop = 5f;           // m
        //  켜고 끄기 대신 속도에 비례한 목표를 부드럽게 따라간다 — 일정 속도로 출발·정지하면 덜컹인다.
        private const float SmoothTime = 0.3f;      // s

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private float offsetVelocity;

        internal System.Action<Vector3> applyPivot;
        internal float Offset { get; private set; }

        public FlappyFallLookDown(IPlayerContext playerContext, GameFramework.World.EntityRegistry entityRegistry,
                                  CameraController cameraController)
        {
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
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
            float target = -MaxDrop * Mathf.Clamp01((-vy - StartSpeed) / (FullSpeed - StartSpeed));
            Offset = Mathf.SmoothDamp(Offset, target, ref offsetVelocity, SmoothTime, Mathf.Infinity, deltaTime);
            applyPivot(Vector3.up * Offset);
        }

        public void Dispose()
        {
            Offset = 0f;
            offsetVelocity = 0f;
            applyPivot(Vector3.zero);
        }
    }
}
