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
