namespace LOP.UI
{
    /// <summary>
    /// Dodge 조작 — 이동 하나. 스틱 값을 카메라 기준 방향으로 돌려 입력 매니저로 넘기고,
    /// 내 목숨을 서버 상태에서 읽어 보여 준다.
    /// </summary>
    public class DodgePadViewModel : System.IDisposable
    {
        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;
        private readonly DodgeClientState state;
        private readonly IGameDataStore gameDataStore;
        private readonly R3.ReactiveProperty<string> livesText = new R3.ReactiveProperty<string>("");

        public DodgePadViewModel(PlayerInputManager input, CameraController cameraController,
                                 DodgeClientState state, IGameDataStore gameDataStore)
        {
            this.input = input;
            this.cameraController = cameraController;
            this.state = state;
            this.gameDataStore = gameDataStore;
        }

        public R3.ReadOnlyReactiveProperty<string> LivesTextProperty => livesText;

        /// <summary>매 프레임 서버 상태에서 내 목숨을 읽는다(연속 상태는 pull). 몸이 사라져도 id는 userEntityId로 남는다.</summary>
        public void Refresh()
        {
            bool known = state.TryGetLife(gameDataStore.userEntityId, out int lives, out long eliminatedTick);
            livesText.Value = LivesText(known, lives, eliminatedTick);
        }

        public static string LivesText(bool known, int lives, long eliminatedTick)
        {
            if (!known) return "";
            if (eliminatedTick >= 0) return "탈락 — 관전 중";
            return "목숨 " + new string('●', System.Math.Max(0, lives));
        }

        public void Dispose() => livesText.Dispose();

        /// <summary>방향 스틱. −1~1로 정규화된 값이 들어온다. 0을 넘겨야 몸이 선다(held 모델).</summary>
        public void Move(UnityEngine.Vector2 stick)
        {
            // 식은 SkydivePadViewModel.Move와 같다 — 화면에서 위로 밀면 화면 위쪽으로 간다.
            float yAngle = cameraController.MainCamera.transform.eulerAngles.y;
            var cameraRotation = UnityEngine.Quaternion.Euler(0, yAngle, 0);
            UnityEngine.Vector3 transformed = cameraRotation * new UnityEngine.Vector3(stick.x, 0, stick.y);
            input.SetMovement(transformed.x, transformed.z);
        }

        /// <summary>데스크톱용 WASD. 안 누르면 0을 밀어 선다.</summary>
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

            // 대각선으로 눌러도 빨라지지 않게 — 스틱은 반지름으로 이미 잘린다.
            Move(dir == UnityEngine.Vector2.zero ? UnityEngine.Vector2.zero : dir.normalized);
        }
    }
}
