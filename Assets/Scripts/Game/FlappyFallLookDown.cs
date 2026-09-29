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
        private const float FallThreshold = -15f;   // m/s — 이보다 빨리 떨어질 때만
        private const float MaxDrop = 5f;           // m
        private const float ShiftSpeed = 10f;       // m/s — 카메라 중심이 옮겨 가는 빠르기

        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

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
            float target = vy < FallThreshold ? -MaxDrop : 0f;
            Offset = Mathf.MoveTowards(Offset, target, ShiftSpeed * deltaTime);
            applyPivot(Vector3.up * Offset);
        }

        public void Dispose()
        {
            Offset = 0f;
            applyPivot(Vector3.zero);
        }
    }
}
