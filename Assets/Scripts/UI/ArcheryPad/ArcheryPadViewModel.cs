using UnityEngine;

namespace LOP.UI
{
    /// <summary>
    /// 조작 패드의 커맨드. 터치 좌표 해석은 View가 하고, 여기서는 그 결과를 시점과 입력으로 넘긴다.
    /// <b>조준은 카메라가 보는 방향</b>이라 각도를 여기서 들고 있지 않다 — 매 프레임 카메라에서
    /// 읽어 입력에 싣는 일은 <see cref="LOP.ArcheryAimView"/>가 한다.
    /// </summary>
    public class ArcheryPadViewModel
    {
        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly IPlayerContext playerContext;
        private readonly ArcheryConfig config;
        private readonly ArcheryCourse course;
        private readonly ArcheryConsumed consumed;
        private readonly ArcheryWorld world;
        private readonly GameFramework.Runner.IRunner runner;

        //  매 프레임 새로 만들지 않고 돌려 쓴다 — course.Fill이 채워 준다.
        private readonly System.Collections.Generic.List<ArcheryTarget> targetBuffer =
            new System.Collections.Generic.List<ArcheryTarget>();

        //  겨누는 속도(도/초). 아래 화각을 기준으로 정한 값이고, 당겨서 화면이 좁아지면 그
        //  비율만큼 같이 줄어든다 — 그래야 손동작 하나가 **화면 위에서** 늘 같은 거리를 움직인다.
        //  (저격 게임의 "줌 감도 보정"과 같은 것. 안 하면 줌인할수록 손이 미쳐 날뛴다.)
        private const float ReferenceFov = ArcheryAimView.WideFov;

        //  톡 쳤을 때의 속도. 당긴 상태(화각 22도)에서 약 2.2도/초가 되는데, 90m 과녁의 10점
        //  링(0.25도)을 건너는 데 7프레임쯤 걸리는 속도다 — 미세조정이 되는 하한선.
        private const float FineDegreesPerSecond = 6f;

        //  계속 누르고 있을 때 도달하는 속도. 당긴 상태에서 약 22도/초 = 1초에 화면 하나.
        private const float MaxDegreesPerSecond = 60f;

        //  누르고 있으면 이 시간에 걸쳐 Fine에서 Max까지 오르고, 떼면 **즉시** Fine으로 돌아간다.
        //  키보드는 세기를 못 주므로 "짧게 톡 치면 느리게, 길게 누르면 빠르게"로 세기를 만든다.
        //  떼자마자 되돌리는 것이 핵심 — 안 그러면 연타할수록 점점 빨라져 미세조정이 사라진다.
        private const float LookRampSeconds = 0.45f;

        //  한 프레임이 크게 밀렸을 때(씬 로드·GC) 그 시간만큼을 한 번에 돌리면 화면이 툭 튄다.
        //  속도를 쌓던 옛 방식은 감쇠가 그걸 눌러 줬지만 지금은 곧장 각도로 가므로 여기서 막는다.
        //  20fps에 해당하는 값 — 이보다 느린 프레임은 "그만큼 돌았다" 치지 않는다.
        private const float MaxLookDeltaSeconds = 0.05f;

        //  지금 얼마나 오래 누르고 있나(0~1).
        private float lookRamp;

        private bool drawing;

        public ArcheryPadViewModel(
            PlayerInputManager input,
            CameraController cameraController,
            GameFramework.World.EntityRegistry entityRegistry,
            IPlayerContext playerContext,
            ArcheryConfig config,
            ArcheryCourse course,
            ArcheryConsumed consumed,
            ArcheryWorld world,
            GameFramework.Runner.IRunner runner)
        {
            this.input = input;
            this.cameraController = cameraController;
            this.entityRegistry = entityRegistry;
            this.playerContext = playerContext;
            this.config = config;
            this.course = course;
            this.consumed = consumed;
            this.world = world;
            this.runner = runner;
        }

        // ── 활 조준기(사이트) ───────────────────────────────────────────────────────
        //
        //  실제 양궁이 이 문제를 푸는 방식이다. 90m 과녁은 6도를 올려 쏴야 하는데, 기준이
        //  십자선 하나뿐이면 **빈 하늘을 겨누고** 과녁은 화면 한참 아래에 남는다. 거리별 핀을
        //  두면 "핀을 과녁에 얹는" 일이 되어 겨누는 자리에 과녁이 있다.
        //
        //  조준선(삭제됨)과 다른 점: 핀은 **거리별 기준선**일 뿐 착탄점이 아니다. 어느 핀을
        //  쓸지 고르는 것도, 흔들리는 화면에서 그 핀을 10점에 붙들고 있는 것도 쏘는 사람 몫이다.

        /// <summary>조준기에 핀을 놓을 거리들(m). 사거리 맵이 아니면 비어 있다.</summary>
        public System.Collections.Generic.IReadOnlyList<ArcheryRangeStand> SightStands =>
            config?.Range?.Stands ?? (System.Collections.Generic.IReadOnlyList<ArcheryRangeStand>)
                System.Array.Empty<ArcheryRangeStand>();

        /// <summary>
        /// 이 거리의 과녁을 맞히려면 <b>십자선보다 몇 도 아래</b>에 과녁을 두어야 하는가.
        /// 지금 당긴 정도(=화살 속도)로 계산하므로 당길수록 핀이 제자리를 찾는다. 못 닿으면 NaN.
        /// </summary>
        public float SightPinDegrees(float distanceMeters)
        {
            float speed = ArcheryAimSystem.SpeedFor(SimDrawRatio);
            return ArcheryTrajectory.LaunchPitchDegrees(distanceMeters, TargetHeightOffset(), speed);
        }

        //  화면이 아니라 **시뮬이 들고 있는** 당김을 쓴다. 쏘는 힘을 정하는 것이 그 값이라,
        //  화면이 제 손가락 값을 쓰면 핀이 실제 화살과 다른 속도를 그린다.
        private float SimDrawRatio
        {
            get
            {
                var entity = entityRegistry.Get(playerContext.entityId);
                return entity?.Get<ArcheryAim>()?.DrawRatio ?? 0f;
            }
        }

        //  과녁 중심이 화살 떠나는 높이보다 얼마나 위/아래에 있나(m). 사거리 맵은 과녁이
        //  1.30m, 눈높이가 1.40m라 10cm 아래인데, 그 10cm가 45m에서 10점 링 반지름의 절반을
        //  먹는다 — 무시하면 잘 쏴도 계속 조금 높게 맞는다. 값을 박지 않고 지금 서 있는
        //  과녁에서 직접 잰다(레인 높이가 바뀌어도 따라온다).
        private float TargetHeightOffset()
        {
            var entity = entityRegistry.Get(playerContext.entityId);
            if (entity == null || course == null || world == null || runner?.tickUpdater == null)
            {
                return 0f;
            }

            double interval = runner.tickUpdater.interval;
            if (interval <= 0d)
            {
                return 0f;
            }

            double renderTick = (runner.tickUpdater.elapsedTime - interval) / interval;
            int step = course.IndexAt((long)System.Math.Floor(renderTick), world.GameplayStartTick);
            if (step < 0 || (course.StepCount != 0 && step >= course.StepCount))
            {
                return 0f;
            }

            targetBuffer.Clear();
            course.Fill(targetBuffer, step, world.GameplayStartTick);
            for (int i = 0; i < targetBuffer.Count; i++)
            {
                if (targetBuffer[i].OwnerUserId != playerContext.entityId)
                {
                    continue;
                }
                if (consumed != null && consumed.IsTargetGone(targetBuffer[i].WaveIndex, targetBuffer[i].SlotIndex))
                {
                    continue;
                }

                float eyeY = entity.Get<GameFramework.World.Transform>().Position.Y + ArcheryAimSystem.EyeHeight;
                return targetBuffer[i].Origin.y - eyeY;
            }

            return 0f;   // 지금 내 과녁이 없다 — 수평으로 친다
        }

        /// <summary>내가 지금까지 모은 점수. 서버 스냅샷이 채우는 값이라 <b>매 프레임 읽어</b> 쓴다.</summary>
        public int Score
        {
            get
            {
                var entity = entityRegistry.Get(playerContext.entityId);
                return entity?.Get<ArcheryScore>()?.Value ?? 0;
            }
        }

        /// <summary>남은 화살. <b>−1이면 무제한</b>(원형 맵)이라 화면이 아예 안 띄운다.</summary>
        public int ArrowsLeft
        {
            get
            {
                var entity = entityRegistry.Get(playerContext.entityId);
                var quiver = entity?.Get<ArcheryQuiver>();
                return quiver?.Remaining ?? -1;
            }
        }

        /// <summary>지금 시위를 당기고 있나. 조준점을 띄울지 정한다 — 시뮬 상태를 읽는다(화면이 따로 세지 않는다).</summary>
        public bool Drawing
        {
            get
            {
                var entity = entityRegistry.Get(playerContext.entityId);
                return entity?.Get<ArcheryAim>()?.Drawing ?? false;
            }
        }

        /// <summary>
        /// 왼쪽 영역 드래그 — 시점을 돌린다. 받는 값은 <b>화면 크기 대비 비율</b>이다(픽셀이 아니다).
        /// x는 가로 폭 대비, y는 세로 높이 대비. 한 화면만큼 끌면 딱 한 화면만큼 돈다 —
        /// 손가락 밑의 그림이 손가락을 따라온다.
        /// </summary>
        public void LookBy(Vector2 deltaFraction)
        {
            //  가로와 세로는 화각이 다르다(가로 화각 = 세로 화각을 화면 비율로 늘린 것). 한 값으로
            //  묶으면 가로로 끌 때만 어긋나는데, 가로로 긴 화면에서는 그 차이가 30%까지 벌어진다.
            AimBy(new Vector2(deltaFraction.x * HorizontalFov, deltaFraction.y * CurrentFov));
        }

        /// <summary>
        /// 데스크톱 편의: WASD(방향키도 같이)로 시점을 돌린다. 마우스가 하나뿐인 PC에서는 왼쪽 드래그와
        /// 오른쪽 당김을 동시에 할 수 없어서, 조준을 키보드로 옮겨 마우스를 당김 전용으로 비운다.
        /// 이 게임엔 이동이 없어 WASD가 비어 있다. 매 프레임 호출된다.
        /// 선례: <see cref="SkydivePadViewModel"/>의 같은 이름 메서드.
        /// </summary>
        public void PollKeyboard()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
            {
                lookRamp = 0f;
                return;   // 키보드가 없는 기기(모바일)
            }

            var look = Vector2.zero;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) { look.x += 1f; }
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) { look.x -= 1f; }
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) { look.y += 1f; }
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) { look.y -= 1f; }

            if (look == Vector2.zero)
            {
                lookRamp = 0f;
                return;
            }

            float deltaSeconds = Mathf.Min(Time.deltaTime, MaxLookDeltaSeconds);
            lookRamp = Mathf.Min(1f, lookRamp + deltaSeconds / LookRampSeconds);
            float degreesPerSecond = Mathf.Lerp(FineDegreesPerSecond, MaxDegreesPerSecond, lookRamp);
            AimBy(look.normalized * (degreesPerSecond * FovScale * deltaSeconds));
        }

        /// <summary>조준기 핀을 픽셀로 옮길 때 화각이 필요해서 화면이 카메라를 본다.</summary>
        public Camera Camera => cameraController.MainCamera;

        /// <summary>지금 화각(도). 당길수록 좁아진다 — 카메라가 없으면 기준값으로 친다.</summary>
        private float CurrentFov
        {
            get
            {
                var camera = cameraController.MainCamera;
                return camera != null ? camera.fieldOfView : ReferenceFov;
            }
        }

        /// <summary>가로 화각(도). 세로 화각을 화면 가로세로 비율로 늘린 값이다.</summary>
        private float HorizontalFov
        {
            get
            {
                var camera = cameraController.MainCamera;
                if (camera == null)
                {
                    return ReferenceFov;
                }
                float halfVertical = camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
                return 2f * Mathf.Atan(Mathf.Tan(halfVertical) * camera.aspect) * Mathf.Rad2Deg;
            }
        }

        private float FovScale => CurrentFov / ReferenceFov;

        //  속도를 쌓지 않는 경로로 넣는다 — 부른 만큼만 돌고 떼면 그 자리에 선다.
        private void AimBy(Vector2 degrees)
        {
            cameraController.AimBy(degrees);
        }

        /// <summary>
        /// 이 거리만큼 끌면 완전히 당긴 것이다(화면 짧은 변 대비 비율). 엄지로 한 번에 끌 수 있는
        /// 만큼으로 잡는다 — 손을 옮겨 짚어야 하는 거리면 조작이 아니라 곡예가 된다.
        /// </summary>
        private const float FullDrawDragFraction = 0.22f;

        private Vector2 drawOrigin;
        private Vector2 drawCurrent;

        /// <summary>지금 끌고 있는 정도(0~1). 화면이 시위와 조준선을 이 값으로 그린다.</summary>
        public float DrawRatio { get; private set; }

        /// <summary>임계치를 넘겨 시위가 실제로 걸렸나. 못 넘으면 떼도 안 쏜다.</summary>
        public bool DrawArmed => DrawRatio >= ArcheryAimSystem.DrawThreshold;

        /// <summary>손가락을 처음 댄 자리(화면 좌표). 게이지가 여기 그려진다.</summary>
        public Vector2 DrawOrigin => drawOrigin;

        /// <summary>지금 손가락 자리(화면 좌표).</summary>
        public Vector2 DrawCurrent => drawCurrent;

        /// <summary>완전히 당기는 데 필요한 거리(픽셀). 게이지 바깥 원의 반지름이다.</summary>
        public float FullDrawPixels => Mathf.Min(Screen.width, Screen.height) * FullDrawDragFraction;

        /// <summary>손가락을 댄 자리를 기억한다. 여기서부터 끈 거리가 곧 당김이다.</summary>
        public void BeginDraw(Vector2 position)
        {
            drawing = true;
            drawOrigin = position;
            drawCurrent = position;
            DrawRatio = 0f;
            input.SetDrawing(true);
            input.SetDrawRatio(0f);
        }

        /// <summary>끌고 있는 동안 매번. 댄 자리에서 멀어진 만큼이 당김이다.</summary>
        public void DragDraw(Vector2 position)
        {
            if (drawing == false)
            {
                return;
            }
            drawCurrent = position;
            //  화면 짧은 변으로 나눈다 — 해상도가 달라도 같은 손동작이면 같은 값이 된다.
            float unit = FullDrawPixels;
            DrawRatio = unit > 0f ? Mathf.Clamp01(Vector2.Distance(position, drawOrigin) / unit) : 0f;
            input.SetDrawRatio(DrawRatio);
        }

        /// <summary>두 번 불려도 한 번만 쏜다 — 아래 View가 뗌을 두 경로로 받기 때문이다.</summary>
        public void EndDraw()
        {
            if (drawing == false)
            {
                return;
            }
            drawing = false;
            input.SetDrawing(false);
            //  임계치를 못 넘었으면 취소다 — 쏘라는 신호를 아예 안 보낸다.
            if (DrawRatio >= ArcheryAimSystem.DrawThreshold)
            {
                input.SetRelease();
            }
            DrawRatio = 0f;
        }
    }
}
