using System;
using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 완주·탈락한 뒤의 조작면. 남은 사람을 골라 보고, 판을 떠난다.
    ///
    /// <para>문구 화면(<see cref="RaceFinishView"/>·<see cref="RaceEliminatedView"/>)과 나눈 이유:
    /// 그 둘은 아래 월드를 가리지 않는 순수 오버레이라 버튼을 넣으면 규칙이 깨지고,
    /// 완주와 탈락이 필요로 하는 조작이 똑같아 한 곳에 두면 중복이 없다.</para>
    ///
    /// <para>나가기는 화면 교체(큰 흐름)라 여기서 처리하지 않고 콜백으로 넘긴다 —
    /// <see cref="MatchResultView"/>와 같은 짝이다.</para>
    /// </summary>
    public class RaceSpectateView : UIView
    {
        private readonly RaceSpectateViewModel _viewModel;

        private Label _status;
        private Button _prev;
        private Button _next;
        private Button _leave;
        private IVisualElementScheduledItem _tick;
        private Action _onLeave;

        public RaceSpectateView(RaceSpectateViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public void SetLeaveCallback(Action onLeave) => _onLeave = onLeave;

        public override void OnOpen()
        {
            base.OnOpen();

            _status = Root.Q<Label>("spectate-status");
            _prev = Root.Q<Button>("spectate-prev");
            _next = Root.Q<Button>("spectate-next");
            _leave = Root.Q<Button>("spectate-leave");

            _prev.clicked += OnPrevClicked;
            _next.clicked += OnNextClicked;
            _leave.clicked += OnLeaveClicked;

            //  보는 대상은 변경 알림이 없는 샘플링 값이라 매 프레임 읽는다(RaceStartView와 같은 방식).
            _tick = Root.schedule.Execute(_ => _status.text = _viewModel.StatusText()).Every(0);
        }

        public override void OnClose()
        {
            if (_prev != null) { _prev.clicked -= OnPrevClicked; }
            if (_next != null) { _next.clicked -= OnNextClicked; }
            if (_leave != null) { _leave.clicked -= OnLeaveClicked; }

            base.OnClose();
        }

        private void OnPrevClicked() => _viewModel.Prev();
        private void OnNextClicked() => _viewModel.Next();
        private void OnLeaveClicked() => _onLeave?.Invoke();

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
