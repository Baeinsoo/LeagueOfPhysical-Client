using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 카메라 중심을 통로에 고정한다 — 원조 Flappy처럼 플레이필드 높이가 일정하고, 새가 통로 안에서
    /// 오르내려도 구도는 그대로다. <see cref="FlappyFallLookDown"/>·<see cref="FlappyCameraDistance"/>와
    /// 같은 패턴 — SmoothDamp로 목표를 따라가야 맵을 찾는 순간 카메라가 덜컹이지 않는다.
    ///
    /// <para>표시(<see cref="FlappyCorridorLine"/>)가 없는 맵(전통 코스)도 있다 — 그동안은 아무것도
    /// 하지 않는다(<see cref="CameraController.PivotOffset"/>이 기본값 0에 머문다). 맵 로드가 늦을 수
    /// 있어 못 찾으면 1초에 한 번씩만 다시 찾는다 — 매 틱 <c>FindFirstObjectByType</c>를 돌리지 않는다.</para>
    /// </summary>
    public class FlappyCorridorCamera : ITickable, System.IDisposable
    {
        private const float SmoothTime = 0.12f;   // s
        private const float SearchInterval = 1f;   // s

        private readonly FlappySpectate spectate;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly FlappyConfig config;

        private FlappyCorridorLine line;
        private float searchCooldown;
        private float offsetVelocity;

        internal System.Action<Vector3> applyPivot;
        internal float Offset { get; private set; }

        public FlappyCorridorCamera(FlappySpectate spectate, GameFramework.World.EntityRegistry entityRegistry,
                                    CameraController cameraController, FlappyConfig config)
        {
            this.spectate = spectate;
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
            if (line == null)
            {
                searchCooldown -= deltaTime;
                if (searchCooldown > 0f)
                {
                    return;
                }
                searchCooldown = SearchInterval;
                line = Object.FindFirstObjectByType<FlappyCorridorLine>();
                if (line == null)
                {
                    return;
                }
            }

            string id = spectate.Current;
            if (string.IsNullOrEmpty(id))
            {
                return;
            }
            var transform = entityRegistry.Get(id)?.Get<GameFramework.World.Transform>();
            if (transform == null)
            {
                return;
            }

            float birdCenterY = transform.Position.Y + config.BodyRadius;
            float target = TargetOffset(line.CenterAt(transform.Position.X), birdCenterY);
            Offset = Mathf.SmoothDamp(Offset, target, ref offsetVelocity, SmoothTime, Mathf.Infinity, deltaTime);
            applyPivot(Vector3.up * Offset);
        }

        //  계산만 떼어 테스트한다 — 통로 중심과 새 몸 중심의 차가 곧 카메라 피벗이 따라갈 목표다.
        internal static float TargetOffset(float centerY, float birdCenterY) => centerY - birdCenterY;

        public void Dispose()
        {
            Offset = 0f;
            offsetVelocity = 0f;
            applyPivot(Vector3.zero);
        }
    }
}
