using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 인게임 터치 입력 View. UGUI GamePad/JoyStick/CameraTouchController를 UI Toolkit 포인터 이벤트로 통합.
    /// Window 밴드 최하단에 깔려(전체화면 카메라 드래그 배경이 picking), 위 화면의 위젯이 입력을 먼저 가져간다.
    /// 입력을 ViewModel 커맨드로 포워딩하는 얇은 바인더(도메인 로직 없음). 멀티터치는 요소별 포인터 캡처로 독립.
    /// </summary>
    public class GamePadView : UIView
    {
        private const float BackgroundSize = 160f;
        private const float HandleSize = 70f;
        private const float MaxRadius = (BackgroundSize - HandleSize) / 2f;

        private readonly GamePadViewModel _viewModel;

        private VisualElement _joystickArea;
        private VisualElement _joystickBg;
        private VisualElement _joystickHandle;

        private IVisualElementScheduledItem _tick;

        private int _joystickPointerId = -1;
        private Vector2 _joystickCenter; // joystick-area 로컬 좌표 기준 중심

        public GamePadView(GamePadViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _joystickArea = Root.Q<VisualElement>("joystick-area");
            _joystickBg = Root.Q<VisualElement>("joystick-bg");
            _joystickHandle = Root.Q<VisualElement>("joystick-handle");

            _joystickBg.style.display = DisplayStyle.None;

            Root.Q<VisualElement>("camera-drag")
                .AddManipulator(new TouchZoneManipulator(_viewModel.CameraLook));

            _joystickArea.RegisterCallback<PointerDownEvent>(OnJoystickPointerDown);
            _joystickArea.RegisterCallback<PointerMoveEvent>(OnJoystickPointerMove);
            _joystickArea.RegisterCallback<PointerUpEvent>(OnJoystickPointerUp);

            Root.Q<Button>("attack-button").clicked += _viewModel.Attack;
            Root.Q<Button>("jump-button").clicked += _viewModel.Jump;
            Root.Q<Button>("dash-button").clicked += _viewModel.Dash;
            Root.Q<Button>("haste-button").clicked += _viewModel.Haste;
            Root.Q<Button>("global-attack-button").clicked += _viewModel.GlobalAttack;

            // UIView는 MonoBehaviour가 아니므로 Update 대신 패널 스케줄러로 매 프레임 틱(키보드 폴링 + 조이스틱 지속 이동).
            _tick = Root.schedule.Execute(Tick).Every(0);
        }

        private void Tick(TimerState _)
        {
            _viewModel.PollKeyboard();
            if (_joystickPointerId != -1)
            {
                _viewModel.FeedMove();         // 조이스틱 (센터=0 포함 push)
            }
            else
            {
                _viewModel.FeedKeyboardMove();  // WASD (안 누르면 0 push)
            }
        }

        private void OnJoystickPointerDown(PointerDownEvent evt)
        {
            if (_joystickPointerId != -1)
            {
                return; // 이미 다른 손가락이 조이스틱 점유 중
            }

            _joystickPointerId = evt.pointerId;
            _joystickArea.CapturePointer(evt.pointerId);

            // 누른 위치에 조이스틱 배경을 띄운다(플로팅 조이스틱).
            _joystickCenter = (Vector2)evt.localPosition;
            _joystickBg.style.display = DisplayStyle.Flex;
            _joystickBg.style.left = _joystickCenter.x - BackgroundSize / 2f;
            _joystickBg.style.top = _joystickCenter.y - BackgroundSize / 2f;

            UpdateJoystick((Vector2)evt.localPosition);
            evt.StopPropagation();
        }

        private void OnJoystickPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _joystickPointerId)
            {
                return;
            }

            UpdateJoystick((Vector2)evt.localPosition);
        }

        private void OnJoystickPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _joystickPointerId)
            {
                return;
            }

            _joystickArea.ReleasePointer(evt.pointerId);
            _joystickPointerId = -1;
            _viewModel.ClearMove();

            _joystickBg.style.display = DisplayStyle.None;
            _joystickHandle.style.left = (BackgroundSize - HandleSize) / 2f;
            _joystickHandle.style.top = (BackgroundSize - HandleSize) / 2f;
        }

        private void UpdateJoystick(Vector2 localPosition)
        {
            Vector2 delta = localPosition - _joystickCenter;
            if (delta.magnitude > MaxRadius)
            {
                delta = delta.normalized * MaxRadius;
            }

            // handle은 배경(joystick-bg) 로컬 좌표 기준. 배경 중심(BackgroundSize/2)에서 delta만큼 이동.
            _joystickHandle.style.left = BackgroundSize / 2f + delta.x - HandleSize / 2f;
            _joystickHandle.style.top = BackgroundSize / 2f + delta.y - HandleSize / 2f;

            // 원본은 단위 방향(거리 무관 일정 속도). UI Toolkit Y는 아래로 증가 → 위로 드래그 = 전진이 되도록 Y 반전.
            Vector2 dir = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.zero;
            _viewModel.SetMove(new Vector2(dir.x, -dir.y));
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;

                if (disposing)
                {
                    _tick?.Pause();
                }
            }

            base.Dispose(disposing);
        }
    }
}
