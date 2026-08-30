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
        private readonly ReactiveProperty<string> postureName = new ReactiveProperty<string>("대자");

        public ReadOnlyReactiveProperty<float> StaminaRatio => staminaRatio;
        public ReadOnlyReactiveProperty<string> PostureName => postureName;

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
        /// 자세 슬라이더. −1(완전히 왼쪽)~+1(완전히 오른쪽). 오른쪽이 다이브, 왼쪽이 패러세일이다.
        /// </summary>
        public void Posture(float slider)
        {
            input.SetGlide(slider <= -GlideThreshold);
            input.SetPosture(slider > 0f ? slider : 0f);
        }

        /// <summary>손을 떼면 대자로 돌아온다.</summary>
        public void ReleasePosture()
        {
            input.SetGlide(false);
            input.SetPosture(0f);
        }

        public void CameraLook(UnityEngine.Vector2 delta) => cameraController.ProcessTouchInput(delta);

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

            var posture = entity.Get<LOP.Posture>();
            if (posture != null)
            {
                postureName.Value = posture.Gliding ? "패러세일" : (posture.Axis > 0.5f ? "다이브" : "대자");
            }
        }

        public void Dispose()
        {
            staminaRatio.Dispose();
            postureName.Dispose();
        }
    }
}
