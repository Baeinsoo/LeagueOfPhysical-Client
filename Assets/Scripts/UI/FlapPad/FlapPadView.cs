using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// Flappy Race 입력 화면. 화면 전체가 입력면이고, 누르는 순간 날갯짓이 나간다
    /// (떼는 걸 기다리면 그만큼 늦게 뜬다). ViewModel 커맨드로 넘기기만 하는 얇은 바인더다.
    /// </summary>
    public class FlapPadView : UIView
    {
        private readonly FlapPadViewModel _viewModel;

        private VisualElement _surface;
        private IVisualElementScheduledItem _tick;

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

            // UIView는 MonoBehaviour가 아니라 Update가 없다 — 패널 스케줄러로 매 프레임 키보드를 본다.
            _tick = Root.schedule.Execute(_ => _viewModel.PollKeyboard()).Every(0);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            _viewModel.Flap();
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
