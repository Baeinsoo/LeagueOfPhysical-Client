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

        // 키 한 프레임이 손가락을 이만큼 끈 것과 같다(px). 드래그와 **같은 경로**로 넣어야
        // 감속·상하 한계각이 저절로 같아진다 — 마우스로 겨눈 것과 키보드로 겨눈 것이 달라지지 않게.
        private const float KeyLookSpeed = 8f;

        private bool drawing;

        public ArcheryPadViewModel(
            PlayerInputManager input,
            CameraController cameraController,
            GameFramework.World.EntityRegistry entityRegistry,
            IPlayerContext playerContext)
        {
            this.input = input;
            this.cameraController = cameraController;
            this.entityRegistry = entityRegistry;
            this.playerContext = playerContext;
        }

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

        /// <summary>지금 시위를 당기고 있나. 조준점을 띄울지 정한다 — 시뮬 상태를 읽는다(화면이 따로 세지 않는다).</summary>
        public bool Drawing
        {
            get
            {
                var entity = entityRegistry.Get(playerContext.entityId);
                return entity?.Get<ArcheryAim>()?.Drawing ?? false;
            }
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

        /// <summary>
        /// 이 거리만큼 끌면 완전히 당긴 것이다(화면 짧은 변 대비 비율). 엄지로 한 번에 끌 수 있는
        /// 만큼으로 잡는다 — 손을 옮겨 짚어야 하는 거리면 조작이 아니라 곡예가 된다.
        /// </summary>
        private const float FullDrawDragFraction = 0.22f;

        private Vector2 drawOrigin;
        private Vector2 drawCurrent;

        /// <summary>지금 끌고 있는 정도(0~1). 화면이 시위와 조준선을 이 값으로 그린다.</summary>
        public float DrawRatio { get; private set; }

        /// <summary>임계치를 넘겨 시위가 실제로 걸렸나. 못 넘으면 떼도 안 쏜다.</summary>
        public bool DrawArmed => DrawRatio >= ArcheryAimSystem.DrawThreshold;

        /// <summary>손가락을 처음 댄 자리(화면 좌표). 게이지가 여기 그려진다.</summary>
        public Vector2 DrawOrigin => drawOrigin;

        /// <summary>지금 손가락 자리(화면 좌표).</summary>
        public Vector2 DrawCurrent => drawCurrent;

        /// <summary>완전히 당기는 데 필요한 거리(픽셀). 게이지 바깥 원의 반지름이다.</summary>
        public float FullDrawPixels => Mathf.Min(Screen.width, Screen.height) * FullDrawDragFraction;

        /// <summary>손가락을 댄 자리를 기억한다. 여기서부터 끈 거리가 곧 당김이다.</summary>
        public void BeginDraw(Vector2 position)
        {
            drawing = true;
            drawOrigin = position;
            drawCurrent = position;
            DrawRatio = 0f;
            input.SetDrawing(true);
            input.SetDrawRatio(0f);
        }

        /// <summary>끌고 있는 동안 매번. 댄 자리에서 멀어진 만큼이 당김이다.</summary>
        public void DragDraw(Vector2 position)
        {
            if (drawing == false)
            {
                return;
            }
            drawCurrent = position;
            //  화면 짧은 변으로 나눈다 — 해상도가 달라도 같은 손동작이면 같은 값이 된다.
            float unit = FullDrawPixels;
            DrawRatio = unit > 0f ? Mathf.Clamp01(Vector2.Distance(position, drawOrigin) / unit) : 0f;
            input.SetDrawRatio(DrawRatio);
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
            //  임계치를 못 넘었으면 취소다 — 쏘라는 신호를 아예 안 보낸다.
            if (DrawRatio >= ArcheryAimSystem.DrawThreshold)
            {
                input.SetRelease();
            }
            DrawRatio = 0f;
        }
    }
}
