using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace LOP.UI
{
    /// <summary>프로필에 보여줄 큐 하나의 전적. 기록이 없으면 HasRecord가 false다.</summary>
    public readonly struct ProfileQueueStats
    {
        public readonly string QueueName;
        public readonly bool HasRecord;
        public readonly int Mmr;
        public readonly int GamesPlayed;
        public readonly int FirstPlaces;
        public readonly string AveragePlacement;

        public ProfileQueueStats(string queueName, bool hasRecord, int mmr, int gamesPlayed, int firstPlaces, string averagePlacement)
        {
            QueueName = queueName;
            HasRecord = hasRecord;
            Mmr = mmr;
            GamesPlayed = gamesPlayed;
            FirstPlaces = firstPlaces;
            AveragePlacement = averagePlacement;
        }
    }

    /// <summary>전적 목록의 한 판. 카드 하나에 그려진다.</summary>
    public readonly struct ProfileMatchEntry
    {
        public readonly string GameModeName;
        /// <summary>"캐주얼 · 첫번째 맵" — 어느 큐에서 어느 맵으로 했는지. 없으면 빈 문자열.</summary>
        public readonly string Subtitle;
        public readonly string EndedAt;
        public readonly string MyResult;
        public readonly bool HasMyResult;
        public readonly IReadOnlyList<MatchResultRow> Rows;

        public ProfileMatchEntry(string gameModeName, string subtitle, string endedAt, string myResult, bool hasMyResult, IReadOnlyList<MatchResultRow> rows)
        {
            GameModeName = gameModeName;
            Subtitle = subtitle;
            EndedAt = endedAt;
            MyResult = myResult;
            HasMyResult = hasMyResult;
            Rows = rows;
        }
    }

    /// <summary>
    /// 프로필 ViewModel. 열릴 때 레이팅을 다시 받아온다 — 스토어는 로그인 때 한 번만 채워져,
    /// 그대로 읽으면 판을 하고 와도 로그인 시점의 낡은 값이 뜬다.
    /// 도착 전/후가 시간에 따라 바뀌는 라이브 상태라 R3로 노출한다(결과 화면과 다른 점).
    /// </summary>
    public sealed class ProfileViewModel : IDisposable
    {
        //  한 화면에 보여줄 판 수. 서버도 상한(50)을 갖고 있어 이 값이 그대로 쓰인다.
        private const int HistoryLimit = 20;

        //  큐를 TbQueue에서 읽는 것은 로비 선택 UI 슬라이스 몫이다 — 여기도 호출부 관례대로 id를 박는다.
        private static readonly (int id, string name)[] Queues =
        {
            (1, "캐주얼"),
            (2, "랭크"),
        };

        private readonly IUserDataStore _userDataStore;
        private readonly LOP.MasterData.LOPMasterData _masterData;
        private readonly IWindowManager _windowManager;
        private readonly CancellationTokenSource _cts = new();

        private readonly ReactiveProperty<IReadOnlyList<ProfileQueueStats>> _stats = new(null);
        private readonly ReactiveProperty<string> _status = new("불러오는 중…");
        private readonly ReactiveProperty<IReadOnlyList<ProfileMatchEntry>> _matches = new(null);
        private readonly ReactiveProperty<string> _identity = new(string.Empty);

        /// <summary>도착 전에는 null.</summary>
        public ReadOnlyReactiveProperty<IReadOnlyList<ProfileQueueStats>> Stats => _stats;

        /// <summary>비어 있으면 숨긴다. 로딩·실패 안내에 쓴다.</summary>
        public ReadOnlyReactiveProperty<string> Status => _status;

        /// <summary>최근 판 목록. 도착 전에는 null.</summary>
        public ReadOnlyReactiveProperty<IReadOnlyList<ProfileMatchEntry>> Matches => _matches;

        /// <summary>화면 상단의 `이름#태그`. 개명하면 바뀌므로 라이브 상태다.</summary>
        public ReadOnlyReactiveProperty<string> Identity => _identity;

        public ProfileViewModel(IUserDataStore userDataStore, LOP.MasterData.LOPMasterData masterData, IWindowManager windowManager)
        {
            _userDataStore = userDataStore;
            _masterData = masterData;
            _windowManager = windowManager;

            RefreshIdentity();
            LoadAsync().Forget();
        }

        /// <summary>
        /// 이름 바꾸기 모달을 띄운다. 바뀌었으면 스토어를 다시 읽어 상단을 갱신한다 —
        /// 스토어는 응답 발행을 구독해 이미 채워져 있다(UserDataStore.HandleChangeDisplayName).
        /// </summary>
        public async void RequestRename()
        {
            bool changed = await _windowManager.OpenModalAsync<ChangeDisplayNameView, bool>();
            if (changed == false || _cts.IsCancellationRequested)
            {
                return;
            }

            RefreshIdentity();
        }

        private void RefreshIdentity()
        {
            var user = _userDataStore.user;
            _identity.Value = user == null || string.IsNullOrEmpty(user.displayName)
                ? string.Empty
                : $"{user.displayName}#{user.tag}";
        }

        private bool _disposed;

        public void Dispose()
        {
            //  IDisposable은 여러 번 불려도 안전해야 한다. CancellationTokenSource는 dispose된 뒤
            //  Cancel하면 ObjectDisposedException을 던지므로 가드가 필요하다.
            //  (sealed라 파생이 없어 Dispose(bool) 없이 이 형태가 표준이다.)
            if (_disposed) return;
            _disposed = true;

            //  받아오는 도중에 화면이 사라질 수 있다. 먼저 끊어야 아래에서 dispose한 프로퍼티에
            //  값을 쓰려다 터지지 않는다.
            _cts.Cancel();
            _cts.Dispose();

            _stats.Dispose();
            _status.Dispose();
            _matches.Dispose();
            _identity.Dispose();
        }

        private async UniTaskVoid LoadAsync()
        {
            string userId = _userDataStore.user?.id;
            if (string.IsNullOrEmpty(userId))
            {
                _status.Value = "전적을 불러올 수 없습니다.";
                return;
            }

            try
            {
                foreach (var queue in Queues)
                {
                    await WebAPI.GetUserRating(userId, queue.id, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                //  화면이 먼저 닫힌 것뿐이다. 프로퍼티는 이미 dispose됐으니 건드리지 않는다.
                return;
            }
            catch (Exception e)
            {
                //  받아온 게 하나도 없으면 아래 Build가 전부 "기록 없음"으로 채운다 — 그건 거짓말이라
                //  실패를 그대로 알린다. 스토어에 남아 있던 로그인 시점 값도 쓰지 않는다.
                Debug.LogError($"Failed to load user rating. Error: {e.Message}");
                if (_cts.IsCancellationRequested) return;

                _status.Value = "전적을 불러오지 못했습니다.";
                return;
            }

            if (_cts.IsCancellationRequested) return;

            _status.Value = string.Empty;
            _stats.Value = Build(_userDataStore.userRatingByQueueId);

            //  전적은 요약보다 늦게 와도 된다. 실패해도 위 요약은 이미 떠 있으므로 화면 전체를
            //  실패로 되돌리지 않는다 — 목록만 비워 둔다.
            try
            {
                var response = await WebAPI.GetMatchHistory(userId, HistoryLimit, _cts.Token);
                if (_cts.IsCancellationRequested) return;

                _matches.Value = BuildMatches(response.matches, userId);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load match history. Error: {e.Message}");
                if (_cts.IsCancellationRequested) return;

                _matches.Value = new List<ProfileMatchEntry>();
            }
        }

        private IReadOnlyList<ProfileMatchEntry> BuildMatches(MatchHistoryEntryDto[] matches, string myUserId)
        {
            var entries = new List<ProfileMatchEntry>();
            if (matches == null) return entries;

            foreach (var match in matches)
            {
                var rows = BuildHistoryRows(match.participants, myUserId);
                var mine = FindMine(match.participants, myUserId);
                //  줄이 이미 무승부인지 알고 있다. 여기서 다시 세면 둘이 어긋날 수 있다.
                bool isDraw = rows.Count > 0 && rows[0].IsDraw;

                entries.Add(new ProfileMatchEntry(
                    GameModeName(match.rounds),
                    Subtitle(match.queueId, match.rounds),
                    FormatEndedAt(match.endedAt),
                    mine == null
                        ? string.Empty
                        : $"{(isDraw ? "무승부" : $"{mine.placement}등")}  {MatchResultViewModel.FormatDelta(mine.mmrBefore, mine.mmrAfter)}",
                    mine != null,
                    rows));
            }

            return entries;
        }

        private string GameModeName(MatchHistoryRoundDto[] rounds)
        {
            //  지금은 판당 라운드가 하나뿐이라 첫 라운드의 모드가 곧 그 판의 모드다.
            if (rounds == null || rounds.Length == 0) return "알 수 없음";

            var mode = _masterData.Tables.TbGameMode.GetOrDefault(rounds[0].gameModeId);
            return mode == null ? $"모드 {rounds[0].gameModeId}" : mode.Name;
        }

        /// <summary>
        /// 큐와 맵을 한 줄로. 모드 이름만으로는 "어느 판이었나"가 안 드러난다 — 같은 게임을
        /// 캐주얼로 했는지 랭크로 했는지, 어느 맵이었는지가 전적에서 구분점이 된다.
        /// </summary>
        private string Subtitle(int queueId, MatchHistoryRoundDto[] rounds)
        {
            var parts = new List<string>(2);

            var queue = _masterData.Tables.TbQueue.GetOrDefault(queueId);
            if (queue != null) parts.Add(queue.Name);

            if (rounds != null && rounds.Length > 0)
            {
                var map = _masterData.Tables.TbMap.GetOrDefault(rounds[0].mapId);
                if (map != null) parts.Add(map.Name);
            }

            return string.Join(" · ", parts);
        }

        private static MatchHistoryParticipantDto FindMine(MatchHistoryParticipantDto[] participants, string myUserId)
        {
            if (participants == null) return null;

            foreach (var participant in participants)
            {
                if (participant.userId == myUserId) return participant;
            }

            return null;
        }

        /// <summary>서버가 준 ISO 시각을 로컬 날짜로. 못 읽으면 빈 문자열(날짜 없다고 화면이 죽지 않게).</summary>
        private static string FormatEndedAt(string endedAt)
        {
            return DateTime.TryParse(endedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed.ToLocalTime().ToString("MM/dd HH:mm", System.Globalization.CultureInfo.InvariantCulture)
                : string.Empty;
        }

        /// <summary>
        /// 결과 화면과 같은 줄 모양을 쓰되 이름은 실제 값을 쓴다 — 전적엔 확정 시점 이름이 담겨 있다
        /// (결과 화면은 그게 없어서 "플레이어 N"으로 매긴다).
        /// </summary>
        private static IReadOnlyList<MatchResultRow> BuildHistoryRows(MatchHistoryParticipantDto[] participants, string myUserId)
        {
            var rows = new List<MatchResultRow>();
            if (participants == null) return rows;

            var sorted = new List<MatchHistoryParticipantDto>(participants);
            sorted.Sort((left, right) =>
            {
                int byPlacement = left.placement.CompareTo(right.placement);
                return byPlacement != 0 ? byPlacement : string.CompareOrdinal(left.userId, right.userId);
            });

            var placements = new List<int>(sorted.Count);
            foreach (var participant in sorted) { placements.Add(participant.placement); }
            bool isDraw = MatchResultViewModel.IsDrawn(placements);

            foreach (var participant in sorted)
            {
                bool isMe = participant.userId == myUserId;
                rows.Add(new MatchResultRow(participant.placement,
                    isMe ? "나" : ShortName(participant.displayName), isMe, isDraw));
            }

            return rows;
        }

        //  전적에 박히는 값은 "이름#태그"다 — 이름 최대 12자 + '#' + 태그 6자 = 19자.
        //  그보다 짧게 자르면 태그가 잘려 **그럴듯하지만 틀린 신원**이 화면에 뜬다
        //  (예: 김철수김철수#K7QM2X → 김철수김철수#K7QM). 신원을 보여주려고 만든 화면이니
        //  자르더라도 태그는 온전해야 한다.
        private const int IdentityMaxLength = 12 + 1 + 6;

        private static string ShortName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return "알 수 없음";

            return displayName.Length <= IdentityMaxLength
                ? displayName
                : displayName.Substring(0, IdentityMaxLength);
        }

        private static IReadOnlyList<ProfileQueueStats> Build(IReadOnlyDictionary<int, UserRating> ratingByQueueId)
        {
            var stats = new List<ProfileQueueStats>(Queues.Length);

            foreach (var queue in Queues)
            {
                if (ratingByQueueId.TryGetValue(queue.id, out var rating) && rating.gamesPlayed > 0)
                {
                    //  평균 등수는 판수로 나눈다 — 판수 0이면 0으로 나누므로 위 가드가 필수다.
                    //  로캘이 쉼표 소수점이면 "3,5등"이 된다 — 표기를 고정한다.
                    string average = ((double)rating.placementSum / rating.gamesPlayed)
                        .ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

                    stats.Add(new ProfileQueueStats(
                        queue.name, true, rating.mmr, rating.gamesPlayed, rating.firstPlaces, average));
                }
                else
                {
                    stats.Add(new ProfileQueueStats(queue.name, false, 0, 0, 0, null));
                }
            }

            return stats;
        }
    }
}
