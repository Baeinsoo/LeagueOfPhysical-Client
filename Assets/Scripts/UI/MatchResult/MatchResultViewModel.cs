using System.Collections.Generic;

namespace LOP.UI
{
    /// <summary>결과 화면 등수표의 한 줄.</summary>
    public readonly struct MatchResultRow
    {
        public readonly int Placement;
        public readonly string DisplayName;
        public readonly bool IsMe;

        public MatchResultRow(int placement, string displayName, bool isMe)
        {
            Placement = placement;
            DisplayName = displayName;
            IsMe = isMe;
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
            IsDraw = IsDrawn(Rows);

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

            int otherNumber = 0;
            foreach (var participant in sorted)
            {
                bool isMe = participant.userId == myUserId;
                string displayName = isMe ? MyName : $"플레이어 {++otherNumber}";

                rows.Add(new MatchResultRow(participant.placement, displayName, isMe));
            }

            return rows;
        }

        //  1등이 둘 이상이면 승자가 없다. 판치기가 턴 상한에 닿으면 이렇게 끝난다.
        private static bool IsDrawn(IReadOnlyList<MatchResultRow> rows)
        {
            int firstPlaces = 0;
            foreach (var row in rows)
            {
                if (row.Placement == 1) { firstPlaces++; }
            }
            return firstPlaces > 1;
        }

        /// <summary>점수 증감을 부호가 보이게. 결과 화면과 프로필 전적이 같은 표기를 쓴다.</summary>
        public static string FormatDelta(int before, int after)
        {
            int delta = after - before;

            if (delta > 0) return $"+{delta}";
            if (delta < 0) return delta.ToString();
            return "±0";
        }
    }
}
