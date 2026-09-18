using UnityEngine;
using VContainer.Unity;

namespace LOP
{
    /// <summary>
    /// 매 프레임 카메라가 보는 방향을 조준으로 넘기고, <b>당기는 동안 시야를 좁히고 카메라
    /// 자체를 흔든다.</b> 좁아지는 만큼 어디에 무엇이 뜨는지 못 보게 되는 것이 당김의 대가고,
    /// 그림이 함께 흔들려야 손떨림이 화면 전체의 일로 느껴진다(조준선만 떨리면 어색하다).
    /// 당긴 정도는 여기서 따로 세지 않고 시뮬 상태(<see cref="ArcheryAim"/>)를 읽는다 —
    /// 화면이 같은 값을 두 번 세면 언젠가 갈라진다.
    /// </summary>
    public class ArcheryAimView : ILateTickable
    {
        private const float WideFov = 60f;

        //  더 세게 좁힌다(기존 32도) — 90m 과녁이 화면 높이의 2.4%로 너무 작게 보였다는 피드백.
        //  22도면 같은 거리에서 약 3.5%가 된다.
        private const float DrawnFov = 22f;

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
        private readonly GameFramework.Runner.IRunner runner;
        private readonly ArcheryConfig config;

        public ArcheryAimView(PlayerInputManager input,
                              CameraController cameraController, IPlayerContext playerContext,
                              GameFramework.World.EntityRegistry entityRegistry,
                              GameFramework.Runner.IRunner runner, ArcheryConfig config)
        {
            this.input = input;
            this.cameraController = cameraController;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.runner = runner;
            this.config = config;
        }

        public void LateTick()
        {
            var camera = cameraController.MainCamera;
            if (camera == null)
            {
                return;
            }

            //  카메라가 스스로 겨눈 각(흔들림을 더하기 전)을 읽는다 — 화면에 그려지는 각은 아래에서
            //  카메라 자체에 흔들림을 얹으므로, 거기서 읽으면 조준에도 흔들림이 실려 시뮬이 같은
            //  흔들림을 한 번 더 더하게 된다(이중 적용). 유니티의 x 회전은 양수가 아래를 본다.
            //  조준 각도는 양수가 위이므로 부호를 뒤집는다.
            float yaw = cameraController.Yaw;
            float pitch = -cameraController.Pitch;
            input.SetAim(yaw, pitch);

            var aim = MyAim();

            //  화면 자체를 흔든다 — 조준선만 떨리고 그림이 가만히 있으면 어색하다는 피드백.
            //  시뮬(ArcheryAimSystem.Tick)·조준가이드선과 같은 함수(ArcheryShake.Offset)를 불러야
            //  세 곳이 갈라지지 않는다.
            cameraController.AimSwayDegrees = SwayFor(aim);

            //  당길 때는 손가락을 바짝 따라가고, 풀릴 때만 천천히 — 당긴 정도는 바로 읽혀야 하지만
            //  놓은 뒤 되돌아가는 길은 급할 이유가 없다.
            float target = aim != null && aim.Drawing ? aim.DrawRatio : 0f;
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

        //  오래 당기고 있으면 실제로 쏠 화살도 흔들린다 — 당기지 않을 때는 0으로 돌려놓는다.
        //  안 그러면 쏜 뒤에도 화면이 기울어진 채 남는다.
        private Vector2 SwayFor(ArcheryAim aim)
        {
            if (aim == null || aim.Drawing == false)
            {
                return Vector2.zero;
            }

            float heldSeconds = CurrentHeldSeconds(aim);
            int phaseSeed = ArcheryShake.PhaseSeedOf(playerContext.entityId);
            Vector2 offset = ArcheryShake.Offset(heldSeconds, phaseSeed, config);

            //  offset.y는 조준 좌표계(위가 양수), AimSwayDegrees.y는 유니티 부호(아래가 양수) —
            //  그대로 넣으면 화면이 실제 흔들림과 반대로 기운다.
            return new Vector2(offset.x, -offset.y);
        }

        //  ArcheryAimGuideView.CurrentHeldSeconds와 같은 계산 — 화면은 정수 틱 사이도 물어보므로
        //  렌더 시각(소수 틱)을 쓴다.
        private float CurrentHeldSeconds(ArcheryAim aim)
        {
            if (runner?.tickUpdater == null)
            {
                return 0f;
            }
            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return 0f;
            }

            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            return ArcheryAimSystem.HeldSeconds(aim.DrawStartTick, renderTick, (float)interval);
        }

        //  당김은 시간이 아니라 손가락이 끈 거리가 정한다 — 시뮬 상태를 그대로 읽는다.
        //  화면이 같은 값을 따로 세면 언젠가 갈라진다.
        private ArcheryAim MyAim()
        {
            if (playerContext.entityId == null)
            {
                return null;
            }
            return entityRegistry.Get(playerContext.entityId)?.Get<ArcheryAim>();
        }
    }
}
