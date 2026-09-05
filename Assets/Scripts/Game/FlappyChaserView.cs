using System;
using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 추격자를 화면에 세운다. 위치는 시계만 보면 나오므로(<see cref="FlappyChaserCurve"/>)
    /// 서버에서 받는 것이 없다.
    ///
    /// <para><b>어느 시각으로 그릴지</b>가 이 클래스의 핵심이다. 화면엔 시간대가 둘이다 —
    /// 내 새는 예측이라 조금 앞, 남의 새는 지연 보간이라 조금 뒤. 벽은 하나뿐이라 둘 다 맞출 수
    /// 없어서 <b>지금 보고 있는 새</b>의 시각(<see cref="EntityRenderClock"/>)에 맞춘다.
    /// 대가로, 내가 살아 있는 동안 남이 잡히면 그 새가 벽 안으로 3m쯤 들어간 뒤에 사라진다.</para>
    ///
    /// <para>씬에 미리 놓을 대상이 아니고 프레임마다 트랜스폼 하나만 옮기므로 MonoBehaviour가
    /// 아니라 진입점이다. 카메라가 LateUpdate에서 움직이므로 그 뒤에 읽는다.</para>
    /// </summary>
    public class FlappyChaserView : ILateTickable, IDisposable
    {
        private const float WallHeight = 300f;
        private const float WallThickness = 2f;

        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly GameFramework.World.IWorld world;
        private readonly EntityRenderClock renderClock;
        private readonly IPlayerContext playerContext;
        private readonly CameraController cameraController;
        private readonly FinishLineBounds finishLine;
        private readonly FlappyConfig config;

        private GameObject wall;

        /// <summary>지금 그려진 벽의 x. HUD가 이 값을 읽어야 숫자와 그림이 어긋나지 않는다.</summary>
        public float X { get; private set; }

        public FlappyChaserView(GameFramework.World.EntityRegistry entityRegistry,
                                GameFramework.World.IWorld world,
                                EntityRenderClock renderClock,
                                IPlayerContext playerContext,
                                CameraController cameraController,
                                FinishLineBounds finishLine,
                                FlappyConfig config)
        {
            this.entityRegistry = entityRegistry;
            this.world = world;
            this.renderClock = renderClock;
            this.playerContext = playerContext;
            this.cameraController = cameraController;
            this.finishLine = finishLine;
            this.config = config;
        }

        public void LateTick()
        {
            EnsureWall();

            //  벽은 결승선에서 멈춘다 — 서버의 잡는 판정과 같은 상한을 써야 화면이 맞는다.
            float stopAtX = finishLine.TryGet(out var bounds) ? bounds.min.x : float.MaxValue;
            X = FlappyChaserCurve.XAt(
                config, ElapsedSeconds(FlappyWatchTarget.Resolve(entityRegistry, playerContext.entityId)),
                stopAtX);

            Vector3 position = wall.transform.position;
            position.x = X;
            //  세로는 카메라를 따라간다 — 고도가 변하는 맵에서도 화면 세로를 늘 덮게.
            if (cameraController.MainCamera != null)
            {
                position.y = cameraController.MainCamera.transform.position.y;
            }
            wall.transform.position = position;
        }

        private float ElapsedSeconds(string watchedEntityId)
        {
            double secondsPerTick = renderClock.SecondsPerTick;
            if (secondsPerTick <= 0d || world.GameplayStartTick == long.MaxValue)
            {
                return 0f;   // 아직 출발 정보가 없다 — 벽은 시작점에 서 있는다
            }
            //  틱이 아니라 연속 시각으로 묻는다. 틱으로 자르면 벽이 0.02초 단위로만 움직여
            //  0.2m씩 점프하고, 60fps 화면에서는 여섯 프레임 중 하나가 제자리에 선다.
            return (float)(renderClock.TimeFor(watchedEntityId) - world.GameplayStartTick * secondsPerTick);
        }

        private void EnsureWall()
        {
            if (wall != null)
            {
                return;
            }

            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "FlappyChaserWall";

            //  물리 몸을 주면 새를 밀어 클·서 시뮬이 갈린다. 판정은 서버의 x 비교뿐이다.
            var collider = wall.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.Destroy(collider);
            }

            wall.transform.localScale = new Vector3(WallThickness, WallHeight, WallThickness);

            //  추격자의 정체는 아트 단계 몫이라 지금은 붉은 판으로 자리만 잡는다.
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                wall.GetComponent<MeshRenderer>().sharedMaterial =
                    new Material(shader) { color = new Color(0.85f, 0.15f, 0.15f) };
            }
        }

        public void Dispose()
        {
            if (wall != null)
            {
                UnityEngine.Object.Destroy(wall);
                wall = null;
            }
        }
    }
}
