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

        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly IPlayerContext playerContext;
        private readonly GameFramework.World.EntityRegistry entityRegistry;
        private readonly SkydiveConfig config;

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
                                   SkydiveConfig config)
        {
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
            float yaw = cameraController.MainCamera.transform.eulerAngles.y * UnityEngine.Mathf.Deg2Rad;
            float cos = UnityEngine.Mathf.Cos(yaw);
            float sin = UnityEngine.Mathf.Sin(yaw);
            input.SetMovement(stick.x * cos + stick.y * sin, -stick.x * sin + stick.y * cos);
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

            statusText.Value = Describe(entity.Get<LOP.MotionState>(),
                                        entity.Get<LOP.Posture>(),
                                        entity.Get<GameFramework.World.Velocity>());
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

        private static string PoseName(LOP.Posture posture)
        {
            if (posture == null)
            {
                return "대자";
            }
            return posture.Gliding ? "패러세일" : (posture.Axis > 0.5f ? "다이브" : "대자");
        }

        /// <summary>점프. 발 딛고 있을 때만 실제로 뛴다 — 그 판정은 시뮬이 한다.</summary>
        public void Jump() => input.SetJump(true);

        /// <summary>데스크톱 편의: Space로도 뛴다. 매 프레임 호출된다.</summary>
        public void PollKeyboard()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                Jump();
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
