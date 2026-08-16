using GameFramework;
using R3;
using System;
using System.Collections.Generic;

namespace LOP.UI
{
    /// <summary>
    /// 매칭 기능의 프레젠테이션 어댑터. Model인 MatchStateMachine(FSM)을 주시해 매칭 진행 상태를
    /// R3 신호(IsMatching)로 노출하고, Play/Cancel 커맨드를 FSM 이벤트로 전달한다.
    /// 대기 오버레이 열고/닫기(네비게이션)는 이 VM이 아니라 MatchmakingCoordinator가 담당한다
    /// — VM은 도메인 신호만 노출한다(아키텍처: 작은 흐름=VM / 큰 흐름=코디네이터).
    /// </summary>
    public class MatchmakingViewModel : IDisposable
    {
        private readonly MatchStateMachine _matchStateMachine;
        private readonly IMatchmakingDataStore _matchmakingDataStore;
        private readonly IUserLocationService _userLocationService;

        private readonly ReactiveProperty<bool> _isMatching = new(false);
        private readonly Subject<CancellationReason> _matchmakingFailed = new();
        private readonly ReactiveProperty<int> _selectedGameModeId = new(0);

        /// <summary>매칭 진행 중 여부. 코디네이터가 구독해 대기 오버레이를 열고/닫는다.</summary>
        public ReadOnlyReactiveProperty<bool> IsMatching => _isMatching;

        /// <summary>매칭이 실패로 끝났다. 코디네이터가 구독해 안내를 띄운다(목적지는 VM이 모른다).</summary>
        public Observable<CancellationReason> MatchmakingFailed => _matchmakingFailed;

        /// <summary>고를 수 있는 게임 목록. 마스터데이터에서 오며 런타임에 변하지 않는다.</summary>
        public IReadOnlyList<GameChoice> Games { get; }

        /// <summary>지금 고른 게임(TbGameMode.id). View가 구독해 카드 강조를 갱신한다.</summary>
        public ReadOnlyReactiveProperty<int> SelectedGameModeId => _selectedGameModeId;

        public MatchmakingViewModel(
            MatchStateMachine matchStateMachine,
            IMatchmakingDataStore matchmakingDataStore,
            IUserLocationService userLocationService,
            PlayableGameProvider playableGameProvider)
        {
            _matchStateMachine = matchStateMachine;
            _matchmakingDataStore = matchmakingDataStore;
            _userLocationService = userLocationService;

            Games = playableGameProvider.Games;
            if (Games.Count > 0)
            {
                _selectedGameModeId.Value = Games[0].GameModeId;
            }
        }

        /// <summary>게임 선택 커맨드(카드 클릭). 목록에 없는 값은 무시한다.</summary>
        public void Select(int gameModeId)
        {
            if (TryFindGame(gameModeId, out _) == false)
            {
                return;
            }

            _selectedGameModeId.Value = gameModeId;
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
            //  게임과 맵은 짝으로 보내야 한다 — 서버가 "이 맵이 이 게임 소속인지"를 검사하고,
            //  어긋나면 티켓이 INVALID_MAP으로 거절된다. 그래서 목록이 게임에 맵을 미리 붙여 둔다.
            if (TryFindGame(_selectedGameModeId.Value, out var game) == false)
            {
                //  고를 수 있는 게임이 하나도 없다는 뜻 — 마스터데이터가 잘못된 것이라 조용히 넘기지 않는다.
                UnityEngine.Debug.LogError("입장 가능한 게임이 없다. TbGameMode의 씬 경로와 TbMap 연결을 확인할 것.");
                return;
            }

            _matchmakingDataStore.queueId = 1;      // TbQueue: Casual
            _matchmakingDataStore.gameModeId = game.GameModeId;
            _matchmakingDataStore.mapId = game.MapId;

            _matchStateMachine.Fire(MatchEvent.PlayClicked);
        }

        private bool TryFindGame(int gameModeId, out GameChoice found)
        {
            foreach (var game in Games)
            {
                if (game.GameModeId == gameModeId)
                {
                    found = game;
                    return true;
                }
            }

            found = default;
            return false;
        }

        /// <summary>취소 커맨드(대기 화면 취소 버튼). FSM에 CancelClicked 발행.</summary>
        public void Cancel()
        {
            _matchStateMachine.Fire(MatchEvent.CancelClicked);
        }

        private void OnStateChange(IState<MatchEvent> previous, IState<MatchEvent> current)
        {
            _isMatching.Value = current is InMatchmaking;

            //  대기를 벗어나는 순간에만 본다. FSM이 벗어나는 계기가 위치 변화 구독이므로,
            //  여기 도달했을 때 위치 값은 이미 새것(None + 사유)이다.
            if (previous is InMatchmaking && current is not InMatchmaking)
            {
                if (_userLocationService.UserLocation.CurrentValue.locationDetail is NoneLocationDetail detail
                    && detail.cancellationReason == CancellationReason.Timeout)
                {
                    _matchmakingFailed.OnNext(detail.cancellationReason);
                }
            }
        }

        public void Dispose()
        {
            _matchStateMachine.onStateChange -= OnStateChange;
            _matchStateMachine.Stop();
            _isMatching.Dispose();
            _matchmakingFailed.Dispose();
            _selectedGameModeId.Dispose();
        }
    }
}
