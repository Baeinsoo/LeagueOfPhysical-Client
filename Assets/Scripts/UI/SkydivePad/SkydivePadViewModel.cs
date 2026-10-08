using R3;

namespace LOP.UI
{
    /// <summary>
    /// 조작 패드의 상태와 커맨드. 터치 좌표 해석은 View가 하고, 여기서는 그 결과를
    /// 입력 매니저로 넘기고 화면에 보일 값을 노출한다.
    /// </summary>
    public class SkydivePadViewModel : System.IDisposable
    {
        // 슬라이더를 이만큼 왼쪽으로 밀면 패러세일이 펴진다. 도구라 반쯤 펼칠 수 없다.
        private const float GlideThreshold = 0.45f;

        // 방향키 한 프레임이 손가락을 이만큼 끈 것과 같다(px). 드래그와 같은 경로로 넣어
        // 감속·한계각이 저절로 같아진다.
        private const float KeyLookSpeed = 8f;

        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly SkydiveConfig config;
        private readonly SavePadField savePads;
        private readonly CatchTargetField catchTargets;
        private readonly GameFramework.Runner.IRunner runner;

        //  별 조각이 이보다 가까우면 "잡기!"를 띄운다 — 대자로 2초 안에 닿을 거리쯤.
        private const float CatchHintDistance = 40f;

        private readonly ReactiveProperty<float> staminaRatio = new ReactiveProperty<float>(1f);
        private readonly ReactiveProperty<bool> grounded = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<string> statusText = new ReactiveProperty<string>("-");

        public ReadOnlyReactiveProperty<float> StaminaRatio => staminaRatio;

        /// <summary>
        /// 지금 상태 한 줄. 몸 기울기만으로는 "선 채로 낙하"와 "활공 중 대자"가 구분되지 않아
        /// 상태 이름을 직접 띄우고, 공중일 때만 낙하 속도를 붙인다(자세별 종단 속도 확인용).
        /// </summary>
        public ReadOnlyReactiveProperty<string> StatusText => statusText;

        /// <summary>발 딛고 있나. 점프 버튼은 이때만 보인다 — 낙하 중엔 뜰 곳이 없다.</summary>
        public ReadOnlyReactiveProperty<bool> Grounded => grounded;

        public SkydivePadViewModel(PlayerInputManager input, CameraController cameraController,
                                   IPlayerContext playerContext,
                                   GameFramework.World.EntityRegistry entityRegistry,
                                   SkydiveConfig config,
                                   SavePadField savePads,
                                   CatchTargetField catchTargets,
                                   GameFramework.Runner.IRunner runner)
        {
            this.savePads = savePads;
            this.catchTargets = catchTargets;
            this.runner = runner;
            this.input = input;
            this.cameraController = cameraController;
            this.playerContext = playerContext;
            this.entityRegistry = entityRegistry;
            this.config = config;
        }

        /// <summary>방향 스틱. 값은 −1~1로 정규화된 것이 들어온다.</summary>
        public void Move(UnityEngine.Vector2 stick)
        {
            // 카메라가 보는 방향 기준으로 돌린다 — 화면에서 위로 밀면 화면 위쪽으로 간다.
            // 식은 GamePadViewModel.PushMovement와 같게 쓴다(같은 개념을 두 어휘로 적지 않는다).
            float yAngle = cameraController.MainCamera.transform.eulerAngles.y;
            var cameraRotation = UnityEngine.Quaternion.Euler(0, yAngle, 0);
            UnityEngine.Vector3 transformed = cameraRotation * new UnityEngine.Vector3(stick.x, 0, stick.y);
            input.SetMovement(transformed.x, transformed.z);
        }

        /// <summary>
        /// WASD로도 같은 방향 이동을 준다. 마우스가 하나뿐인 데스크톱에서는 스틱과 자세 슬라이더를
        /// 동시에 잡을 수 없어서, 이동을 키보드로 빼면 마우스가 슬라이더 전용이 된다.
        /// 안 누르고 있으면 0을 밀어야 몸이 멈춘다(스틱과 같은 held 모델).
        /// 선례: <see cref="GamePadViewModel"/>.FeedKeyboardMove.
        /// </summary>
        public void MoveByKeyboard()
        {
            var dir = UnityEngine.Vector2.zero;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed) { dir.y += 1f; }
                if (keyboard.sKey.isPressed) { dir.y -= 1f; }
                if (keyboard.dKey.isPressed) { dir.x += 1f; }
                if (keyboard.aKey.isPressed) { dir.x -= 1f; }
            }

            // 대각선으로 눌러도 빨라지지 않게 정규화한다 — 스틱은 반지름으로 클램프돼 이미 그렇다.
            Move(dir == UnityEngine.Vector2.zero ? UnityEngine.Vector2.zero : dir.normalized);
        }

        /// <summary>
        /// 자세 슬라이더. −1(완전히 왼쪽)~+1(완전히 오른쪽). 오른쪽이 다이브, 왼쪽이 패러세일이다.
        /// </summary>
        public void Posture(float slider)
        {
            input.SetPosing(true);
            input.SetGlide(slider <= -GlideThreshold);
            input.SetPosture(slider > 0f ? slider : 0f);
        }

        /// <summary>손을 떼면 대자로 돌아온다. 스카이다이빙 상태 자체는 착지 전까지 유지된다.</summary>
        public void ReleasePosture()
        {
            input.SetPosing(false);
            input.SetGlide(false);
            input.SetPosture(0f);
        }

        /// <summary>매 프레임 월드에서 읽어 화면 값을 갱신한다(연속 상태는 pull).</summary>
        /// <summary>낙하 속도선 세기(0~1) — 떨어지는 속도로 정한다(<see cref="SkydiveLookRules.SpeedLines"/>).</summary>
        public float SpeedLines { get; private set; }

        public void Refresh()
        {
            var entity = string.IsNullOrEmpty(playerContext.entityId)
                ? null
                : entityRegistry.Get(playerContext.entityId);
            if (entity == null)
            {
                return;
            }

            var stamina = entity.Get<Stamina>();
            if (stamina != null && config.StaminaMax > 0f)
            {
                staminaRatio.Value = stamina.Current / config.StaminaMax;
            }

            grounded.Value = entity.Get<GameFramework.World.GroundState>()?.IsGrounded ?? false;
            var velocity = entity.Get<GameFramework.World.Velocity>();
            SpeedLines = velocity == null ? 0f : LOP.SkydiveLookRules.SpeedLines(-velocity.Linear.Y);

            statusText.Value = Describe(entity.Get<LOP.MotionState>(),
                                        entity.Get<LOP.Posture>(),
                                        entity.Get<GameFramework.World.Velocity>())
                               + SaveSuffix(entity.Get<LOP.SkydiveSave>())
                               + CatchSuffix(entity);
        }

        private static string Describe(LOP.MotionState motion, LOP.Posture posture,
                                       GameFramework.World.Velocity velocity)
        {
            if (motion == null)
            {
                return "-";
            }

            // 아래로 갈수록 커 보이는 편이 읽기 쉬워 부호를 뒤집는다.
            float fall = velocity == null ? 0f : -velocity.Linear.Y;

            switch (motion.Value)
            {
                case LOP.SkydiveMotionState.Walking:
                    return "걷기";
                case LOP.SkydiveMotionState.Falling:
                    return $"낙하  {fall:F0}";
                default:
                    return $"{PoseName(posture)}  {fall:F0}";
            }
        }

        //  별 조각이 가까우면 "잡기!" — 결승 전에만. 별 자리는 시뮬과 같은 식(틱)으로 구한다.
        private string CatchSuffix(GameFramework.World.Entity entity)
        {
            if (catchTargets == null || catchTargets.All.Count == 0 || runner?.tickUpdater == null)
            {
                return string.Empty;
            }
            if (entity.Get<LOP.FinishState>()?.Finished ?? false)
            {
                return string.Empty;
            }
            var me = GameFramework.World.EntityMotionExtensions.GetPosition(entity);
            long tick = runner.tickUpdater.tick;
            foreach (var t in catchTargets.All)
            {
                if (UnityEngine.Vector3.Distance(LOP.CatchTargetGeometry.PositionAt(t, tick), me) <= CatchHintDistance)
                {
                    return "   ★ 별에 닿으면 끝!";
                }
            }
            return string.Empty;
        }

        //  발판 맵에서만 — 저장했으면 어디인지, 안 했으면 죽으면 출발로 간다는 것을 늘 보이게.
        private string SaveSuffix(LOP.SkydiveSave save)
        {
            if (savePads == null || savePads.Count == 0 || save == null)
            {
                return string.Empty;
            }
            return savePads.TryGetLabel(save.PadId, out string label) ? $"   저장: {label}" : "   저장 없음";
        }

        private static string PoseName(LOP.Posture posture)
        {
            if (posture == null)
            {
                return "대자";
            }
            return posture.Gliding ? "패러세일" : (posture.Axis > 0.5f ? "다이브" : "대자");
        }

        /// <summary>카메라 드래그. 화면에서 끈 거리를 그대로 넘긴다.</summary>
        public void CameraLook(UnityEngine.Vector2 delta) => cameraController.ProcessTouchInput(delta);

        /// <summary>점프. 발 딛고 있을 때만 실제로 뛴다 — 그 판정은 시뮬이 한다.</summary>
        public void Jump() => input.SetJump(true);

        /// <summary>
        /// 데스크톱 편의: Space로 점프, 방향키로 카메라. 매 프레임 호출된다.
        /// 카메라를 키로도 받는 이유 — 마우스가 하나뿐인데 패러세일이 홀드라, 활공 중에는
        /// 마우스가 슬라이더에 묶여 화면을 끌 손이 없다.
        /// </summary>
        public void PollKeyboard()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                Jump();
            }

            var look = UnityEngine.Vector2.zero;
            if (keyboard.rightArrowKey.isPressed) { look.x += 1f; }
            if (keyboard.leftArrowKey.isPressed) { look.x -= 1f; }
            if (keyboard.upArrowKey.isPressed) { look.y += 1f; }
            if (keyboard.downArrowKey.isPressed) { look.y -= 1f; }
            if (look != UnityEngine.Vector2.zero)
            {
                CameraLook(look.normalized * KeyLookSpeed);
            }
        }

        public void Dispose()
        {
            staminaRatio.Dispose();
            grounded.Dispose();
            statusText.Dispose();
        }
    }
}
