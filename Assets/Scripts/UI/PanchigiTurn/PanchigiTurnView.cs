using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>내 차례와 판 상황 두 줄. 남은 시간·뒤집힌 개수가 매 프레임 변하므로 스케줄러로 갱신한다.</summary>
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

            var turnLabel = Root.Q<Label>("turn-label");
            var flipLabel = Root.Q<Label>("flip-label");
            var dropOutLabel = Root.Q<Label>("dropout-label");
            _tick = Root.schedule.Execute(_ =>
            {
                turnLabel.text = _viewModel.Label();
                flipLabel.text = _viewModel.FlipLabel();
                dropOutLabel.text = _viewModel.DropOutLabel();
            }).Every(0);
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
