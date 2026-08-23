using UnityEngine;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// Flappy Race 입력 화면. 화면 전체가 입력면이고, 누르는 순간 날갯짓이 나간다
    /// (떼는 걸 기다리면 그만큼 늦게 뜬다). 같은 손가락을 끌면 카메라가 돈다.
    /// ViewModel 커맨드로 넘기기만 하는 얇은 바인더다.
    /// </summary>
    public class FlapPadView : UIView
    {
        private readonly FlapPadViewModel _viewModel;

        private VisualElement _surface;
        private IVisualElementScheduledItem _tick;

        private int _pointerId = -1;
        private Vector2 _lastPosition;   // panel 좌표

        public FlapPadView(FlapPadViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _surface = Root.Q<VisualElement>("flap-surface");
            _surface.RegisterCallback<PointerDownEvent>(OnPointerDown);
            _surface.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _surface.RegisterCallback<PointerUpEvent>(OnPointerUp);

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 키보드를 본다.
            _tick = Root.schedule.Execute(_ => _viewModel.PollKeyboard()).Every(0);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (_pointerId != -1)
            {
                return;   // 이미 다른 손가락이 잡고 있다
            }

            _pointerId = evt.pointerId;
            _surface.CapturePointer(evt.pointerId);
            _lastPosition = (Vector2)evt.position;

            _viewModel.Flap();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _pointerId)
            {
                return;
            }

            Vector2 current = (Vector2)evt.position;
            Vector2 delta = current - _lastPosition;
            _lastPosition = current;

            // panel Y는 아래로 증가 — 카메라 쪽 부호(위로 증가)에 맞춰 뒤집는다.
            _viewModel.CameraLook(new Vector2(delta.x, -delta.y));
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _pointerId)
            {
                return;
            }

            _surface.ReleasePointer(evt.pointerId);
            _pointerId = -1;
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
