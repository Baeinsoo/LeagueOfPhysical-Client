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
        private readonly ArcheryImpactLog impactLog;
        private readonly ArcheryConfig config;

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


        public ArcheryPadViewModel(
            PlayerInputManager input,
            CameraController cameraController,
            GameFramework.World.EntityRegistry entityRegistry,
            IPlayerContext playerContext,
            ArcheryImpactLog impactLog,
            ArcheryConfig config)
        {
            this.input = input;
            this.cameraController = cameraController;
            this.entityRegistry = entityRegistry;
            this.playerContext = playerContext;
            this.impactLog = impactLog;
            this.config = config;
        }

        /// <summary>지금 자리에서 내가 쏜 것들. 화면이 기록판에 점으로 찍는다.</summary>
        public System.Collections.Generic.IReadOnlyList<ArcheryImpactLog.Shot> Impacts => impactLog.Shots;

        /// <summary>기록판이 모으고 있는 자리 번호. 바뀌면 화면이 점을 다시 그린다.</summary>
        public int ImpactWave => impactLog.Wave;

        /// <summary>
        /// 기록판에 그릴 점수 띠 — 바깥 비율과 점수의 짝, 중심에서 바깥 순서다.
        /// <b>과녁을 그리는 값과 같은 데이터</b>에서 나오므로 그림과 실제 채점이 갈라질 수 없다.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<ArcheryRingBand> Bands => config.Range.Kind.Bands;

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

        //  ── 내려놓기 자리 ────────────────────────────────────────────────
        //
        //  손가락 하나가 당김과 조준을 다 하므로 "떼기"는 발사일 수밖에 없다. 그래서 취소는
        //  따로 제스처를 줘야 하는데, 남은 재료가 움직임뿐이고 그 움직임은 이미 조준이 쓴다.
        //
        //  그래서 취소 자리를 **조준이 실제로 쓰지 않는 곳**에 둔다. 당긴 상태(화각 22°)에서
        //  조준이 도는 거리는 홀드오버 1.12° + 미세조정 ±3° 정도 = 화면 세로의 14% 남짓이다.
        //  원의 안쪽 가장자리가 20%라 그 밖이다.
        //
        //  자리는 **화면 아래가 아니라 누른 자리 기준**이다 — 그래야 엄지가 갈 거리가 늘 같다.
        //  (화면 아래 띠였을 땐 위에서 누르면 35mm, 아래에서 누르면 0mm였다.)

        /// <summary>누른 자리에서 아래로 이만큼 떨어진 곳이 내려놓기 자리다(화면 세로를 1로 본 값).
        /// 6.1인치 가로 기준 약 20mm — 엄지가 한 번 접으면 닿는 거리다.</summary>
        public const float CancelBelowPressInHeights = 0.31f;

        /// <summary>
        /// 내려놓기 원의 반지름(화면 세로를 1로 본 값). 폰에서 약 5mm = <b>지름 10mm</b>.
        ///
        /// <para>⚠️ 화면 <b>비율</b>이라 큰 화면에서는 그만큼 커 보인다 — 에디터 Game 뷰에서
        /// 터무니없이 크게 보이는 건 정상이고, 폰에서는 엄지만 하다. 비율로 잡는 이유는
        /// <b>정확성이 비율에 달려 있기 때문</b>이다(위 14% 계산은 각도라 화면 비율이다).
        /// 손가락 크기는 화면 크기에 안 비례하므로, 줄이더라도 폰 기준 <b>지름 7~8mm</b>가
        /// 바닥이다 — 애플 44pt·구글 48dp가 그쯤이고 그 아래로는 보지 않고 못 누른다.</para>
        /// </summary>
        public const float CancelRadiusInHeights = 0.075f;

        /// <summary>
        /// 내려놓기 원의 한가운데. 화면이 원을 그리는 자리이자 판정하는 자리다 —
        /// 둘이 같은 값에서 나와야 "원 밖에서 뗐는데 안 나간다"가 안 생긴다.
        ///
        /// <para><paramref name="screenWidthInHeights"/>는 세로를 1로 봤을 때의 가로 길이다
        /// (16:9 가로 화면이면 1.78). 화면 끝을 알아야 밖으로 나가는 걸 막는다.</para>
        /// </summary>
        public static Vector2 CancelTargetCenter(Vector2 pressInHeights, float screenWidthInHeights)
        {
            //  기본은 아래다. 다만 가로로 쥐면 엄지가 낮게 쉬어서 화면 아래쪽을 누르는 일이
            //  잦은데, 거기선 "아래로 31%"가 화면 밖이다. 그럴 땐 **위로 뒤집는다** —
            //  거리는 어느 쪽이든 같으므로 조준이 쓰는 범위 밖이라는 성질이 유지된다.
            //  (가까이 당겨 붙이면 안 된다. 그러면 겨누다 취소된다.)
            float below = pressInHeights.y - CancelBelowPressInHeights;
            float y = below >= CancelRadiusInHeights
                ? below
                : pressInHeights.y + CancelBelowPressInHeights;

            //  가로로 삐져나가면 안쪽으로 민다. 미는 건 거리를 **늘리기만** 하므로 안전하다.
            float maxX = Mathf.Max(CancelRadiusInHeights, screenWidthInHeights - CancelRadiusInHeights);
            float x = Mathf.Clamp(pressInHeights.x, CancelRadiusInHeights, maxX);

            return new Vector2(x, y);
        }

        /// <summary>그 원 안인가.</summary>
        public static bool InsideCancelTarget(Vector2 pressInHeights, Vector2 pointInHeights,
            float screenWidthInHeights)
        {
            return (pointInHeights - CancelTargetCenter(pressInHeights, screenWidthInHeights)).sqrMagnitude
                   <= CancelRadiusInHeights * CancelRadiusInHeights;
        }

        /// <summary>
        /// 손가락이 지금 내려놓기 자리 안에 있나. 화면이 원을 붉게 켜는 데 쓴다.
        ///
        /// <para><b>여기 있다고 활이 내려가지는 않는다</b> — 판정은 <b>뗄 때만</b> 한다.
        /// 그래서 원 위를 지나 더 아래로 겨누는 것이 막히지 않는다. 누를 때가 아니라 뗄 때가
        /// 결정한다는 건 터치 버튼의 표준 동작이기도 하다(WCAG 2.5.2).</para>
        /// </summary>
        public bool OverCancelTarget { get; private set; }

        /// <summary>화면이 원을 그릴 자리. 잡고 있는 동안에만 쓴다.</summary>
        public Vector2 CancelTargetCenterInHeights => CancelTargetCenter(pressInHeights, screenWidthInHeights);

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

        //  활을 들기 직전에 보던 곳. 내려놓으면 여기로 돌아온다 — 아래 ReturnAimAfterCancel 참고.
        private float aimAtPressYaw;
        private float aimAtPressPitch;

        //  손가락을 처음 댄 자리. 내려놓기 원이 여기를 기준으로 선다.
        private Vector2 pressInHeights;

        //  세로를 1로 봤을 때의 가로 길이. 원이 화면 밖으로 안 나가게 하는 데 쓴다.
        private float screenWidthInHeights = 16f / 9f;

        //  돌아가는 데 걸리는 시간(초). 활을 내리는 동안 화각도 22°→60°로 넓어지므로
        //  그 속도와 얼추 맞춰야 화면이 따로 노는 느낌이 안 난다.
        private const float ReturnSeconds = 0.18f;

        //  되돌리기는 지수 감쇠라 수학적으로는 안 끝난다 — 이만큼 지나면 그만둔다.
        private const float ReturnGiveUpSeconds = 0.6f;

        private bool returningAim;
        private float returnElapsed;

        //  누른 손가락이 무엇을 하려는지 아직 모르는 상태를 거친다.
        //  <b>가만히 있으면 활, 곧바로 끌면 시야</b> — 길게 누르기와 쓸기를 가르는 표준 방식이다.
        //  이게 없으면 화면을 쓸어 둘러보려 할 때마다 활이 당겨지고, 화살이 유한한 사거리 맵에서는
        //  그게 곧 손해다.
        private enum Press
        {
            None,
            Deciding,   // 눌렀는데 아직 활인지 시야인지 모른다
            Looking,    // 곧바로 끌었다 — 이 제스처는 끝까지 시야만 돌린다
            Drawing,    // 가만히 버텼다 — 활이 올라왔다
        }
        private Press press = Press.None;

        /// <summary>이만큼 가만히 있으면 활이 올라오기 시작한다(초).</summary>
        public const float PressHoldSeconds = 0.15f;

        /// <summary>그 안에 이만큼(화면 높이 대비) 움직이면 "시야를 돌리려던 것"으로 본다.</summary>
        public const float DragSlopFraction = 0.02f;

        /// <summary>누름이 무엇이 될지.</summary>
        public enum PressIntent { Undecided, Look, Draw }

        /// <summary>
        /// 누른 뒤 <paramref name="travelFraction"/>만큼 움직이고 <paramref name="heldSeconds"/>가
        /// 지났을 때 이 누름이 무엇이 되는가.
        ///
        /// <para><b>움직임이 먼저다</b> — 슬롭을 넘겼으면 그 뒤로 아무리 오래 눌러도 시야다.
        /// 반대로 두면 "쓸어 둘러보다 손가락을 잠깐 멈췄는데 활이 올라오는" 일이 생긴다.</para>
        /// </summary>
        public static PressIntent Decide(float travelFraction, float heldSeconds)
        {
            if (travelFraction >= DragSlopFraction)
            {
                return PressIntent.Look;
            }
            return heldSeconds >= PressHoldSeconds ? PressIntent.Draw : PressIntent.Undecided;
        }

        private float pressSeconds;
        private float pressTravel;

        /// <summary>
        /// 손가락을 댔다. 활인지 시야인지는 아직 정하지 않는다.
        /// 받는 값은 <b>화면 세로를 1로 본 좌표</b>이고 y 양수가 위다(가로는 1을 넘는다).
        /// </summary>
        public void BeginPress(Vector2 positionInHeights, float screenWidthInHeights)
        {
            press = Press.Deciding;
            this.screenWidthInHeights = screenWidthInHeights;
            pressSeconds = 0f;
            pressTravel = 0f;
            OverCancelTarget = false;
            pressInHeights = positionInHeights;
            aimAtPressYaw = cameraController.Yaw;
            aimAtPressPitch = cameraController.Pitch;

            //  새로 겨누려는 것이니 앞선 되돌리기는 여기서 끝낸다.
            returningAim = false;
        }

        /// <summary>
        /// 손가락이 움직였다. <paramref name="deltaFraction"/>은 화면 크기 대비 비율,
        /// <paramref name="positionInHeights"/>는 화면 세로를 1로 본 좌표다.
        /// 둘 다 <b>x 양수가 오른쪽, y 양수가 위</b>로 같은 관습을 쓴다.
        /// </summary>
        public void MovePointer(Vector2 deltaFraction, Vector2 positionInHeights)
        {
            if (press == Press.None)
            {
                return;
            }

            if (press == Press.Deciding)
            {
                pressTravel += deltaFraction.magnitude;
                if (Decide(pressTravel, pressSeconds) == PressIntent.Look)
                {
                    press = Press.Looking;   // 활은 안 든다 — 이 제스처가 끝날 때까지
                }
            }

            if (press == Press.Looking)
            {
                AimByDrag(deltaFraction);
                return;
            }

            if (press != Press.Drawing)
            {
                return;   // 아직 Deciding — 움직임이 슬롭 안이라 아무것도 안 한다
            }

            //  원 안에 있는지는 **화면에 알려 주기 위해서만** 본다. 활을 내리지도, 조준을
            //  멈추지도 않는다 — 그래야 원 위를 지나 더 아래로 겨눌 수 있다.
            OverCancelTarget = InsideCancelTarget(pressInHeights, positionInHeights, screenWidthInHeights);

            AimByDrag(deltaFraction);
        }

        /// <summary>매 프레임. 누름이 무엇이 될지 정하고, 내려놓는 동안 조준을 되돌린다.</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            if (press == Press.Deciding)
            {
                pressSeconds += deltaSeconds;
                if (Decide(pressTravel, pressSeconds) == PressIntent.Draw)
                {
                    press = Press.Drawing;
                    input.SetDrawing(true);
                    input.SetDrawRatio(1f);
                }
                return;
            }

            ReturnAimAfterCancel(deltaSeconds);
        }

        /// <summary>
        /// 내려놓은 <b>뒤에</b> 조준을 활을 들기 직전 자리로 되돌린다.
        ///
        /// <para><b>왜 필요한가</b>: 취소하려면 손가락을 원까지 내려야 하는데, 그 손가락이 곧
        /// 조준이라 내려간 만큼(화면 세로의 31% = 당긴 상태에서 <b>아래로 6.8°</b>) 조준도 같이
        /// 끌려 내려간다. 90m 홀드오버 전체가 1.1°이니 그냥 두면 조준이 통째로 날아간다.</para>
        ///
        /// <para>판정이 <b>뗄 때</b> 나므로 되돌리기도 뗀 뒤에 돈다 — 잡고 있는 동안 돌리면
        /// 겨누는 손가락과 싸운다.</para>
        /// </summary>
        private void ReturnAimAfterCancel(float deltaSeconds)
        {
            if (returningAim == false)
            {
                return;
            }

            returnElapsed += deltaSeconds;
            if (returnElapsed >= ReturnGiveUpSeconds)
            {
                returningAim = false;
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

        //  끈 만큼 시야를 돌린다. 화각으로 감도가 보정돼 당길수록 저절로 정밀해진다.
        private void AimByDrag(Vector2 deltaFraction)
        {
            AimBy(new Vector2(deltaFraction.x * HorizontalFov, deltaFraction.y * CurrentFov));
        }

        /// <summary>두 번 불려도 한 번만 쏜다 — 아래 View가 뗌을 두 경로로 받기 때문이다.</summary>
        public void EndPress()
        {
            if (press == Press.None)
            {
                return;
            }

            bool wasDrawing = press == Press.Drawing;
            press = Press.None;

            if (wasDrawing == false)
            {
                return;   // 시야만 돌렸거나, 활이 올라오기 전에 뗐다 — 화살은 없다
            }

            input.SetDrawing(false);

            //  내려놓기 원 안에서 뗐거나 시위가 안 걸렸으면 쏘라는 신호를 아예 안 보낸다.
            if (OverCancelTarget == false && DrawArmed)
            {
                input.SetRelease();
            }
            else if (OverCancelTarget)
            {
                BeginAimReturn();
            }
            OverCancelTarget = false;
        }

        /// <summary>
        /// 쏘지 않고 활을 내린다. 화면 밖으로 손가락이 나가 터치를 뺏겼을 때도 이리로 온다 —
        /// <b>알림을 쓸어 내리거나 전화가 와서 화살이 나가면 안 되기 때문이다.</b>
        /// </summary>
        public void Cancel()
        {
            if (press == Press.None)
            {
                return;
            }

            bool wasDrawing = press == Press.Drawing;
            press = Press.None;
            OverCancelTarget = false;

            if (wasDrawing == false)
            {
                return;
            }

            input.SetDrawing(false);
            BeginAimReturn();
        }

        private void BeginAimReturn()
        {
            returningAim = true;
            returnElapsed = 0f;
        }

        /// <summary>지금 활을 들고 있나. 화면이 띠와 조준점을 켜는 데 쓴다.</summary>
        public bool Holding => press == Press.Drawing;

    }
}
