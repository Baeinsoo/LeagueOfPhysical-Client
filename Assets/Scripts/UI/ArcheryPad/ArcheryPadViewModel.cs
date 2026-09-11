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

        // 키 한 프레임이 손가락을 이만큼 끈 것과 같다(px). 드래그와 **같은 경로**로 넣어야
        // 감속·상하 한계각이 저절로 같아진다 — 마우스로 겨눈 것과 키보드로 겨눈 것이 달라지지 않게.
        private const float KeyLookSpeed = 8f;

        private bool drawing;

        public ArcheryPadViewModel(PlayerInputManager input, CameraController cameraController)
        {
            this.input = input;
            this.cameraController = cameraController;
        }

        /// <summary>왼쪽 영역 드래그 — 시점을 돌린다. 조준은 이 시점을 그대로 따른다.</summary>
        public void LookBy(Vector2 deltaPixels)
        {
            cameraController.ProcessTouchInput(deltaPixels);
        }

        /// <summary>
        /// 데스크톱 편의: WASD(방향키도 같이)로 시점을 돌린다. 마우스가 하나뿐인 PC에서는 왼쪽 드래그와
        /// 오른쪽 당김을 동시에 할 수 없어서, 조준을 키보드로 옮겨 마우스를 당김 전용으로 비운다.
        /// 이 게임엔 이동이 없어 WASD가 비어 있다. 매 프레임 호출된다.
        /// 선례: <see cref="SkydivePadViewModel"/>의 같은 이름 메서드.
        /// </summary>
        public void PollKeyboard()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null)
            {
                return;   // 키보드가 없는 기기(모바일)
            }

            var look = Vector2.zero;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) { look.x += 1f; }
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) { look.x -= 1f; }
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) { look.y += 1f; }
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) { look.y -= 1f; }

            if (look != Vector2.zero)
            {
                LookBy(look.normalized * KeyLookSpeed);
            }
        }

        public void BeginDraw()
        {
            drawing = true;
            input.SetDrawing(true);
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
            input.SetRelease();
        }
    }
}
