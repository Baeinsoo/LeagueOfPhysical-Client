using UnityEngine.UIElements;

namespace LOP.UI
{
    /// <summary>
    /// 로비 홈 허브 View(프론트엔드 베이스). Play 버튼을 매칭 커맨드로 전달하는 얇은 바인더.
    /// 매칭 흐름·대기 오버레이는 MatchmakingCoordinator가 담당하므로 여기선 다루지 않는다.
    /// 하단 네비바(상점/프로필/설정)의 동작 배선은 Slice C에서 추가한다.
    /// </summary>
    public class LobbyHomeView : UIView
    {
        private readonly MatchmakingViewModel _matchmaking;

        private Button _playButton;

        public LobbyHomeView(MatchmakingViewModel matchmaking)
        {
            _matchmaking = matchmaking;
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

        private void OnPlayClicked() => _matchmaking.Play();
    }
}
