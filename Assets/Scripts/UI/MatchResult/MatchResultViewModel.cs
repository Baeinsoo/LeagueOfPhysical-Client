using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace LOP.UI
{
    /// <summary>등수표의 한 줄. 결과 화면과 프로필 전적이 같은 줄 구조를 쓴다.</summary>
    public readonly struct MatchResultRow
    {
        public readonly int Placement;
        public readonly string DisplayName;
        public readonly bool IsMe;

        //  이 판이 무승부였나. 화면에 어떻게 적을지는 여기서 정하지 않는다 —
        //  그리는 쪽이 공용 표기 함수를 거쳐 정한다.
        public readonly bool IsDraw;

        //  이 판이 점수 개념을 쓰는 모드였나. 플랩왕·스카이다이브·판치기처럼 점수가 없는 모드는
        //  자루가 비어서 오므로 false다 — "점수 없음"과 "0점"은 다른 사실이라 갈라 둔다.
        public readonly bool HasScore;
        public readonly int Score;
        public readonly int Gained;
        public readonly int Lost;

        //  판 도중 나가 끝까지 안 돌아왔다 — 그래서 꼴찌다. 화면이 "나감"이라고 알린다(서버가 자루에 싣는 사실).
        public readonly bool IsLeft;

        public MatchResultRow(int placement, string displayName, bool isMe, bool isDraw = false)
            : this(placement, displayName, isMe, isDraw, hasScore: false, score: 0, gained: 0, lost: 0)
        {
        }

        public MatchResultRow(int placement, string displayName, bool isMe, bool isDraw,
            bool hasScore, int score, int gained, int lost, bool isLeft = false)
        {
            IsLeft = isLeft;
            Placement = placement;
            DisplayName = displayName;
            IsMe = isMe;
            IsDraw = isDraw;
            HasScore = hasScore;
            Score = score;
            Gained = gained;
            Lost = lost;
        }
    }

    /// <summary>
    /// 결과 화면 ViewModel. 스토어에 남은 직전 매치 결과를 표시용 줄 목록으로 바꾸고, 랭크 판이면
    /// 로비에 그 매치의 내 결과를 물어 티어·LP 줄을 만든다(게임 서버 메시지엔 랭크가 없다).
    /// 캐주얼은 숨은 점수를 보여 주지 않는다 — 롤 일반 게임처럼 숫자가 어디에도 안 나온다.
    /// 등수는 열릴 때 한 번 정해지고, 랭크 줄만 도착이 늦어 R3로 노출한다.
    /// </summary>
    public class MatchResultViewModel : IDisposable
    {
        private const string MyName = "나";

        private readonly ReactiveProperty<string> _rankLine = new(string.Empty);
        private readonly ReactiveProperty<string> _rewardLine = new(string.Empty);
        private readonly CancellationTokenSource _cts = new();

        public IReadOnlyList<MatchResultRow> Rows { get; }

        /// <summary>1등이 여럿이면 아무도 이긴 게 아니다 — 화면이 등수 대신 그렇게 말해야 한다.</summary>
        public bool IsDraw { get; }

        /// <summary>랭크 판의 "골드 II 45 → 63 LP (+18)". 캐주얼이거나 아직·못 받았으면 빈 문자열(줄을 숨긴다).</summary>
        public ReadOnlyReactiveProperty<string> RankLine => _rankLine;

        /// <summary>"+50 코인 · +120 XP" 같은 보상 줄. 캐주얼·랭크 모두 뜬다. 보상이 없거나 아직·못 받았으면
        /// 빈 문자열(줄을 숨긴다). 랭크 줄과 같은 조회(GetMyMatch)에서 함께 채워진다 — 게임서버 메시지엔 보상이 없다.</summary>
        public ReadOnlyReactiveProperty<string> RewardLine => _rewardLine;

        [VContainer.Inject]
        public MatchResultViewModel(IMatchResultDataStore matchResultDataStore, IUserDataStore userDataStore, LOP.MasterData.LOPMasterData masterData)
            : this(matchResultDataStore, userDataStore, masterData.Tables.TbQueue, masterData.Tables.TbRankDivision, WebAPI.GetMyMatch)
        {
        }

        /// <param name="fetchMatch">(userId, matchId, ct) → 그 매치의 내 결과. 시험이 가짜를 꽂는다.</param>
        public MatchResultViewModel(IMatchResultDataStore matchResultDataStore, IUserDataStore userDataStore,
            LOP.MasterData.TbQueue queues, LOP.MasterData.TbRankDivision divisions,
            Func<string, string, CancellationToken, UniTask<GetMyMatchResponse>> fetchMatch)
        {
            var result = matchResultDataStore.result;
            string myUserId = userDataStore.user?.id;

            Rows = BuildRows(result?.participants, myUserId);
            IsDraw = Rows.Count > 0 && Rows[0].IsDraw;

            LoadMatchAsync(result?.matchId, myUserId, queues, divisions, fetchMatch).Forget();
        }

        //  랭크 줄과 보상 줄은 같은 조회(GetMyMatch) 한 번으로 채운다 — 랭크 큐 여부와 무관하게
        //  보상은 항상 찾는다(캐주얼도 보상이 있다), 랭크 줄만 랭크 큐일 때 채운다.
        private async UniTaskVoid LoadMatchAsync(string matchId, string myUserId,
            LOP.MasterData.TbQueue queues, LOP.MasterData.TbRankDivision divisions,
            Func<string, string, CancellationToken, UniTask<GetMyMatchResponse>> fetchMatch)
        {
            if (string.IsNullOrEmpty(matchId) || string.IsNullOrEmpty(myUserId)) return;

            try
            {
                var response = await fetchMatch(myUserId, matchId, _cts.Token);
                if (_cts.IsCancellationRequested || response?.match == null) return;

                bool showRank = queues.GetOrDefault(response.match.queueId)?.HasVisibleRank == true;

                foreach (var p in response.match.participants ?? Array.Empty<MatchHistoryParticipantDto>())
                {
                    if (p.userId != myUserId) continue;

                    if (showRank && p.rank != null)
                    {
                        _rankLine.Value = RankFormat.ResultLine(p.rank, divisions);
                    }

                    _rewardLine.Value = RewardFormat.Line(p.reward);
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                //  화면이 먼저 닫혔다 — 쓸 곳이 없다.
            }
            catch (Exception e)
            {
                //  못 받으면 줄을 숨긴 채 둔다. 등수표는 이미 떠 있다.
                UnityEngine.Debug.LogWarning($"Failed to load match result. matchId: {matchId}, error: {e.Message}");
            }
        }

        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
            _rankLine.Dispose();
            _rewardLine.Dispose();
        }

        /// <summary>서버가 자루에 "나감" 표시를 실었나.</summary>
        public static bool IsLeft(System.Collections.Generic.IReadOnlyDictionary<string, int> stats) =>
            stats != null && stats.TryGetValue(MatchStatKeys.Left, out int left) && left != 0;

        /// <summary>
        /// 등수 오름차순으로 정렬해 줄을 만든다. 본인은 "나", 나머지는 정렬 순서대로 "플레이어 1·2…".
        /// 닉네임 개념이 아직 없어 userId를 그대로 띄우지 않기 위한 표기다.
        /// </summary>
        private static IReadOnlyList<MatchResultRow> BuildRows(MatchParticipantResult[] participants, string myUserId)
        {
            var rows = new List<MatchResultRow>();

            //  보고가 실패한 판은 등수가 없다. 화면이 빈 목록을 보고 "매치 종료"로 물러선다.
            if (participants == null || participants.Length == 0)
            {
                return rows;
            }

            var sorted = new List<MatchParticipantResult>(participants);

            //  동점끼리의 순서가 실행마다 흔들리지 않게 userId로 갈라 준다(서수 비교 = 바이트 순).
            sorted.Sort((left, right) =>
            {
                int byPlacement = left.placement.CompareTo(right.placement);
                return byPlacement != 0
                    ? byPlacement
                    : string.CompareOrdinal(left.userId, right.userId);
            });

            var placements = new List<int>(sorted.Count);
            foreach (var participant in sorted) { placements.Add(participant.placement); }
            bool isDraw = IsDrawn(placements);

            int otherNumber = 0;
            foreach (var participant in sorted)
            {
                bool isMe = participant.userId == myUserId;
                string displayName = isMe ? MyName : $"플레이어 {++otherNumber}";
                var (hasScore, score, gained, lost) = ExtractScore(participant.stats);

                rows.Add(new MatchResultRow(participant.placement, displayName, isMe, isDraw,
                    hasScore, score, gained, lost, IsLeft(participant.stats)));
            }

            return rows;
        }

        /// <summary>
        /// 자루에서 점수 3종을 뽑는다. 자루가 비었거나(점수 없는 모드) 점수 키가 없으면 점수 없음으로
        /// 판정한다 — 결과 화면·프로필 전적이 같은 판정을 쓰게 여기 한 곳에 둔다.
        /// </summary>
        public static (bool hasScore, int score, int gained, int lost) ExtractScore(IReadOnlyDictionary<string, int> stats)
        {
            if (stats == null || !stats.TryGetValue(ArcheryStatKeys.Score, out int score))
            {
                return (false, 0, 0, 0);
            }

            stats.TryGetValue(ArcheryStatKeys.Gained, out int gained);
            stats.TryGetValue(ArcheryStatKeys.Lost, out int lost);
            return (true, score, gained, lost);
        }

        /// <summary>
        /// 전원이 같은 등수로 오면 무승부다(승자 없이 끝난 판). 1등이 여럿이어도 뒤가 등수별로
        /// 갈리면(예: 1,1,3,4) 그건 공동 1등일 뿐 — 전원이 동점이어야 비로소 무승부다.
        /// </summary>
        public static bool IsDrawn(IReadOnlyList<int> placements)
        {
            if (placements.Count < 2) return false;

            int first = placements[0];
            foreach (int placement in placements)
            {
                if (placement != first) return false;
            }
            return true;
        }

        /// <summary>그 등수에 몇 명이 몰려 있나. "공동 N등" 표기 여부를 가른다.</summary>
        public static int TiedCount(IReadOnlyList<int> placements, int placement)
        {
            int count = 0;
            foreach (int p in placements)
            {
                if (p == placement) { count++; }
            }
            return count;
        }

        /// <summary>
        /// 무승부면 등수 자리를 비운다 — 전원 동점을 "1등"이라 적으면 이긴 것처럼 읽힌다.
        /// 무승부가 아니지만 그 등수에 둘 이상 몰려 있으면(2위 이하 동점) "공동 N등"으로 적는다.
        /// </summary>
        public static string FormatPlacement(int placement, bool isDraw, int tiedCount)
        {
            if (isDraw) return "-";
            return tiedCount > 1 ? $"공동 {placement}등" : $"{placement}등";
        }

        /// <summary>점수 증감을 부호가 보이게. 결과 화면과 프로필 전적이 같은 표기를 쓴다.</summary>
        public static string FormatDelta(int before, int after)
        {
            int delta = after - before;

            if (delta > 0) return $"+{delta}";
            if (delta < 0) return delta.ToString();
            return "±0";
        }

        /// <summary>
        /// 점수 자리 표기. 벌점이 0이면 획득 내역을 굳이 안 보여준다 — 그때는 획득이 곧 점수와
        /// 같은 값이라 괄호 안이 점수를 그대로 되풀이할 뿐이다.
        /// </summary>
        public static string FormatScore(int score, int gained, int lost)
        {
            return lost > 0
                ? $"{score}점 (획득 {gained} · 벌점 {lost})"
                : $"{score}점";
        }
    }
}
