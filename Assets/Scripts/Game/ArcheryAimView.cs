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

        //  화면이 따라가는 당김이 한 프레임에 이만큼보다 빨리 변하지 않는다(초당 비율).
        //  쏘는 순간 시뮬은 당김을 0으로 **한 번에** 떨어뜨리는데, 그 계단을 화면이 그대로 따라가면
        //  화각이 32도에서 60도로 튄다. 여기서 한 번 매끈하게 만들면 줌이든 뭐든 이 값을 읽는
        //  모든 곳이 같이 부드러워진다 — 화면마다 따로 완충을 두지 않아도 된다.
        //  ⚠️ 시뮬 값(서버로 가는 힘)은 건드리지 않는다. 늦추면 빨리 끌었을 때 힘이 덜 실린다.
        private const float DrawRatioRisePerSecond = 6f;
        private const float DrawRatioFallPerSecond = 2.5f;

        private float shownDrawRatio;

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

            //  당길 때는 손가락을 바짝 따라가고, 풀릴 때만 천천히 — 당긴 정도는 바로 읽혀야 하지만
            //  놓은 뒤 되돌아가는 길은 급할 이유가 없다.
            float target = MyDrawRatio();
            float ratePerSecond = target > shownDrawRatio
                ? DrawRatioRisePerSecond
                : DrawRatioFallPerSecond;
            shownDrawRatio = Mathf.MoveTowards(
                shownDrawRatio, target, ratePerSecond * Time.deltaTime);

            camera.fieldOfView = Mathf.Lerp(
                camera.fieldOfView,
                Mathf.Lerp(WideFov, DrawnFov, shownDrawRatio),
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
