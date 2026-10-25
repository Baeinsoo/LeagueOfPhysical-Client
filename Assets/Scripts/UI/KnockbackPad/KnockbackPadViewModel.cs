using UnityEngine;

namespace LOP.UI
{
    /// <summary>Knockback 조작 — 이동 하나. 스틱 값을 카메라 기준 방향으로 돌려 입력 매니저로 넘긴다.</summary>
    public class KnockbackPadViewModel : System.IDisposable
    {
        private readonly PlayerInputManager input;
        private readonly CameraController cameraController;

        public KnockbackPadViewModel(PlayerInputManager input, CameraController cameraController)
        {
            this.input = input;
            this.cameraController = cameraController;
        }

        /// <summary>방향 스틱. −1~1로 정규화된 값이 들어온다. 0을 넘겨야 몸이 선다(held 모델).</summary>
        public void Move(Vector2 stick)
        {
            var world = DodgePadViewModel.CameraRelative(stick, cameraController.MainCamera);
            input.SetMovement(world.x, world.y);
        }

        /// <summary>데스크톱용 WASD. 안 누르면 0을 밀어 선다.</summary>
        public void MoveByKeyboard()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            bool w = false, a = false, s = false, d = false;
            if (keyboard != null)
            {
                w = keyboard.wKey.isPressed;
                a = keyboard.aKey.isPressed;
                s = keyboard.sKey.isPressed;
                d = keyboard.dKey.isPressed;
            }
            Move(Combine(w, a, s, d));
        }

        /// <summary>누른 키들을 한 방향으로. 대각선도 길이 1이라 빨라지지 않는다.</summary>
        internal static Vector2 Combine(bool w, bool a, bool s, bool d)
        {
            var dir = Vector2.zero;
            if (w) dir.y += 1f;
            if (s) dir.y -= 1f;
            if (d) dir.x += 1f;
            if (a) dir.x -= 1f;
            return dir == Vector2.zero ? Vector2.zero : dir.normalized;
        }

        public void Dispose()
        {
        }
    }
}
