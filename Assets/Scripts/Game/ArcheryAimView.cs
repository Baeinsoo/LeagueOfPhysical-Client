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

        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;

        public ArcheryAimView(PlayerInputManager input,
                              CameraController cameraController, IPlayerContext playerContext,
                              GameFramework.World.EntityRegistry entityRegistry)
        {
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

            //  여기서 읽는 회전은 카메라가 아직 이번 프레임 것을 쓰기 전이라 직전 프레임,
            //  곧 지금 화면에 보이는 바로 그 방향이다. 보이는 것과 화살 가는 곳이 늘 맞는 이유가
            //  이것이다 — 우리가 계산한 값이 아니라 플레이어가 실제로 보고 쏜 방향을 보낸다.
            // 유니티의 x 회전은 양수가 아래를 본다. 조준 각도는 양수가 위이므로 부호를 뒤집는다.
            float yaw = camera.transform.eulerAngles.y;
            float pitch = -Mathf.DeltaAngle(0f, camera.transform.eulerAngles.x);
            input.SetAim(yaw, pitch);

            camera.fieldOfView = Mathf.Lerp(
                camera.fieldOfView,
                Mathf.Lerp(WideFov, DrawnFov, MyDrawRatio()),
                Time.deltaTime * FovLerpPerSecond);
        }


        //  당김은 시간이 아니라 손가락이 끈 거리가 정한다 — 시뮬 상태를 그대로 읽는다.
        //  화면이 같은 값을 따로 세면 언젠가 갈라진다.
        private float MyDrawRatio()
        {
            if (playerContext.entityId == null)
            {
                return 0f;
            }
            var aim = entityRegistry.Get(playerContext.entityId)?.Get<ArcheryAim>();
            return aim != null && aim.Drawing ? aim.DrawRatio : 0f;
        }
    }
}
