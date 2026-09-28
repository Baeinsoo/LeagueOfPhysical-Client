namespace LOP.UI
{
    /// <summary>Dodge 조작 — 이동 하나. 스틱 값을 카메라 기준 방향으로 돌려 입력 매니저로 넘긴다.</summary>
    public class DodgePadViewModel
    {
        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;

        public DodgePadViewModel(PlayerInputManager input, CameraController cameraController)
        {
            this.input = input;
            this.cameraController = cameraController;
        }

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
