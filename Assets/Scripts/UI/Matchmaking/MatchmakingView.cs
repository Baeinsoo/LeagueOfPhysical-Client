using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 로비 매치메이킹 화면 View. Play 버튼 클릭을 ViewModel 커맨드로 전달하는 얇은 바인더.
    /// 흐름 시작과 대기 오버레이 제어는 MatchmakingCoordinator가 담당하므로 여기선 다루지 않는다.
    /// </summary>
    public class MatchmakingView : UIView
    {
        private readonly MatchmakingViewModel _viewModel;

        private Button _playButton;

        public MatchmakingView(MatchmakingViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public override UILayer Layer => UILayer.Window;

        public override void OnOpen()
        {
            base.OnOpen();

            _playButton = Root.Q<Button>("play-button");
            _playButton.clicked += OnPlayClicked;
        }

        public override void OnClose()
        {
            if (_playButton != null) _playButton.clicked -= OnPlayClicked;
            base.OnClose();
        }

        private void OnPlayClicked() => _viewModel.Play();
    }
}
