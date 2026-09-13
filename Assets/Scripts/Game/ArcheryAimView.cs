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
        private readonly ArcheryConfig config;

        public ArcheryAimView(GameFramework.Runner.IRunner runner, PlayerInputManager input,
                              CameraController cameraController, IPlayerContext playerContext,
                              GameFramework.World.EntityRegistry entityRegistry,
                              ArcheryConfig config)
        {
            this.runner = runner;
            this.input = input;
            this.cameraController = cameraController;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.config = config;
        }

        public void LateTick()
        {
            var camera = cameraController.MainCamera;
            if (camera == null)
            {
                return;
            }

            //  여기서 정한 흔들림을 카메라가 실제로 돌리는 건 다음 프레임이다 — CameraController는
            //  [DefaultExecutionOrder(3000)]의 LateUpdate이고 이 ILateTickable은 다른 player-loop
            //  단계에서 도는 VContainer 훅이라, 이번 프레임에 쓴 값을 카메라가 아직 못 읽는다.
            //  그래도 어긋나지 않는 이유는 아래 input.SetAim이 "지금 계산한 값"이 아니라
            //  "카메라가 실제로 화면에 그려낸 회전"(camera.transform.eulerAngles)을 읽기 때문이다 —
            //  흔들림이 한 프레임 늦게 반영되어도, 화면에 보이는 방향과 화살이 날아갈 방향은
            //  항상 같은 값에서 나온다.
            cameraController.AimSwayDegrees = SwayDegrees();

            // 유니티의 x 회전은 양수가 아래를 본다. 조준 각도는 양수가 위이므로 부호를 뒤집는다.
            float yaw = camera.transform.eulerAngles.y;
            float pitch = -Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x);
            input.SetAim(yaw, pitch);

            camera.fieldOfView = Mathf.Lerp(
                camera.fieldOfView,
                Mathf.Lerp(WideFov, DrawnFov, MyDrawRatio()),
                Time.deltaTime * FovLerpPerSecond);
        }

        private Vector2 SwayDegrees()
        {
            if (playerContext.entityId == null)
            {
                return Vector2.zero;
            }
            var aim = entityRegistry.Get(playerContext.entityId)?.Get<ArcheryAim>();
            if (aim == null || aim.Drawing == false)
            {
                return Vector2.zero;   // 안 당기고 있으면 안 흔들린다
            }
            if (runner?.tickUpdater == null)
            {
                return Vector2.zero;
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return Vector2.zero;
            }

            //  화면 시각은 정수 틱이 아니라 renderTick이다 — 시뮬과 같은 식에 같은 시각을 넣는다.
            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            float held = ArcheryAimSystem.HeldSeconds(aim.DrawStartTick, renderTick, (float)interval);

            var offset = ArcheryShake.Offset(held, ArcheryShake.PhaseSeedOf(playerContext.entityId), config);
            //  조준 좌표계는 위가 양수, 유니티 x 회전은 아래가 양수다 — 위아래를 뒤집어 넘긴다.
            return new Vector2(offset.x, -offset.y);
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

            if (runner?.tickUpdater == null)
            {
                return 0f;   // 씬 진입 초기거나 언로드 도중 — 러너가 아직/더 이상 안 물려 있다
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
