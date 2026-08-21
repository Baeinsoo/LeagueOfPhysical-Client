namespace LOP
{
    /// <summary>직전 매치의 결과. 보고가 실패한 판은 participants가 비어 있다.</summary>
    public class MatchResult
    {
        public string matchId;
        public MatchParticipantResult[] participants;
        public bool hasRatingChange;
        public int myMmrBefore;
        public int myMmrAfter;
    }

    public class MatchParticipantResult
    {
        public string userId;
        public int placement;
    }
}
