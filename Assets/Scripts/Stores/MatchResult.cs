using System.Collections.Generic;

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

        //  모드별 결과 지표(활쏘기: score/gained/lost). 점수 개념이 없는 모드는 빈 사전으로 온다 —
        //  "점수 없음"과 "0점"은 다른 사실이라 null 대신 빈 사전을 그대로 둔다.
        public Dictionary<string, int> stats;
    }
}
