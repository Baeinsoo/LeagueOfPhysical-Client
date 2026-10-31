using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// Knockback 조작 화면. 누른 자리가 중심이 되는 떠 있는 스틱 하나뿐이다.
    /// 좌표 해석만 하고 ViewModel로 넘긴다(DodgePadView에서 하트·자막을 뺀 것).
    /// </summary>
    public class KnockbackPadView : UIView
    {
        // 스틱을 끝까지 민 것으로 치는 거리(px). 손 크기 기준이라 화면 크기와 무관하다.
        private const float StickRadius = 80f;
        // 발사 버튼을 끌 때 끝까지 끈 것으로 치는 거리(px).
        private const float FireDragRadius = 90f;

        private readonly KnockbackPadViewModel _viewModel;

        private VisualElement _joystickBg;
        private VisualElement _joystickHandle;
        private VisualElement _fireButton;
        private VisualElement _fireHandle;
        private VisualElement _stickArea;
        private readonly List<VisualElement> _ammoFills = new List<VisualElement>();
        private int _firePointer = -1;
        private Vector2 _fireOrigin;
        private Vector2 _fireDrag;      // 마지막 끌기 벡터(화면 기준 위 = +y, 버튼 반경 대비 −1~1)
        private float _fireMaxDrag01;
        private float _fireDownTime;
        private int _stickPointer = -1;
        private Vector2 _stickOrigin;
        private IVisualElementScheduledItem _tick;
        private bool _disposed;

        public KnockbackPadView(KnockbackPadViewModel viewModel)
        {
            _viewModel = viewModel;
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

            _fireButton = Root.Q<VisualElement>("fire-button");
            _fireHandle = Root.Q<VisualElement>("fire-handle");
            _fireButton.RegisterCallback<PointerDownEvent>(OnFireDown);
            _fireButton.RegisterCallback<PointerMoveEvent>(OnFireMove);
            _fireButton.RegisterCallback<PointerUpEvent>(OnFireUp);
            _fireButton.RegisterCallback<PointerCaptureOutEvent>(_ => ResetFire());
            BuildAmmoBar();

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 돈다.
            _tick = Root.schedule.Execute(_ => Tick()).Every(0);
        }

        private void Tick()
        {
            // 스틱을 잡고 있는 동안은 키보드를 읽지 않는다 — 둘 다 밀면 나중 것이 앞의 것을 지운다.
            if (_stickPointer == -1)
            {
                _viewModel.MoveByKeyboard();
            }

            PollDesktopFire();
            UpdateAmmoBar();
        }

        // PC: 스페이스 = 자동 조준, 마우스 왼쪽 = 마우스 바닥점 방향. 버튼·스틱 위의 클릭은 그쪽 조작이라 거른다.
        private void PollDesktopFire()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
            {
                _viewModel.FireAuto();
            }

            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && !IsOverPad(mouse.position.ReadValue()))
            {
                _viewModel.FireAtGroundPoint(mouse.position.ReadValue());
            }
        }

        private bool IsOverPad(Vector2 screen)
        {
            var layout = Root.layout;
            if (layout.width <= 0f || Screen.width <= 0)
            {
                return false;
            }
            var panel = new Vector2(screen.x * layout.width / Screen.width, (Screen.height - screen.y) * layout.height / Screen.height);
            return _fireButton.worldBound.Contains(panel) || _stickArea.worldBound.Contains(panel);
        }

        private void BuildAmmoBar()
        {
            var bar = Root.Q<VisualElement>("ammo-bar");
            for (int i = 0; i < _viewModel.AmmoMax; i++)
            {
                var cell = new VisualElement();
                cell.AddToClassList("ammo-cell");
                cell.style.flexGrow = 1;
                cell.style.marginLeft = i == 0 ? 0 : 4;
                var fill = new VisualElement();
                fill.AddToClassList("ammo-fill");
                cell.Add(fill);
                bar.Add(cell);
                _ammoFills.Add(fill);
            }
        }

        // 찬 칸은 가득, 충전 중인 칸(다음 칸)은 진행만큼, 나머지는 빈 칸.
        private void UpdateAmmoBar()
        {
            int ammo = _viewModel.Ammo;
            float progress = _viewModel.ReloadProgress01;
            for (int i = 0; i < _ammoFills.Count; i++)
            {
                float fill = i < ammo ? 1f : i == ammo ? progress : 0f;
                _ammoFills[i].style.width = Length.Percent(fill * 100f);
            }
        }

        private void OnFireDown(PointerDownEvent evt)
        {
            if (_firePointer != -1)
            {
                return;
            }
            _firePointer = evt.pointerId;
            _fireOrigin = evt.position;
            _fireDrag = Vector2.zero;
            _fireMaxDrag01 = 0f;
            _fireDownTime = Time.unscaledTime;
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
            Vector2 clamped = Vector2.ClampMagnitude(delta, FireDragRadius);
            // UI의 y는 아래가 양수라 뒤집어야 "위로 끌면 화면 위쪽"이 된다.
            _fireDrag = new Vector2(clamped.x, -clamped.y) / FireDragRadius;
            _fireMaxDrag01 = Mathf.Max(_fireMaxDrag01, _fireDrag.magnitude);
            _fireHandle.style.translate = new Translate(clamped.x * 0.5f, clamped.y * 0.5f);
        }

        private void OnFireUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _firePointer)
            {
                return;
            }
            // 놓는 순간의 값을 먼저 챙긴다 — 캡처를 풀면 CaptureOut이 ResetFire로 값을 지운다.
            var kind = KnockbackPadViewModel.Classify(Time.unscaledTime - _fireDownTime, _fireMaxDrag01);
            Vector2 drag = _fireDrag;
            ((VisualElement)evt.currentTarget).ReleasePointer(evt.pointerId);

            if (kind == KnockbackPadViewModel.TapOrDrag.Tap)
            {
                _viewModel.FireAuto();
            }
            else if (kind == KnockbackPadViewModel.TapOrDrag.Drag && drag.magnitude >= KnockbackPadViewModel.DragThreshold)
            {
                // 끌다가 가운데로 돌아와 놓으면(위 조건이 거짓) 취소다.
                _viewModel.FireTowardScreenDirection(drag);
            }
            ResetFire();
        }

        private void ResetFire()
        {
            _firePointer = -1;
            _fireDrag = Vector2.zero;
            _fireMaxDrag01 = 0f;
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

            // UI의 y는 아래가 양수라 뒤집어야 "위로 밀면 앞으로"가 된다.
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

        // left/top은 부모 기준이라 넘기는 좌표도 부모 좌표계여야 한다.
        private static void Place(VisualElement element, Vector2 center)
        {
            element.style.left = center.x - element.resolvedStyle.width * 0.5f;
            element.style.top = center.y - element.resolvedStyle.height * 0.5f;
        }

        // 손잡이는 배경의 자식이라 배경 안쪽(contentRect) 한가운데를 기준으로 민 만큼만 벗어난다.
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
                    // 스틱을 잡은 채 창이 닫혀도 걷던 입력이 남지 않게.
                    _viewModel.Move(Vector2.zero);
                    _viewModel.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
