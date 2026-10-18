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
        private readonly ArcheryCourse course;

        //  걷기 스틱을 민 양. 스틱을 놓으면 0이 되고, 그 0도 매 프레임 밀어야 캐릭이 선다.
        private Vector2 moveStick;

        //  WASD로 민 양. 스틱과 따로 두고, 스틱이 놀고 있을 때만 쓴다 — 한 변수에 둘 다
        //  쓰면 손가락이 스틱을 놓는 순간 키보드 값까지 0으로 지워진다.
        private Vector2 keyMove;

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
            ArcheryConfig config,
            ArcheryCourse course)
        {
            this.input = input;
            this.cameraController = cameraController;
            this.entityRegistry = entityRegistry;
            this.playerContext = playerContext;
            this.impactLog = impactLog;
            this.config = config;
            this.course = course;
            AimAccel = ArcheryAimAccel.LoadStrength();
        }

        /// <summary>조준 가속 세기. 개발 빌드에서만 화면 버튼으로 바꾼다.</summary>
        public ArcheryAimAccel.Strength AimAccel { get; private set; }

        public string AimAccelLabel => ArcheryAimAccel.LabelOf(AimAccel);

        /// <summary>다음 세기로 넘기고 저장한다(개발 빌드만 저장된다).</summary>
        public void CycleAimAccel()
        {
            AimAccel = ArcheryAimAccel.Next(AimAccel);
            ArcheryAimAccel.SaveStrength(AimAccel);
        }

        /// <summary>걷기 스틱을 보이나. 걷는 속도가 0인 코스(한 발 승부·투석기·원형)는 숨긴다 —
        /// 그 구석을 스틱이 가져가면 거기서 누른 손가락이 활을 못 든다.</summary>
        public bool ShowMoveStick => course.MoveSpeed > 0f;

        /// <summary>
        /// 지금 화면을 그리는 카메라. 화면이 "+10"을 맞은 자리에 띄우려면 월드→화면 투영이 필요하다.
        /// <b><c>Camera.main</c>을 쓰지 말 것</b> — 조준 카메라가 그 태그를 달고 있다는 보장이 없고,
        /// 빗나가면 팝업이 전부 화면 한가운데로 떨어져 "어디를 맞혔나"가 사라진다.
        /// </summary>
        public Camera Camera => cameraController.MainCamera;

        /// <summary>지금 자리에서 내가 쏜 것들. 화면이 기록판에 점으로 찍는다.</summary>
        public System.Collections.Generic.IReadOnlyList<ArcheryImpactLog.Shot> Impacts => impactLog.Shots;

        /// <summary>기록판이 모으고 있는 자리 번호. 바뀌면 화면이 점을 다시 그린다.</summary>
        public int ImpactWave => impactLog.Wave;

        /// <summary>
        /// 발당 점수 앞에 "+"를 붙이나. 한 발 승부에서 그 값은 <b>띠 점수</b>(1~10)이고 점수로 더해지지
        /// 않는다(라운드 순위 점수만 더한다) — "+10"으로 쓰면 10점을 얻은 것처럼 읽힌다.
        /// </summary>
        public string PointsPrefix => config.CourseKind == ArcheryCourseKind.ShootOff ? "" : "+";

        /// <summary>맞은 자리에 띄우는 글. 한 발 승부의 10점은 "10!!"(점수로 더해지지 않으니 +를 안 붙인다).</summary>
        public static string PopupTextFor(ArcheryCourseKind kind, int points)
        {
            if (kind == ArcheryCourseKind.ShootOff)
            {
                return points == 10 ? "10!!" : points.ToString();
            }
            return "+" + points;
        }

        public string PopupText(int points) => PopupTextFor(config.CourseKind, points);

        /// <summary>착탄 기록판을 보이나. 한 발 승부는 결과 화면의 큰 과녁이, 투석기는 띠가 없어 숨긴다.</summary>
        public static bool ShowsImpactPlot(ArcheryCourseKind kind)
            => kind != ArcheryCourseKind.ShootOff && kind != ArcheryCourseKind.Catapult;

        /// <summary>맞은 자리에 "+N"을 띄우나. 투석기는 HUD가 누가 맞혔는지까지("OO +3") 띄우므로 끈다.</summary>
        public static bool ShowsHitPopups(ArcheryCourseKind kind) => kind != ArcheryCourseKind.Catapult;

        public bool ShowImpactPlot => ShowsImpactPlot(config.CourseKind);

        public bool ShowHitPopups => ShowsHitPopups(config.CourseKind);

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
        /// 데스크톱 편의: WASD = 걷기, 화살표 = 미세 조준, 스페이스 = 활 버튼, Escape = 취소. 매 프레임 호출된다.
        /// 선례: <see cref="SkydivePadViewModel"/>의 같은 이름 메서드.
        /// </summary>
        public void PollKeyboard()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            PollSpace(keyboard);
            if (keyboard == null)
            {
                lookRamp = 0f;
                keyMove = Vector2.zero;
                return;   // 키보드가 없는 기기(모바일)
            }

            //  WASD = 걷기. 화면 스틱과 같은 값으로 넘긴다.
            var walk = Vector2.zero;
            if (keyboard.dKey.isPressed) { walk.x += 1f; }
            if (keyboard.aKey.isPressed) { walk.x -= 1f; }
            if (keyboard.wKey.isPressed) { walk.y += 1f; }
            if (keyboard.sKey.isPressed) { walk.y -= 1f; }
            keyMove = walk.sqrMagnitude > 1f ? walk.normalized : walk;

            //  화살표 = 조준. 겨누는 데는 **멈춘 채 아주 조금씩** 움직이는 조작이 필요한데
            //  (90m 10점 링이 0.04°다) 마우스 드래그로는 그 크기를 내기 어렵다. 그래서
            //  키보드 미세 조준은 남겨 두고 걷기와 손가락만 나눈다.
            var look = Vector2.zero;
            if (keyboard.rightArrowKey.isPressed) { look.x += 1f; }
            if (keyboard.leftArrowKey.isPressed) { look.x -= 1f; }
            if (keyboard.upArrowKey.isPressed) { look.y += 1f; }
            if (keyboard.downArrowKey.isPressed) { look.y -= 1f; }

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
        /// 활을 든 손가락이 지금 취소 버튼 위에 있나. 화면이 버튼을 붉게 켜는 데 쓴다.
        ///
        /// <para><b>여기 있다고 활이 내려가지는 않는다</b> — 판정은 <b>뗄 때만</b> 한다.
        /// 그래서 버튼 위를 지나 더 겨누는 것이 막히지 않는다. 누를 때가 아니라 뗄 때가
        /// 결정한다는 건 터치 버튼의 표준 동작이기도 하다(WCAG 2.5.2).</para>
        /// </summary>
        public bool OverCancelButton { get; private set; }

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

        //  세로를 1로 봤을 때의 가로 길이. 손가락 빠르기를 가로·세로 같은 자로 재는 데 쓴다.
        private float screenWidthInHeights = 16f / 9f;

        //  돌아가는 데 걸리는 시간(초). 활을 내리는 동안 화각도 22°→60°로 넓어지므로
        //  그 속도와 얼추 맞춰야 화면이 따로 노는 느낌이 안 난다.
        private const float ReturnSeconds = 0.18f;

        //  되돌리기는 지수 감쇠라 수학적으로는 안 끝난다 — 이만큼 지나면 그만둔다.
        private const float ReturnGiveUpSeconds = 0.6f;

        private bool returningAim;
        private float returnElapsed;

        //  손가락마다 역할을 나눈다 — 활 버튼에서 시작한 손가락만 활을 다루고, 나머지는 겨누기만 한다.
        private readonly ArcheryPointerRouter router = new ArcheryPointerRouter();

        //  손가락마다 따로 재는 빠르기. 두 손가락이 같이 움직일 때 한 값으로 섞으면
        //  천천히 다듬는 손가락에도 다른 손가락의 빠르기만큼 가속이 붙는다.
        private sealed class Finger
        {
            public readonly ArcheryAimAccel.FingerSpeed Speed = new ArcheryAimAccel.FingerSpeed();
            public float SecondsSinceMove;
        }
        private readonly System.Collections.Generic.Dictionary<int, Finger> fingers =
            new System.Collections.Generic.Dictionary<int, Finger>();

        //  스페이스를 지난 프레임에 누르고 있었나. 누른 순간과 뗀 순간만 골라낸다.
        private bool spaceHeld;

        /// <summary>
        /// 손가락이 닿았다. <paramref name="onBowButton"/>이면 활을 들고, 아니면 그 손가락은 끝까지 겨누기만 한다.
        /// <paramref name="onCancelButton"/>이면 겨누지 않고, 활을 든 중이면 <b>안 쏘고 내려놓는다</b>.
        /// <paramref name="screenWidthInHeights"/>는 세로를 1로 봤을 때의 가로 길이다(16:9 가로면 1.78).
        /// </summary>
        public void PointerDown(int pointerId, bool onBowButton, bool onCancelButton, float screenWidthInHeights)
        {
            this.screenWidthInHeights = screenWidthInHeights;
            var role = router.Down(pointerId, onBowButton, onCancelButton);
            fingers[pointerId] = new Finger();

            if (role == ArcheryPointerRouter.Role.Cancel)
            {
                Finish(ArcheryPointerRouter.Ending.PutDown);
                return;
            }
            if (role != ArcheryPointerRouter.Role.Bow)
            {
                return;
            }

            OverCancelButton = false;
            aimAtPressYaw = cameraController.Yaw;
            aimAtPressPitch = cameraController.Pitch;

            //  새로 겨누려는 것이니 앞선 되돌리기는 여기서 끝낸다.
            returningAim = false;

            input.SetDrawing(true);
            input.SetDrawRatio(1f);
        }

        /// <summary>
        /// 손가락이 움직였다. <paramref name="deltaFraction"/>은 화면 크기 대비 비율이고
        /// <b>x 양수가 오른쪽, y 양수가 위</b>다. <paramref name="overCancelButton"/>은 손가락이 지금 취소 버튼 위인지.
        /// </summary>
        public void PointerMove(int pointerId, Vector2 deltaFraction, bool overCancelButton)
        {
            if (router.Aims(pointerId) == false || fingers.TryGetValue(pointerId, out var finger) == false)
            {
                return;
            }

            //  빠르기는 세로를 1로 본 같은 자로 잰다 — 가로를 가로 길이로 나눈 값을 그대로 쓰면
            //  같은 빠르기라도 가로로 그을 때 느리게 읽힌다.
            Vector2 moveInHeights = new Vector2(deltaFraction.x * screenWidthInHeights, deltaFraction.y);
            float speed = finger.Speed.Add(moveInHeights.magnitude, finger.SecondsSinceMove);
            finger.SecondsSinceMove = 0f;
            float gain = ArcheryAimAccel.Gain(speed, ArcheryAimAccel.MaxGainOf(AimAccel));

            if (router.RoleOf(pointerId) == ArcheryPointerRouter.Role.Bow)
            {
                //  취소 버튼 위인지는 **화면에 알려 주기 위해서만** 본다. 활을 내리지도, 조준을
                //  멈추지도 않는다 — 그래야 버튼 위를 지나 더 겨눌 수 있다.
                OverCancelButton = overCancelButton;
            }

            //  내려놓은 뒤 조준이 되돌아가는 중에 다른 손가락이 겨누면, 그 손가락이 이긴다.
            returningAim = false;
            AimByDrag(deltaFraction, gain);
        }

        /// <summary>손가락을 뗐다. 활을 든 손가락일 때만 쏘거나 내린다 — 겨누던 손가락은 아무 일 없다.</summary>
        public void PointerUp(int pointerId)
        {
            fingers.Remove(pointerId);
            Finish(router.Up(pointerId, DrawArmed, OverCancelButton));
        }

        /// <summary>
        /// 터치를 뺏겼다(화면 밖·알림 스와이프·전화·앱 전환). 활을 들고 있었다면 <b>쏘지 않고</b> 내린다 —
        /// 터치를 뺏긴 건 "쏘겠다"는 뜻이 아니다.
        /// </summary>
        public void PointerLost(int pointerId)
        {
            fingers.Remove(pointerId);
            Finish(router.Lost(pointerId));
        }

        private void Finish(ArcheryPointerRouter.Ending ending)
        {
            if (ending == ArcheryPointerRouter.Ending.None)
            {
                return;
            }

            input.SetDrawing(false);
            if (ending == ArcheryPointerRouter.Ending.Fire)
            {
                input.SetRelease();
            }
            else if (ending == ArcheryPointerRouter.Ending.PutDown)
            {
                BeginAimReturn();
            }
            OverCancelButton = false;
        }

        //  데스크톱 편의: 스페이스를 누르고 있는 동안 활 버튼을 누른 것과 같다. 마우스로는 화면을
        //  끌어 겨눈다 — 마우스 하나로 버튼을 누르면 겨누는 손이 없어지기 때문이다.
        //  Escape는 취소 버튼이다 — 당기는 중이면 안 쏘고 내려놓고, 그 뒤 스페이스를 떼도 안 쏜다.
        private void PollSpace(UnityEngine.InputSystem.Keyboard keyboard)
        {
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Finish(router.Cancel());
            }

            bool held = keyboard != null && keyboard.spaceKey.isPressed;
            if (held == spaceHeld)
            {
                return;
            }
            spaceHeld = held;
            if (held)
            {
                PointerDown(ArcheryPointerRouter.KeyboardPointerId, true, false, screenWidthInHeights);
            }
            else
            {
                PointerUp(ArcheryPointerRouter.KeyboardPointerId);
            }
        }

        /// <summary>
        /// 걷기 스틱을 민 양(−1~1). 화면이 스틱을 잡고 있는 동안 갱신한다.
        /// </summary>
        public void SetMove(Vector2 stick)
        {
            moveStick = stick;
        }

        /// <summary>스틱에서 손을 뗐다.</summary>
        public void ClearMove()
        {
            moveStick = Vector2.zero;
        }

        /// <summary>
        /// 지금 스틱 값을 이동 입력으로 밀어 넣는다. <b>매 프레임</b> 불러야 한다 —
        /// 손을 뗐을 때의 0도 밀어야 캐릭터가 선다(안 밀면 마지막 방향으로 계속 걷는다).
        /// </summary>
        public void FeedMove()
        {
            //  손가락이 스틱을 잡고 있으면 그쪽이 이긴다 — 데스크톱 WASD는 스틱이 놀 때만.
            Vector2 stick = moveStick.sqrMagnitude > 0f ? moveStick : keyMove;
            Vector3 world = MoveStickDirection.ToWorld(stick, Camera.transform.eulerAngles.y);
            input.SetMovement(world.x, world.z);
        }

        /// <summary>
        /// 매 프레임, 손가락 빠르기를 재는 시계를 민다. <b>실제 시간</b>을 넣는다 — 게임 시간이 멈추거나
        /// 느려져도 손가락은 그대로 움직이므로, 게임 시간으로 재면 빠르기가 부풀어 조준이 튄다.
        /// </summary>
        public void AdvanceFingerClock(float unscaledDeltaSeconds)
        {
            if (unscaledDeltaSeconds <= 0f)
            {
                return;
            }
            foreach (var finger in fingers.Values)
            {
                finger.SecondsSinceMove += unscaledDeltaSeconds;
            }
        }

        /// <summary>매 프레임. 내려놓은 뒤 조준을 되돌린다.</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            ReturnAimAfterCancel(deltaSeconds);
        }

        /// <summary>
        /// 내려놓은 <b>뒤에</b> 조준을 활을 들기 직전 자리로 되돌린다.
        ///
        /// <para><b>왜 필요한가</b>: 활 손가락을 왼쪽 취소 버튼까지 끌어오면 그 손가락이 곧
        /// 조준이라 끈 만큼 조준도 같이 끌려간다. 90m 홀드오버 전체가 1.1°이니 그냥 두면 조준이
        /// 통째로 날아간다. 다른 손가락으로 톡 쳐서 취소했어도 들기 전 자리로 돌아가는 편이 같은 손맛이다.</para>
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

        //  끈 만큼 시야를 돌린다. 화각으로 감도가 보정돼 당길수록 저절로 정밀해지고,
        //  빨리 그은 만큼 가속 배율이 위에 곱해진다(천천히 움직이면 1배).
        /// <summary>끈 거리 → 각도의 기본 배율. 1이면 손가락 밑 물체가 손가락을 그대로 따라온다(1:1).
        /// 10-18 사람 판 "더 빠릿하게" — 투석기 표적이 넓게 흩어져 1:1로는 엄지가 못 따라간다.</summary>
        public const float DragBaseGain = 1.6f;
        /// <summary>위아래만 더 곱한다 — 엄지가 위아래로 움직일 수 있는 거리가 좌우보다 짧다.</summary>
        public const float DragPitchExtra = 1.25f;

        private void AimByDrag(Vector2 deltaFraction, float accelGain)
        {
            AimBy(new Vector2(deltaFraction.x * HorizontalFov, deltaFraction.y * CurrentFov * DragPitchExtra) * (DragBaseGain * accelGain));
        }

        private void BeginAimReturn()
        {
            returningAim = true;
            returnElapsed = 0f;
        }

        /// <summary>지금 활을 들고 있나. 화면이 버튼을 밝히는 데 쓴다.</summary>
        public bool Holding => router.Drawing;

        /// <summary>취소 버튼이 지금 먹히나. 화면이 버튼을 보일지/숨길지(display:none) 정하는 데 쓴다.</summary>
        public bool CancelActive => router.Drawing;

    }
}
