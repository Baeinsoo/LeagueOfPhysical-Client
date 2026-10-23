using UnityEngine;

namespace LOP.UI
{
    /// <summary>활이 받는 입력. <see cref="PlayerInputManager"/>가 구현한다.</summary>
    public interface IBowInput
    {
        void SetDrawing(bool drawing);
        void SetDrawRatio(float ratio);
        void SetRelease();
        void SetMovement(float horizontal, float vertical);
    }

    /// <summary>조준 조작이 돌리는 카메라. <see cref="CameraController"/>가 구현한다.</summary>
    public interface IAimCamera
    {
        float Yaw { get; }
        float Pitch { get; }
        /// <summary>화면이 지금 보는 방향(도). 걷기 스틱의 "위"가 이 방향이다.</summary>
        float CameraYawDegrees { get; }
        void AimBy(Vector2 degrees);
        Camera MainCamera { get; }
    }

    /// <summary>
    /// 활 조작판의 조작 부분 — 키보드·화각·시위 판정·손가락 역할·끌어 조준·되돌리기·이동 스틱.
    /// 활쏘기와 말 활쏘기가 같은 조작판 화면을 쓰도록 <see cref="ArcheryPadViewModel"/>에서 떼어 냈다.
    /// </summary>
    public sealed class ArcheryBowControls
    {
        private readonly IBowInput input;
        private readonly IAimCamera aimCamera;
        private readonly System.Func<float> drawRatio;
        private readonly System.Action<bool> onRelease;

        //  걷기 스틱을 민 양. 스틱을 놓으면 0이 되고, 그 0도 매 프레임 밀어야 캐릭이 선다.
        private Vector2 moveStick;

        //  WASD로 민 양. 스틱과 따로 두고, 스틱이 놀고 있을 때만 쓴다 — 한 변수에 둘 다
        //  쓰면 손가락이 스틱을 놓는 순간 키보드 값까지 0으로 지워진다.
        private Vector2 keyMove;

        //  겨누는 속도(도/초). 아래 화각을 기준으로 정한 값이고, 당겨서 화면이 좁아지면 그
        //  비율만큼 같이 줄어든다 — 그래야 손동작 하나가 **화면 위에서** 늘 같은 거리를 움직인다.
        //  (저격 게임의 "줌 감도 보정"과 같은 것. 안 하면 줌인할수록 손이 미쳐 날뛴다.)
        //  모드마다 넓은 화각이 달라서(말 활쏘기는 62도) 그 모드의 넓은 화각을 받는다.
        public float ReferenceFov { get; }

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

        /// <param name="drawRatio">시뮬이 가진 지금 당김(0~1). 화면이 따로 세지 않는다.</param>
        /// <param name="onRelease">화살이 실제로 나갈 놓기에 부른다. 인수 = 그 순간 시위가 걸렸나. null 가능.</param>
        /// <param name="referenceFov">겨누는 속도의 기준 화각 = 그 모드의 당기지 않은 화각(도).</param>
        public ArcheryBowControls(IBowInput input, IAimCamera camera, System.Func<float> drawRatio, System.Action<bool> onRelease,
                                  float referenceFov = ArcheryAimView.WideFov)
        {
            ReferenceFov = referenceFov;
            this.input = input;
            this.aimCamera = camera;
            this.drawRatio = drawRatio;
            this.onRelease = onRelease;
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
                var camera = aimCamera.MainCamera;
                return camera != null ? camera.fieldOfView : ReferenceFov;
            }
        }

        /// <summary>가로 화각(도). 세로 화각을 화면 가로세로 비율로 늘린 값이다.</summary>
        private float HorizontalFov
        {
            get
            {
                var camera = aimCamera.MainCamera;
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
            aimCamera.AimBy(degrees);
        }

        /// <summary>
        /// 활을 든 손가락이 지금 시위 풀기 버튼 위에 있나. 화면이 버튼을 붉게 켜는 데 쓴다.
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
        public float DrawRatio => drawRatio();

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
        /// 손가락이 닿았다. <paramref name="hit"/>이 활 버튼이면 활을 들고, 화면이면 그 손가락은 끝까지 겨누기만 한다.
        /// 시위 풀기 버튼이면 겨누지 않고, 활을 든 중이면 <b>안 쏘고 내려놓는다</b>. 손잡이면 아무것도 안 하고
        /// <see cref="ArcheryPointerRouter.Role.Handle"/>을 돌려준다 — 버튼 옮기기는 화면이 한다.
        /// <paramref name="screenWidthInHeights"/>는 세로를 1로 봤을 때의 가로 길이다(16:9 가로면 1.78).
        /// </summary>
        public ArcheryPointerRouter.Role PointerDown(int pointerId, ArcheryButtonLayout.Hit hit, float screenWidthInHeights)
        {
            this.screenWidthInHeights = screenWidthInHeights;
            var role = router.Down(pointerId, hit);
            fingers[pointerId] = new Finger();

            if (role == ArcheryPointerRouter.Role.Cancel)
            {
                Finish(ArcheryPointerRouter.Ending.PutDown);
                return role;
            }
            if (role != ArcheryPointerRouter.Role.Bow)
            {
                return role;
            }

            OverCancelButton = false;
            aimAtPressYaw = aimCamera.Yaw;
            aimAtPressPitch = aimCamera.Pitch;

            //  새로 겨누려는 것이니 앞선 되돌리기는 여기서 끝낸다.
            returningAim = false;

            input.SetDrawing(true);
            input.SetDrawRatio(1f);
            return role;
        }

        /// <summary>이 손가락이 지금 버튼을 옮기고 있나. 활을 들면 옮기던 손잡이 손가락도 멈춘다.</summary>
        public bool MovesButton(int pointerId) => router.RoleOf(pointerId) == ArcheryPointerRouter.Role.Handle;

        /// <summary>
        /// 손가락이 움직였다. <paramref name="deltaFraction"/>은 화면 크기 대비 비율이고
        /// <b>x 양수가 오른쪽, y 양수가 위</b>다. <paramref name="overCancelButton"/>은 손가락이 지금 시위 풀기 버튼 위인지.
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
                //  시위 풀기 버튼 위인지는 **화면에 알려 주기 위해서만** 본다. 활을 내리지도, 조준을
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
                //  화살이 실제로 나갈 놓기에만 "톡". 시위가 걸렸나는 이 순간의 시뮬 값으로 본다 —
                //  시뮬은 다음 틱에 같은 값(drawAtRelease)으로 쏠지 말지를 정한다.
                onRelease?.Invoke(DrawArmed);
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
        //  Escape는 시위 풀기 버튼이다 — 당기는 중이면 안 쏘고 내려놓고, 그 뒤 스페이스를 떼도 안 쏜다.
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
                PointerDown(ArcheryPointerRouter.KeyboardPointerId, ArcheryButtonLayout.Hit.Bow, screenWidthInHeights);
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
            Vector3 world = MoveStickDirection.ToWorld(stick, aimCamera.CameraYawDegrees);
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
        /// <para><b>왜 필요한가</b>: 활 손가락을 왼쪽 시위 풀기 버튼까지 끌어오면 그 손가락이 곧
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
                aimCamera.Yaw, aimCamera.Pitch,
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

        /// <summary>시위 풀기 버튼이 지금 먹히나. 화면이 버튼을 보일지/숨길지(display:none) 정하는 데 쓴다.</summary>
        public bool CancelActive => router.Drawing;

    }
}
