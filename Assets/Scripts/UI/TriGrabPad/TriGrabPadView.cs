using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 삼색 그랩 조작 화면. 왼쪽 반은 누른 자리가 중심이 되는 이동 스틱, 오른쪽 반은 쏘기(톡·끌기·취소).
    /// 좌표 해석만 하고 판정은 ViewModel이 한다.
    /// </summary>
    public class TriGrabPadView : UIView
    {
        private const float StickRadius = 80f;
        private const float FireHandleRadius = 60f;
        private const float NotReadyOpacity = 0.35f;

        private readonly TriGrabPadViewModel _viewModel;
        private readonly TriGrabAim _aim;

        private VisualElement _joystickBg;
        private VisualElement _joystickHandle;
        private VisualElement _stickArea;
        private VisualElement _fireArea;
        private VisualElement _fireButton;
        private VisualElement _fireHandle;
        private int _stickPointer = -1;
        private Vector2 _stickOrigin;
        private int _firePointer = -1;
        private Vector2 _fireOrigin;
        private Vector2 _fireDelta;     // 화면 px, 위 = +y
        private float _fireMaxPx;
        private IVisualElementScheduledItem _tick;
        private bool _disposed;

        public TriGrabPadView(TriGrabPadViewModel viewModel, TriGrabAim aim)
        {
            _viewModel = viewModel;
            _aim = aim;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _joystickBg = Root.Q<VisualElement>("joystick-bg");
            _joystickHandle = Root.Q<VisualElement>("joystick-handle");
            Hide(_joystickBg);

            _stickArea = Root.Q<VisualElement>("joystick-area");
            _stickArea.RegisterCallback<PointerDownEvent>(OnStickDown);
            _stickArea.RegisterCallback<PointerMoveEvent>(OnStickMove);
            _stickArea.RegisterCallback<PointerUpEvent>(OnStickUp);
            _stickArea.RegisterCallback<PointerCaptureOutEvent>(_ => ResetStick());

            _fireArea = Root.Q<VisualElement>("fire-area");
            _fireButton = Root.Q<VisualElement>("fire-button");
            _fireHandle = Root.Q<VisualElement>("fire-handle");
            _fireArea.RegisterCallback<PointerDownEvent>(OnFireDown);
            _fireArea.RegisterCallback<PointerMoveEvent>(OnFireMove);
            _fireArea.RegisterCallback<PointerUpEvent>(OnFireUp);
            _fireArea.RegisterCallback<PointerCaptureOutEvent>(_ => ResetFire());

            _tick = Root.schedule.Execute(_ => Tick()).Every(0);
        }

        private void Tick()
        {
            if (_stickPointer == -1)
            {
                _viewModel.MoveByKeyboard();
            }
            _viewModel.Apply();
            PollDesktopFire();

            bool ready = _viewModel.Ready;
            _fireButton.style.opacity = ready ? 1f : NotReadyOpacity;
            _fireButton.EnableInClassList("fire-button--ready", ready);

            _aim.Active = _firePointer != -1
                          && _viewModel.TryGetAim(_fireDelta, _fireDelta.magnitude, out _aim.Origin, out _aim.Dir);
        }

        // 에디터에선 마우스를 손가락처럼 받아 폰 조작을 그대로 해 본다. 빌드 PC에선 클릭 = 그 바닥점 쪽.
        private static bool MouseActsAsTouch => UnityEngine.Application.isEditor;

        private void PollDesktopFire()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                _viewModel.FireTap();
            }
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (!MouseActsAsTouch && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                _viewModel.FireAtGroundPoint(mouse.position.ReadValue());
            }
        }

        private void OnFireDown(PointerDownEvent evt)
        {
            if (_firePointer != -1 || !KnockbackPadViewModel.AcceptsFirePointer(evt.pointerType, MouseActsAsTouch))
            {
                return;
            }
            _firePointer = evt.pointerId;
            _fireOrigin = evt.position;
            _fireDelta = Vector2.zero;
            _fireMaxPx = 0f;
            ((VisualElement)evt.currentTarget).CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnFireMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _firePointer)
            {
                return;
            }
            Vector2 delta = (Vector2)evt.position - _fireOrigin;
            //  UI의 y는 아래가 양수라 뒤집어야 "위로 끌면 화면 위쪽"이 된다.
            _fireDelta = new Vector2(delta.x, -delta.y);
            _fireMaxPx = Mathf.Max(_fireMaxPx, delta.magnitude);
            Vector2 shown = Vector2.ClampMagnitude(delta, FireHandleRadius);
            _fireHandle.style.translate = new Translate(shown.x, shown.y);
        }

        private void OnFireUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _firePointer)
            {
                return;
            }
            //  캡처를 풀면 CaptureOut이 값을 지우므로 먼저 챙긴다.
            var outcome = TriGrabPadViewModel.OutcomeOnRelease(_fireMaxPx, _fireDelta.magnitude);
            Vector2 drag = _fireDelta;
            ((VisualElement)evt.currentTarget).ReleasePointer(evt.pointerId);
            if (outcome == TriGrabPadViewModel.ReleaseOutcome.Tap)
            {
                _viewModel.FireTap();
            }
            else if (outcome == TriGrabPadViewModel.ReleaseOutcome.Fire)
            {
                _viewModel.FireDrag(drag);
            }
            ResetFire();
        }

        private void ResetFire()
        {
            _firePointer = -1;
            _fireDelta = Vector2.zero;
            _fireMaxPx = 0f;
            _aim.Clear();
            if (_fireHandle != null)
            {
                _fireHandle.style.translate = new Translate(0, 0);
            }
        }

        private void OnStickDown(PointerDownEvent evt)
        {
            _stickPointer = evt.pointerId;
            _stickOrigin = evt.localPosition;
            Show(_joystickBg);
            Place(_joystickBg, _stickOrigin);
            CenterInParent(_joystickHandle, _joystickBg, Vector2.zero);
            ((VisualElement)evt.currentTarget).CapturePointer(evt.pointerId);
        }

        private void OnStickMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _stickPointer)
            {
                return;
            }
            Vector2 delta = (Vector2)evt.localPosition - _stickOrigin;
            Vector2 clamped = Vector2.ClampMagnitude(delta, StickRadius);
            CenterInParent(_joystickHandle, _joystickBg, clamped);
            _viewModel.Move(new Vector2(clamped.x / StickRadius, -clamped.y / StickRadius));
        }

        private void OnStickUp(PointerUpEvent evt)
        {
            if (evt.pointerId == _stickPointer)
            {
                ((VisualElement)evt.currentTarget).ReleasePointer(evt.pointerId);
                ResetStick();
            }
        }

        private void ResetStick()
        {
            _stickPointer = -1;
            Hide(_joystickBg);
            _viewModel.Move(Vector2.zero);
        }

        private static void Place(VisualElement element, Vector2 center)
        {
            element.style.left = center.x - element.resolvedStyle.width * 0.5f;
            element.style.top = center.y - element.resolvedStyle.height * 0.5f;
        }

        private static void CenterInParent(VisualElement handle, VisualElement parent, Vector2 offset)
        {
            var inner = parent.contentRect;
            Place(handle, new Vector2(inner.width * 0.5f, inner.height * 0.5f) + offset);
        }

        // display가 아니라 visibility — display:none이면 폭이 0이 돼 다시 켠 프레임에 손잡이가 구석으로 튄다.
        private static void Show(VisualElement element) => element.style.visibility = Visibility.Visible;
        private static void Hide(VisualElement element) => element.style.visibility = Visibility.Hidden;

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    _tick?.Pause();
                    _aim.Clear();
                    _viewModel.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
