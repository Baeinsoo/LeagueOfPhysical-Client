using GameFramework;
using R3;
using System;

namespace LOP.UI
{
    /// <summary>
    /// 매칭 기능의 프레젠테이션 어댑터. Model인 MatchStateMachine(FSM)을 주시해 매칭 진행 상태를
    /// R3 신호(IsMatching)로 노출하고, Play/Cancel 커맨드를 FSM 이벤트로 전달한다.
    /// 대기 오버레이 열고/닫기(네비게이션)는 이 VM이 아니라 MatchmakingCoordinator가 담당한다
    /// — VM은 도메인 신호만 노출한다(아키텍처: 작은 흐름=VM / 큰 흐름=코디네이터).
    /// </summary>
    public class MatchMakingViewModel : IDisposable
    {
        private readonly MatchStateMachine _matchStateMachine;
        private readonly IMatchMakingDataStore _matchMakingDataStore;

        private readonly ReactiveProperty<bool> _isMatching = new(false);

        /// <summary>매칭 진행 중 여부. 코디네이터가 구독해 대기 오버레이를 열고/닫는다.</summary>
        public ReadOnlyReactiveProperty<bool> IsMatching => _isMatching;

        public MatchMakingViewModel(
            MatchStateMachine matchStateMachine,
            IMatchMakingDataStore matchMakingDataStore)
        {
            _matchStateMachine = matchStateMachine;
            _matchMakingDataStore = matchMakingDataStore;
        }

        /// <summary>흐름 시작. FSM 구독 + 시작(현재 위치 확인 → 적절한 상태로 진입). 코디네이터가 호출한다.</summary>
        public void StartFlow()
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

        /// <summary>취소 커맨드(대기 화면 취소 버튼). FSM에 CancelClicked 발행.</summary>
        public void Cancel()
        {
            _matchStateMachine.Fire(MatchEvent.CancelClicked);
        }

        private void OnStateChange(IState<MatchEvent> previous, IState<MatchEvent> current)
        {
            _isMatching.Value = current is InWaitingRoom;
        }

        public void Dispose()
        {
            _matchStateMachine.onStateChange -= OnStateChange;
            _matchStateMachine.Stop();
            _isMatching.Dispose();
        }
    }
}
