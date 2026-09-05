using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 완주했음과 등수를 알린다.
    ///
    /// <para>Notification이 아니라 Window인 이유는 <see cref="RaceStartView"/>와 같다:
    /// 토스트가 아니라 게임 화면이라 로딩·결과 같은 전체화면 오버레이에 <b>가려져야</b> 한다.</para>
    /// </summary>
    public class RaceFinishView : UIView
    {
        private readonly RaceFinishViewModel _viewModel;

        private Label _place;
        private IVisualElementScheduledItem _tick;

        public RaceFinishView(RaceFinishViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _place = Root.Q<Label>("race-finish-place");

            //  등수는 변경 알림이 없는 샘플링 값이라 매 프레임 읽는다(RaceStartView와 같은 방식).
            _tick = Root.schedule.Execute(_ => _place.text = _viewModel.PlacementText()).Every(0);
        }

        private bool _disposed;

        protected override void Dispose(bool disposing)
        {
            if (_disposed == false)
            {
                _disposed = true;

                if (disposing)
                {
                    _tick?.Pause();
                    _tick = null;
                }
            }

            base.Dispose(disposing);
        }
    }
}
