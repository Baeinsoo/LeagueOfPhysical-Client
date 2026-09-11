using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 매 프레임 카메라가 보는 방향을 조준으로 넘기고, <b>당기는 동안 시야를 좁힌다.</b>
    /// 좁아지는 만큼 어디에 무엇이 뜨는지 못 보게 되는 것이 당김의 대가다.
    /// 당긴 정도는 여기서 따로 세지 않고 시뮬 상태(<see cref="ArcheryAim"/>)를 읽는다 —
    /// 화면이 같은 값을 두 번 세면 언젠가 갈라진다.
    /// </summary>
    public class ArcheryAimView : ILateTickable
    {
        private const float WideFov = 60f;
        private const float DrawnFov = 32f;
        private const float FovLerpPerSecond = 8f;

        private readonly GameFramework.Runner.IRunner runner;
        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public ArcheryAimView(GameFramework.Runner.IRunner runner, PlayerInputManager input,
                              CameraController cameraController, IPlayerContext playerContext,
                              GameFramework.World.EntityRegistry entityRegistry)
        {
            this.runner = runner;
            this.input = input;
            this.cameraController = cameraController;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
        }

        public void LateTick()
        {
            var camera = cameraController.MainCamera;
            if (camera == null)
            {
                return;
            }

            // 유니티의 x 회전은 양수가 아래를 본다. 조준 각도는 양수가 위이므로 부호를 뒤집는다.
            float yaw = camera.transform.eulerAngles.y;
            float pitch = -Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x);
            input.SetAim(yaw, pitch);

            camera.fieldOfView = Mathf.Lerp(
                camera.fieldOfView,
                Mathf.Lerp(WideFov, DrawnFov, MyDrawRatio()),
                Time.deltaTime * FovLerpPerSecond);
        }

        private float MyDrawRatio()
        {
            if (playerContext.entityId == null)
            {
                return 0f;
            }
            var aim = entityRegistry.Get(playerContext.entityId)?.Get<ArcheryAim>();
            if (aim == null || aim.Drawing == false)
            {
                return 0f;
            }

            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return 0f;
            }
            // 화면 시각은 정수 틱이 아니라 renderTick이다 — 시뮬과 같은 식에 같은 시각을 넣는다.
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            return ArcheryAimSystem.DrawRatio(aim.DrawStartTick, (long)System.Math.Floor(renderTick), (float)interval);
        }
    }
}
