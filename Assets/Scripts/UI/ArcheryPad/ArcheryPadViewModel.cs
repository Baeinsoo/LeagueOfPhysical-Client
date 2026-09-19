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
            IPlayerContext playerContext)
        {
            this.input = input;
            this.cameraController = cameraController;
            this.entityRegistry = entityRegistry;
            this.playerContext = playerContext;
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
        /// 손가락 드래그 — 시점을 돌린다. 화면 전체가 조작면이라 왼쪽/오른쪽 영역 구분은 없다 —
        /// 끈 만큼 시점을 돌린다. 받는 값은 <b>화면 크기 대비 비율</b>이고,
        /// <b>x는 양수가 오른쪽, y는 양수가 위</b>다 — 패널 좌표(아래가 +y)와 부호가 반대이니
        /// 넘기는 쪽에서 뒤집어 줘야 한다. 두 축이 같은 관습을 써야 조작이 성립한다.
        /// (픽셀이 아니다). x는 가로 폭 대비, y는 세로 높이 대비. 한 화면만큼 끌면 딱 한 화면만큼
        /// 돈다 — 손가락 밑의 그림이 손가락을 따라온다.
        /// </summary>
        public void LookBy(Vector2 deltaFraction)
        {
            //  가로와 세로는 화각이 다르다(가로 화각 = 세로 화각을 화면 비율로 늘린 것). 한 값으로
            //  묶으면 가로로 끌 때만 어긋나는데, 가로로 긴 화면에서는 그 차이가 30%까지 벌어진다.
            AimBy(new Vector2(deltaFraction.x * HorizontalFov, deltaFraction.y * CurrentFov));
        }

        /// <summary>
        /// 데스크톱 편의: WASD(방향키도 같이)로 시점을 돌린다. 손가락(마우스) 하나로 화면을
        /// 누르면 활도 함께 당겨지므로, 화살을 메기지 않고 시점만 둘러보고 싶을 때 WASD를 쓴다.
        /// 이 게임엔 이동이 없어 WASD가 비어 있어 이 용도로 쓸 수 있다. 매 프레임 호출된다.
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
        /// 화면 아래 이만큼이 <b>내려놓기</b> 자리다. 여기서 떼면 화살이 안 나간다.
        ///
        /// <para>손가락 하나가 당김과 조준을 다 하므로 "떼기"는 발사일 수밖에 없다 — 취소는
        /// 따로 제스처를 줘야 한다. 젤다는 드는 버튼과 쏘는 버튼이 달라 취소가 공짜지만,
        /// 손가락 하나로 옮기면 그게 안 된다.</para>
        /// </summary>
        public const float LowerBandFraction = 0.15f;

        /// <summary>지금 내려놓기 자리에 손가락이 있나. 화면이 띠를 붉게 켜는 데 쓴다.</summary>
        public bool Lowering { get; private set; }

        /// <summary>
        /// 지금 얼마나 당겨졌나(0~1). <b>시뮬 값을 읽는다</b> — 화면이 따로 세면 클라와 서버가
        /// 다른 값을 보게 된다. 화면이 하는 일은 "잡고 있다"고 말하는 것뿐이다.
        /// </summary>
        public float DrawRatio
        {
            get
            {
                var entity = entityRegistry.Get(playerContext.entityId);
                return entity?.Get<ArcheryAim>()?.DrawRatio ?? 0f;
            }
        }

        /// <summary>임계치를 넘겨 시위가 걸렸나. 못 넘으면 떼도 안 쏜다.</summary>
        public bool DrawArmed => DrawRatio >= ArcheryAimSystem.DrawThreshold;

        //  활을 들기 직전에 보던 곳. 내려놓으면 여기로 돌아온다 — 아래 UpdateAim 참고.
        private float aimAtPressYaw;
        private float aimAtPressPitch;

        //  돌아가는 데 걸리는 시간(초). 활을 내리는 동안 화각도 22°→60°로 넓어지므로
        //  그 속도와 얼추 맞춰야 화면이 따로 노는 느낌이 안 난다.
        private const float ReturnSeconds = 0.18f;

        /// <summary>손가락을 댔다 — 활이 올라오기 시작한다. 얼마나 올라오는지는 시뮬이 정한다.</summary>
        public void BeginDraw()
        {
            drawing = true;
            Lowering = false;
            aimAtPressYaw = cameraController.Yaw;
            aimAtPressPitch = cameraController.Pitch;
            input.SetDrawing(true);
            input.SetDrawRatio(1f);
        }

        /// <summary>
        /// 손가락이 움직였다 — 내려놓기 자리에 있는지만 갱신한다(겨누기는 <see cref="LookBy"/>).
        /// </summary>
        public void UpdatePointer(Vector2 positionFraction)
        {
            if (drawing == false)
            {
                return;
            }

            Lowering = positionFraction.y > (1f - LowerBandFraction);

            //  내리는 동안에도 Drawing은 **true로 둔다**. false로 내리면 다시 올릴 때
            //  DrawStartTick이 새로 찍혀 흔들림 피로가 초기화된다 — 띠에 담갔다 빼는 것이
            //  이득이 되면 안 된다. 목표만 0으로 낮춰 활이 내려가게 한다.
            input.SetDrawRatio(Lowering ? 0f : 1f);
        }

        /// <summary>
        /// 내려놓는 동안 조준을 <b>활을 들기 직전 자리로</b> 되돌린다. 매 프레임 불린다.
        ///
        /// <para><b>왜 필요한가</b>: 취소하려면 손가락을 띠(화면 아래 15%)까지 내려야 하는데,
        /// 그 손가락이 곧 조준이라 내려가는 동안 조준도 같이 끌려 내려간다. 화면 가운데쯤에서
        /// 시작하면 화면 높이의 35%를 끄는 셈이고, 당긴 상태(화각 22°)에서 그건 <b>아래로 7.7°</b>다
        /// — 90m 홀드오버 전체가 1.1°이니 취소 한 번에 조준이 통째로 날아간다. 특히 먼 과녁일수록
        /// 위를 겨누고 있어 손해가 크다.</para>
        ///
        /// <para>활을 내려놓으면 시야도 원래대로 돌아가는 것이 물리적으로도 맞다. 되돌아가는
        /// 동안 <see cref="LookBy"/>는 화면 쪽에서 막는다 — 안 막으면 손가락이 띠 안에서
        /// 꿈틀댈 때마다 되돌아가는 것과 싸운다.</para>
        /// </summary>
        public void UpdateAim(float deltaSeconds)
        {
            if (drawing == false || Lowering == false || deltaSeconds <= 0f)
            {
                return;
            }

            //  남은 거리에 비례해 줄어드는 감쇠 — 끝에서 뚝 끊기지 않는다.
            float k = 1f - Mathf.Exp(-deltaSeconds / ReturnSeconds);

            //  부호 변환과 최단 방향 계산은 CameraController.StepToward 한 곳에 있다
            //  (여기서 다시 하면 반드시 한 번은 틀린다 — 시험이 거기를 지킨다).
            AimBy(CameraController.StepToward(
                cameraController.Yaw, cameraController.Pitch,
                aimAtPressYaw, aimAtPressPitch, k));
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

            //  내려놓은 채로 뗐거나 시위가 안 걸렸으면 쏘라는 신호를 아예 안 보낸다.
            if (Lowering == false && DrawArmed)
            {
                input.SetRelease();
            }
            Lowering = false;
        }
    }
}
