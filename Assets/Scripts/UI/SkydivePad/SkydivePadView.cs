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
        private Button _jumpButton;

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

            // 누르기 전에는 떠 있는 컨트롤을 감춰 둔다.
            Hide(_joystickBg);
            Hide(_postureTrack);

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

            _jumpButton = Root.Q<Button>("jump-button");
            _jumpButton.clicked += _viewModel.Jump;
            // 떠 있는 컨트롤(visibility)과 달리 display를 쓴다 — 숨긴 동안 레이아웃에서 빠져
            // 터치도 아예 안 받는다. 이 버튼은 resolvedStyle을 읽지 않아 0폭 문제가 없다.
            _viewModel.Grounded
                .Subscribe(on => _jumpButton.style.display = on ? DisplayStyle.Flex : DisplayStyle.None)
                .AddTo(Disposables);

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 돈다.
            _tick = Root.schedule.Execute(_ => Tick()).Every(0);
        }

        private void Tick()
        {
            _viewModel.Refresh();
            _viewModel.PollKeyboard();   // Space 점프 — 버튼과 무관하게 늘 받는다

            // 스틱을 잡고 있는 동안은 키보드를 읽지 않는다 — 둘 다 밀면 나중 것이 앞의 것을 지운다.
            if (_stickPointer == -1)
            {
                _viewModel.MoveByKeyboard();
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

        private void OnSliderDown(PointerDownEvent evt)
        {
            _sliderPointer = evt.pointerId;
            _sliderOrigin = evt.localPosition;
            Show(_postureTrack);
            Place(_postureTrack, _sliderOrigin);
            CenterInParent(_postureHandle, _postureTrack, Vector2.zero);
            ((VisualElement)evt.currentTarget).CapturePointer(evt.pointerId);
        }

        private void OnSliderMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _sliderPointer)
            {
                return;
            }

            float dx = Mathf.Clamp(((Vector2)evt.localPosition).x - _sliderOrigin.x, -SliderRadius, SliderRadius);
            CenterInParent(_postureHandle, _postureTrack, new Vector2(dx, 0f));
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
            Hide(_postureTrack);
            _viewModel.ReleasePosture();
        }

        // 요소의 중심을 그 자리에 둔다. position:absolute의 left/top은 <b>부모 기준</b>이라,
        // 넘기는 좌표도 그 요소의 부모 좌표계여야 한다.
        private static void Place(VisualElement element, Vector2 center)
        {
            element.style.left = center.x - element.resolvedStyle.width * 0.5f;
            element.style.top = center.y - element.resolvedStyle.height * 0.5f;
        }

        // 손잡이는 트랙(배경)의 <b>자식</b>이다. 그래서 누른 지점(영역 좌표)을 그대로 주면 안 된다 —
        // 트랙이 이미 그 지점으로 옮겨져 있어서 좌표가 두 번 더해져 엉뚱한 데로 날아간다.
        // 손잡이는 항상 트랙 한가운데를 기준으로, 민 만큼만 벗어난다.
        private static void CenterInParent(VisualElement handle, VisualElement parent, Vector2 offset)
        {
            var center = new Vector2(parent.resolvedStyle.width * 0.5f, parent.resolvedStyle.height * 0.5f);
            Place(handle, center + offset);
        }

        // 떠 있는 컨트롤은 누르기 전에는 안 보인다 — 안 그러면 아무도 안 만진 손잡이가
        // 영역 구석에 덩그러니 놓여 "고장난 것"처럼 보인다.
        // display가 아니라 visibility를 쓴다: display:none이면 레이아웃에서 빠져 폭·높이가 0이 되고,
        // 다시 켠 그 프레임에 resolvedStyle을 읽는 배치 계산이 0을 물어 손잡이가 구석으로 튄다.
        private static void Show(VisualElement element) => element.style.visibility = Visibility.Visible;
        private static void Hide(VisualElement element) => element.style.visibility = Visibility.Hidden;

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
