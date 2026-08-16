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
        private readonly ReactiveProperty<string> _selectedGameName = new(string.Empty);
        private readonly ReactiveProperty<string> _selectedMapName = new(string.Empty);

        private int _gameIndex;
        private int _mapIndex;

        /// <summary>매칭 진행 중 여부. 코디네이터가 구독해 대기 오버레이를 열고/닫는다.</summary>
        public ReadOnlyReactiveProperty<bool> IsMatching => _isMatching;

        /// <summary>매칭이 실패로 끝났다. 코디네이터가 구독해 안내를 띄운다(목적지는 VM이 모른다).</summary>
        public Observable<CancellationReason> MatchmakingFailed => _matchmakingFailed;

        /// <summary>고를 수 있는 게임 목록. 마스터데이터에서 오며 런타임에 변하지 않는다.</summary>
        public IReadOnlyList<GameChoice> Games { get; }

        /// <summary>지금 고른 게임 이름. View가 구독해 PLAY 버튼 안 칩 글자를 갱신한다.</summary>
        public ReadOnlyReactiveProperty<string> SelectedGameName => _selectedGameName;

        /// <summary>지금 고른 맵 이름. 게임을 넘기면 그 게임의 첫 맵으로 함께 바뀐다.</summary>
        public ReadOnlyReactiveProperty<string> SelectedMapName => _selectedMapName;

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
            RefreshSelectionNames();
        }

        /// <summary>게임 칩 커맨드 — 다음 게임으로 넘긴다. 맵은 그 게임의 첫 맵으로 돌아간다.</summary>
        public void NextGame()
        {
            if (Games.Count == 0)
            {
                return;
            }

            _gameIndex = (_gameIndex + 1) % Games.Count;

            //  게임이 바뀌면 이전 게임의 맵 번호를 그대로 쓸 수 없다 — 맵 개수가 달라 범위를 벗어난다.
            _mapIndex = 0;

            RefreshSelectionNames();
        }

        /// <summary>맵 칩 커맨드 — 지금 게임의 다음 맵으로 넘긴다. 맵이 하나뿐이면 제자리다.</summary>
        public void NextMap()
        {
            var maps = CurrentGame().Maps;
            if (maps == null || maps.Count == 0)
            {
                return;
            }

            _mapIndex = (_mapIndex + 1) % maps.Count;

            RefreshSelectionNames();
        }

        private void RefreshSelectionNames()
        {
            _selectedGameName.Value = CurrentGame().Name ?? string.Empty;
            _selectedMapName.Value = CurrentMap().Name ?? string.Empty;
        }

        private GameChoice CurrentGame()
        {
            return Games.Count > 0 ? Games[_gameIndex] : default;
        }

        private MapChoice CurrentMap()
        {
            var maps = CurrentGame().Maps;
            return maps != null && maps.Count > 0 ? maps[_mapIndex] : default;
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
            //  어긋나면 티켓이 INVALID_MAP으로 거절된다. 그래서 목록이 게임에 맵을 붙여 두고,
            //  맵 선택도 그 게임 안에서만 넘어간다.
            if (Games.Count == 0)
            {
                //  고를 수 있는 게임이 하나도 없다는 뜻 — 마스터데이터가 잘못된 것이라 조용히 넘기지 않는다.
                UnityEngine.Debug.LogError("입장 가능한 게임이 없다. TbGameMode의 씬 경로와 TbMap 연결을 확인할 것.");
                return;
            }

            _matchmakingDataStore.queueId = 1;      // TbQueue: Casual
            _matchmakingDataStore.gameModeId = CurrentGame().GameModeId;
            _matchmakingDataStore.mapId = CurrentMap().MapId;

            _matchStateMachine.Fire(MatchEvent.PlayClicked);
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
            _selectedGameName.Dispose();
            _selectedMapName.Dispose();
        }
    }
}
