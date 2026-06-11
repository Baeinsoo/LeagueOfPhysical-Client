using GameFramework;
using System;
using VContainer;

namespace LOP.UI
{
    /// <summary>
    /// 매칭 기능의 프레젠테이션 담당. Model인 <see cref="MatchStateMachine"/>(FSM)을 주시해 매칭 진행 상태
    /// (<see cref="IsMatching"/>)를 도출하고, Play/Cancel 커맨드를 FSM 이벤트로 전달한다.
    ///
    /// 매칭 대기 화면은 전역 <see cref="IWindowManager"/>가 관리하는 팝업이다. 트리 안 요소가 아니라
    /// 매니저가 인스턴스를 만들어 끼워 넣어야 존재하므로, "값에 display를 바인딩하면 자동으로 보인다"는
    /// 선언적 방식이 성립하지 않는다. 그래서 값이 바뀌는 <see cref="IsMatching"/> setter에서 VM이 직접
    /// Open/Close를 호출한다(다이얼로그/내비게이션 서비스 패턴 — 전역 뷰라 VM이 직접 호출).
    ///
    /// 매칭은 다른 씬에서도 호출될 수 있어, 오버레이 제어를 특정 씬 View가 아니라 흐름(FSM)과 같은
    /// 수명을 갖는 이 VM에 둔다. 나중에 FSM 등록을 상위 스코프로 올리면 이 VM도 함께 따라 올라간다.
    /// </summary>
    public class MatchMakingViewModel : IDisposable
    {
        private readonly MatchStateMachine _matchStateMachine;
        private readonly IMatchMakingDataStore _matchMakingDataStore;
        private readonly IWindowManager _windowManager;

        private MatchingWaitingView _waitingView;
        private bool _isMatching;

        public MatchMakingViewModel(
            MatchStateMachine matchStateMachine,
            IMatchMakingDataStore matchMakingDataStore,
            IWindowManager windowManager)
        {
            _matchStateMachine = matchStateMachine;
            _matchMakingDataStore = matchMakingDataStore;
            _windowManager = windowManager;
        }

        /// <summary>
        /// 매칭 진행 중 여부. 전역 매니저가 관리하는 팝업이라 값 바인딩으로 자동 표시가 안 되므로,
        /// 값이 바뀌는 이 setter에서 대기 화면을 직접 열고/닫는다.
        /// </summary>
        public bool IsMatching
        {
            get => _isMatching;
            private set
            {
                if (_isMatching == value)
                {
                    return;
                }

                _isMatching = value;

                if (value)
                {
                    _waitingView = _windowManager.Open<MatchingWaitingView>();
                    _waitingView.SetCancelCallback(Cancel);
                }
                else if (_waitingView != null)
                {
                    _windowManager.Close(_waitingView);
                    _waitingView = null;
                }
            }
        }

        /// <summary>로비 화면 표시 시작 시 호출. FSM 구독 + 시작(현재 위치 확인 → 적절한 상태로 진입).</summary>
        public void Start()
        {
            _matchStateMachine.onStateChange += OnStateChange;
            _matchStateMachine.Start();
        }

        /// <summary>Play 버튼 커맨드. 매칭 파라미터 세팅 후 FSM에 PlayClicked 발행.</summary>
        public void Play()
        {
            _matchMakingDataStore.matchType = GameMode.Normal;
            _matchMakingDataStore.subGameId = "FlapWang";
            _matchMakingDataStore.mapId = "FlapWangMap";

            _matchStateMachine.Fire(MatchEvent.PlayClicked);
        }

        /// <summary>취소 커맨드(대기 화면 취소 버튼). FSM에 CancelClicked 발행 → 상태 이탈 → 대기 화면 닫힘.</summary>
        public void Cancel()
        {
            _matchStateMachine.Fire(MatchEvent.CancelClicked);
        }

        private void OnStateChange(IState<MatchEvent> previous, IState<MatchEvent> current)
        {
            IsMatching = current is InWaitingRoom;
        }

        public void Dispose()
        {
            _matchStateMachine.onStateChange -= OnStateChange;
            _matchStateMachine.Stop();

            if (_waitingView != null)
            {
                _windowManager.Close(_waitingView);
                _waitingView = null;
            }
        }
    }
}
