using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>내 차례 한 줄. 남은 시간이 매 프레임 변하므로 스케줄러로 갱신한다.</summary>
    public class PanchigiTurnView : UIView
    {
        private readonly PanchigiTurnViewModel _viewModel;

        private IVisualElementScheduledItem _tick;

        public PanchigiTurnView(PanchigiTurnViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            var label = Root.Q<Label>("turn-label");
            _tick = Root.schedule.Execute(_ => label.text = _viewModel.Label()).Every(0);
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
