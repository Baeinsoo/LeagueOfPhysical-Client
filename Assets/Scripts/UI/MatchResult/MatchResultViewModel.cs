using System.Collections.Generic;

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

        public MatchResultRow(int placement, string displayName, bool isMe, bool isDraw = false)
            : this(placement, displayName, isMe, isDraw, hasScore: false, score: 0, gained: 0, lost: 0)
        {
        }

        public MatchResultRow(int placement, string displayName, bool isMe, bool isDraw,
            bool hasScore, int score, int gained, int lost)
        {
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
    /// 결과 화면 ViewModel. 스토어에 남은 직전 매치 결과를 표시용 줄 목록과 점수 문자열로 바꾼다.
    /// 화면이 열릴 때 한 번 읽고 끝나는 값이라 R3 스트림을 두지 않는다(라이브로 바뀌는 상태가 없다).
    /// </summary>
    public class MatchResultViewModel
    {
        private const string MyName = "나";

        public IReadOnlyList<MatchResultRow> Rows { get; }
        public bool HasRatingChange { get; }

        /// <summary>"1138 (+138)" 형태. 변화가 없으면 빈 문자열.</summary>
        public string RatingText { get; }

        /// <summary>1등이 여럿이면 아무도 이긴 게 아니다 — 화면이 등수 대신 그렇게 말해야 한다.</summary>
        public bool IsDraw { get; }

        public MatchResultViewModel(IMatchResultDataStore matchResultDataStore, IUserDataStore userDataStore)
        {
            var result = matchResultDataStore.result;

            Rows = BuildRows(result?.participants, userDataStore.user?.id);
            IsDraw = Rows.Count > 0 && Rows[0].IsDraw;

            HasRatingChange = result?.hasRatingChange ?? false;
            RatingText = HasRatingChange
                ? $"{result.myMmrAfter} ({FormatDelta(result.myMmrBefore, result.myMmrAfter)})"
                : string.Empty;
        }

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
                    hasScore, score, gained, lost));
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
        /// 1등이 여럿이면 아무도 이긴 게 아니다(무승부). 승자 없이 끝난 판은 전원 공동 1등으로 온다.
        /// </summary>
        public static bool IsDrawn(IReadOnlyList<int> placements)
        {
            int firstPlaces = 0;
            foreach (int placement in placements)
            {
                if (placement == 1) { firstPlaces++; }
            }
            return firstPlaces > 1;
        }

        /// <summary>무승부면 등수 자리를 비운다 — 공동 1등을 "1등"이라 적으면 이긴 것처럼 읽힌다.</summary>
        public static string FormatPlacement(int placement, bool isDraw)
        {
            return isDraw ? "-" : $"{placement}등";
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
