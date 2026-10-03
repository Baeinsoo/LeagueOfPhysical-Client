using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// Dodge 조작 화면. 누른 자리가 중심이 되는 떠 있는 스틱 하나뿐이다.
    /// 좌표 해석만 하고 ViewModel로 넘긴다(SkydivePadView에서 슬라이더·점프를 뺀 것).
    /// </summary>
    public class DodgePadView : UIView
    {
        // 스틱을 끝까지 민 것으로 치는 거리(px). 손 크기 기준이라 화면 크기와 무관하다.
        private const float StickRadius = 80f;

        private readonly DodgePadViewModel _viewModel;

        private VisualElement _joystickBg;
        private VisualElement _joystickHandle;
        private int _stickPointer = -1;
        private Vector2 _stickOrigin;
        private IVisualElementScheduledItem _tick;
        private bool _disposed;

        public DodgePadView(DodgePadViewModel viewModel)
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

            var livesBox = Root.Q<VisualElement>("lives-box");
            var hearts = Root.Q<VisualElement>("hearts");
            var livesLabel = Root.Q<Label>("lives-label");
            _viewModel.LivesProperty.Subscribe(l =>
            {
                ShowHearts(hearts, l.full, l.empty);
                livesLabel.text = l.note;
                livesLabel.style.display = string.IsNullOrEmpty(l.note) ? DisplayStyle.None : DisplayStyle.Flex;
                livesBox.style.display = l.full + l.empty == 0 && string.IsNullOrEmpty(l.note) ? DisplayStyle.None : DisplayStyle.Flex;
            }).AddTo(Disposables);
            var hitFlash = Root.Q<VisualElement>("hit-flash");
            _viewModel.HitFlashProperty.Subscribe(on =>
            {
                hitFlash.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
                livesBox.EnableInClassList("lives-box--hit", on);
            }).AddTo(Disposables);

            var stageBox = Root.Q<VisualElement>("stage-box");
            var stageLabel = Root.Q<Label>("stage-label");
            var stageFill = Root.Q<VisualElement>("stage-bar-fill");
            var caption = Root.Q<Label>("caption-label");
            _viewModel.StageTextProperty.Subscribe(t =>
            {
                stageLabel.text = t;
                stageBox.style.display = string.IsNullOrEmpty(t) ? DisplayStyle.None : DisplayStyle.Flex;
            }).AddTo(Disposables);
            _viewModel.StageProgressProperty.Subscribe(p => stageFill.style.width = Length.Percent(p * 100f)).AddTo(Disposables);
            _viewModel.SuddenDeathProperty.Subscribe(on => stageFill.EnableInClassList("stage-bar-fill--sudden", on)).AddTo(Disposables);
            _viewModel.CaptionTextProperty.Subscribe(t =>
            {
                caption.text = t;
                caption.style.display = string.IsNullOrEmpty(t) ? DisplayStyle.None : DisplayStyle.Flex;
            }).AddTo(Disposables);

            var stickArea = Root.Q<VisualElement>("joystick-area");
            stickArea.RegisterCallback<PointerDownEvent>(OnStickDown);
            stickArea.RegisterCallback<PointerMoveEvent>(OnStickMove);
            stickArea.RegisterCallback<PointerUpEvent>(OnStickUp);
            stickArea.RegisterCallback<PointerCaptureOutEvent>(_ => ResetStick());

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 돈다.
            _tick = Root.schedule.Execute(_ => Tick()).Every(0);
        }

        // 개수가 바뀔 때만 하트를 더하거나 뺀다 — 매 프레임 새로 만들지 않는다.
        private static void ShowHearts(VisualElement row, int full, int empty)
        {
            int total = full + empty;
            while (row.childCount < total) row.Add(new HeartElement());
            while (row.childCount > total) row.RemoveAt(row.childCount - 1);
            for (int i = 0; i < total; i++) ((HeartElement)row[i]).Filled = i < full;
        }

        private void Tick()
        {
            _viewModel.Refresh();

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
