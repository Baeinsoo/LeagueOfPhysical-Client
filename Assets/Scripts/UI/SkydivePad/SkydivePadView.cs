using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// Skydive 조작 화면. 왼쪽은 방향 스틱, 오른쪽은 자세 슬라이더 — 둘 다 <b>누른 자리가 중립</b>인
    /// 떠 있는 컨트롤이다(화면 가장자리에서 밀 여유가 없는 문제와 엄지를 눈으로 맞추는 문제를 함께 없앤다).
    /// ViewModel 커맨드로 넘기기만 하는 얇은 바인더다.
    /// </summary>
    public class SkydivePadView : UIView
    {
        // 스틱을 끝까지 민 것으로 치는 거리(px). 화면 크기와 무관한 고정값이라 손 크기 기준이다.
        private const float StickRadius = 80f;
        private const float SliderRadius = 110f;

        private readonly SkydivePadViewModel _viewModel;

        private VisualElement _joystickBg;
        private VisualElement _joystickHandle;
        private VisualElement _postureTrack;
        private VisualElement _postureHandle;
        private VisualElement _staminaFill;
        private Label _postureLabel;

        private int _stickPointer = -1;
        private Vector2 _stickOrigin;
        private int _sliderPointer = -1;
        private Vector2 _sliderOrigin;

        private IVisualElementScheduledItem _tick;

        public SkydivePadView(SkydivePadViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _joystickBg = Root.Q<VisualElement>("joystick-bg");
            _joystickHandle = Root.Q<VisualElement>("joystick-handle");
            _postureTrack = Root.Q<VisualElement>("posture-track");
            _postureHandle = Root.Q<VisualElement>("posture-handle");
            _staminaFill = Root.Q<VisualElement>("stamina-fill");
            _postureLabel = Root.Q<Label>("posture-label");

            var stickArea = Root.Q<VisualElement>("joystick-area");
            stickArea.RegisterCallback<PointerDownEvent>(OnStickDown);
            stickArea.RegisterCallback<PointerMoveEvent>(OnStickMove);
            stickArea.RegisterCallback<PointerUpEvent>(OnStickUp);
            stickArea.RegisterCallback<PointerCaptureOutEvent>(_ => ResetStick());

            var postureArea = Root.Q<VisualElement>("posture-area");
            postureArea.RegisterCallback<PointerDownEvent>(OnSliderDown);
            postureArea.RegisterCallback<PointerMoveEvent>(OnSliderMove);
            postureArea.RegisterCallback<PointerUpEvent>(OnSliderUp);
            postureArea.RegisterCallback<PointerCaptureOutEvent>(_ => ResetSlider());

            _viewModel.StaminaRatio
                .Subscribe(ratio => _staminaFill.style.width = Length.Percent(Mathf.Clamp01(ratio) * 100f))
                .AddTo(Disposables);

            _viewModel.PostureName
                .Subscribe(name => _postureLabel.text = name)
                .AddTo(Disposables);

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 월드를 읽는다.
            _tick = Root.schedule.Execute(_ => _viewModel.Refresh()).Every(0);
        }

        private void OnStickDown(PointerDownEvent evt)
        {
            _stickPointer = evt.pointerId;
            _stickOrigin = evt.localPosition;
            Place(_joystickBg, _stickOrigin);
            Place(_joystickHandle, _stickOrigin);
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
            Place(_joystickHandle, _stickOrigin + clamped);

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
            _viewModel.Move(Vector2.zero);
        }

        private void OnSliderDown(PointerDownEvent evt)
        {
            _sliderPointer = evt.pointerId;
            _sliderOrigin = evt.localPosition;
            Place(_postureTrack, _sliderOrigin);
            Place(_postureHandle, _sliderOrigin);
            ((VisualElement)evt.currentTarget).CapturePointer(evt.pointerId);
        }

        private void OnSliderMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _sliderPointer)
            {
                return;
            }

            float dx = Mathf.Clamp(((Vector2)evt.localPosition).x - _sliderOrigin.x, -SliderRadius, SliderRadius);
            Place(_postureHandle, new Vector2(_sliderOrigin.x + dx, _sliderOrigin.y));
            _viewModel.Posture(dx / SliderRadius);
        }

        private void OnSliderUp(PointerUpEvent evt)
        {
            if (evt.pointerId == _sliderPointer)
            {
                ((VisualElement)evt.currentTarget).ReleasePointer(evt.pointerId);
                ResetSlider();
            }
        }

        private void ResetSlider()
        {
            _sliderPointer = -1;
            Place(_postureHandle, _sliderOrigin);
            _viewModel.ReleasePosture();
        }

        // 요소의 중심을 그 자리에 둔다. UXML에서 position:absolute라 left/top으로 옮긴다.
        private static void Place(VisualElement element, Vector2 center)
        {
            element.style.left = center.x - element.resolvedStyle.width * 0.5f;
            element.style.top = center.y - element.resolvedStyle.height * 0.5f;
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
                    _viewModel.Dispose();
                }
            }

            base.Dispose(disposing);
        }
    }
}
