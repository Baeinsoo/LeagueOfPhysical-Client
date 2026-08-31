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
        private readonly LastPlayedSelectionStore _lastPlayed;

        private readonly ReactiveProperty<bool> _isMatching = new(false);
        private readonly Subject<CancellationReason> _matchmakingFailed = new();
        private readonly ReactiveProperty<int> _selectedGameIndex = new(0);
        private readonly ReactiveProperty<int> _selectedMapIndex = new(0);

        /// <summary>매칭 진행 중 여부. 코디네이터가 구독해 대기 오버레이를 열고/닫는다.</summary>
        public ReadOnlyReactiveProperty<bool> IsMatching => _isMatching;

        /// <summary>매칭이 실패로 끝났다. 코디네이터가 구독해 안내를 띄운다(목적지는 VM이 모른다).</summary>
        public Observable<CancellationReason> MatchmakingFailed => _matchmakingFailed;

        /// <summary>고를 수 있는 게임 목록. 마스터데이터에서 오며 런타임에 변하지 않는다.</summary>
        public IReadOnlyList<GameChoice> Games { get; }

        /// <summary>지금 고른 게임의 <see cref="Games"/> 안 위치. View의 드롭다운이 이 값을 따라간다.</summary>
        public ReadOnlyReactiveProperty<int> SelectedGameIndex => _selectedGameIndex;

        /// <summary>지금 고른 맵의 <see cref="CurrentMaps"/> 안 위치.</summary>
        public ReadOnlyReactiveProperty<int> SelectedMapIndex => _selectedMapIndex;

        /// <summary>지금 고른 게임의 맵들. 게임이 바뀌면 내용이 바뀌므로 View가 목록을 다시 채운다.</summary>
        public IReadOnlyList<MapChoice> CurrentMaps => CurrentGame().Maps ?? System.Array.Empty<MapChoice>();

        public MatchmakingViewModel(
            MatchStateMachine matchStateMachine,
            IMatchmakingDataStore matchmakingDataStore,
            IUserLocationService userLocationService,
            PlayableGameProvider playableGameProvider,
            LastPlayedSelectionStore lastPlayed)
        {
            _matchStateMachine = matchStateMachine;
            _matchmakingDataStore = matchmakingDataStore;
            _userLocationService = userLocationService;
            _lastPlayed = lastPlayed;

            Games = playableGameProvider.Games;

            RestoreLastPlayed();
        }

        /// <summary>
        /// 마지막으로 플레이한 게임·맵을 골라 둔다. 저장된 것이 없거나 그 사이 마스터데이터에서
        /// 사라졌으면 아무 일도 하지 않는다 — 기본값(첫 항목)이 그대로 남는다.
        /// </summary>
        private void RestoreLastPlayed()
        {
            if (_lastPlayed.TryLoad(out int gameModeId, out int mapId) == false)
            {
                return;
            }

            for (int g = 0; g < Games.Count; g++)
            {
                if (Games[g].GameModeId != gameModeId)
                {
                    continue;
                }

                var maps = Games[g].Maps;
                for (int m = 0; m < maps.Count; m++)
                {
                    if (maps[m].MapId != mapId)
                    {
                        continue;
                    }

                    //  맵까지 찾은 뒤에야 게임을 바꾼다. 게임만 먼저 정하고 맵을 못 찾으면
                    //  "저장해 둔 게임 + 엉뚱한 첫 맵"이라는 어중간한 조합이 남는다.
                    _selectedGameIndex.Value = g;
                    _selectedMapIndex.Value = m;
                    return;
                }
                return;   // 그 게임에 그 맵이 없다 — 조합이 깨졌으므로 통째로 기본값을 쓴다
            }
        }

        /// <summary>게임 드롭다운 커맨드. 맵은 그 게임의 첫 맵으로 돌아간다.</summary>
        public void SelectGame(int index)
        {
            if (index < 0 || index >= Games.Count)
            {
                return;
            }

            _selectedGameIndex.Value = index;

            //  게임이 바뀌면 이전 게임의 맵 번호를 그대로 쓸 수 없다 — 맵 개수가 달라 범위를 벗어난다.
            _selectedMapIndex.Value = 0;
        }

        /// <summary>맵 드롭다운 커맨드. 지금 고른 게임의 맵 중에서만 고른다.</summary>
        public void SelectMap(int index)
        {
            if (index < 0 || index >= CurrentMaps.Count)
            {
                return;
            }

            _selectedMapIndex.Value = index;
        }

        private GameChoice CurrentGame()
        {
            return Games.Count > 0 && _selectedGameIndex.Value < Games.Count
                ? Games[_selectedGameIndex.Value]
                : default;
        }

        private MapChoice CurrentMap()
        {
            var maps = CurrentMaps;
            return maps.Count > 0 && _selectedMapIndex.Value < maps.Count
                ? maps[_selectedMapIndex.Value]
                : default;
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

            //  고른 순간이 아니라 실제로 플레이한 것만 기억한다 — 드롭다운을 뒤적이다 만 것까지
            //  남으면 "마지막에 한 게임"이 아니게 된다.
            _lastPlayed.Save(_matchmakingDataStore.gameModeId, _matchmakingDataStore.mapId);

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
                //  사유가 붙어 있으면 안내한다. 유저가 직접 취소한 것(User)은 자기가 아는 일이라 뺀다.
                //  사유가 없는 것(None)은 자가치유로 풀린 경우다 — 서버도 왜인지 모르니 할 말이 없다.
                if (_userLocationService.UserLocation.CurrentValue.locationDetail is NoneLocationDetail detail
                    && detail.cancellationReason != CancellationReason.None
                    && detail.cancellationReason != CancellationReason.User)
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
            _selectedGameIndex.Dispose();
            _selectedMapIndex.Dispose();
        }
    }
}
