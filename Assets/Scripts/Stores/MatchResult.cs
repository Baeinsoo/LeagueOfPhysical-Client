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

        //  경기 끝 순간 클라가 아는 큐·종목·맵. 모르면 0. 결과 단계가 판 색(랭크)·머리띠를 응답 없이 바로 정한다.
        public int queueId;
        public int gameModeId;
        public int mapId;
    }

    public class MatchParticipantResult
    {
        public string userId;
        public int placement;

        //  모드별 결과 지표(활쏘기: score/gained/lost). 점수 개념이 없는 모드는 빈 사전으로 온다 —
        //  "점수 없음"과 "0점"은 다른 사실이라 null 대신 빈 사전을 그대로 둔다.
        public Dictionary<string, int> stats;

        //  서버가 결과와 함께 실어 준 그 판 이름·룩. 옛 서버면 빈 이름·null(결과 단계가 기본값으로 그린다).
        public string displayName;
        public PlayerLook look;
    }
}
